using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// ECS System: Handles miasma regrowth from frontier tiles.
/// Runs as a Burst-compiled job for maximum performance.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial class MiasmaRegrowthSystem : SystemBase
{
    private EntityQuery frontierTilesQuery;

    protected override void OnCreate()
    {
        // Query for frontier tiles (boundary tiles eligible for regrowth)
        frontierTilesQuery = GetEntityQuery(
            ComponentType.ReadWrite<MiasmaTileComponent>()
        );
    }

    protected override void OnUpdate()
    {
        // TEMPORARILY DISABLED for performance testing
        // Regrowth will be handled by MiasmaManager for now
        return;
        
        /* DISABLED CODE - Re-enable when ready
        float currentTime = (float)UnityEngine.Time.time;
        float regrowDelay = 1.5f;
        float regrowChance = 0.15f;
        int regrowBudget = 512;

        // Process regrowth as a job
        // Ensure seed is non-zero (Random requires non-zero seed)
        uint seed = (uint)(currentTime * 1000);
        if (seed == 0) seed = 1; // Ensure non-zero
        
        var regrowthJob = new RegrowthJob
        {
            currentTime = currentTime,
            regrowDelay = regrowDelay,
            regrowChance = regrowChance,
            regrowBudget = regrowBudget,
            random = new Unity.Mathematics.Random(seed)
        };

        Dependency = regrowthJob.ScheduleParallel(frontierTilesQuery, Dependency);
        */
    }

    [BurstCompile]
    partial struct RegrowthJob : IJobEntity
    {
        public float currentTime;
        public float regrowDelay;
        public float regrowChance;
        public int regrowBudget;
        public Unity.Mathematics.Random random;

        void Execute(ref MiasmaTileComponent tile)
        {
            // Only process frontier tiles that are cleared
            if (!tile.isFrontier || !tile.isCleared) return;

            // Check delay
            if (currentTime - tile.timeCleared < regrowDelay) return;

            // Random chance
            if (random.NextFloat() < regrowChance)
            {
                tile.isCleared = false;
                tile.timeCleared = 0f;
                tile.isFrontier = false;
            }
        }
    }
}
