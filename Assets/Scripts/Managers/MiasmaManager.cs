using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages miasma state using inverse model - tracks cleared tiles only.
/// Miasma is assumed everywhere except where cleared.
/// Improved regrowth system with multi-zone filtering, TTL, and dynamic budgets.
/// </summary>
public class MiasmaManager : MonoBehaviour
{
    public static MiasmaManager Instance { get; private set; }

    [Header("Tile Settings")]
    [Tooltip("World units per tile")]
    public float tileSize = 0.25f;  // World units per tile
    public int viewPadding = 20;   // Extra tiles beyond viewport

    [Header("Regrowth - Basic")]
    [Tooltip("Seconds before regrowth can occur")]
    public float regrowDelay = 1.0f;
    [Tooltip("Base chance per frame per eligible tile (0-1)")]
    public float regrowChance = 0.6f;  // 60% like old code
    [Tooltip("Multiplier for regrow speed")]
    public float regrowSpeedFactor = 1.0f;
    [Tooltip("Base max tiles to regrow per frame (will scale with viewport)")]
    public int regrowBudget = 512;

    [Header("Regrowth - Zones")]
    [Tooltip("Padding for base keep zone (viewport + this)")]
    public int regrowScanPad = 6;
    [Tooltip("Tiles beyond keep zone where regrowth happens")]
    public int offscreenRegrowPad = 36;  // 6 * 6
    [Tooltip("Tiles beyond which cleared tiles are auto-forgotten")]
    public int offscreenForgetPad = 72;  // 6 * 12

    [Header("Memory Management")]
    [Tooltip("Max cleared tiles before oldest are removed (0 = unlimited)")]
    public int maxClearedCap = 50000;
    [Tooltip("Max frontier tiles to scan per frame (prevents huge scans)")]
    public int maxRegrowScanPerFrame = 4000;
    [Tooltip("Auto-remove cleared tiles older than this (seconds, 0 = disabled)")]
    public float clearedTTL = 20f;

    // Cleared tiles: key = tile coord, value = time cleared
    private Dictionary<Vector2Int, float> clearedTiles = new Dictionary<Vector2Int, float>();
    
    // Frontier: boundary tiles eligible for regrowth
    private HashSet<Vector2Int> frontier = new HashSet<Vector2Int>();

    // Dynamic budgets (calculated from viewport)
    private int currentRegrowBudget;
    private float currentViewW, currentViewH;

    // Player position tracking (for zone calculations)
    private Vector3 lastPlayerPos;
    private Camera mainCamera;

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
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
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

        // Update dynamic budgets if viewport changed
        UpdateBudgets();

        // Process regrowth with multi-zone filtering
        ProcessRegrowth();

        // Cleanup old tiles (TTL and hard cap)
        ProcessCleanup();
    }

    void UpdateBudgets()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        float viewW = mainCamera.orthographicSize * 2f * mainCamera.aspect;
        float viewH = mainCamera.orthographicSize * 2f;

        // Only recalculate if viewport changed significantly
        if (Mathf.Abs(viewW - currentViewW) > 0.1f || Mathf.Abs(viewH - currentViewH) > 0.1f)
        {
            currentViewW = viewW;
            currentViewH = viewH;

            int viewCols = Mathf.CeilToInt(viewW / tileSize);
            int viewRows = Mathf.CeilToInt(viewH / tileSize);
            int screenTiles = viewCols * viewRows;

            // Dynamic budget = max(screenTiles, baseBudget)
            currentRegrowBudget = Mathf.Max(screenTiles, regrowBudget);
        }
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
    /// Clear miasma in a cone shape (sector/pie slice)
    /// Matches the visual: pie slice from origin to length at given angle
    /// </summary>
    public int ClearCone(Vector3 origin, Vector3 direction, float length, float halfAngleDeg)
    {
        int cleared = 0;
        float halfAngle = halfAngleDeg * Mathf.Deg2Rad;
        direction.y = 0f;  // Keep on XZ plane
        direction.Normalize();

        // Calculate cone bounds in tile space
        int tileRadius = Mathf.CeilToInt(length / tileSize) + 1;
        Vector2Int originTile = WorldToTile(origin);
        float lengthSq = length * length;

        // Check all tiles in a square around origin
        for (int dx = -tileRadius; dx <= tileRadius; dx++)
        {
            for (int dz = -tileRadius; dz <= tileRadius; dz++)
            {
                Vector2Int tile = new Vector2Int(originTile.x + dx, originTile.y + dz);
                Vector3 tileWorld = TileToWorld(tile);
                
                // Vector from origin to tile center
                Vector3 toTile = tileWorld - origin;
                toTile.y = 0f;
                
                float distSq = toTile.sqrMagnitude;
                
                // Check if within length
                if (distSq > lengthSq) continue;
                
                // Check if within angle (cone)
                if (toTile.magnitude < 0.001f)
                {
                    // At origin, always clear
                    if (!clearedTiles.ContainsKey(tile))
                    {
                        clearedTiles[tile] = Time.time;
                        cleared++;
                        UpdateFrontier(tile);
                    }
                    continue;
                }
                
                toTile.Normalize();
                
                // Calculate angle between direction and toTile
                float dot = Vector3.Dot(direction, toTile);
                float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f));
                
                // Check if within halfAngle
                if (angle <= halfAngle)
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

        // Get player position for zone calculations
        Vector3 playerPos = GetPlayerPosition();
        if (playerPos == Vector3.zero && mainCamera != null)
        {
            // Fallback: use camera position projected to ground
            playerPos = mainCamera.transform.position;
            playerPos.y = 0f;
        }

        // Calculate zones based on viewport
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        float viewW = mainCamera.orthographicSize * 2f * mainCamera.aspect;
        float viewH = mainCamera.orthographicSize * 2f;
        int viewCols = Mathf.CeilToInt(viewW / tileSize);
        int viewRows = Mathf.CeilToInt(viewH / tileSize);

        Vector2Int centerTile = WorldToTile(playerPos);

        // Base keep zone (viewport + scanPad)
        int keepLeft = centerTile.x - viewCols / 2 - regrowScanPad;
        int keepTop = centerTile.y - viewRows / 2 - regrowScanPad;
        int keepRight = keepLeft + viewCols + regrowScanPad * 2;
        int keepBottom = keepTop + viewRows + regrowScanPad * 2;

        // Extended regrow zone (keep + offscreenRegrowPad)
        int regLeft = keepLeft - offscreenRegrowPad;
        int regTop = keepTop - offscreenRegrowPad;
        int regRight = keepRight + offscreenRegrowPad;
        int regBottom = keepBottom + offscreenRegrowPad;

        // Far forget zone (beyond offscreenForgetPad)
        int forgetLeft = keepLeft - Mathf.Max(offscreenForgetPad, offscreenRegrowPad + regrowScanPad);
        int forgetTop = keepTop - Mathf.Max(offscreenForgetPad, offscreenRegrowPad + regrowScanPad);
        int forgetRight = keepRight + Mathf.Max(offscreenForgetPad, offscreenRegrowPad + regrowScanPad);
        int forgetBottom = keepBottom + Mathf.Max(offscreenForgetPad, offscreenRegrowPad + regrowScanPad);

        // Process regrowth with zone filtering
        int budget = currentRegrowBudget;
        List<Vector2Int> toRegrow = new List<Vector2Int>();
        List<Vector2Int> toForget = new List<Vector2Int>();
        float currentTime = Time.time;
        float chance = regrowChance * regrowSpeedFactor;
        int scanned = 0;

        foreach (var tile in frontier)
        {
            if (budget <= 0 || scanned >= maxRegrowScanPerFrame) break;
            scanned++;

            if (!clearedTiles.TryGetValue(tile, out float clearedTime))
            {
                frontier.Remove(tile);
                continue;
            }

            // Check if tile is in forget zone - auto-forget
            if (tile.x < forgetLeft || tile.x >= forgetRight || tile.y < forgetTop || tile.y >= forgetBottom)
            {
                toForget.Add(tile);
                continue;
            }

            // Skip if outside regrow zone (but not in forget zone)
            if (tile.x < regLeft || tile.x >= regRight || tile.y < regTop || tile.y >= regBottom)
            {
                continue;
            }

            // Must be boundary to regrow
            if (!IsBoundary(tile))
            {
                frontier.Remove(tile);
                continue;
            }

            // Check delay
            if (currentTime - clearedTime < regrowDelay) continue;

            // Random chance
            if (UnityEngine.Random.value < chance)
            {
                toRegrow.Add(tile);
                budget--;
            }
        }

        // Apply forget (remove far-off tiles)
        foreach (var tile in toForget)
        {
            RemoveClearedTile(tile);
        }

        // Apply regrowth
        foreach (var tile in toRegrow)
        {
            RemoveClearedTile(tile);
        }

        if (toRegrow.Count > 0 || toForget.Count > 0)
        {
            OnClearedChanged?.Invoke();
        }
    }

    void ProcessCleanup()
    {
        if (clearedTiles.Count == 0) return;

        float currentTime = Time.time;
        List<Vector2Int> toRemove = new List<Vector2Int>();

        // TTL cleanup: remove tiles older than clearedTTL
        if (clearedTTL > 0)
        {
            foreach (var kvp in clearedTiles)
            {
                if (currentTime - kvp.Value > clearedTTL)
                {
                    toRemove.Add(kvp.Key);
                }
            }
        }

        // Hard cap cleanup: if exceeded, remove oldest tiles
        if (maxClearedCap > 0 && clearedTiles.Count > maxClearedCap)
        {
            int overflow = clearedTiles.Count - maxClearedCap;
            // Get oldest tiles
            var sorted = clearedTiles.OrderBy(kvp => kvp.Value).Take(overflow);
            foreach (var kvp in sorted)
            {
                if (!toRemove.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }
        }

        // Apply removals
        foreach (var tile in toRemove)
        {
            RemoveClearedTile(tile);
        }

        if (toRemove.Count > 0)
        {
            OnClearedChanged?.Invoke();
        }
    }

    void RemoveClearedTile(Vector2Int tile)
    {
        if (!clearedTiles.ContainsKey(tile)) return;

        clearedTiles.Remove(tile);
        frontier.Remove(tile);
        UpdateNeighborFrontier(tile);
    }

    Vector3 GetPlayerPosition()
    {
        var derelict = GameObject.Find("Derelict");
        if (derelict != null)
        {
            return derelict.transform.position;
        }
        return Vector3.zero;
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
