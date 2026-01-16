using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ECS System: Renders miasma tiles using GPU instancing.
/// This is the high-performance rendering system.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
public partial class MiasmaRenderingSystem : SystemBase
{
    private Mesh tileMesh;
    private Material miasmaMaterial;
    private EntityQuery renderableTilesQuery;
    private Matrix4x4[] matricesArray = new Matrix4x4[1023];

    protected override void OnCreate()
    {
        // Query for tiles that should be rendered (not cleared, in view)
        renderableTilesQuery = GetEntityQuery(
            ComponentType.ReadOnly<MiasmaTileComponent>(),
            ComponentType.ReadOnly<MiasmaRenderTag>()
        );

        RequireForUpdate(renderableTilesQuery);
    }

    protected override void OnStartRunning()
    {
        CreateTileMesh();
        CreateMaterial();
    }

    protected override void OnUpdate()
    {
        if (tileMesh == null || miasmaMaterial == null) return;

        // Get all renderable tiles
        var tiles = renderableTilesQuery.ToComponentDataArray<MiasmaTileComponent>(Allocator.TempJob);

        if (tiles.Length == 0)
        {
            tiles.Dispose();
            return;
        }

        // Build matrices for GPU instancing
        int visibleCount = 0;
        float scale = 0.25f * 1.5f; // tileSize * overlap (should match MiasmaManager.tileSize)
        
        for (int i = 0; i < tiles.Length; i++)
        {
            if (!tiles[i].isCleared)
            {
                float3 pos = tiles[i].worldPosition;
                matricesArray[visibleCount] = Matrix4x4.TRS(
                    new Vector3(pos.x, pos.y, pos.z),
                    Quaternion.Euler(0f, 45f, 0f), // Rotate 45° for diamond appearance
                    new Vector3(scale, 1f, scale)
                );
                visibleCount++;
            }
        }

        // Draw in batches (Unity limit: 1023 per call)
        int batchSize = 1023;
        int drawn = 0;

        while (drawn < visibleCount)
        {
            int currentBatch = math.min(batchSize, visibleCount - drawn);
            
            // Create temporary array for this batch
            Matrix4x4[] batchMatrices = new Matrix4x4[currentBatch];
            System.Array.Copy(matricesArray, drawn, batchMatrices, 0, currentBatch);
            
            Graphics.DrawMeshInstanced(
                tileMesh,
                0,
                miasmaMaterial,
                batchMatrices,
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
