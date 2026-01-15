using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders miasma as a sheet with holes where cleared.
/// Uses GPU instancing for performance.
/// </summary>
public class MiasmaRenderer : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Camera mainCamera;

    [Header("Visual Settings")]
    public Color miasmaColor = new Color(0.5f, 0f, 0.7f, 0.9f);  // Purple
    public float renderHeight = 0.01f;  // Y position of miasma sheet

    [Header("Performance")]
    public int maxTilesPerBatch = 1023;  // Unity limit for DrawMeshInstanced

    private Mesh tileMesh;
    private Material miasmaMaterial;
    private Matrix4x4[] matrices;
    private List<Matrix4x4> visibleMatrices = new List<Matrix4x4>();
    private MaterialPropertyBlock propertyBlock;

    private int lastMinX, lastMaxX, lastMinZ, lastMaxZ;
    private bool needsRebuild = true;

    void Start()
    {
        CreateTileMesh();
        CreateMaterial();
        matrices = new Matrix4x4[maxTilesPerBatch];
        propertyBlock = new MaterialPropertyBlock();

        // Subscribe to changes
        if (MiasmaManager.Instance != null)
        {
            MiasmaManager.Instance.OnClearedChanged += OnClearedChanged;
        }

        // Find player if not assigned
        if (player == null)
        {
            var derelict = GameObject.Find("Derelict");
            if (derelict != null) player = derelict.transform;
        }

        // Find camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void OnDestroy()
    {
        if (MiasmaManager.Instance != null)
        {
            MiasmaManager.Instance.OnClearedChanged -= OnClearedChanged;
        }
    }

    void OnClearedChanged()
    {
        needsRebuild = true;
    }

    void Update()
    {
        if (MiasmaManager.Instance == null || player == null) return;

        // Check if view bounds changed
        float viewW = mainCamera.orthographicSize * 2f * mainCamera.aspect;
        float viewH = mainCamera.orthographicSize * 2f;

        int minX, maxX, minZ, maxZ;
        MiasmaManager.Instance.GetVisibleBounds(player.position, viewW, viewH,
            out minX, out maxX, out minZ, out maxZ);

        if (minX != lastMinX || maxX != lastMaxX || minZ != lastMinZ || maxZ != lastMaxZ)
        {
            needsRebuild = true;
            lastMinX = minX; lastMaxX = maxX;
            lastMinZ = minZ; lastMaxZ = maxZ;
        }

        if (needsRebuild)
        {
            RebuildMesh();
            needsRebuild = false;
        }

        // Draw miasma tiles
        DrawMiasma();
    }

    void RebuildMesh()
    {
        visibleMatrices.Clear();

        if (MiasmaManager.Instance == null) return;

        float tileSize = MiasmaManager.Instance.tileSize;
        HashSet<Vector2Int> clearedSet = new HashSet<Vector2Int>(MiasmaManager.Instance.GetClearedTiles());

        // Generate matrices for all visible tiles that are NOT cleared
        for (int x = lastMinX; x <= lastMaxX; x++)
        {
            for (int z = lastMinZ; z <= lastMaxZ; z++)
            {
                Vector2Int tile = new Vector2Int(x, z);
                
                // Skip cleared tiles (no miasma there)
                if (clearedSet.Contains(tile)) continue;

                Vector3 pos = new Vector3(
                    (x + 0.5f) * tileSize,
                    renderHeight,
                    (z + 0.5f) * tileSize
                );

                Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(tileSize, 1f, tileSize));
                visibleMatrices.Add(matrix);
            }
        }
    }

    void DrawMiasma()
    {
        if (visibleMatrices.Count == 0) return;

        // Draw in batches (Unity limit is 1023 per call)
        int drawn = 0;
        while (drawn < visibleMatrices.Count)
        {
            int batchSize = Mathf.Min(maxTilesPerBatch, visibleMatrices.Count - drawn);
            
            for (int i = 0; i < batchSize; i++)
            {
                matrices[i] = visibleMatrices[drawn + i];
            }

            Graphics.DrawMeshInstanced(tileMesh, 0, miasmaMaterial, matrices, batchSize, propertyBlock);
            drawn += batchSize;
        }
    }

    void CreateTileMesh()
    {
        // Simple quad (1x1, will be scaled by matrix)
        tileMesh = new Mesh();

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(-0.5f, 0f, 0.5f)
        };

        int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        tileMesh.vertices = vertices;
        tileMesh.triangles = triangles;
        tileMesh.uv = uvs;
        tileMesh.RecalculateNormals();
    }

    void CreateMaterial()
    {
        // Use custom instanced shader
        Shader shader = Shader.Find("Custom/UnlitInstanced");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
        
        miasmaMaterial = new Material(shader);
        miasmaMaterial.SetColor("_Color", miasmaColor);
        miasmaMaterial.enableInstancing = true;
    }
}
