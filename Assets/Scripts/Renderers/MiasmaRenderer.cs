using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders miasma as a sheet with holes where cleared.
/// Uses GPU instancing for performance.
/// </summary>
public class MiasmaRenderer : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Camera mainCamera;

    [Header("Visual Settings")]
    public Color miasmaColor = new Color(0.5f, 0f, 0.7f, 0.9f);  // Purple
    public float renderHeight = 0.01f;  // Y position of miasma sheet
    
    [Header("Sheet Size")]
    [Tooltip("Multiplier for viewport size. 1.0 = exact viewport, 1.5 = 50% larger")]
    public float sizeMultiplier = 1.2f;  // Make sheet larger than viewport (1.0 = exact fit)

    [Header("Performance")]
    public int maxTilesPerBatch = 1023;  // Unity limit for DrawMeshInstanced
    
    [Header("Debug")]
    [Tooltip("Disable this renderer to use ECS rendering instead")]
    public bool rendererEnabled = true;

    private Mesh tileMesh;
    private Material miasmaMaterial;
    private Matrix4x4[] matrices;
    private List<Matrix4x4> visibleMatrices = new List<Matrix4x4>();
    private MaterialPropertyBlock propertyBlock;

    private int lastMinX, lastMaxX, lastMinZ, lastMaxZ;
    private Vector3 lastPlayerPos;
    private bool needsRebuild = true;
    private HashSet<Vector2Int> cachedClearedSet = new HashSet<Vector2Int>(); // Cache to avoid recreating
    private int lastClearedCount = -1; // Track when to refresh cache

    void Start()
    {
        CreateTileMesh();
        CreateMaterial();
        matrices = new Matrix4x4[maxTilesPerBatch];
        propertyBlock = new MaterialPropertyBlock();

        // Subscribe to changes
        if (MiasmaManager.Instance != null)
        {
            MiasmaManager.Instance.OnClearedChanged += OnClearedChanged;
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
    }

    void OnDestroy()
    {
        if (MiasmaManager.Instance != null)
        {
            MiasmaManager.Instance.OnClearedChanged -= OnClearedChanged;
        }
    }

    void OnClearedChanged()
    {
        needsRebuild = true;
    }

    void Update()
    {
        // Disable if flag is set (for ECS testing)
        if (!rendererEnabled) return;
        
        if (MiasmaManager.Instance == null || player == null) return;

        // Player-centric: always update bounds centered on player
        float viewW = mainCamera.orthographicSize * 2f * mainCamera.aspect;
        float viewH = mainCamera.orthographicSize * 2f;

        int minX, maxX, minZ, maxZ;
        MiasmaManager.Instance.GetVisibleBounds(player.position, viewW, viewH,
            out minX, out maxX, out minZ, out maxZ);

        // Rebuild if bounds changed OR player moved significantly
        // Increased threshold to reduce rebuild frequency
        bool boundsChanged = (minX != lastMinX || maxX != lastMaxX || minZ != lastMinZ || maxZ != lastMaxZ);
        bool playerMoved = Vector3.Distance(player.position, lastPlayerPos) > MiasmaManager.Instance.tileSize * 1.0f; // Increased from 0.5f

        if (boundsChanged || playerMoved || needsRebuild)
        {
            lastMinX = minX; lastMaxX = maxX;
            lastMinZ = minZ; lastMaxZ = maxZ;
            lastPlayerPos = player.position;
            needsRebuild = true;
        }

        if (needsRebuild)
        {
            RebuildMesh();
            needsRebuild = false;
        }

        // Draw miasma tiles
        DrawMiasma();
    }

    void RebuildMesh()
    {
        visibleMatrices.Clear();

        if (MiasmaManager.Instance == null || mainCamera == null || player == null) return;

        float tileSize = MiasmaManager.Instance.tileSize;
        
        // Cache cleared set to avoid recreating HashSet every frame
        int currentClearedCount = MiasmaManager.Instance.ClearedCount;
        if (currentClearedCount != lastClearedCount)
        {
            cachedClearedSet.Clear();
            foreach (var tile in MiasmaManager.Instance.GetClearedTiles())
            {
                cachedClearedSet.Add(tile);
            }
            lastClearedCount = currentClearedCount;
        }
        HashSet<Vector2Int> clearedSet = cachedClearedSet;

        // Calculate camera-aligned rectangle (covers viewport + extra)
        float viewW = mainCamera.orthographicSize * 2f * mainCamera.aspect;
        float viewH = mainCamera.orthographicSize * 2f;
        float sheetW = viewW * Mathf.Max(1.0f, sizeMultiplier);  // At least cover viewport
        
        // Expand height to account for isometric compression (30° elevation = ~15% compression)
        // cos(30°) ≈ 0.866, so we need to expand by ~1.155 to compensate
        float isometricCompensation = 1.5f;  // Reduced from 2.0f for better performance
        float sheetH = viewH * Mathf.Max(1.0f, sizeMultiplier) * isometricCompensation;

        // Get camera's forward and right vectors (projected to XZ plane)
        Vector3 camForward = mainCamera.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();
        Vector3 camRight = Vector3.Cross(Vector3.up, camForward).normalized;

        // Calculate tile grid - ensure we have enough tiles to fill the sheet
        int tilesW = Mathf.CeilToInt(sheetW / tileSize) + 1;  // +1 to ensure coverage
        int tilesH = Mathf.CeilToInt(sheetH / tileSize) + 1;
        
        // Pre-allocate list capacity to reduce allocations
        int estimatedTiles = tilesW * tilesH;
        if (visibleMatrices.Capacity < estimatedTiles)
        {
            visibleMatrices.Capacity = estimatedTiles;
        }
        
        Vector3 playerPos = player.position;
        Vector3 sheetCenter = new Vector3(playerPos.x, renderHeight, playerPos.z);
        Vector3 sheetBottomLeft = sheetCenter - camRight * (sheetW * 0.5f) - camForward * (sheetH * 0.5f);

        // Generate tiles in camera-aligned grid - tiles should overlap slightly to avoid gaps
        for (int tx = 0; tx < tilesW; tx++)
        {
            for (int tz = 0; tz < tilesH; tz++)
            {
                // Position in camera-aligned space (tiles edge-to-edge)
                Vector3 localPos = camRight * (tx * tileSize) + camForward * (tz * tileSize);
                Vector3 worldPos = sheetBottomLeft + localPos + new Vector3(tileSize * 0.5f, 0f, tileSize * 0.5f);

                // Check if this world position's tile is cleared
                Vector2Int tile = MiasmaManager.Instance.WorldToTile(worldPos);
                
                // Skip cleared tiles (no miasma there)
                if (clearedSet.Contains(tile)) continue;

                // Create matrix - tiles overlap to eliminate visible gaps
                // 5% overlap should eliminate gaps completely
                float scale = tileSize * 1.5f;
                Matrix4x4 matrix = Matrix4x4.TRS(worldPos, Quaternion.identity, new Vector3(scale, 1f, scale));
                visibleMatrices.Add(matrix);
            }
        }
    }

    void DrawMiasma()
    {
        if (visibleMatrices.Count == 0) return;

        // Draw in batches (Unity limit is 1023 per call)
        int drawn = 0;
        while (drawn < visibleMatrices.Count)
        {
            int batchSize = Mathf.Min(maxTilesPerBatch, visibleMatrices.Count - drawn);
            
            for (int i = 0; i < batchSize; i++)
            {
                matrices[i] = visibleMatrices[drawn + i];
            }

            Graphics.DrawMeshInstanced(tileMesh, 0, miasmaMaterial, matrices, batchSize, propertyBlock);
            drawn += batchSize;
        }
    }

    void CreateTileMesh()
    {
        // Simple quad (1x1, will be scaled by matrix)
        tileMesh = new Mesh();

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(-0.5f, 0f, 0.5f)
        };

        int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        tileMesh.vertices = vertices;
        tileMesh.triangles = triangles;
        tileMesh.uv = uvs;
        tileMesh.RecalculateNormals();
    }

    void CreateMaterial()
    {
        // Use custom instanced shader
        Shader shader = Shader.Find("Custom/UnlitInstanced");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
        
        miasmaMaterial = new Material(shader);
        miasmaMaterial.SetColor("_Color", miasmaColor);
        miasmaMaterial.enableInstancing = true;
    }
}
