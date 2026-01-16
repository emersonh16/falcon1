using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Bridge between old MiasmaManager and new ECS system.
/// Manages ECS world and converts between old/new systems.
/// </summary>
public class MiasmaECSManager : MonoBehaviour
{
    public static MiasmaECSManager Instance { get; private set; }

    [Header("References")]
    public Transform player;
    public Camera mainCamera;

    [Header("ECS Settings")]
    [Tooltip("Auto-synced from MiasmaManager. Only set manually if MiasmaManager not found.")]
    public float tileSize = 0.25f;
    public int viewPadding = 20;

    private World ecsWorld;
    private EntityManager entityManager;
    private bool isInitialized = false;
    
    // Lookup: tile coord -> entity
    private Dictionary<int2, Entity> tileEntityMap = new Dictionary<int2, Entity>();
    
    // Track last bounds to avoid recreating tiles every frame
    private int lastMinX, lastMaxX, lastMinZ, lastMaxZ;
    private Vector3 lastPlayerPos;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        InitializeECS();
        
        // Sync tile size from MiasmaManager
        if (MiasmaManager.Instance != null)
        {
            tileSize = MiasmaManager.Instance.tileSize;
            viewPadding = MiasmaManager.Instance.viewPadding;
        }
        
        // Find player if not assigned
        if (player == null)
        {
            var derelict = GameObject.Find("Derelict");
            if (derelict != null) player = derelict.transform;
        }
        
        // Find camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        // Subscribe to MiasmaManager changes
        if (MiasmaManager.Instance != null)
        {
            MiasmaManager.Instance.OnClearedChanged += OnMiasmaClearedChanged;
        }
    }
    
    void OnDestroy()
    {
        if (MiasmaManager.Instance != null)
        {
            MiasmaManager.Instance.OnClearedChanged -= OnMiasmaClearedChanged;
        }
    }
    
    void OnMiasmaClearedChanged()
    {
        // Sync cleared tiles from MiasmaManager to ECS
        SyncClearedTiles();
    }

    void InitializeECS()
    {
        if (isInitialized) return;

        // Get or create ECS world
        if (World.DefaultGameObjectInjectionWorld == null)
        {
            ecsWorld = new World("MiasmaWorld");
            World.DefaultGameObjectInjectionWorld = ecsWorld;
        }
        else
        {
            ecsWorld = World.DefaultGameObjectInjectionWorld;
        }

        entityManager = ecsWorld.EntityManager;
        
        // Systems are automatically created and managed by Unity ECS
        // They will run automatically once created

        isInitialized = true;
        Debug.Log("Miasma ECS System Initialized");
    }

    void Update()
    {
        // DISABLED: ECS system is disabled - using MiasmaRenderer instead
        // This prevents ECS from creating/updating entities in the background
        return;
        
        if (!isInitialized || player == null || mainCamera == null) return;
        
        // Update tiles based on player position (like MiasmaRenderer does)
        UpdateTilesForViewport();
        
        // Sync cleared state from MiasmaManager (only when changed)
        // Note: SyncClearedTiles is now called from OnMiasmaClearedChanged event
    }
    
    void UpdateTilesForViewport()
    {
        if (MiasmaManager.Instance == null) return;
        
        // Calculate viewport bounds (same logic as MiasmaRenderer)
        float viewW = mainCamera.orthographicSize * 2f * mainCamera.aspect;
        float viewH = mainCamera.orthographicSize * 2f;
        
        int minX, maxX, minZ, maxZ;
        MiasmaManager.Instance.GetVisibleBounds(player.position, viewW, viewH,
            out minX, out maxX, out minZ, out maxZ);
        
        // Only recreate if bounds changed significantly or player moved
        bool boundsChanged = (minX != lastMinX || maxX != lastMaxX || minZ != lastMinZ || maxZ != lastMaxZ);
        bool playerMoved = Vector3.Distance(player.position, lastPlayerPos) > tileSize * 0.5f;
        
        if (boundsChanged || playerMoved)
        {
            // Remove tiles that are far from player (cleanup)
            CleanupDistantTiles(minX, maxX, minZ, maxZ);
            
            // Create new tiles in viewport (full sheet, camera-aligned)
            CreateTilesInBounds(minX, maxX, minZ, maxZ);
            
            lastMinX = minX; lastMaxX = maxX;
            lastMinZ = minZ; lastMaxZ = maxZ;
            lastPlayerPos = player.position;
        }
    }
    
    void SyncClearedTiles()
    {
        if (MiasmaManager.Instance == null) return;
        
        // Get all cleared tiles from MiasmaManager
        var clearedTiles = MiasmaManager.Instance.GetClearedTiles();
        HashSet<Vector2Int> clearedSet = new HashSet<Vector2Int>(clearedTiles);
        
        // Update ECS entities - mark cleared tiles
        // Limit updates per frame for performance
        int updatesThisFrame = 0;
        int maxUpdatesPerFrame = 500;
        
        foreach (var kvp in tileEntityMap)
        {
            if (updatesThisFrame >= maxUpdatesPerFrame) break;
            
            int2 tileCoord = kvp.Key;
            Entity entity = kvp.Value;
            
            if (!entityManager.Exists(entity)) continue;
            
            var tile = entityManager.GetComponentData<MiasmaTileComponent>(entity);
            Vector2Int tileVec = new Vector2Int(tileCoord.x, tileCoord.y);
            bool shouldBeCleared = clearedSet.Contains(tileVec);
            
            // Update if state changed
            if (shouldBeCleared && !tile.isCleared)
            {
                tile.isCleared = true;
                tile.timeCleared = Time.time;
                entityManager.SetComponentData(entity, tile);
                updatesThisFrame++;
            }
            else if (!shouldBeCleared && tile.isCleared)
            {
                // Regrowth: tile is no longer cleared
                tile.isCleared = false;
                tile.timeCleared = 0f;
                entityManager.SetComponentData(entity, tile);
                updatesThisFrame++;
            }
        }
    }
    
    void CleanupDistantTiles(int minX, int maxX, int minZ, int maxZ)
    {
        // Remove tiles that are outside the current bounds
        List<int2> toRemove = new List<int2>();
        foreach (var kvp in tileEntityMap)
        {
            int2 coord = kvp.Key;
            if (coord.x < minX - 10 || coord.x > maxX + 10 || 
                coord.y < minZ - 10 || coord.y > maxZ + 10)
            {
                toRemove.Add(coord);
            }
        }
        
        // Remove entities
        foreach (var coord in toRemove)
        {
            if (tileEntityMap.TryGetValue(coord, out Entity entity))
            {
                if (entityManager.Exists(entity))
                {
                    entityManager.DestroyEntity(entity);
                }
                tileEntityMap.Remove(coord);
            }
        }
    }
    
    void CreateTilesInBounds(int minX, int maxX, int minZ, int maxZ)
    {
        // Create ALL tiles immediately to fill viewport (like MiasmaRenderer)
        // Use camera-aligned grid for proper coverage
        float viewW = mainCamera.orthographicSize * 2f * mainCamera.aspect;
        float viewH = mainCamera.orthographicSize * 2f;
        float sizeMultiplier = 1.2f;
        float isometricCompensation = 2.0f;
        
        float sheetW = viewW * Mathf.Max(1.0f, sizeMultiplier);
        float sheetH = viewH * Mathf.Max(1.0f, sizeMultiplier) * isometricCompensation;
        
        // Reduce tile density for better performance - use larger tiles
        // But ensure we still cover the viewport
        
        // Get camera's forward and right vectors (projected to XZ plane)
        Vector3 camForward = mainCamera.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();
        Vector3 camRight = Vector3.Cross(Vector3.up, camForward).normalized;
        
        // Calculate how many tiles we need
        // Use tileSize directly but ensure overlap eliminates gaps
        int tilesW = Mathf.CeilToInt(sheetW / tileSize) + 3; // Extra padding for coverage
        int tilesH = Mathf.CeilToInt(sheetH / tileSize) + 3;
        
        Vector3 playerPos = player.position;
        Vector3 sheetCenter = new Vector3(playerPos.x, 0.01f, playerPos.z);
        Vector3 sheetBottomLeft = sheetCenter - camRight * (sheetW * 0.5f) - camForward * (sheetH * 0.5f);
        
        // Create tiles in camera-aligned grid (like MiasmaRenderer)
        // Use a HashSet to track created coordinates to avoid duplicates
        HashSet<int2> createdThisFrame = new HashSet<int2>();
        int tilesCreated = 0;
        
        for (int tx = 0; tx < tilesW; tx++)
        {
            for (int tz = 0; tz < tilesH; tz++)
            {
                // Position in camera-aligned space
                Vector3 localPos = camRight * (tx * tileSize) + camForward * (tz * tileSize);
                Vector3 worldPos = sheetBottomLeft + localPos + new Vector3(tileSize * 0.5f, 0f, tileSize * 0.5f);
                
                // Convert to tile coordinate for lookup
                if (MiasmaManager.Instance != null)
                {
                    Vector2Int tileCoord = MiasmaManager.Instance.WorldToTile(worldPos);
                    int2 coord = new int2(tileCoord.x, tileCoord.y);
                    
                    // Only create if doesn't exist (check both maps)
                    if (!tileEntityMap.ContainsKey(coord) && !createdThisFrame.Contains(coord))
                    {
                        CreateTileEntityAtPosition(coord, worldPos);
                        createdThisFrame.Add(coord);
                        tilesCreated++;
                    }
                }
            }
        }
        
        // Debug: Log tile creation
        if (tilesCreated > 0)
        {
            Debug.Log($"Created {tilesCreated} miasma tiles. Total tiles: {tileEntityMap.Count}");
        }
    }
    
    void CreateTileEntityAtPosition(int2 tileCoord, Vector3 worldPosition)
    {
        // Check if already exists
        if (tileEntityMap.ContainsKey(tileCoord)) return;
        
        // Create entity
        Entity tileEntity = entityManager.CreateEntity();
        
        float3 worldPos = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
        
        // Check if cleared in MiasmaManager
        bool isCleared = false;
        float timeCleared = 0f;
        if (MiasmaManager.Instance != null)
        {
            Vector2Int tileVec = new Vector2Int(tileCoord.x, tileCoord.y);
            var clearedTiles = MiasmaManager.Instance.GetClearedTiles();
            HashSet<Vector2Int> clearedSet = new HashSet<Vector2Int>(clearedTiles);
            isCleared = clearedSet.Contains(tileVec);
            if (isCleared)
            {
                timeCleared = Time.time; // Approximate - MiasmaManager doesn't expose exact time
            }
        }
        
        entityManager.AddComponentData(tileEntity, new MiasmaTileComponent
        {
            tileCoord = tileCoord,
            worldPosition = worldPos,
            isCleared = isCleared,
            timeCleared = timeCleared,
            isFrontier = false
        });
        
        entityManager.AddComponent<MiasmaRenderTag>(tileEntity);
        
        // Store in lookup
        tileEntityMap[tileCoord] = tileEntity;
    }
    
    // Legacy method - kept for compatibility
    void CreateTileEntity(int2 tileCoord)
    {
        // Convert tile coordinate to world position
        float3 worldPos = new float3(
            (tileCoord.x + 0.5f) * tileSize,
            0.01f,
            (tileCoord.y + 0.5f) * tileSize
        );
        CreateTileEntityAtPosition(tileCoord, new Vector3(worldPos.x, worldPos.y, worldPos.z));
    }

}
