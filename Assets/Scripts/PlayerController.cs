using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraPivot;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 1f;

    private CharacterController controller;
    private InputAction moveAction, lookAction, jumpAction, dashAction;

    private float pitch;
    private float verticalVelocity;
    private float lastGroundedTime = -10f;
    private float lastJumpPressedTime = -10f;

    private bool isDashing;
    private float dashEndTime;
    private float nextDashTime;
    private Vector3 dashDirection;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        dashAction = new InputAction("Dash", InputActionType.Button, "<Keyboard>/leftShift");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
    }

    private void Update()
    {
        HandleLook();

        Vector2 input = moveAction.ReadValue<Vector2>();

        HandleDash(input);
        HandleJumpAndGravity();
        
        Vector3 horizontal;
        if (isDashing)
        {
            horizontal = dashDirection * dashSpeed;
        }
        else
        {
            horizontal = (transform.right * input.x + transform.forward * input.y) * moveSpeed;
        }

        Vector3 velocity = horizontal + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
        
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = lookAction.ReadValue<Vector2>() * lookSensitivity;

        transform.Rotate(0f, delta.x, 0f);

        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleDash(Vector2 input)
    {
        if (isDashing && Time.time >= dashEndTime)
        {
            isDashing = false;
        }

        if (dashAction.WasPressedThisFrame() && !isDashing && Time.time >= nextDashTime)
        {
            Vector2 dir;
            if (input == Vector2.zero)
                dir = Vector2.up;
            else if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                dir = new Vector2(Mathf.Sign(input.x), 0f);
            else
                dir = new Vector2(0f, Mathf.Sign(input.y));

            dashDirection = transform.right * dir.x + transform.forward * dir.y;
            isDashing = true;
            dashEndTime = Time.time + dashDuration;
            nextDashTime = Time.time + dashCooldown;
        }
    }

    private void HandleJumpAndGravity()
    {
        if (controller.isGrounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }

        if (jumpAction.WasPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;
        }

        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBufferTime;

        if (canJump && wantsJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastGroundedTime = -10f;
            lastJumpPressedTime = -10f;
        }

        if (isDashing)
        {
            verticalVelocity = 0f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }
}