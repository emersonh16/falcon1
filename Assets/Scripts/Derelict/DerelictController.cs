using UnityEngine;

/// <summary>
/// Simple player controller for the Derelict.
/// WASD movement in screen-space (isometric-aware).
/// Uses CharacterController for proper collision handling.
/// </summary>
public class DerelictController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;  // Restored original speed - we'll find root cause

    private Vector3 moveForward;
    private Vector3 moveRight;
    private CharacterController characterController;

    void Start()
    {
        // Hardcoded for 45° isometric camera
        // W = up-right on screen, D = down-right, etc.
        moveForward = new Vector3(1, 0, 1).normalized;   // W goes up-screen
        moveRight = new Vector3(1, 0, -1).normalized;    // D goes right-screen
        
        // Use CharacterController for proper collision (Unity best practice for character movement)
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }
        
        // Configure CharacterController to match the flat circular sprite exactly
        // Sprite: radius 0.5f, positioned at Y=0.05, flat on XZ plane
        characterController.radius = 0.5f;  // Matches sprite radius exactly
        characterController.height = 0.2f;  // Thin but tall enough for reliable collision detection
        characterController.center = new Vector3(0, 0.05f, 0);  // Match sprite Y position exactly (sprite center)
        characterController.slopeLimit = 90f;  // Can't walk up slopes
        characterController.stepOffset = 0f;   // No step offset
        characterController.skinWidth = 0.01f;  // Small skin width for precise collision
        
        // Debug: Log CharacterController bounds
        Bounds ccBounds = characterController.bounds;
        Debug.Log($"CharacterController bounds: min={ccBounds.min}, max={ccBounds.max}, center={ccBounds.center}, size={ccBounds.size}");
        
        // Remove Rigidbody if it exists (CharacterController handles physics)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Destroy(rb);
        }
        
        // Remove other colliders if they exist (CharacterController has its own)
        Collider[] colliders = GetComponents<Collider>();
        foreach (var col in colliders)
        {
            if (col != characterController)
            {
                Destroy(col);
            }
        }
    }

    void Update()
    {
        // Get input
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        // Calculate movement direction in world space
        Vector3 direction = (moveForward * v + moveRight * h).normalized;

        // Move using CharacterController.Move - it will STOP automatically when hitting colliders
        if (direction.magnitude > 0.1f && characterController != null)
        {
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            
            // CharacterController.Move automatically stops movement when hitting colliders
            // The movement vector will be modified if collision occurs
            CollisionFlags flags = characterController.Move(movement);
            
            // If we hit something, movement was blocked (this is automatic)
            // No need to manually check - CharacterController handles it
        }
    }
}
