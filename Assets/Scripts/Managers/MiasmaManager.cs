using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages miasma state using inverse model - tracks cleared tiles only.
/// Miasma is assumed everywhere except where cleared.
/// </summary>
public class MiasmaManager : MonoBehaviour
{
    public static MiasmaManager Instance { get; private set; }

    [Header("Tile Settings")]
    public float tileSize = 2f;  // World units per tile
    public int viewPadding = 6;  // Extra tiles beyond viewport

    [Header("Regrowth")]
    public float regrowDelay = 1.5f;    // Seconds before regrowth can occur
    public float regrowChance = 0.15f;  // Chance per frame per eligible tile
    public int regrowBudget = 128;      // Max tiles to regrow per frame

    // Cleared tiles: key = tile coord, value = time cleared
    private Dictionary<Vector2Int, float> clearedTiles = new Dictionary<Vector2Int, float>();
    
    // Frontier: boundary tiles eligible for regrowth
    private HashSet<Vector2Int> frontier = new HashSet<Vector2Int>();

    // Events
    public event Action OnClearedChanged;

    // Stats
    public int ClearedCount => clearedTiles.Count;
    public int FrontierCount => frontier.Count;

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
        // Subscribe to beam events
        if (BeamManager.Instance != null)
        {
            BeamManager.Instance.OnBeamFired += OnBeamFired;
        }
    }

    void OnDestroy()
    {
        if (BeamManager.Instance != null)
        {
            BeamManager.Instance.OnBeamFired -= OnBeamFired;
        }
    }

    void Update()
    {
        ProcessRegrowth();
    }

    void OnBeamFired(Vector3 worldPos, float radius)
    {
        if (radius > 0)
        {
            ClearArea(worldPos, radius);
        }
    }

    /// <summary>
    /// Clear miasma in a circular area
    /// </summary>
    public int ClearArea(Vector3 worldPos, float radius)
    {
        int cleared = 0;
        float radiusSq = radius * radius;
        int tileRadius = Mathf.CeilToInt(radius / tileSize);

        Vector2Int centerTile = WorldToTile(worldPos);

        for (int dx = -tileRadius; dx <= tileRadius; dx++)
        {
            for (int dz = -tileRadius; dz <= tileRadius; dz++)
            {
                Vector2Int tile = new Vector2Int(centerTile.x + dx, centerTile.y + dz);
                
                // Check if tile center is within radius
                Vector3 tileCenter = TileToWorld(tile);
                float distSq = (tileCenter.x - worldPos.x) * (tileCenter.x - worldPos.x) +
                               (tileCenter.z - worldPos.z) * (tileCenter.z - worldPos.z);

                if (distSq <= radiusSq)
                {
                    if (!clearedTiles.ContainsKey(tile))
                    {
                        clearedTiles[tile] = Time.time;
                        cleared++;
                        UpdateFrontier(tile);
                    }
                }
            }
        }

        if (cleared > 0)
        {
            OnClearedChanged?.Invoke();
        }

        return cleared;
    }

    /// <summary>
    /// Check if a world position has miasma (not cleared)
    /// </summary>
    public bool HasMiasma(Vector3 worldPos)
    {
        Vector2Int tile = WorldToTile(worldPos);
        return !clearedTiles.ContainsKey(tile);
    }

    /// <summary>
    /// Get all cleared tiles for rendering
    /// </summary>
    public IEnumerable<Vector2Int> GetClearedTiles()
    {
        return clearedTiles.Keys;
    }

    /// <summary>
    /// Get visible tile bounds around a position
    /// </summary>
    public void GetVisibleBounds(Vector3 center, float viewWidth, float viewHeight, 
        out int minX, out int maxX, out int minZ, out int maxZ)
    {
        int halfW = Mathf.CeilToInt(viewWidth / tileSize / 2f) + viewPadding;
        int halfH = Mathf.CeilToInt(viewHeight / tileSize / 2f) + viewPadding;
        Vector2Int centerTile = WorldToTile(center);

        minX = centerTile.x - halfW;
        maxX = centerTile.x + halfW;
        minZ = centerTile.y - halfH;
        maxZ = centerTile.y + halfH;
    }

    void ProcessRegrowth()
    {
        if (frontier.Count == 0) return;

        int budget = regrowBudget;
        List<Vector2Int> toRegrow = new List<Vector2Int>();
        float currentTime = Time.time;

        foreach (var tile in frontier)
        {
            if (budget <= 0) break;

            if (!clearedTiles.TryGetValue(tile, out float clearedTime)) continue;

            // Check delay
            if (currentTime - clearedTime < regrowDelay) continue;

            // Random chance
            if (UnityEngine.Random.value < regrowChance)
            {
                toRegrow.Add(tile);
                budget--;
            }
        }

        // Apply regrowth
        foreach (var tile in toRegrow)
        {
            clearedTiles.Remove(tile);
            frontier.Remove(tile);
            UpdateNeighborFrontier(tile);
        }

        if (toRegrow.Count > 0)
        {
            OnClearedChanged?.Invoke();
        }
    }

    void UpdateFrontier(Vector2Int tile)
    {
        // Check if this tile is on boundary
        if (IsBoundary(tile))
            frontier.Add(tile);
        else
            frontier.Remove(tile);

        // Update neighbors
        UpdateNeighborFrontier(tile);
    }

    void UpdateNeighborFrontier(Vector2Int tile)
    {
        CheckFrontier(new Vector2Int(tile.x - 1, tile.y));
        CheckFrontier(new Vector2Int(tile.x + 1, tile.y));
        CheckFrontier(new Vector2Int(tile.x, tile.y - 1));
        CheckFrontier(new Vector2Int(tile.x, tile.y + 1));
    }

    void CheckFrontier(Vector2Int tile)
    {
        if (!clearedTiles.ContainsKey(tile))
        {
            frontier.Remove(tile);
            return;
        }

        if (IsBoundary(tile))
            frontier.Add(tile);
        else
            frontier.Remove(tile);
    }

    bool IsBoundary(Vector2Int tile)
    {
        // A cleared tile is boundary if any neighbor is NOT cleared (has miasma)
        return !clearedTiles.ContainsKey(new Vector2Int(tile.x - 1, tile.y)) ||
               !clearedTiles.ContainsKey(new Vector2Int(tile.x + 1, tile.y)) ||
               !clearedTiles.ContainsKey(new Vector2Int(tile.x, tile.y - 1)) ||
               !clearedTiles.ContainsKey(new Vector2Int(tile.x, tile.y + 1));
    }

    public Vector2Int WorldToTile(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / tileSize),
            Mathf.FloorToInt(worldPos.z / tileSize)
        );
    }

    public Vector3 TileToWorld(Vector2Int tile)
    {
        return new Vector3(
            (tile.x + 0.5f) * tileSize,
            0f,
            (tile.y + 0.5f) * tileSize
        );
    }
}
