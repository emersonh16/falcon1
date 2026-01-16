using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// ECS Component: Represents a single miasma tile.
/// Each tile is an entity with this component.
/// </summary>
public struct MiasmaTileComponent : IComponentData
{
    public int2 tileCoord;      // Tile coordinate (X, Z in world)
    public float3 worldPosition; // World position of tile center
    public bool isCleared;      // True if cleared, false if has miasma
    public float timeCleared;   // Time when cleared (for regrowth)
    public bool isFrontier;     // True if on boundary (eligible for regrowth)
}

/// <summary>
/// Tag component: Marks tiles that should be rendered
/// </summary>
public struct MiasmaRenderTag : IComponentData { }

/// <summary>
/// Tag component: Marks tiles that need to be updated
/// </summary>
public struct MiasmaNeedsUpdate : IComponentData { }
