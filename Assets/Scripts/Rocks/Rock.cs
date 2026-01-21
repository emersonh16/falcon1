using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A rock formation made of voxels (isometric squares).
/// Can be short (all voxels below miasma) or tall (some voxels poke through).
/// </summary>
public class Rock : MonoBehaviour
{
    [Header("Rock Settings")]
    public float voxelSize = 0.25f;  // Same as miasma tile size - depth dimension
    public float voxelWidth = 0.3f;  // Width of rectangle (longer dimension for isometric look) - 1/4th of 1.2
    public float voxelHeight = 0.075f;  // Height of each voxel (for mountain-like stacking) - 1/4th of 0.3
    public float tallThreshold = 0.05f;  // Height above which voxels are "tall"
    public Color rockColor = new Color(0.6f, 0.4f, 0.2f);  // Brown
    
    private List<RockVoxel> voxels = new List<RockVoxel>();
    private List<RockVoxel> shortVoxels = new List<RockVoxel>();
    private List<RockVoxel> tallVoxels = new List<RockVoxel>();
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshFilter tallMeshFilter;
    private MeshRenderer tallMeshRenderer;
    private Material shortRockMaterial;  // Material for short rocks (below miasma)
    private Material tallRockMaterial;   // Material for tall rocks (above miasma)
    
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
        
        // Create separate materials for short and tall rocks
        // Using Y-position for depth sorting instead of render queue (more reliable for transparent objects)
        
        shortRockMaterial = new Material(Shader.Find("Sprites/Default"));
        shortRockMaterial.color = rockColor;
        // Short rocks will be at Y < 0.01, so depth sorting puts them behind miasma
        
        tallRockMaterial = new Material(Shader.Find("Sprites/Default"));
        tallRockMaterial.color = rockColor;
        // Tall rocks will be at Y > 0.01, so depth sorting puts them in front of miasma
        
        // Ensure materials are separate instances
        meshRenderer.material = shortRockMaterial;
        tallMeshRenderer.material = tallRockMaterial;
        
        // Verify materials are different
        if (meshRenderer.material == tallMeshRenderer.material)
        {
            Debug.LogError("Rock materials are the same! This will cause rendering issues.");
        }
        
        // Set sorting: short rocks below miasma, tall rocks above
        meshRenderer.sortingOrder = -1;  // Below miasma
        tallMeshRenderer.sortingOrder = 1;  // Above miasma
        
        // Debug: Log render queues
        Debug.Log($"Short rock render queue: {shortRockMaterial.renderQueue}, Tall rock render queue: {tallRockMaterial.renderQueue}");
    }

    /// <summary>
    /// Generate a large organic mountain/boulder formation from voxels.
    /// Uses layered growth with natural tapering for mountain/boulder ranges.
    /// </summary>
    public void GenerateRock(int voxelCount, Vector3 basePosition, bool forceTall = false)
    {
        voxels.Clear();
        
        // Clamp voxel count to desired range - MUCH larger formations
        voxelCount = Mathf.Clamp(voxelCount, 200, 800);
        
        // Determine formation type: mountain (tall) or boulder (wide)
        bool isMountain = Random.value > 0.4f;  // 60% mountains, 40% boulders
        
        HashSet<Vector3> usedPositions = new HashSet<Vector3>();
        
        // Calculate base size based on formation type
        float baseRadius = isMountain ? 
            Mathf.Sqrt(voxelCount * 0.3f) * voxelSize :  // Taller, narrower base
            Mathf.Sqrt(voxelCount * 0.5f) * voxelSize;   // Wider, flatter base
        
        // Generate base layer (wide foundation)
        List<Vector3> baseLayer = GenerateBaseLayer(basePosition, baseRadius, usedPositions);
        
        // Build up in layers with natural tapering
        List<Vector3> currentLayer = new List<Vector3>(baseLayer);
        int layersGenerated = 1;
        float currentHeight = 0f;
        
        // Continue building layers until we reach voxel count
        while (usedPositions.Count < voxelCount && currentLayer.Count > 0)
        {
            List<Vector3> nextLayer = new List<Vector3>();
            
            // Calculate layer size (taper as we go up)
            float layerProgress = (float)layersGenerated / 20f;  // Assume max ~20 layers
            float layerScale = Mathf.Lerp(1f, 0.3f, layerProgress);  // Taper from 100% to 30%
            
            // For each voxel in current layer, try to build on top
            foreach (Vector3 parent in currentLayer)
            {
                if (usedPositions.Count >= voxelCount) break;
                
                // Decide if this voxel should have children (not all do)
                float buildChance = isMountain ? 0.7f : 0.5f;  // Mountains build up more
                if (Random.value > buildChance) continue;
                
                // Try to place voxels on top and around parent
                int childrenToPlace = Random.Range(1, 4);  // 1-3 children per parent
                
                for (int c = 0; c < childrenToPlace && usedPositions.Count < voxelCount; c++)
                {
                    // Offset options: directly up, or up+diagonal
                    Vector3[] offsets = new Vector3[]
                    {
                        new Vector3(0, voxelHeight, 0),  // Directly up
                        new Vector3(voxelSize * layerScale, voxelHeight, 0),
                        new Vector3(-voxelSize * layerScale, voxelHeight, 0),
                        new Vector3(0, voxelHeight, voxelSize * layerScale),
                        new Vector3(0, voxelHeight, -voxelSize * layerScale),
                        new Vector3(voxelSize * layerScale * 0.7f, voxelHeight, voxelSize * layerScale * 0.7f),
                        new Vector3(-voxelSize * layerScale * 0.7f, voxelHeight, voxelSize * layerScale * 0.7f),
                        new Vector3(voxelSize * layerScale * 0.7f, voxelHeight, -voxelSize * layerScale * 0.7f),
                        new Vector3(-voxelSize * layerScale * 0.7f, voxelHeight, -voxelSize * layerScale * 0.7f),
                    };
                    
                    Vector3 offset = offsets[Random.Range(0, offsets.Length)];
                    Vector3 candidate = parent + offset;
                    
                    // Snap to voxel grid
                    candidate.x = Mathf.Round(candidate.x / voxelSize) * voxelSize;
                    candidate.z = Mathf.Round(candidate.z / voxelSize) * voxelSize;
                    candidate.y = Mathf.Round(candidate.y / voxelHeight) * voxelHeight;
                    
                    // Check if position is valid (not too far from center for natural shape)
                    float distFromBase = Vector3.Distance(new Vector3(candidate.x, 0, candidate.z), 
                                                         new Vector3(basePosition.x, 0, basePosition.z));
                    if (distFromBase > baseRadius * (1f + layerProgress * 2f)) continue;  // Allow some spread
                    
                    if (!usedPositions.Contains(candidate))
                    {
                        usedPositions.Add(candidate);
                        nextLayer.Add(candidate);
                        currentHeight = Mathf.Max(currentHeight, candidate.y);
                    }
                }
            }
            
            currentLayer = nextLayer;
            layersGenerated++;
            
            // Safety break if we're not making progress
            if (nextLayer.Count == 0 && usedPositions.Count < voxelCount * 0.5f)
            {
                // Fill remaining with random placements near existing
                FillRemainingVoxels(usedPositions, basePosition, baseRadius, voxelCount);
                break;
            }
        }
        
        // Fill any remaining voxel count with organic spread
        if (usedPositions.Count < voxelCount)
        {
            FillRemainingVoxels(usedPositions, basePosition, baseRadius, voxelCount);
        }
        
        // Convert to voxels and separate into short/tall
        bool hasTallVoxel = false;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        Vector3 minPos = Vector3.positiveInfinity;
        Vector3 maxPos = Vector3.negativeInfinity;
        
        shortVoxels.Clear();
        tallVoxels.Clear();
        
        // Miasma is at Y=0.01, so separate voxels based on that height
        float miasmaHeight = 0.01f;
        
        foreach (Vector3 pos in usedPositions)
        {
            // Determine if voxel is tall (above miasma) for rendering purposes
            bool isTallForRendering = pos.y >= miasmaHeight;
            
            // Also check if it's tall for gameplay (above tallThreshold)
            bool isTallForGameplay = pos.y >= tallThreshold;
            if (isTallForGameplay) hasTallVoxel = true;
            
            RockVoxel voxel = new RockVoxel(pos, isTallForGameplay);
            voxels.Add(voxel);
            
            // Separate into short and tall lists based on miasma height (for rendering)
            if (isTallForRendering)
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
        
        // Add collision
        AddColliders();
    }

    /// <summary>
    /// Generate the base layer of the formation (wide foundation).
    /// </summary>
    List<Vector3> GenerateBaseLayer(Vector3 center, float radius, HashSet<Vector3> usedPositions)
    {
        List<Vector3> baseLayer = new List<Vector3>();
        
        // Create a roughly circular/elliptical base
        int baseVoxels = Mathf.RoundToInt(radius / voxelSize * 2f);  // Rough estimate
        baseVoxels = Mathf.Clamp(baseVoxels, 5, 30);  // Reasonable base size
        
        for (int i = 0; i < baseVoxels; i++)
        {
            // Random position in circle/ellipse
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(0f, radius);
            
            // Add some randomness for organic shape
            distance *= Random.Range(0.7f, 1.3f);
            
            Vector3 pos = center + new Vector3(
                Mathf.Cos(angle) * distance,
                Random.Range(0f, voxelHeight * 0.5f),  // Slight height variation
                Mathf.Sin(angle) * distance
            );
            
            // Snap to grid
            pos.x = Mathf.Round(pos.x / voxelSize) * voxelSize;
            pos.z = Mathf.Round(pos.z / voxelSize) * voxelSize;
            pos.y = Mathf.Round(pos.y / voxelHeight) * voxelHeight;
            
            if (!usedPositions.Contains(pos))
            {
                usedPositions.Add(pos);
                baseLayer.Add(pos);
            }
        }
        
        return baseLayer;
    }

    /// <summary>
    /// Fill remaining voxel count with organic spread near existing formation.
    /// </summary>
    void FillRemainingVoxels(HashSet<Vector3> usedPositions, Vector3 basePosition, float baseRadius, int targetCount)
    {
        int attempts = 0;
        int maxAttempts = (targetCount - usedPositions.Count) * 10;
        
        while (usedPositions.Count < targetCount && attempts < maxAttempts)
        {
            // Pick a random existing position
            List<Vector3> existing = new List<Vector3>(usedPositions);
            if (existing.Count == 0) break;
            
            Vector3 parent = existing[Random.Range(0, existing.Count)];
            
            // Try to place nearby
            Vector3 offset = new Vector3(
                Random.Range(-voxelSize * 2f, voxelSize * 2f),
                Random.Range(0, voxelHeight * 2f),
                Random.Range(-voxelSize * 2f, voxelSize * 2f)
            );
            
            Vector3 candidate = parent + offset;
            
            // Snap to grid
            candidate.x = Mathf.Round(candidate.x / voxelSize) * voxelSize;
            candidate.z = Mathf.Round(candidate.z / voxelSize) * voxelSize;
            candidate.y = Mathf.Round(candidate.y / voxelHeight) * voxelHeight;
            
            // Keep within reasonable bounds
            float distFromBase = Vector3.Distance(new Vector3(candidate.x, 0, candidate.z), 
                                                 new Vector3(basePosition.x, 0, basePosition.z));
            if (distFromBase > baseRadius * 1.5f) 
            {
                attempts++;
                continue;
            }
            
            if (!usedPositions.Contains(candidate))
            {
                usedPositions.Add(candidate);
            }
            
            attempts++;
        }
    }

    void GenerateMesh()
    {
        // Generate short voxel mesh (below miasma)
        Mesh shortMesh = new Mesh();
        List<Vector3> shortVertices = new List<Vector3>();
        List<int> shortTriangles = new List<int>();
        
        foreach (var voxel in shortVoxels)
        {
            // Position short rocks BELOW miasma (Y=0.01) so depth sorting puts them behind miasma
            // All short voxels should be at Y <= 0.005 (well below miasma at 0.01)
            Vector3 voxelPos = voxel.position;
            voxelPos.y = Mathf.Min(voxelPos.y, 0.005f);  // Force below miasma
            CreateIsometricRectangle(voxelPos, voxelSize, voxelWidth, voxelHeight, shortVertices, shortTriangles);
        }
        
        if (shortVertices.Count > 0)
        {
            shortMesh.vertices = shortVertices.ToArray();
            shortMesh.triangles = shortTriangles.ToArray();
            shortMesh.RecalculateNormals();
            meshFilter.mesh = shortMesh;
            meshRenderer.enabled = true;
            
            // Update material color if rockColor changed in inspector
            if (shortRockMaterial != null)
            {
                shortRockMaterial.color = rockColor;
            }
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
            // Position tall rocks ABOVE miasma (Y=0.01) so depth sorting puts them in front
            // Only the parts that are tall (above threshold) should be above miasma
            Vector3 voxelPos = voxel.position;
            // If this voxel is below miasma height, move it above
            if (voxelPos.y <= 0.01f)
            {
                voxelPos.y = 0.015f;  // Above miasma at Y=0.01
            }
            // If already above, keep it there
            CreateIsometricRectangle(voxelPos, voxelSize, voxelWidth, voxelHeight, tallVertices, tallTriangles);
        }
        
        if (tallVertices.Count > 0)
        {
            tallMesh.vertices = tallVertices.ToArray();
            tallMesh.triangles = tallTriangles.ToArray();
            tallMesh.RecalculateNormals();
            tallMeshFilter.mesh = tallMesh;
            tallMeshRenderer.enabled = true;
            
            // Update material color if rockColor changed in inspector
            if (tallRockMaterial != null)
            {
                tallRockMaterial.color = rockColor;
            }
        }
        else
        {
            tallMeshRenderer.enabled = false;
        }
    }

    void AddColliders()
    {
        // Remove existing colliders if any
        Collider[] existingColliders = GetComponents<Collider>();
        foreach (var col in existingColliders)
        {
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }
        
        // Remove colliders from children too
        Collider[] childColliders = GetComponentsInChildren<Collider>();
        foreach (var col in childColliders)
        {
            if (col.gameObject != gameObject)
            {
                if (Application.isPlaying)
                    Destroy(col);
                else
                    DestroyImmediate(col);
            }
        }
        
        if (voxels.Count == 0) return;
        
        // Unity best practice: Use BoxColliders per voxel (compound collider)
        // This is more performant and reliable than MeshCollider for complex shapes
        // Mark rock as static for physics optimization
        gameObject.isStatic = true;
        
        // Create EXACT colliders for each voxel - no bubble effect
        // Each collider matches the exact size and position of the visual voxel
        int colliderCount = 0;
        foreach (var voxel in voxels)
        {
            // Calculate local position (relative to rock transform)
            Vector3 localPos = transform.InverseTransformPoint(voxel.position);
            
            // Create BoxCollider that EXACTLY matches the voxel visual in X and Z
            // Y height needs to be tall enough for CharacterController to detect (spans Y=-0.5 to Y=0.5)
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            
            // X and Z dimensions: EXACT match to visual (no bubble)
            // Y dimension: tall enough for CharacterController detection (minimum 1.0 to span Y=-0.5 to Y=0.5)
            float colliderHeight = Mathf.Max(voxelHeight, 1.0f);  // At least 1.0 units tall for reliable collision
            
            boxCollider.center = localPos;
            boxCollider.size = new Vector3(voxelWidth, colliderHeight, voxelSize);  // Exact X/Z, tall enough Y
            boxCollider.enabled = true;
            boxCollider.isTrigger = false;
            
            colliderCount++;
        }
        
        // Force physics update to ensure colliders are registered
        Physics.SyncTransforms();
        
        // Debug log to verify colliders were created
        if (Application.isPlaying)
        {
            BoxCollider[] colliders = GetComponents<BoxCollider>();
            Debug.Log($"Rock '{gameObject.name}': {colliderCount} BoxColliders, IsStatic={gameObject.isStatic}, WorldPos={transform.position}");
            
            if (colliders.Length > 0)
            {
                // Check first collider world bounds
                Bounds worldBounds = colliders[0].bounds;
                Debug.Log($"  First collider world bounds: min={worldBounds.min}, max={worldBounds.max}, center={worldBounds.center}, size={worldBounds.size}");
            }
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
