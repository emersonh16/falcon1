using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// ECS System: Manages tile creation/removal based on player position.
/// Creates tiles in viewport area, removes tiles far from player.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class MiasmaTileManagementSystem : SystemBase
{
    private EntityQuery allTilesQuery;
    private float tileSize = 0.25f;
    private int viewPadding = 20;

    protected override void OnCreate()
    {
        allTilesQuery = GetEntityQuery(ComponentType.ReadWrite<MiasmaTileComponent>());
        // RequireForUpdate doesn't work with MonoBehaviour types in 2021.3
    }

    protected override void OnUpdate()
    {
        // Tile creation/management is handled by MiasmaECSManager
        // This system can be extended for automatic tile management later
    }

    /// <summary>
    /// Update frontier status for tiles (called from main thread)
    /// </summary>
    public void UpdateFrontiers(EntityManager em, NativeParallelHashMap<int2, bool> clearedMap)
    {
        Entities
            .WithAll<MiasmaTileComponent>()
            .ForEach((Entity entity, ref MiasmaTileComponent tile) =>
            {
                if (!tile.isCleared) return;

                // Check if any neighbor is not cleared (makes this a frontier tile)
                int2[] neighbors = new int2[]
                {
                    tile.tileCoord + new int2(-1, 0),
                    tile.tileCoord + new int2(1, 0),
                    tile.tileCoord + new int2(0, -1),
                    tile.tileCoord + new int2(0, 1)
                };

                bool isBoundary = false;
                foreach (var neighbor in neighbors)
                {
                    if (!clearedMap.ContainsKey(neighbor))
                    {
                        isBoundary = true;
                        break;
                    }
                }

                tile.isFrontier = isBoundary;
            }).WithoutBurst().Run();
    }
}
