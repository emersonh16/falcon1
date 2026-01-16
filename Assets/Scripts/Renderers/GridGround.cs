using UnityEngine;

/// <summary>
/// Creates a ground plane with a grid pattern.
/// Attach to an empty GameObject.
/// </summary>
public class GridGround : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridSize = 50;           // Number of cells in each direction
    public float cellSize = 2f;         // Size of each cell in world units
    public Color groundColor = new Color(0.2f, 0.6f, 0.2f);  // Green
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
        
        float totalSize = gridSize * cellSize;
        ground.transform.localScale = new Vector3(totalSize, totalSize, 1f);
        
        // Update ground material color (always update to current color)
        Renderer groundRenderer = ground.GetComponent<Renderer>();
        if (groundRenderer.material != null)
        {
            groundRenderer.material.color = groundColor;
        }
        else
        {
            Material groundMat = new Material(Shader.Find("Sprites/Default"));
            groundMat.color = groundColor;
            groundRenderer.material = groundMat;
        }

        // Only create grid lines if they don't exist
        if (transform.Find("GridLines") == null)
        {
            CreateGridLines(totalSize);
        }
        else
        {
            // Update existing grid line colors
            UpdateGridLineColors();
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
