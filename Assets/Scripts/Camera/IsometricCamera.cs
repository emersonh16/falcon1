using UnityEngine;

/// <summary>
/// Isometric camera setup for Derelict Drifters.
/// Attach to Main Camera. Sets orthographic projection with 30° elevation, 45° azimuth.
/// </summary>
public class IsometricCamera : MonoBehaviour
{
    [Header("Isometric Settings")]
    [Tooltip("Camera elevation angle (looking down)")]
    public float elevation = 30f;
    
    [Tooltip("Camera azimuth angle (rotation around Y)")]
    public float azimuth = 45f;
    
    [Tooltip("Orthographic camera size (zoom level)")]
    public float orthoSize = 10f;
    
    [Tooltip("Distance from target (affects near/far planes)")]
    public float distance = 50f;

    [Header("Follow Target")]
    [Tooltip("Target to follow (assign Derelict)")]
    public Transform target;
    
    [Tooltip("How smoothly camera follows target")]
    public float followSmoothing = 5f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        SetupIsometric();
    }

    void SetupIsometric()
    {
        // Set orthographic
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        
        // Set rotation: first Y (azimuth), then X (elevation)
        transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
        
        // Position camera at fixed offset looking at origin
        Vector3 targetPos = target != null ? target.position : Vector3.zero;
        Vector3 offset = transform.rotation * Vector3.back * distance;
        transform.position = targetPos + offset;
    }

    void LateUpdate()
    {
        if (target != null)
        {
            UpdateCameraPosition(target.position);
        }
    }

    void UpdateCameraPosition(Vector3 targetPos)
    {
        Vector3 offset = transform.rotation * Vector3.back * distance;
        transform.position = targetPos + offset;
    }

    /// <summary>
    /// Call this to update camera settings at runtime
    /// </summary>
    public void RefreshSettings()
    {
        if (cam == null) cam = GetComponent<Camera>();
        cam.orthographicSize = orthoSize;
        transform.rotation = Quaternion.Euler(elevation, azimuth, 0f);
    }

    // Visualize in editor
    void OnValidate()
    {
        if (Application.isPlaying && cam != null)
        {
            RefreshSettings();
        }
    }
}
