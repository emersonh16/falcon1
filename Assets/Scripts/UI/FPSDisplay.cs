using UnityEngine;

/// <summary>
/// Simple FPS counter displayed on screen.
/// Attach to any GameObject in the scene.
/// </summary>
public class FPSDisplay : MonoBehaviour
{
    [Header("Display Settings")]
    [Tooltip("Position on screen (0-1)")]
    public Vector2 position = new Vector2(10f, 10f);
    
    [Tooltip("Font size")]
    public int fontSize = 24;
    
    [Tooltip("Text color")]
    public Color textColor = Color.white;
    
    [Tooltip("Update interval in seconds")]
    public float updateInterval = 0.5f;
    
    private float deltaTime = 0.0f;
    private float fps = 0.0f;
    private float lastUpdateTime = 0.0f;
    private GUIStyle style;
    private Rect rect;

    void Start()
    {
        // Create GUI style
        style = new GUIStyle();
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = fontSize;
        style.normal.textColor = textColor;
        style.fontStyle = FontStyle.Bold;
        
        // Create rect for text position
        rect = new Rect(position.x, position.y, Screen.width, Screen.height);
    }

    void Update()
    {
        // Accumulate delta time
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        
        // Update FPS at specified interval
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            fps = 1.0f / deltaTime;
            lastUpdateTime = Time.time;
        }
    }

    void OnGUI()
    {
        // Display FPS
        string text = $"FPS: {fps:F1}";
        GUI.Label(rect, text, style);
    }
}
