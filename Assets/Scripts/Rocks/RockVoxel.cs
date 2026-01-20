using UnityEngine;

/// <summary>
/// Represents a single voxel (isometric square) in a rock formation.
/// </summary>
[System.Serializable]
public struct RockVoxel
{
    public Vector3 position;  // World position
    public bool isTall;       // True if above miasma threshold (Y >= 0.05)
    
    public RockVoxel(Vector3 pos, bool tall)
    {
        position = pos;
        isTall = tall;
    }
}
