using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders miasma as a single sheet with holes cut via texture mask.
/// Uses RenderTexture to track cleared tiles for efficient GPU-based rendering.
/// </summary>
public class MiasmaRenderer : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Camera mainCamera;

    [Header("Visual Settings")]
    public Color miasmaColor = new Color(0.5f, 0f, 0.7f, 0.9f);  // Purple
    public float renderHeight = 0.01f;  // Y position of miasma sheet
    
    [Header("Texture Settings")]
    [Tooltip("Resolution of the cleared tiles mask texture (power of 2 recommended)")]
    public int textureResolution = 512;
    [Tooltip("World units covered by texture (larger = lower detail, smaller = higher detail)")]
    public float textureWorldSize = 50f;  // World units per texture
    [Tooltip("Circle radius multiplier for clearing (larger = more coverage, ensures no gaps)")]
    [Range(0.5f, 2.0f)]
    public float clearingRadiusMultiplier = 1.2f;  // Multiplier for circle radius when drawing cleared areas
    
    [Header("Sheet Size")]
    [Tooltip("Multiplier for viewport size. 1.0 = exact viewport, 2.0 = 2x larger")]
    public float sizeMultiplier = 2.0f;  // Make sheet larger to prevent edge shimmering

    [Header("Smoothing")]
    [Tooltip("How fast the miasma sheet follows the player (higher = faster, 0 = instant)")]
    [Range(0f, 20f)]
    public float smoothingSpeed = 8f;  // Smooth interpolation speed

    private Mesh sheetMesh;
    private Material miasmaMaterial;
    private RenderTexture clearedMaskTexture;
    private Texture2D updateTexture;  // CPU-side texture for updates
    private MaterialPropertyBlock propertyBlock;

    private Vector3 smoothedSheetCenter;
    private Vector3 lastTextureCenter;  // Track when to re-center texture
    private bool isInitialized = false;
    
    // World-to-texture coordinate mapping
    private float textureScaleX, textureScaleZ;
    private float textureOffsetX, textureOffsetZ;
    
    // Track which tiles need texture updates
    private HashSet<Vector2Int> pendingUpdates = new HashSet<Vector2Int>();
    private bool needsFullRefresh = false;
    
    // Re-center texture when player moves significantly
    private float textureRecenteringThreshold = 10f;  // World units

    void Start()
    {
        CreateSheetMesh();
        CreateTexture();
        CreateMaterial();
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
            MiasmaManager.Instance.OnTilesChanged -= OnTilesChanged;
        }
        
        if (clearedMaskTexture != null)
        {
            clearedMaskTexture.Release();
            Destroy(clearedMaskTexture);
        }
        
        if (updateTexture != null)
        {
            Destroy(updateTexture);
        }
    }

    void OnClearedChanged()
    {
        // Mark that we need to update the texture
        needsFullRefresh = true;
    }

    void OnTilesChanged(HashSet<Vector2Int> changedTiles)
    {
        // Add changed tiles to pending updates for efficient partial refresh
        pendingUpdates.UnionWith(changedTiles);
    }

    void Update()
    {
        if (MiasmaManager.Instance == null || player == null || mainCamera == null) return;

        // Initialize smoothed position on first frame
        if (!isInitialized)
        {
            smoothedSheetCenter = new Vector3(player.position.x, renderHeight, player.position.z);
            lastTextureCenter = smoothedSheetCenter;
            isInitialized = true;
            needsFullRefresh = true;  // Initial full refresh
        }

        // Smooth the sheet center position towards player position
        Vector3 targetCenter = new Vector3(player.position.x, renderHeight, player.position.z);
        if (smoothingSpeed > 0f)
        {
            smoothedSheetCenter = Vector3.Lerp(smoothedSheetCenter, targetCenter, smoothingSpeed * Time.deltaTime);
        }
        else
        {
            smoothedSheetCenter = targetCenter;
        }

        // Check if texture needs re-centering (player moved significantly)
        float distanceFromTextureCenter = Vector3.Distance(
            new Vector3(smoothedSheetCenter.x, 0, smoothedSheetCenter.z),
            new Vector3(lastTextureCenter.x, 0, lastTextureCenter.z)
        );
        
        if (distanceFromTextureCenter > textureRecenteringThreshold)
        {
            // Re-center texture and do full refresh
            lastTextureCenter = smoothedSheetCenter;
            needsFullRefresh = true;
        }

        // Update texture coordinate mapping based on smoothed position
        UpdateTextureMapping();

        // Update texture if needed
        if (needsFullRefresh)
        {
            RefreshFullTexture();
            needsFullRefresh = false;
            pendingUpdates.Clear();
        }
        else if (pendingUpdates.Count > 0)
        {
            UpdateTextureRegions();
        }

        // Update sheet position and size
        UpdateSheetTransform();

        // Draw miasma sheet
        DrawMiasma();
    }

    void UpdateTextureMapping()
    {
        // Calculate world-to-texture coordinate mapping
        // Texture center should align with smoothedSheetCenter
        float halfWorldSize = textureWorldSize * 0.5f;
        
        textureScaleX = 1f / textureWorldSize;
        textureScaleZ = 1f / textureWorldSize;
        textureOffsetX = 0.5f - (smoothedSheetCenter.x * textureScaleX);
        textureOffsetZ = 0.5f - (smoothedSheetCenter.z * textureScaleZ);
        
        // Update shader properties
        if (miasmaMaterial != null)
        {
            miasmaMaterial.SetVector("_WorldToUV", new Vector4(textureScaleX, textureScaleZ, textureOffsetX, textureOffsetZ));
        }
    }

    void UpdateSheetTransform()
    {
        if (mainCamera == null) return;

        // Get viewport corners in screen space (0-1)
        Vector3[] screenCorners = new Vector3[]
        {
            new Vector3(0, 0, 0),           // Bottom-left
            new Vector3(1, 0, 0),           // Bottom-right
            new Vector3(1, 1, 0),           // Top-right
            new Vector3(0, 1, 0)            // Top-left
        };
        
        // Convert to world space at miasma height
        Vector3[] worldCorners = new Vector3[4];
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, renderHeight, 0));
        
        for (int i = 0; i < 4; i++)
        {
            Ray ray = mainCamera.ScreenPointToRay(new Vector3(
                screenCorners[i].x * Screen.width,
                screenCorners[i].y * Screen.height,
                0
            ));
            
            float enter;
            if (groundPlane.Raycast(ray, out enter))
            {
                worldCorners[i] = ray.GetPoint(enter);
            }
            else
            {
                // Fallback: use camera's forward projection
                worldCorners[i] = mainCamera.transform.position + mainCamera.transform.forward * 10f;
                worldCorners[i].y = renderHeight;
            }
        }
        
        // Calculate axis-aligned bounding box of world corners
        Vector3 min = worldCorners[0];
        Vector3 max = worldCorners[0];
        for (int i = 1; i < 4; i++)
        {
            min.x = Mathf.Min(min.x, worldCorners[i].x);
            min.z = Mathf.Min(min.z, worldCorners[i].z);
            max.x = Mathf.Max(max.x, worldCorners[i].x);
            max.z = Mathf.Max(max.z, worldCorners[i].z);
        }
        
        // Add buffer for smooth edges
        float width = max.x - min.x;
        float height = max.z - min.z;
        float buffer = Mathf.Max(width, height) * (sizeMultiplier - 1f) * 0.5f;
        min.x -= buffer;
        min.z -= buffer;
        max.x += buffer;
        max.z += buffer;
        
        // Calculate final center and size
        Vector3 center = (min + max) * 0.5f;
        center.y = renderHeight;
        width = max.x - min.x;
        height = max.z - min.z;
        
        // Position and scale sheet
        transform.position = center;
        transform.localScale = new Vector3(width, 1f, height);
        transform.rotation = Quaternion.identity;  // Flat on ground
    }

    void RefreshFullTexture()
    {
        if (MiasmaManager.Instance == null || updateTexture == null) return;

        // Clear texture (all black = all fog)
        Color32[] pixels = new Color32[textureResolution * textureResolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(0, 0, 0, 255);  // Black = fog present
        }

        // Mark cleared tiles as white (no fog) - draw circles for proper clearing
        HashSet<Vector2Int> clearedSet = new HashSet<Vector2Int>(MiasmaManager.Instance.GetClearedTiles());
        float tileSize = MiasmaManager.Instance.tileSize;
        Color32 clearedColor = new Color32(255, 255, 255, 255);  // White = cleared
        
        foreach (var tile in clearedSet)
        {
            Vector3 worldPos = MiasmaManager.Instance.TileToWorld(tile);
            if (WorldToTextureCoords(worldPos, out int centerX, out int centerY))
            {
                // Calculate radius in texture pixels
                // Use configurable multiplier to ensure complete coverage
                float radiusInWorld = tileSize * 0.5f * clearingRadiusMultiplier;
                float radiusInTex = radiusInWorld * textureScaleX * textureResolution;
                int radiusPixels = Mathf.CeilToInt(radiusInTex) + 1;  // Safety margin
                
                // Draw circle in texture
                int minX = Mathf.Max(0, centerX - radiusPixels);
                int maxX = Mathf.Min(textureResolution - 1, centerX + radiusPixels);
                int minY = Mathf.Max(0, centerY - radiusPixels);
                int maxY = Mathf.Min(textureResolution - 1, centerY + radiusPixels);
                
                float radiusSq = radiusInTex * radiusInTex;
                
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dx = (x - centerX);
                        float dy = (y - centerY);
                        float distSq = dx * dx + dy * dy;
                        
                        if (distSq <= radiusSq)
                        {
                            int index = y * textureResolution + x;
                            pixels[index] = clearedColor;
                        }
                    }
                }
            }
        }

        // Apply to texture
        updateTexture.SetPixels32(pixels);
        updateTexture.Apply(false);
        
        // Copy to RenderTexture
        Graphics.Blit(updateTexture, clearedMaskTexture);
        
        pendingUpdates.Clear();
    }

    void UpdateTextureRegions()
    {
        if (MiasmaManager.Instance == null || updateTexture == null) return;

        // Update only changed tiles - draw circles for proper clearing
        HashSet<Vector2Int> clearedSet = new HashSet<Vector2Int>(MiasmaManager.Instance.GetClearedTiles());
        List<Vector2Int> toUpdate = new List<Vector2Int>(pendingUpdates);
        pendingUpdates.Clear();

        float tileSize = MiasmaManager.Instance.tileSize;
        
        foreach (var tile in toUpdate)
        {
            Vector3 worldPos = MiasmaManager.Instance.TileToWorld(tile);
            if (WorldToTextureCoords(worldPos, out int centerX, out int centerY))
            {
                bool isCleared = clearedSet.Contains(tile);
                
                // Calculate radius in texture pixels
                // Use configurable multiplier to ensure complete coverage
                // This ensures circles overlap and cover the full cleared area from beams
                float radiusInWorld = tileSize * 0.5f * clearingRadiusMultiplier;
                float radiusInTex = radiusInWorld * textureScaleX * textureResolution;
                int radiusPixels = Mathf.CeilToInt(radiusInTex) + 1;  // Safety margin
                
                // Draw circle in texture
                int minX = Mathf.Max(0, centerX - radiusPixels);
                int maxX = Mathf.Min(textureResolution - 1, centerX + radiusPixels);
                int minY = Mathf.Max(0, centerY - radiusPixels);
                int maxY = Mathf.Min(textureResolution - 1, centerY + radiusPixels);
                
                Color32 color = isCleared 
                    ? new Color32(255, 255, 255, 255)  // White = cleared
                    : new Color32(0, 0, 0, 255);        // Black = fog
                
                float radiusSq = radiusInTex * radiusInTex;
                
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dx = (x - centerX);
                        float dy = (y - centerY);
                        float distSq = dx * dx + dy * dy;
                        
                        if (distSq <= radiusSq)
                        {
                            updateTexture.SetPixel(x, y, color);
                        }
                    }
                }
            }
        }

        updateTexture.Apply(false);
        Graphics.Blit(updateTexture, clearedMaskTexture);
    }

    bool WorldToTextureCoords(Vector3 worldPos, out int texX, out int texY)
    {
        // Convert world position to texture UV, then to pixel coordinates
        float u = worldPos.x * textureScaleX + textureOffsetX;
        float v = worldPos.z * textureScaleZ + textureOffsetZ;
        
        texX = Mathf.FloorToInt(u * textureResolution);
        texY = Mathf.FloorToInt(v * textureResolution);
        
        return true;
    }

    void DrawMiasma()
    {
        if (sheetMesh == null || miasmaMaterial == null) return;

        Graphics.DrawMesh(sheetMesh, transform.localToWorldMatrix, miasmaMaterial, 0, null, 0, propertyBlock);
    }

    void CreateSheetMesh()
    {
        // Simple quad covering -1 to +1 in X and Z
        sheetMesh = new Mesh();
        sheetMesh.name = "MiasmaSheet";

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0f, -0.5f),  // Bottom-left
            new Vector3(0.5f, 0f, -0.5f),   // Bottom-right
            new Vector3(0.5f, 0f, 0.5f),    // Top-right
            new Vector3(-0.5f, 0f, 0.5f)    // Top-left
        };

        int[] triangles = new int[] 
        { 
            0, 2, 1,  // First triangle
            0, 3, 2   // Second triangle
        };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        sheetMesh.vertices = vertices;
        sheetMesh.triangles = triangles;
        sheetMesh.uv = uvs;
        sheetMesh.RecalculateNormals();
    }

    void CreateTexture()
    {
        // Create RenderTexture for GPU sampling
        clearedMaskTexture = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.R8);
        clearedMaskTexture.filterMode = FilterMode.Bilinear;
        clearedMaskTexture.wrapMode = TextureWrapMode.Clamp;
        clearedMaskTexture.Create();

        // Create CPU-side texture for updates
        updateTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.R8, false);
        updateTexture.filterMode = FilterMode.Bilinear;
        updateTexture.wrapMode = TextureWrapMode.Clamp;

        // Initialize to all black (all fog)
        Color32[] pixels = new Color32[textureResolution * textureResolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(0, 0, 0, 255);
        }
        updateTexture.SetPixels32(pixels);
        updateTexture.Apply(false);

        // Copy initial state to RenderTexture
        Graphics.Blit(updateTexture, clearedMaskTexture);
    }

    void CreateMaterial()
    {
        Shader shader = Shader.Find("Custom/MiasmaSheet");
        if (shader == null)
        {
            Debug.LogError("MiasmaSheet shader not found! Using fallback.");
            shader = Shader.Find("Sprites/Default");
        }

        miasmaMaterial = new Material(shader);
        miasmaMaterial.SetColor("_Color", miasmaColor);
        miasmaMaterial.SetTexture("_ClearedMask", clearedMaskTexture);
    }
}
