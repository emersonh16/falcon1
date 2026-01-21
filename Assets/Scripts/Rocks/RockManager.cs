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
    public int rockFormationCount = 150;  // Much denser
    [Tooltip("Percentage of rocks that are tall (0-1)")]
    [Range(0f, 1f)]
    public float tallRockPercent = 0.05f;  // 5% tall (even fewer tall rocks)
    [Tooltip("Ground size for even distribution")]
    public float groundSize = 200f;  // Match GridGround size
    
    [Header("Rock Settings")]
    public float voxelSize = 0.25f;  // Same as miasma tile size (1/4th of original)
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
        // Generate rocks evenly spread across the ground
        GenerateRocksEvenly();
        
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
    /// Generate rock formations evenly spread across the ground.
    /// </summary>
    public void GenerateRocksEvenly()
    {
        // Clear existing rocks
        foreach (var rock in allRocks)
        {
            if (rock != null) Destroy(rock.gameObject);
        }
        allRocks.Clear();
        tallRocks.Clear();
        
        // Generate rocks evenly distributed across the ground plane - MUCH denser
        float halfGround = groundSize * 0.5f;
        
        // Use Poisson-like distribution for more natural spread
        List<Rock> newRocks = new List<Rock>();
        List<Vector3> placedPositions = new List<Vector3>();
        float minDistance = 8f;  // Minimum distance between formations
        
        int attempts = 0;
        int maxAttempts = rockFormationCount * 50;  // Try many times to place
        
        for (int i = 0; i < rockFormationCount && attempts < maxAttempts; attempts++)
        {
            // Random position
            Vector3 position = new Vector3(
                Random.Range(-halfGround, halfGround),
                0f,
                Random.Range(-halfGround, halfGround)
            );
            
            // Check if too close to existing formations
            bool tooClose = false;
            foreach (var placed in placedPositions)
            {
                if (Vector3.Distance(new Vector3(position.x, 0, position.z), 
                                   new Vector3(placed.x, 0, placed.z)) < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }
            
            if (tooClose) continue;
            
            placedPositions.Add(position);
            
            // Generate a single large formation at this position
            GameObject rockObj = new GameObject($"RockFormation_{i}");
            Rock rock = rockObj.AddComponent<Rock>();
            
            // Large formations: 200-800 voxels
            int voxelCount = Random.Range(200, 801);
            
            // Determine if this formation should be tall (5% chance)
            bool forceTall = Random.value < tallRockPercent;
            
            // Generate rock shape
            rock.GenerateRock(voxelCount, position, forceTall);
            newRocks.Add(rock);
        }
        
        allRocks.AddRange(newRocks);
        
        // Separate tall rocks
        foreach (var rock in allRocks)
        {
            if (rock.IsTall)
            {
                tallRocks.Add(rock);
            }
        }
        
        Debug.Log($"Generated {allRocks.Count} rocks ({tallRocks.Count} tall) evenly across {groundSize}x{groundSize} ground");
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
                    // Each rock voxel is same size as miasma tile (0.25 vs 0.25)
                    // So we need to clear 1 tile for each tall voxel
                    Vector2Int centerTile = MiasmaManager.Instance.WorldToTile(voxel.position);
                    tilesToClear.Add(centerTile);
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
