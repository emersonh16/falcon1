using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A rock formation made of voxels (isometric squares).
/// Can be short (all voxels below miasma) or tall (some voxels poke through).
/// </summary>
public class Rock : MonoBehaviour
{
    [Header("Rock Settings")]
    public float voxelSize = 1.0f;  // 4x miasma tile size (0.25 * 4) - depth dimension
    public float voxelWidth = 1.2f;  // Width of rectangle (longer dimension for isometric look)
    public float voxelHeight = 0.3f;  // Height of each voxel (for mountain-like stacking)
    public float tallThreshold = 0.05f;  // Height above which voxels are "tall"
    public Color rockColor = new Color(0.6f, 0.4f, 0.2f);  // Brown
    
    private List<RockVoxel> voxels = new List<RockVoxel>();
    private List<RockVoxel> shortVoxels = new List<RockVoxel>();
    private List<RockVoxel> tallVoxels = new List<RockVoxel>();
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshFilter tallMeshFilter;
    private MeshRenderer tallMeshRenderer;
    private Material rockMaterial;
    
    public bool IsTall { get; private set; }
    public Bounds Bounds { get; private set; }
    public List<RockVoxel> Voxels => voxels;
    public List<RockVoxel> TallVoxels => tallVoxels;

    void Awake()
    {
        // Short rock mesh (below miasma)
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        
        // Tall rock mesh (above miasma) - separate GameObject child
        GameObject tallRockObj = new GameObject("TallVoxels");
        tallRockObj.transform.SetParent(transform);
        tallRockObj.transform.localPosition = Vector3.zero;
        tallRockObj.transform.localRotation = Quaternion.identity;
        tallMeshFilter = tallRockObj.AddComponent<MeshFilter>();
        tallMeshRenderer = tallRockObj.AddComponent<MeshRenderer>();
        
        // Create material
        rockMaterial = new Material(Shader.Find("Sprites/Default"));
        rockMaterial.color = rockColor;
        meshRenderer.material = rockMaterial;
        tallMeshRenderer.material = rockMaterial;
        
        // Set sorting: short rocks below miasma, tall rocks above
        // Use sorting layers or ensure proper Z-ordering
        meshRenderer.sortingOrder = -1;  // Below miasma
        tallMeshRenderer.sortingOrder = 1;  // Above miasma
    }

    /// <summary>
    /// Generate a random organic rock shape from voxels.
    /// </summary>
    public void GenerateRock(int voxelCount, Vector3 basePosition, bool forceTall = false)
    {
        voxels.Clear();
        
        // Generate random organic shape by stacking voxels
        HashSet<Vector3> usedPositions = new HashSet<Vector3>();
        List<Vector3> currentLayer = new List<Vector3>();
        
        // Start with base voxel
        Vector3 baseVoxelPos = basePosition;
        baseVoxelPos.y = Random.Range(0f, voxelHeight * 0.5f);  // Start low
        currentLayer.Add(baseVoxelPos);
        usedPositions.Add(baseVoxelPos);
        
        // Build up layers organically (mountain-like)
        for (int i = 1; i < voxelCount; i++)
        {
            if (currentLayer.Count == 0) break;
            
            // Pick a random voxel from current layer to build on
            Vector3 parent = currentLayer[Random.Range(0, currentLayer.Count)];
            
            // Try to place new voxel adjacent to parent (more vertical stacking for mountains)
            Vector3[] offsets = new Vector3[]
            {
                new Vector3(voxelSize, 0, 0),
                new Vector3(-voxelSize, 0, 0),
                new Vector3(0, 0, voxelSize),
                new Vector3(0, 0, -voxelSize),
                new Vector3(voxelSize * 0.5f, voxelHeight, voxelSize * 0.5f),  // Up and diagonal (mountain stacking)
                new Vector3(-voxelSize * 0.5f, voxelHeight, voxelSize * 0.5f),
                new Vector3(voxelSize * 0.5f, voxelHeight, -voxelSize * 0.5f),
                new Vector3(-voxelSize * 0.5f, voxelHeight, -voxelSize * 0.5f),
                new Vector3(0, voxelHeight, 0),  // Directly on top
            };
            
            Vector3? newPos = null;
            int attempts = 0;
            while (newPos == null && attempts < 20)
            {
                Vector3 offset = offsets[Random.Range(0, offsets.Length)];
                Vector3 candidate = parent + offset;
                
                // Snap to voxel grid
                candidate.x = Mathf.Round(candidate.x / voxelSize) * voxelSize;
                candidate.z = Mathf.Round(candidate.z / voxelSize) * voxelSize;
                candidate.y = Mathf.Round(candidate.y / voxelHeight) * voxelHeight;
                
                if (!usedPositions.Contains(candidate))
                {
                    newPos = candidate;
                    usedPositions.Add(candidate);
                    currentLayer.Add(candidate);
                }
                attempts++;
            }
            
            if (newPos == null)
            {
                // Fallback: place randomly near existing voxels
                Vector3 randomParent = currentLayer[Random.Range(0, currentLayer.Count)];
                Vector3 fallback = randomParent + new Vector3(
                    Random.Range(-voxelSize, voxelSize),
                    Random.Range(0, voxelSize * 2),
                    Random.Range(-voxelSize, voxelSize)
                );
                fallback.x = Mathf.Round(fallback.x / voxelSize) * voxelSize;
                fallback.z = Mathf.Round(fallback.z / voxelSize) * voxelSize;
                fallback.y = Mathf.Round(fallback.y / voxelHeight) * voxelHeight;
                
                if (!usedPositions.Contains(fallback))
                {
                    usedPositions.Add(fallback);
                    currentLayer.Add(fallback);
                }
            }
        }
        
        // Convert to voxels and separate into short/tall
        bool hasTallVoxel = false;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        Vector3 minPos = Vector3.positiveInfinity;
        Vector3 maxPos = Vector3.negativeInfinity;
        
        shortVoxels.Clear();
        tallVoxels.Clear();
        
        foreach (Vector3 pos in usedPositions)
        {
            bool isTall = pos.y >= tallThreshold;
            if (isTall) hasTallVoxel = true;
            
            RockVoxel voxel = new RockVoxel(pos, isTall);
            voxels.Add(voxel);
            
            // Separate into short and tall lists
            if (isTall)
            {
                tallVoxels.Add(voxel);
            }
            else
            {
                shortVoxels.Add(voxel);
            }
            
            minY = Mathf.Min(minY, pos.y);
            maxY = Mathf.Max(maxY, pos.y);
            minPos = Vector3.Min(minPos, pos);
            maxPos = Vector3.Max(maxPos, pos);
        }
        
        IsTall = hasTallVoxel || forceTall;
        
        // Calculate bounds
        Bounds = new Bounds((minPos + maxPos) * 0.5f, maxPos - minPos + Vector3.one * voxelSize);
        
        // Generate visual meshes (separate for short and tall)
        GenerateMesh();
    }

    void GenerateMesh()
    {
        // Generate short voxel mesh (below miasma)
        Mesh shortMesh = new Mesh();
        List<Vector3> shortVertices = new List<Vector3>();
        List<int> shortTriangles = new List<int>();
        
        foreach (var voxel in shortVoxels)
        {
            CreateIsometricRectangle(voxel.position, voxelSize, voxelWidth, voxelHeight, shortVertices, shortTriangles);
        }
        
        if (shortVertices.Count > 0)
        {
            shortMesh.vertices = shortVertices.ToArray();
            shortMesh.triangles = shortTriangles.ToArray();
            shortMesh.RecalculateNormals();
            meshFilter.mesh = shortMesh;
            meshRenderer.enabled = true;
        }
        else
        {
            meshRenderer.enabled = false;
        }
        
        // Generate tall voxel mesh (above miasma)
        Mesh tallMesh = new Mesh();
        List<Vector3> tallVertices = new List<Vector3>();
        List<int> tallTriangles = new List<int>();
        
        foreach (var voxel in tallVoxels)
        {
            CreateIsometricRectangle(voxel.position, voxelSize, voxelWidth, voxelHeight, tallVertices, tallTriangles);
        }
        
        if (tallVertices.Count > 0)
        {
            tallMesh.vertices = tallVertices.ToArray();
            tallMesh.triangles = tallTriangles.ToArray();
            tallMesh.RecalculateNormals();
            tallMeshFilter.mesh = tallMesh;
            tallMeshRenderer.enabled = true;
        }
        else
        {
            tallMeshRenderer.enabled = false;
        }
    }

    void CreateIsometricRectangle(Vector3 center, float depth, float width, float height, List<Vector3> vertices, List<int> triangles)
    {
        // Create an isometric rectangle (wider in one dimension) - appears as diamond from isometric camera
        // Width = longer dimension (X), Depth = shorter dimension (Z), Height = Y
        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;
        float halfHeight = height * 0.5f;
        int baseIndex = vertices.Count;
        
        // Create rectangular block (wider in X direction for isometric look)
        // Top face vertices
        vertices.Add(center + new Vector3(-halfWidth, halfHeight, -halfDepth));  // Top-left-back
        vertices.Add(center + new Vector3(halfWidth, halfHeight, -halfDepth));   // Top-right-back
        vertices.Add(center + new Vector3(halfWidth, halfHeight, halfDepth));    // Top-right-front
        vertices.Add(center + new Vector3(-halfWidth, halfHeight, halfDepth));   // Top-left-front
        
        // Bottom face vertices
        vertices.Add(center + new Vector3(-halfWidth, -halfHeight, -halfDepth)); // Bottom-left-back
        vertices.Add(center + new Vector3(halfWidth, -halfHeight, -halfDepth));  // Bottom-right-back
        vertices.Add(center + new Vector3(halfWidth, -halfHeight, halfDepth));   // Bottom-right-front
        vertices.Add(center + new Vector3(-halfWidth, -halfHeight, halfDepth));  // Bottom-left-front
        
        // Top face
        triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 2);
        
        // Bottom face
        triangles.Add(baseIndex + 4); triangles.Add(baseIndex + 5); triangles.Add(baseIndex + 6);
        triangles.Add(baseIndex + 4); triangles.Add(baseIndex + 6); triangles.Add(baseIndex + 7);
        
        // Side faces (4 sides)
        // Front face
        triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 6);
        triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 6); triangles.Add(baseIndex + 7);
        
        // Back face
        triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 5); triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 4); triangles.Add(baseIndex + 5);
        
        // Left face
        triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 7);
        triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 7); triangles.Add(baseIndex + 4);
        
        // Right face
        triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 6); triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 5); triangles.Add(baseIndex + 6);
    }
}
