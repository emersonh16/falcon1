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
    [Tooltip("World units per tile. 0.0625 = 1/8th of original 0.5")]
    public float tileSize = 0.0625f;  // World units per tile (1/8th of 0.5f = much smaller)
    public int viewPadding = 20;   // Extra tiles beyond viewport (increased since tiles are smaller)

    [Header("Regrowth")]
    public float regrowDelay = 1.5f;    // Seconds before regrowth can occur
    public float regrowChance = 0.15f;  // Chance per frame per eligible tile
    public int regrowBudget = 512;      // Max tiles to regrow per frame

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

    private bool beamSubscribed = false;

    void Start()
    {
        SubscribeToBeam();
    }

    void OnDestroy()
    {
        if (BeamManager.Instance != null)
        {
            BeamManager.Instance.OnBeamFired -= OnBeamFired;
            BeamManager.Instance.OnBeamFiredCone -= OnBeamFiredCone;
            BeamManager.Instance.OnBeamFiredLaser -= OnBeamFiredLaser;
        }
    }

    void SubscribeToBeam()
    {
        if (!beamSubscribed && BeamManager.Instance != null)
        {
            BeamManager.Instance.OnBeamFired += OnBeamFired;
            BeamManager.Instance.OnBeamFiredCone += OnBeamFiredCone;
            BeamManager.Instance.OnBeamFiredLaser += OnBeamFiredLaser;
            beamSubscribed = true;
        }
    }

    void Update()
    {
        // Retry subscription if not connected yet
        if (!beamSubscribed)
        {
            SubscribeToBeam();
        }

        // Note: Clearing is now handled via events from BeamRenderer
        // This direct polling is kept as backup but events are primary

        ProcessRegrowth();
    }

    void OnBeamFired(Vector3 worldPos, float radius)
    {
        if (radius > 0)
        {
            ClearArea(worldPos, radius);
        }
    }

    void OnBeamFiredCone(Vector3 origin, Vector3 direction, float length, float halfAngleDeg)
    {
        ClearCone(origin, direction, length, halfAngleDeg);
    }

    void OnBeamFiredLaser(Vector3 origin, Vector3 direction, float length, float thickness)
    {
        ClearLaser(origin, direction, length, thickness);
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
    /// Clear miasma in a cone shape (sector)
    /// </summary>
    public int ClearCone(Vector3 origin, Vector3 direction, float length, float halfAngleDeg)
    {
        int cleared = 0;
        float halfAngle = halfAngleDeg * Mathf.Deg2Rad;
        direction.y = 0f;  // Keep on XZ plane
        direction.Normalize();

        // Sample along the cone length
        float step = tileSize * 0.5f;  // Sample every half tile
        int steps = Mathf.CeilToInt(length / step);

        for (int i = 1; i <= steps; i++)
        {
            float dist = i * step;
            if (dist > length) dist = length;

            Vector3 center = origin + direction * dist;
            float radius = Mathf.Max(tileSize * 0.5f, Mathf.Tan(halfAngle) * dist);

            cleared += ClearArea(center, radius);
        }

        // Also clear at origin
        cleared += ClearArea(origin, tileSize * 0.5f);

        if (cleared > 0)
        {
            OnClearedChanged?.Invoke();
        }

        return cleared;
    }

    /// <summary>
    /// Clear miasma in a laser shape (line with thickness)
    /// </summary>
    public int ClearLaser(Vector3 origin, Vector3 direction, float length, float thickness)
    {
        int cleared = 0;
        direction.y = 0f;  // Keep on XZ plane
        direction.Normalize();

        // Sample along the laser length
        float step = tileSize * 0.4f;  // Sample frequently for smooth line
        int steps = Mathf.CeilToInt(length / step);
        float halfThick = thickness * 0.5f;

        for (int i = 0; i <= steps; i++)
        {
            float dist = i * step;
            if (dist > length) dist = length;

            Vector3 center = origin + direction * dist;
            cleared += ClearArea(center, halfThick);
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
