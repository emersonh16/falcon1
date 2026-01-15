using UnityEngine;

/// <summary>
/// Renders the beam visual and triggers clearing.
/// Attach to Derelict (or child of Derelict).
/// </summary>
public class BeamRenderer : MonoBehaviour
{
    [Header("Visual Settings")]
    public Color beamColor = new Color(1f, 0.9f, 0.2f, 0.3f);  // Yellow, translucent
    public int circleSegments = 32;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material beamMaterial;
    private BeamManager.BeamMode lastMode;
    private float lastRadius;

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

        UpdateVisual();
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
        if (BeamManager.Instance == null) return;

        var mode = BeamManager.Instance.currentMode;
        float radius = BeamManager.Instance.GetCurrentRadius();

        // Update visual if changed
        if (mode != lastMode || !Mathf.Approximately(radius, lastRadius))
        {
            UpdateVisual();
            lastMode = mode;
            lastRadius = radius;
        }

        // Keep beam centered on parent (Derelict), between ground and miasma
        if (transform.parent != null)
        {
            transform.position = transform.parent.position + new Vector3(0f, 0.005f, 0f);
            transform.rotation = Quaternion.identity;
        }

        // Fire beam every frame (for continuous clearing)
        if (mode != BeamManager.BeamMode.Off)
        {
            Vector3 worldPos = transform.parent != null ? transform.parent.position : transform.position;
            BeamManager.Instance.FireBeam(worldPos, radius);
        }
    }

    void OnModeChanged(BeamManager.BeamMode newMode)
    {
        UpdateVisual();
    }

    void UpdateVisual()
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
        // TODO: Add cone and laser visuals
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
}
