using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages all rocks in the world and integrates with miasma system.
/// Singleton - access via RockManager.Instance
/// </summary>
public class RockManager : MonoBehaviour
{
    public static RockManager Instance { get; private set; }

    [Header("Generation Settings")]
    [Tooltip("Number of rock formations to generate")]
    public int rockFormationCount = 10;
    [Tooltip("Percentage of rocks that are tall (0-1)")]
    [Range(0f, 1f)]
    public float tallRockPercent = 0.25f;  // 25% tall
    [Tooltip("Radius around player spawn to generate rocks (spread out more)")]
    public float generationRadius = 25f;  // Increased for more spread
    
    [Header("Rock Settings")]
    public float voxelSize = 1.0f;  // 4x miasma tile size
    public float tallThreshold = 0.05f;
    public Color rockColor = new Color(0.6f, 0.4f, 0.2f);  // Brown

    private List<Rock> allRocks = new List<Rock>();
    private List<Rock> tallRocks = new List<Rock>();

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
        // Generate rocks near player spawn
        Vector3 playerSpawn = GetPlayerSpawnPosition();
        GenerateRocks(playerSpawn);
        
        // Integrate tall rocks with miasma
        UpdateMiasmaForRocks();
    }

    Vector3 GetPlayerSpawnPosition()
    {
        var derelict = GameObject.Find("Derelict");
        if (derelict != null)
        {
            return derelict.transform.position;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// Generate rock formations near the given center position.
    /// </summary>
    public void GenerateRocks(Vector3 center)
    {
        // Clear existing rocks
        foreach (var rock in allRocks)
        {
            if (rock != null) Destroy(rock.gameObject);
        }
        allRocks.Clear();
        tallRocks.Clear();
        
        // Generate clumps
        List<Rock> newRocks = RockClumpGenerator.GenerateClumps(
            center,
            generationRadius,
            rockFormationCount,
            tallRockPercent
        );
        
        allRocks.AddRange(newRocks);
        
        // Separate tall rocks
        foreach (var rock in allRocks)
        {
            if (rock.IsTall)
            {
                tallRocks.Add(rock);
            }
        }
        
        Debug.Log($"Generated {allRocks.Count} rocks ({tallRocks.Count} tall)");
    }

    /// <summary>
    /// Update miasma texture to cut holes for tall rocks.
    /// Each tall rock voxel clears 4 miasma tiles (since rock is 4x larger).
    /// </summary>
    public void UpdateMiasmaForRocks()
    {
        if (MiasmaManager.Instance == null) return;
        
        // Collect all tall voxel positions
        HashSet<Vector2Int> tilesToClear = new HashSet<Vector2Int>();
        
        foreach (var rock in tallRocks)
        {
            foreach (var voxel in rock.Voxels)
            {
                if (voxel.isTall)
                {
                    // Each rock voxel is 4x miasma tile size (1.0 vs 0.25)
                    // So we need to clear 4 tiles (2x2 grid) for each tall voxel
                    Vector2Int centerTile = MiasmaManager.Instance.WorldToTile(voxel.position);
                    
                    // Clear 2x2 grid of tiles (4 tiles total) - exact pixel clearing
                    for (int dx = 0; dx < 2; dx++)
                    {
                        for (int dz = 0; dz < 2; dz++)
                        {
                            Vector2Int tile = new Vector2Int(centerTile.x + dx, centerTile.y + dz);
                            tilesToClear.Add(tile);
                        }
                    }
                }
            }
        }
        
        // Mark tiles as cleared in MiasmaManager
        foreach (var tile in tilesToClear)
        {
            MiasmaManager.Instance.MarkTileCleared(tile);
        }
    }

    public List<Rock> GetAllRocks() => allRocks;
    public List<Rock> GetTallRocks() => tallRocks;
}
