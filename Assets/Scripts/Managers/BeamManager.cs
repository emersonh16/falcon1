using UnityEngine;
using System;

/// <summary>
/// Manages beam state and clearing logic.
/// Singleton - access via BeamManager.Instance
/// </summary>
public class BeamManager : MonoBehaviour
{
    public static BeamManager Instance { get; private set; }

    public enum BeamMode { Off, BubbleMin, BubbleMax, Cone, Laser }

    [Header("Current State")]
    public BeamMode currentMode = BeamMode.BubbleMin;

    [Header("Beam Parameters")]
    public float bubbleMinRadius = 3f;
    public float bubbleMaxRadius = 8f;
    public float coneLength = 14f;  // Similar to old code (224px ≈ 14 units at tileSize 0.5)
    public float coneHalfAngle = 32f;  // degrees (64° total, same as old code)
    public float laserLength = 32f;  // Similar to old code (512px ≈ 32 units)
    public float laserThickness = 0.75f;  // Similar to old code (12px ≈ 0.75 units)

    // Events
    public event Action<BeamMode> OnModeChanged;
    public event Action<Vector3, float> OnBeamFired;  // position, radius (for bubble)
    public event Action<Vector3, Vector3, float, float> OnBeamFiredCone;  // origin, direction, length, halfAngle
    public event Action<Vector3, Vector3, float, float> OnBeamFiredLaser;  // origin, direction, length, thickness

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        HandleModeInput();
    }

    void HandleModeInput()
    {
        // Number keys for direct mode selection
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetMode(BeamMode.Off);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetMode(BeamMode.BubbleMin);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetMode(BeamMode.BubbleMax);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetMode(BeamMode.Cone);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SetMode(BeamMode.Laser);

        // Mouse wheel to cycle modes
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0.01f) CycleMode(-1);  // scroll up = previous mode
        if (scroll < -0.01f) CycleMode(1);  // scroll down = next mode
    }

    public void SetMode(BeamMode mode)
    {
        if (currentMode != mode)
        {
            currentMode = mode;
            OnModeChanged?.Invoke(mode);
        }
    }

    public void CycleMode(int direction)
    {
        int newIndex = (int)currentMode + direction;
        newIndex = Mathf.Clamp(newIndex, 0, (int)BeamMode.Laser);
        SetMode((BeamMode)newIndex);
    }

    public float GetCurrentRadius()
    {
        switch (currentMode)
        {
            case BeamMode.BubbleMin: return bubbleMinRadius;
            case BeamMode.BubbleMax: return bubbleMaxRadius;
            case BeamMode.Cone: 
                // Return approximate clearing radius (cone tip width)
                return Mathf.Tan(coneHalfAngle * Mathf.Deg2Rad) * coneLength;
            case BeamMode.Laser: 
                // Return laser thickness as clearing radius
                return laserThickness;
            default: return 0f;
        }
    }

    /// <summary>
    /// Get clearing parameters for current mode
    /// </summary>
    public void GetClearingParams(Vector3 origin, Vector3 direction, out Vector3 clearPos, out float clearRadius)
    {
        clearPos = origin;
        clearRadius = 0f;

        switch (currentMode)
        {
            case BeamMode.BubbleMin:
                clearPos = origin;
                clearRadius = bubbleMinRadius;
                break;
            case BeamMode.BubbleMax:
                clearPos = origin;
                clearRadius = bubbleMaxRadius;
                break;
            case BeamMode.Cone:
                // Clear along cone path
                clearPos = origin + direction * (coneLength * 0.5f);
                clearRadius = Mathf.Tan(coneHalfAngle * Mathf.Deg2Rad) * coneLength;
                break;
            case BeamMode.Laser:
                // Clear along laser path
                clearPos = origin + direction * (laserLength * 0.5f);
                clearRadius = laserThickness;
                break;
        }
    }

    /// <summary>
    /// Called by BeamRenderer to notify that beam cleared an area (bubble modes)
    /// </summary>
    public void FireBeam(Vector3 position, float radius)
    {
        OnBeamFired?.Invoke(position, radius);
    }

    /// <summary>
    /// Fire cone beam (for clearing)
    /// </summary>
    public void FireBeamCone(Vector3 origin, Vector3 direction)
    {
        OnBeamFiredCone?.Invoke(origin, direction, coneLength, coneHalfAngle);
    }

    /// <summary>
    /// Fire laser beam (for clearing)
    /// </summary>
    public void FireBeamLaser(Vector3 origin, Vector3 direction)
    {
        OnBeamFiredLaser?.Invoke(origin, direction, laserLength, laserThickness);
    }
}
