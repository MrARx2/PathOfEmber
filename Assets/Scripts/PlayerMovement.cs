using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField, Tooltip("Time (seconds) to accelerate to full speed")] private float accelerationTime = 0.08f;
    [SerializeField, Tooltip("Time (seconds) to stop from full speed")] private float decelerationTime = 0.06f;
    [SerializeField, Tooltip("Symmetric X boundary. Player X will be clamped to [-xBoundary, xBoundary]")] private float xBoundary = 4.87f;
    
    [Header("References")]
    [SerializeField] private FloatingJoystick joystick;
    
    private Rigidbody rb;
    private Keyboard keyboard;
    private Vector3 currentVelocity;
    
    public bool IsMoving { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        keyboard = Keyboard.current;

        // Auto-wire joystick if not set in Inspector
        if (joystick == null)
        {
            joystick = FindObjectOfType<FloatingJoystick>();
        }
    }

    private void FixedUpdate()
    {
        MovePlayer();
        ClampWithinBounds();
    }

    private void MovePlayer()
    {
        Vector2 input = GetMovementInput();
        // Desired velocity (top-down): X = left/right, Z = forward/backward
        Vector3 desiredVelocity = Vector3.zero;
        if (input.magnitude > 0.1f)
        {
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            // Normalize to ensure no diagonal speed boost
            if (dir.sqrMagnitude > 1e-4f) dir.Normalize();
            desiredVelocity = dir * moveSpeed;
        }

        // Smooth acceleration/deceleration (Archero-like snappy but slightly eased)
        float dt = Time.fixedDeltaTime;
        float accel = (desiredVelocity.sqrMagnitude > 0.001f) && (currentVelocity.sqrMagnitude <= desiredVelocity.sqrMagnitude)
            ? Mathf.Max(0.0001f, accelerationTime)
            : Mathf.Max(0.0001f, decelerationTime);
        float t = 1f - Mathf.Exp(-dt / accel);
        currentVelocity = Vector3.Lerp(currentVelocity, desiredVelocity, t);

        // Prevent walking over tagged "Collider" by sweep testing and sliding along surfaces
        Vector3 velocityStep = currentVelocity * dt;
        if (velocityStep.sqrMagnitude > 1e-6f)
        {
            Vector3 moveDir = velocityStep.normalized;
            float moveDist = velocityStep.magnitude;
            // Ignore triggers (e.g., projectiles) and only respond to solid obstacles
            if (rb.SweepTest(moveDir, out RaycastHit hit, moveDist, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.CompareTag("Collider"))
                {
                    // Project velocity onto plane to slide along obstacle
                    Vector3 slid = Vector3.ProjectOnPlane(currentVelocity, hit.normal);
                    // Secondary sweep to avoid edge snagging
                    Vector3 slidStep = slid * dt;
                    if (slidStep.sqrMagnitude > 1e-6f)
                    {
                        if (rb.SweepTest(slidStep.normalized, out RaycastHit hit2, slidStep.magnitude, QueryTriggerInteraction.Ignore) && hit2.collider != null && hit2.collider.CompareTag("Collider"))
                        {
                            slid = Vector3.zero; // blocked
                        }
                    }
                    currentVelocity = slid;
                }
            }
        }

        rb.linearVelocity = currentVelocity;

        // Update movement state for external systems (e.g., shooting)
        IsMoving = currentVelocity.sqrMagnitude > 0.01f;
    }
    
    private Vector2 GetMovementInput()
    {
        Vector2 input = Vector2.zero;
        
        // Joystick input (mobile)
        if (joystick != null && joystick.IsActive)
        {
            input.x = joystick.Horizontal;  // Left/Right
            input.y = joystick.Vertical;     // Forward/Backward
        }
        // Keyboard input (desktop)
        else if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y = 1f;     // Forward
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y = -1f;  // Backward
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x = -1f;  // Left
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x = 1f;  // Right
        }
        
        return input;
    }

    private void ClampWithinBounds()
    {
        Vector3 pos = rb.position;
        pos.x = Mathf.Clamp(pos.x, -xBoundary, xBoundary);
        rb.position = pos;
    }
}
