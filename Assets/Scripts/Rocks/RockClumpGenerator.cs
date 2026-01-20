using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates organic rock clumps with tall rocks nested in short rock areas.
/// </summary>
public static class RockClumpGenerator
{
    public static List<Rock> GenerateClumps(Vector3 center, float radius, int clumpCount, float tallRockPercent)
    {
        List<Rock> allRocks = new List<Rock>();
        
        // Spread rocks more randomly across the area (not just in circle)
        // Use uniform distribution for better spread
        for (int i = 0; i < clumpCount; i++)
        {
            // More random distribution - use square area instead of circle
            // This spreads rocks out more evenly
            float x = center.x + Random.Range(-radius, radius);
            float z = center.z + Random.Range(-radius, radius);
            Vector3 clumpCenter = new Vector3(x, 0f, z);
            
            // Generate clump (smaller clumps, more spread out)
            List<Rock> clumpRocks = GenerateClump(clumpCenter, tallRockPercent);
            allRocks.AddRange(clumpRocks);
        }
        
        return allRocks;
    }

    static List<Rock> GenerateClump(Vector3 center, float tallRockPercent)
    {
        List<Rock> rocks = new List<Rock>();
        
        // Smaller clumps, more spread out: 2-5 rocks per clump
        int rockCount = Random.Range(2, 6);
        float clumpRadius = 1.5f;  // Smaller radius for tighter clumps
        
        // Determine which rocks will be tall (20-30% of total)
        int tallRockCount = Mathf.RoundToInt(rockCount * tallRockPercent);
        HashSet<int> tallRockIndices = new HashSet<int>();
        while (tallRockIndices.Count < tallRockCount)
        {
            tallRockIndices.Add(Random.Range(0, rockCount));
        }
        
        // Generate rocks in clump
        for (int i = 0; i < rockCount; i++)
        {
            // Random position within clump
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(0f, clumpRadius);
            Vector3 rockPos = center + new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance
            );
            
            // Create rock
            GameObject rockObj = new GameObject($"Rock_{i}");
            Rock rock = rockObj.AddComponent<Rock>();
            
            // More voxels for mountain-like rocks (5-20 voxels per rock)
            int voxelCount = Random.Range(5, 21);
            
            // Determine if this rock should be tall
            bool forceTall = tallRockIndices.Contains(i);
            
            // Generate rock shape
            rock.GenerateRock(voxelCount, rockPos, forceTall);
            
            rocks.Add(rock);
        }
        
        return rocks;
    }
}
