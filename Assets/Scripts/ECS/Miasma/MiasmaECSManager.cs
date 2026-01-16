using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Bridge between old MiasmaManager and new ECS system.
/// Manages ECS world and converts between old/new systems.
/// </summary>
public class MiasmaECSManager : MonoBehaviour
{
    public static MiasmaECSManager Instance { get; private set; }

    [Header("ECS Settings")]
    public float tileSize = 0.25f;
    public int viewPadding = 20;

    private World ecsWorld;
    private EntityManager entityManager;
    private bool isInitialized = false;

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

    void OnDestroy()
    {
        // Cleanup handled by Unity
    }

    /// <summary>
    /// Create miasma tile entities in visible area
    /// </summary>
    public void CreateTilesInArea(Vector3 center, float viewWidth, float viewHeight)
    {
        if (!isInitialized) InitializeECS();

        int minX, maxX, minZ, maxZ;
        CalculateBounds(center, viewWidth, viewHeight, out minX, out maxX, out minZ, out maxZ);

        // Create entities for tiles in bounds
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                int2 tileCoord = new int2(x, z);
                
                // Check if entity already exists
                // (In real implementation, use a lookup dictionary)
                
                // Create entity
                Entity tileEntity = entityManager.CreateEntity();
                
                float3 worldPos = new float3(
                    (x + 0.5f) * tileSize,
                    0.01f,
                    (z + 0.5f) * tileSize
                );

                entityManager.AddComponentData(tileEntity, new MiasmaTileComponent
                {
                    tileCoord = tileCoord,
                    worldPosition = worldPos,
                    isCleared = false,
                    timeCleared = 0f,
                    isFrontier = false
                });

                entityManager.AddComponent<MiasmaRenderTag>(tileEntity);
            }
        }
    }

    void CalculateBounds(Vector3 center, float viewWidth, float viewHeight, 
        out int minX, out int maxX, out int minZ, out int maxZ)
    {
        int halfW = Mathf.CeilToInt(viewWidth / tileSize / 2f) + viewPadding;
        int halfH = Mathf.CeilToInt(viewHeight / tileSize / 2f) + viewPadding;
        
        int2 centerTile = new int2(
            Mathf.FloorToInt(center.x / tileSize),
            Mathf.FloorToInt(center.z / tileSize)
        );

        minX = centerTile.x - halfW;
        maxX = centerTile.x + halfW;
        minZ = centerTile.y - halfH;
        maxZ = centerTile.y + halfH;
    }
}
