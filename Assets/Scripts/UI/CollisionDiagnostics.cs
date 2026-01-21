using UnityEngine;

/// <summary>
/// Diagnostic tool to check collision setup.
/// Attach to player GameObject to see collision info.
/// </summary>
public class CollisionDiagnostics : MonoBehaviour
{
    [Header("Diagnostics")]
    public bool showDebugInfo = true;
    public bool logCollisions = true;
    
    private CharacterController cc;
    private int collisionCount = 0;
    
    void Start()
    {
        cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            Debug.LogError("CollisionDiagnostics: No CharacterController found!");
        }
    }
    
    void Update()
    {
        if (!showDebugInfo) return;
        
        // Check for nearby colliders
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, 2f);
        
        if (nearbyColliders.Length > 0)
        {
            Debug.Log($"Nearby colliders: {nearbyColliders.Length}");
            foreach (var col in nearbyColliders)
            {
                Debug.Log($"  - {col.name}: IsStatic={col.gameObject.isStatic}, Enabled={col.enabled}, Type={col.GetType().Name}");
            }
        }
    }
    
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        collisionCount++;
        if (logCollisions)
        {
            Debug.Log($"COLLISION #{collisionCount}: Hit {hit.gameObject.name} at {hit.point}. Normal: {hit.normal}");
        }
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.yellow;
        
        string info = $"CharacterController: {(cc != null ? "YES" : "NO")}\n";
        info += $"Collisions detected: {collisionCount}\n";
        info += $"Position: {transform.position}\n";
        info += $"IsGrounded: {(cc != null ? cc.isGrounded.ToString() : "N/A")}";
        
        GUI.Label(new Rect(10, 50, 400, 200), info, style);
    }
}
