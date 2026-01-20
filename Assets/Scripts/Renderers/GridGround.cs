using UnityEngine;

/// <summary>
/// Creates a ground plane with a grid pattern.
/// Attach to an empty GameObject.
/// </summary>
public class GridGround : MonoBehaviour
{
    [Header("Ground Settings")]
    public float groundSize = 200f;     // Size of ground plane (world units)
    public Color groundColor = new Color(0.2f, 0.6f, 0.2f, 1f);  // Solid green
    
    [Header("Grid Lines (Optional)")]
    public bool showGridLines = false;  // Toggle grid lines on/off
    public int gridSize = 50;           // Number of cells in each direction
    public float cellSize = 2f;         // Size of each cell in world units
    public Color lineColor = new Color(0.3f, 0.7f, 0.3f);     // Lighter green
    public float lineWidth = 0.05f;

    void Start()
    {
        CreateGrid();
    }

    void CreateGrid()
    {
        // Check if ground already exists (from previous run or scene)
        Transform existingGround = transform.Find("GroundPlane");
        GameObject ground;
        
        if (existingGround != null)
        {
            ground = existingGround.gameObject;
        }
        else
        {
            // Create ground quad
            ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "GroundPlane";
            ground.transform.parent = transform;
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        
        // Use groundSize instead of calculated size for solid ground
        ground.transform.localScale = new Vector3(groundSize, groundSize, 1f);
        
        // Create/update ground material with solid green color
        Renderer groundRenderer = ground.GetComponent<Renderer>();
        Material groundMat;
        
        // Use Unlit shader for solid color that doesn't require lighting
        Shader unlitShader = Shader.Find("Unlit/Color");
        if (unlitShader == null)
        {
            unlitShader = Shader.Find("Sprites/Default");
        }
        
        if (groundRenderer.material == null || groundRenderer.material.shader.name != unlitShader.name)
        {
            groundMat = new Material(unlitShader);
            groundRenderer.material = groundMat;
        }
        else
        {
            groundMat = groundRenderer.material;
        }
        
        groundMat.color = groundColor;

        // Handle grid lines based on showGridLines setting
        Transform gridLinesParent = transform.Find("GridLines");
        if (showGridLines)
        {
            float totalSize = gridSize * cellSize;
            if (gridLinesParent == null)
            {
                CreateGridLines(totalSize);
            }
            else
            {
                // Update existing grid line colors
                UpdateGridLineColors();
            }
        }
        else
        {
            // Remove grid lines if they exist and we don't want them
            if (gridLinesParent != null)
            {
                DestroyImmediate(gridLinesParent.gameObject);
            }
        }
    }

    void UpdateGridLineColors()
    {
        Transform gridLines = transform.Find("GridLines");
        if (gridLines == null) return;

        foreach (Transform line in gridLines)
        {
            LineRenderer lr = line.GetComponent<LineRenderer>();
            if (lr != null && lr.material != null)
            {
                lr.material.color = lineColor;
            }
        }
    }

    void CreateGridLines(float totalSize)
    {
        GameObject linesParent = new GameObject("GridLines");
        linesParent.transform.parent = transform;
        linesParent.transform.localPosition = new Vector3(0f, 0.01f, 0f);

        float halfSize = totalSize / 2f;

        // Vertical lines (along Z)
        for (int i = 0; i <= gridSize; i++)
        {
            float x = -halfSize + i * cellSize;
            CreateLine(linesParent.transform, 
                new Vector3(x, 0, -halfSize), 
                new Vector3(x, 0, halfSize));
        }

        // Horizontal lines (along X)
        for (int i = 0; i <= gridSize; i++)
        {
            float z = -halfSize + i * cellSize;
            CreateLine(linesParent.transform, 
                new Vector3(-halfSize, 0, z), 
                new Vector3(halfSize, 0, z));
        }
    }

    void CreateLine(Transform parent, Vector3 start, Vector3 end)
    {
        GameObject line = new GameObject("Line");
        line.transform.parent = parent;
        
        LineRenderer lr = line.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.useWorldSpace = false;
        
        Material lineMat = new Material(Shader.Find("Sprites/Default"));
        lineMat.color = lineColor;
        lr.material = lineMat;
    }
}
