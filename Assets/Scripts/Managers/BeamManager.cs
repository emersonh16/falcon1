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
    public float coneLength = 14f;
    public float coneHalfAngle = 32f;  // degrees
    public float laserLength = 32f;
    public float laserThickness = 0.75f;

    // Events
    public event Action<BeamMode> OnModeChanged;
    public event Action<Vector3, float> OnBeamFired;  // position, radius

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
            default: return 0f;
        }
    }

    /// <summary>
    /// Called by BeamRenderer to notify that beam cleared an area
    /// </summary>
    public void FireBeam(Vector3 position, float radius)
    {
        OnBeamFired?.Invoke(position, radius);
    }
}
