using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders the beam visual and triggers clearing.
/// Attach to Derelict (or child of Derelict).
/// </summary>
public class BeamRenderer : MonoBehaviour
{
    [Header("Visual Settings")]
    public Color beamColor = new Color(1f, 0.9f, 0.2f, 0.3f);  // Yellow, translucent
    public int circleSegments = 32;
    public Camera mainCamera;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material beamMaterial;
    private BeamManager.BeamMode lastMode;
    private float lastRadius;
    private Vector3 lastMouseWorldPos;

    void Start()
    {
        // Create mesh components
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // Create material
        beamMaterial = new Material(Shader.Find("Sprites/Default"));
        beamMaterial.color = beamColor;
        meshRenderer.material = beamMaterial;

        // Subscribe to mode changes
        if (BeamManager.Instance != null)
        {
            BeamManager.Instance.OnModeChanged += OnModeChanged;
        }

        // Find camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Initial visual update (will be updated properly in Update with mouse position)
        if (BeamManager.Instance != null)
        {
            Vector3 playerPos = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 defaultDir = Vector3.forward;
            UpdateVisual(playerPos, defaultDir, 0f);
        }
    }

    void OnDestroy()
    {
        if (BeamManager.Instance != null)
        {
            BeamManager.Instance.OnModeChanged -= OnModeChanged;
        }
    }

    void Update()
    {
        if (BeamManager.Instance == null || mainCamera == null) return;

        var mode = BeamManager.Instance.currentMode;
        Vector3 playerPos = transform.parent != null ? transform.parent.position : transform.position;

        // Get mouse world position on ground plane
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 direction = (mouseWorldPos - playerPos).normalized;
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        // Update visual if changed
        if (mode != lastMode)
        {
            UpdateVisual(playerPos, direction, angle);
            lastMode = mode;
        }
        else if (mode == BeamManager.BeamMode.Cone || mode == BeamManager.BeamMode.Laser)
        {
            // Update visual every frame for cone/laser (they rotate with mouse)
            UpdateVisual(playerPos, direction, angle);
        }

        // Keep beam centered on parent (Derelict), between ground and miasma
        if (transform.parent != null)
        {
            transform.position = transform.parent.position + new Vector3(0f, 0.005f, 0f);
            
            // Rotate to face mouse for cone/laser
            if (mode == BeamManager.BeamMode.Cone || mode == BeamManager.BeamMode.Laser)
            {
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
            else
            {
                transform.rotation = Quaternion.identity;
            }
        }

        // Fire beam every frame (for continuous clearing)
        if (mode != BeamManager.BeamMode.Off)
        {
            if (mode == BeamManager.BeamMode.BubbleMin || mode == BeamManager.BeamMode.BubbleMax)
            {
                float radius = BeamManager.Instance.GetCurrentRadius();
                BeamManager.Instance.FireBeam(playerPos, radius);
            }
            else if (mode == BeamManager.BeamMode.Cone)
            {
                BeamManager.Instance.FireBeamCone(playerPos, direction);
            }
            else if (mode == BeamManager.BeamMode.Laser)
            {
                BeamManager.Instance.FireBeamLaser(playerPos, direction);
            }
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null) return Vector3.zero;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, 0f);  // Ground at Y=0
        
        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        
        // Fallback: project mouse to ground
        return new Vector3(ray.origin.x, 0f, ray.origin.z);
    }

    void OnModeChanged(BeamManager.BeamMode newMode)
    {
        // Visual will update in next Update() call
        // Immediate clear happens automatically via Update()
    }

    void UpdateVisual(Vector3 playerPos, Vector3 direction, float angle)
    {
        if (BeamManager.Instance == null) return;

        var mode = BeamManager.Instance.currentMode;

        if (mode == BeamManager.BeamMode.Off)
        {
            meshRenderer.enabled = false;
            return;
        }

        meshRenderer.enabled = true;

        if (mode == BeamManager.BeamMode.BubbleMin || mode == BeamManager.BeamMode.BubbleMax)
        {
            float radius = BeamManager.Instance.GetCurrentRadius();
            meshFilter.mesh = CreateCircleMesh(radius, circleSegments);
        }
        else if (mode == BeamManager.BeamMode.Cone)
        {
            meshFilter.mesh = CreateConeMesh(BeamManager.Instance.coneLength, BeamManager.Instance.coneHalfAngle, circleSegments);
        }
        else if (mode == BeamManager.BeamMode.Laser)
        {
            meshFilter.mesh = CreateLaserMesh(BeamManager.Instance.laserLength, BeamManager.Instance.laserThickness);
        }
    }

    Mesh CreateCircleMesh(float radius, int segments)
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        // Center vertex
        vertices[0] = Vector3.zero;

        // Edge vertices (on XZ plane)
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            // Triangle
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % segments + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }

    Mesh CreateConeMesh(float length, float halfAngleDeg, int segments)
    {
        Mesh mesh = new Mesh();
        float halfAngle = halfAngleDeg * Mathf.Deg2Rad;

        // Ice cream cone / flashlight shape:
        // - Point at origin (tip)
        // - Two straight edges extending outward
        // - Semicircle at the far end
        
        // Calculate width at far end (semicircle diameter)
        float endWidth = Mathf.Tan(halfAngle) * length * 2f;
        float endRadius = endWidth * 0.5f;
        
        // Number of vertices: 1 tip + semicircle segments + 2 edge points
        int semicircleSegs = segments / 2;  // Half circle
        Vector3[] vertices = new Vector3[1 + semicircleSegs + 1 + 2];  // tip + semicircle + 2 edge points
        List<int> triangles = new List<int>();
        
        int vertexIndex = 0;
        
        // Tip vertex at origin
        vertices[vertexIndex++] = Vector3.zero;
        int tipIndex = 0;
        
        // Left edge point (at far end)
        float leftAngle = -halfAngle;
        Vector3 leftEdge = new Vector3(Mathf.Sin(leftAngle) * length, 0f, Mathf.Cos(leftAngle) * length);
        vertices[vertexIndex++] = leftEdge;
        int leftEdgeIndex = vertexIndex - 1;
        
        // Semicircle at far end (from left to right)
        for (int i = 0; i <= semicircleSegs; i++)
        {
            float angle = leftAngle + (halfAngle * 2f * i / semicircleSegs);
            float x = Mathf.Sin(angle) * length;
            float z = Mathf.Cos(angle) * length;
            vertices[vertexIndex++] = new Vector3(x, 0f, z);
        }
        
        // Right edge point (at far end)
        float rightAngle = halfAngle;
        Vector3 rightEdge = new Vector3(Mathf.Sin(rightAngle) * length, 0f, Mathf.Cos(rightAngle) * length);
        vertices[vertexIndex++] = rightEdge;
        int rightEdgeIndex = vertexIndex - 1;
        
        // Create triangles:
        // 1. Left edge triangle (tip -> left edge -> first semicircle point)
        triangles.Add(tipIndex);
        triangles.Add(leftEdgeIndex);
        triangles.Add(leftEdgeIndex + 1);
        
        // 2. Semicircle triangles (tip -> semicircle points)
        for (int i = 0; i < semicircleSegs; i++)
        {
            triangles.Add(tipIndex);
            triangles.Add(leftEdgeIndex + 1 + i);
            triangles.Add(leftEdgeIndex + 1 + i + 1);
        }
        
        // 3. Right edge triangle (tip -> last semicircle point -> right edge)
        triangles.Add(tipIndex);
        triangles.Add(rightEdgeIndex - 1);
        triangles.Add(rightEdgeIndex);

        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }

    Mesh CreateLaserMesh(float length, float thickness)
    {
        Mesh mesh = new Mesh();

        float halfThick = thickness * 0.5f;

        // Rectangle: 4 vertices
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-halfThick, 0f, 0f),           // Left start
            new Vector3(halfThick, 0f, 0f),          // Right start
            new Vector3(halfThick, 0f, length),      // Right end
            new Vector3(-halfThick, 0f, length)      // Left end
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

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        return mesh;
    }
}
