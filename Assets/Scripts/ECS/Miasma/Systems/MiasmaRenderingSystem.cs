using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// ECS System: Renders miasma tiles using GPU instancing.
/// This is the high-performance rendering system.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class MiasmaRenderingSystem : SystemBase
{
    private Mesh tileMesh;
    private Material miasmaMaterial;
    private EntityQuery renderableTilesQuery;
    private List<Matrix4x4> matricesList = new List<Matrix4x4>(); // Dynamic list for all tiles

    protected override void OnCreate()
    {
        // Query for tiles that should be rendered (not cleared, in view)
        renderableTilesQuery = GetEntityQuery(
            ComponentType.ReadOnly<MiasmaTileComponent>(),
            ComponentType.ReadOnly<MiasmaRenderTag>()
        );

        // In 2021.3, RequireForUpdate works differently - just check query in OnUpdate
    }

    protected override void OnStartRunning()
    {
        CreateTileMesh();
        CreateMaterial();
    }

    protected override void OnUpdate()
    {
        // DISABLED: ECS rendering is slower than MiasmaRenderer for this use case
        // Use MiasmaRenderer instead (set rendererEnabled = true)
        return;
        
        if (tileMesh == null || miasmaMaterial == null) return;
        
        // Check if we have any tiles to render
        if (renderableTilesQuery.IsEmptyIgnoreFilter) return;

        // Get all renderable tiles
        var tiles = renderableTilesQuery.ToComponentDataArray<MiasmaTileComponent>(Allocator.TempJob);

        if (tiles.Length == 0)
        {
            tiles.Dispose();
            return;
        }

        // Build matrices for GPU instancing
        matricesList.Clear();
        
        // Get tile size from MiasmaECSManager or use default
        float tileSize = 0.25f;
        if (MiasmaECSManager.Instance != null)
        {
            tileSize = MiasmaECSManager.Instance.tileSize;
        }
        float scale = tileSize * 1.8f; // Increased overlap to eliminate gaps
        
        // Collect all non-cleared tiles
        for (int i = 0; i < tiles.Length; i++)
        {
            if (!tiles[i].isCleared)
            {
                float3 pos = tiles[i].worldPosition;
                matricesList.Add(Matrix4x4.TRS(
                    new Vector3(pos.x, pos.y, pos.z),
                    Quaternion.Euler(0f, 45f, 0f), // Rotate 45° for diamond appearance
                    new Vector3(scale, 1f, scale)
                ));
            }
        }

        // Draw in batches (Unity limit: 1023 per call)
        int batchSize = 1023;
        int drawn = 0;
        Matrix4x4[] batchArray = new Matrix4x4[batchSize];

        while (drawn < matricesList.Count)
        {
            int currentBatch = math.min(batchSize, matricesList.Count - drawn);
            
            // Copy batch to array
            for (int i = 0; i < currentBatch; i++)
            {
                batchArray[i] = matricesList[drawn + i];
            }
            
            Graphics.DrawMeshInstanced(
                tileMesh,
                0,
                miasmaMaterial,
                batchArray,
                currentBatch
            );
            drawn += currentBatch;
        }

        // Cleanup
        tiles.Dispose();
    }

    void CreateTileMesh()
    {
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
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1)
        };

        tileMesh.vertices = vertices;
        tileMesh.triangles = triangles;
        tileMesh.uv = uvs;
        tileMesh.RecalculateNormals();
    }

    void CreateMaterial()
    {
        Shader shader = Shader.Find("Custom/UnlitInstanced");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
        
        miasmaMaterial = new Material(shader);
        miasmaMaterial.SetColor("_Color", new Color(0.5f, 0f, 0.7f, 0.9f));
        miasmaMaterial.enableInstancing = true;
    }
}
