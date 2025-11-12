using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    
    [Header("References")]
    [SerializeField] private FloatingJoystick joystick;
    
    private Rigidbody rb;
    private Keyboard keyboard;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        keyboard = Keyboard.current;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MovePlayer()
    {
        Vector2 input = GetMovementInput();
        
        // Top-down movement: X = left/right, Z = forward/backward
        if (input.magnitude > 0.1f)
        {
            Vector3 movement = new Vector3(input.x, 0, input.y) * moveSpeed;
            rb.linearVelocity = movement;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }
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
}
