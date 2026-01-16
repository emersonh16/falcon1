using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// ECS System: Handles clearing miasma tiles when beam fires.
/// Runs on main thread to integrate with existing BeamManager.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class MiasmaClearingSystem : SystemBase
{
    private EntityQuery clearedTilesQuery;
    private EntityQuery miasmaTilesQuery;

    protected override void OnCreate()
    {
        // Query for cleared tiles
        clearedTilesQuery = GetEntityQuery(
            ComponentType.ReadWrite<MiasmaTileComponent>(),
            ComponentType.ReadWrite<MiasmaNeedsUpdate>()
        );

        // Query for all miasma tiles
        miasmaTilesQuery = GetEntityQuery(ComponentType.ReadWrite<MiasmaTileComponent>());
    }

    protected override void OnUpdate()
    {
        // This system will be called from MiasmaManager when beam fires
        // For now, it's a placeholder that will be integrated
    }

    /// <summary>
    /// Clear tiles in a circular area (called from MiasmaManager)
    /// </summary>
    public void ClearArea(float3 worldPos, float radius, float tileSize, EntityCommandBuffer ecb)
    {
        float radiusSq = radius * radius;
        int tileRadius = (int)math.ceil(radius / tileSize);
        int2 centerTile = WorldToTile(worldPos, tileSize);

        // Find and clear tiles in radius
        Entities
            .WithAll<MiasmaTileComponent>()
            .ForEach((Entity entity, ref MiasmaTileComponent tile) =>
            {
                float2 tileCenter = new float2(tile.worldPosition.x, tile.worldPosition.z);
                float2 worldPos2D = new float2(worldPos.x, worldPos.z);
                float distSq = math.distancesq(tileCenter, worldPos2D);

                if (distSq <= radiusSq && !tile.isCleared)
                {
                    tile.isCleared = true;
                    tile.timeCleared = (float)UnityEngine.Time.time;
                    ecb.AddComponent<MiasmaNeedsUpdate>(entity);
                }
            }).WithoutBurst().Run();
    }

    private int2 WorldToTile(float3 worldPos, float tileSize)
    {
        return new int2(
            (int)math.floor(worldPos.x / tileSize),
            (int)math.floor(worldPos.z / tileSize)
        );
    }
}
