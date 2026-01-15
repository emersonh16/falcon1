using UnityEngine;

/// <summary>
/// Simple player controller for the Derelict.
/// WASD movement in screen-space (isometric-aware).
/// </summary>
public class DerelictController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;

    private Vector3 moveForward;
    private Vector3 moveRight;

    void Start()
    {
        // Hardcoded for 45° isometric camera
        // W = up-right on screen, D = down-right, etc.
        moveForward = new Vector3(1, 0, 1).normalized;   // W goes up-screen
        moveRight = new Vector3(1, 0, -1).normalized;    // D goes right-screen
    }

    void Update()
    {
        // Get input
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        // Calculate movement direction in world space
        Vector3 direction = (moveForward * v + moveRight * h).normalized;

        // Move
        if (direction.magnitude > 0.1f)
        {
            transform.position += direction * moveSpeed * Time.deltaTime;
        }
    }
}
