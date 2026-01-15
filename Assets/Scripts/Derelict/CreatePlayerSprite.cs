using UnityEngine;

/// <summary>
/// Creates a simple circle sprite for the player at runtime.
/// Attach to an empty GameObject named "Derelict".
/// </summary>
public class CreatePlayerSprite : MonoBehaviour
{
    public Color playerColor = new Color(0.2f, 0.8f, 0.4f); // Green
    public float radius = 0.5f;
    public int segments = 32;

    void Awake()
    {
        // Create mesh
        Mesh mesh = CreateCircleMesh(radius, segments);
        
        // Add MeshFilter
        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        
        // Add MeshRenderer with unlit material
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        // Use Sprites/Default shader which always exists
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material mat = new Material(shader);
        mat.color = playerColor;
        mr.material = mat;
        
        // Rotate to lie flat on XZ plane, slightly above miasma
        transform.localPosition = new Vector3(0f, 0.05f, 0f);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    Mesh CreateCircleMesh(float r, int seg)
    {
        Mesh mesh = new Mesh();
        
        Vector3[] vertices = new Vector3[seg + 1];
        int[] triangles = new int[seg * 3];
        
        // Center vertex
        vertices[0] = Vector3.zero;
        
        // Edge vertices
        for (int i = 0; i < seg; i++)
        {
            float angle = (float)i / seg * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0);
            
            // Triangle
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % seg + 1;
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        return mesh;
    }
}
