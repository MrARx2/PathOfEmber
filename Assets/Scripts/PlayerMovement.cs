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
    [SerializeField, Tooltip("Velocity magnitude above which the player counts as moving (for animation & shooting)")] private float moveThreshold = 0.1f;
    
    
    [Header("References")]
    [SerializeField] private FloatingJoystick joystick;

    [Header("Visual & Animation")]
    [SerializeField] private Transform playerVisual;
    [SerializeField] private Animator animator;
    [SerializeField] private bool debugAnimation;
    [SerializeField] private float rotationLerpSpeed = 15f;
    [SerializeField, Tooltip("Yaw offset (degrees) applied to the visual relative to movement direction (for sideways bow poses etc.)")] private float visualYawOffsetDegrees = 0f;

    private Rigidbody rb;
    private Keyboard keyboard;
    private Vector3 currentVelocity;
    private Vector3 lastMoveDirection = Vector3.forward;
    private float baseMoveSpeed;
    private float moveThresholdSquared;

    public float PlayerSpeed { get; private set; }
    public bool IsMoving { get; private set; }
    public float VisualYawOffsetDegrees => visualYawOffsetDegrees;

    /// <summary>
    /// Sets the movement speed multiplier (used by PlayerAbilities for power-ups).
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        // Safety: only apply if base speed is initialized
        if (baseMoveSpeed <= 0f)
        {
            Debug.LogWarning($"[PlayerMovement] SetSpeedMultiplier called but baseMoveSpeed not initialized ({baseMoveSpeed}). Ignoring.");
            return;
        }
        
        if (multiplier > 0f)
        {
            float newSpeed = baseMoveSpeed * multiplier;
            moveSpeed = newSpeed;
        }
    }

    private const float WalkAnimPortion = 0.5f;
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        baseMoveSpeed = moveSpeed;
        keyboard = Keyboard.current;
        moveThresholdSquared = moveThreshold * moveThreshold;

        // Auto-wire joystick if not set in Inspector
        if (joystick == null)
        {
            joystick = FindFirstObjectByType<FloatingJoystick>();
        }

        // Auto-assign visual and animator if missing
        if (playerVisual == null)
        {
            // Prefer a child named "Main Character"
            Transform found = transform.Find("Main Character");
            if (found != null) playerVisual = found;
            if (playerVisual == null)
            {
                // Fallback: first child with an Animator
                Animator childAnim = GetComponentInChildren<Animator>();
                if (childAnim != null) playerVisual = childAnim.transform;
            }
        }

        if (animator == null && playerVisual != null)
        {
            animator = playerVisual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = playerVisual.GetComponentInChildren<Animator>();
            }
        }
    }

    private void OnValidate()
    {
        moveThresholdSquared = moveThreshold * moveThreshold;
    }

    private void FixedUpdate()
    {
        MovePlayer();
        ClampWithinBounds();
    }

    private void Update()
    {
        // Keep animator responsive even if physics timing differs
        UpdateAnimationAndVisual();
    }

    private void MovePlayer()
    {
        Vector2 input = GetMovementInput();
        // Desired velocity (top-down): X = left/right, Z = forward/backward
        Vector3 desiredVelocity = Vector3.zero;
        if (input.sqrMagnitude > 0f)
        {
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            // Normalize to ensure no diagonal speed boost
            if (dir.sqrMagnitude > 1e-4f)
            {
                dir.Normalize();
                lastMoveDirection = dir;
                desiredVelocity = dir * moveSpeed;
            }
        }

        // Smooth acceleration/deceleration (Archero-like snappy but slightly eased)
        float dt = Time.fixedDeltaTime;
        float accel = (desiredVelocity.sqrMagnitude > 0.001f) && (currentVelocity.sqrMagnitude <= desiredVelocity.sqrMagnitude)
            ? Mathf.Max(0.0001f, accelerationTime)
            : Mathf.Max(0.0001f, decelerationTime);
        float t = 1f - Mathf.Exp(-dt / accel);
        currentVelocity = Vector3.Lerp(currentVelocity, desiredVelocity, t);

        // Simple collision check using cheap raycast instead of expensive SweepTest
        // SweepTest projects entire collider every frame - extremely expensive!
        Vector3 velocityStep = currentVelocity * dt;
        if (velocityStep.sqrMagnitude > 1e-6f)
        {
            Vector3 moveDir = velocityStep.normalized;
            float moveDist = velocityStep.magnitude;
            
            // Use simple raycast (cheap) instead of SweepTest (expensive full collider projection)
            RaycastHit hit;
            if (Physics.Raycast(rb.position + Vector3.up * 0.5f, moveDir, out hit, moveDist + 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.CompareTag("Collider"))
                {
                    // Project velocity onto plane to slide along obstacle
                    currentVelocity = Vector3.ProjectOnPlane(currentVelocity, hit.normal);
                }
            }
        }

        // Keep physics velocity in sync with our smoothed currentVelocity
        rb.linearVelocity = currentVelocity;

        // Update movement state for external systems (e.g., shooting & animation)
        Vector3 horiz = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        IsMoving = horiz.sqrMagnitude > moveThresholdSquared;
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

    private void UpdateAnimationAndVisual()
    {
        // Use our own smoothed currentVelocity as single source of truth for animation
        Vector3 sourceVel = currentVelocity;
        Vector3 horizontalVelocity = new Vector3(sourceVel.x, 0f, sourceVel.z);
        float speed = horizontalVelocity.magnitude;
        float walkSpeed = Mathf.Max(0.0001f, baseMoveSpeed);
        float currentTopSpeed = Mathf.Max(walkSpeed, moveSpeed);

        if (!IsMoving)
        {
            // Hard idle: no locomotion-based blend or rotation. Shooting logic may rotate the visual instead.
            PlayerSpeed = 0f;
            if (animator != null)
            {
                animator.SetFloat(AnimSpeed, 0f);
            }

            return;
        }

        if (speed <= walkSpeed)
        {
            float tWalk = Mathf.Clamp01(speed / walkSpeed);
            PlayerSpeed = tWalk * WalkAnimPortion;
        }
        else
        {
            float extraSpeed = Mathf.Clamp(speed - walkSpeed, 0f, currentTopSpeed - walkSpeed);
            float runRange = Mathf.Max(0.0001f, currentTopSpeed - walkSpeed);
            float tRun = extraSpeed / runRange;
            PlayerSpeed = Mathf.Lerp(WalkAnimPortion, 1f, tRun);
        }
        
        if (playerVisual != null)
        {
            Vector3 lookDir = lastMoveDirection.sqrMagnitude > 1e-4f
                ? lastMoveDirection
                : (horizontalVelocity.sqrMagnitude > 1e-6f ? horizontalVelocity.normalized : playerVisual.forward);

            if (lookDir.sqrMagnitude > 1e-4f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDir, Vector3.up);
                if (Mathf.Abs(visualYawOffsetDegrees) > 0.001f)
                {
                    targetRotation *= Quaternion.Euler(0f, visualYawOffsetDegrees, 0f);
                }
                float lerpFactor = rotationLerpSpeed <= 0f ? 1f : rotationLerpSpeed * Time.deltaTime;
                playerVisual.rotation = Quaternion.Slerp(playerVisual.rotation, targetRotation, Mathf.Clamp01(lerpFactor));
            }
        }

        if (animator != null)
        {
            animator.SetFloat(AnimSpeed, PlayerSpeed);
        }
    }
}
