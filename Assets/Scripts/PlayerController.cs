using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private enum MoveState { Normal, Dashing, Sliding }

    [Header("References")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;
    [Tooltip("What counts as ceiling/ground for headroom and slope checks. Exclude the Player layer.")]
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float groundAcceleration = 70f;
    [SerializeField] private float airAcceleration = 30f;
    [Tooltip("How fast extra speed (from dash/slide) bleeds off back to move speed.")]
    [SerializeField] private float groundOverspeedDecay = 10f;
    [SerializeField] private float airOverspeedDecay = 2f;
    [Tooltip("Radians/sec you can bend carried momentum with input.")]
    [SerializeField] private float momentumSteerRate = 4f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float groundStickVelocity = -2f;

    [Header("Run")]
    [SerializeField] private float runSpeed = 9f;
    [Tooltip("Hold Shift longer than this to run. A quicker tap is a dash.")]
    [SerializeField] private float runHoldThreshold = 0.18f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 22f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.8f;
    [SerializeField] private int maxAirDashes = 1;
    [Tooltip("Speed you carry out of a dash. Above move speed = a little glide.")]
    [SerializeField] private float dashEndSpeed = 11f;
    [Tooltip("Fraction of dash speed kept when you jump out of a dash.")]
    [SerializeField, Range(0f, 1f)] private float dashJumpCarry = 0.7f;

    [Header("Slide")]
    [SerializeField] private float slideStartSpeed = 11f;
    [SerializeField] private float maxSlideSpeed = 24f;
    [SerializeField] private float slideFriction = 5f;
    [SerializeField] private float slideMinSpeed = 3.5f;
    [SerializeField] private float slideCrawlSpeed = 2.5f;
    [SerializeField] private float slideSteerRate = 1.2f;
    [SerializeField] private float slideSlopeAccel = 30f;
    [SerializeField] private float slideStickVelocity = -8f;
    [Tooltip("Capsule height while sliding. Must be more than 2x the controller radius.")]
    [SerializeField] private float slideHeight = 1f;
    [SerializeField] private float slideCooldown = 0.4f;
    [SerializeField] private float slideBufferTime = 0.15f;
    [SerializeField] private float slideJumpBoost = 1.1f;

    [Header("Wall Jump")]
    [SerializeField] private float wallJumpHeight = 1.6f;
    [Tooltip("Minimum speed you get kicked off the wall at.")]
    [SerializeField] private float wallJumpSpeed = 10f;
    [Tooltip("Fraction of your current speed kept through the kick.")]
    [SerializeField, Range(0f, 1f)] private float wallJumpSpeedKeep = 0.8f;
    [Tooltip("Push away from the wall vs. along it in the direction you're looking.")]
    [SerializeField] private float wallJumpAwayWeight = 1f;
    [SerializeField] private float wallJumpForwardWeight = 0.8f;
    [Tooltip("How long after touching a wall you can still wall jump.")]
    [SerializeField] private float wallCoyoteTime = 0.15f;
    [SerializeField] private float wallJumpCooldown = 0.2f;
    [Tooltip("Wall jumps allowed per airtime (resets on landing). 0 = unlimited.")]
    [SerializeField] private int maxWallJumps = 3;
    [SerializeField] private bool wallJumpRefreshesDash = true;
    [Tooltip("Max fall speed while pushing into a wall. 0 disables wall sliding.")]
    [SerializeField] private float wallSlideFallSpeed = 4f;

    [Header("Jump Feel")]
    [Tooltip("Gravity multiplier while falling. Higher = snappier landings.")]
    [SerializeField] private float fallGravityScale = 1.4f;
    [Tooltip("Gravity multiplier near the top of a jump. Lower = a brief hang.")]
    [SerializeField] private float apexGravityScale = 0.65f;
    [SerializeField] private float apexThreshold = 1.5f;
    [Tooltip("Gravity multiplier when you let go of jump early (variable jump height).")]
    [SerializeField] private float jumpCutScale = 2.5f;

    [Header("Camera Juice")]
    [SerializeField] private float maxFovKick = 12f;
    [SerializeField] private float fovSmoothing = 10f;
    [SerializeField] private float dashFovPunch = 10f;
    [SerializeField] private float fovPunchDecay = 8f;
    [SerializeField] private float maxRoll = 5f;
    [SerializeField] private float rollSmoothing = 12f;
    [SerializeField] private float slideCameraDrop = 0.5f;
    [SerializeField] private float cameraHeightSmoothing = 14f;
    [SerializeField] private float wallJumpFovPunch = 6f;
    [SerializeField] private float wallSlideRoll = 8f;
    [SerializeField] private float landDipAmount = 0.25f;
    [SerializeField] private float landDipMinSpeed = 8f;
    [SerializeField] private float landDipRecovery = 10f;
    [SerializeField] private float dashShake = 0.5f;
    [SerializeField] private float shakeAmplitude = 0.06f;
    [SerializeField] private float shakeDecay = 3f;

    [Header("Camera Motion")]
    [SerializeField] private float bobStride = 1.6f;
    [SerializeField] private float bobVertical = 0.035f;
    [SerializeField] private float bobHorizontal = 0.02f;
    [SerializeField] private float bobRoll = 0.6f;
    [SerializeField] private float runLeanPitch = 1.5f;
    [SerializeField] private float runFovBonus = 4f;
    [Tooltip("How far the camera trails behind sudden speed changes.")]
    [SerializeField] private float swayAmount = 0.006f;
    [SerializeField] private float swayMax = 0.12f;
    [SerializeField] private float swayFollow = 8f;
    [SerializeField] private float jumpPitchKick = -1.5f;
    [SerializeField] private float landPitchKick = 3f;
    [SerializeField] private float pitchKickRecovery = 9f;

    [Header("Optional Effects")]
    [SerializeField] private TrailRenderer dashTrail;
    [SerializeField] private ParticleSystem dashBurst;
    [SerializeField] private ParticleSystem slideSparks;

    [Header("Events (hook audio/VFX here)")]
    public UnityEvent onJump;
    public UnityEvent onWallJump;
    public UnityEvent onLand;
    public UnityEvent onDash;
    public UnityEvent onSlideStart;
    public UnityEvent onSlideEnd;
    public UnityEvent onRunStart;
    public UnityEvent onRunEnd;
    public UnityEvent onFootstep;

    // Handy for HUD / other scripts
    public bool IsDashing => state == MoveState.Dashing;
    public bool IsSliding => state == MoveState.Sliding;
    public bool IsWallSliding => isWallSliding;
    public bool IsRunning => isRunning;
    public float LastLandSpeed { get; private set; }

    // Permission flags: external systems (e.g. stamina) can switch abilities off without touching this script
    public bool AllowRun { get; set; } = true;
    public bool AllowDash { get; set; } = true;
    public bool AllowSlide { get; set; } = true;
    public bool AllowWallJump { get; set; } = true;
    public float HorizontalSpeed => horizontalVelocity.magnitude;
    /// <summary>1 right after dashing, 0 when the dash is ready.</summary>
    public float DashCooldown01 => Mathf.Clamp01((nextDashTime - Time.time) / dashCooldown);

    private CharacterController controller;
    private InputAction moveAction, lookAction, jumpAction, dashAction, slideAction;

    private MoveState state = MoveState.Normal;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float pitch;
    private bool wasGrounded;

    private float lastGroundedTime = -10f;
    private float lastJumpPressedTime = -10f;
    private float lastSlidePressedTime = -10f;

    private Vector3 dashDirection;
    private float dashEndTime;
    private float nextDashTime;
    private bool dashGrounded;
    private int airDashesUsed;

    private Vector3 wallNormal;
    private float lastWallTime = -10f;
    private float nextWallJumpTime;
    private int wallJumpsUsed;
    private bool isWallSliding;

    private bool isRunning;
    private float shiftDownTime = -10f;
    private bool dashTapped;

    private float nextSlideTime;
    private float standingHeight;
    private Vector3 standingCenter;

    private Vector3 cameraBaseLocalPos;
    private float baseFov;
    private float fovPunch;
    private float roll;
    private float cameraYOffset;
    private float landOffset;
    private float trauma;
    private bool canCutJump;
    private float bobPhase;
    private float bobBlend;
    private float leanPitch;
    private float pitchKick;
    private Vector3 swayVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        standingHeight = controller.height;
        standingCenter = controller.center;
        slideHeight = Mathf.Max(slideHeight, controller.radius * 2f + 0.01f);

        if (playerCamera == null) playerCamera = cameraPivot.GetComponentInChildren<Camera>();
        baseFov = playerCamera.fieldOfView;
        cameraBaseLocalPos = cameraPivot.localPosition;

        if (dashTrail != null) dashTrail.emitting = false;

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        dashAction = new InputAction("Dash", InputActionType.Button, "<Keyboard>/leftShift");
        slideAction = new InputAction("Slide", InputActionType.Button, "<Keyboard>/leftCtrl");
        slideAction.AddBinding("<Keyboard>/c");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
        slideAction.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
        slideAction.Disable();
    }

    private void OnDestroy()
    {
        moveAction.Dispose();
        lookAction.Dispose();
        jumpAction.Dispose();
        dashAction.Dispose();
        slideAction.Dispose();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 wishDir = transform.right * input.x + transform.forward * input.y;
        if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

        HandleLook();

        bool grounded = controller.isGrounded;
        if (grounded && !wasGrounded) HandleLanding();
        wasGrounded = grounded;
        if (grounded)
        {
            lastGroundedTime = Time.time;
            airDashesUsed = 0;
            wallJumpsUsed = 0;
        }

        if (jumpAction.WasPressedThisFrame()) lastJumpPressedTime = Time.time;
        if (slideAction.WasPressedThisFrame()) lastSlidePressedTime = Time.time;

        // Shift: a quick tap (released within the threshold) is a dash, holding it is a run
        if (dashAction.WasPressedThisFrame()) shiftDownTime = Time.time;
        dashTapped = dashAction.WasReleasedThisFrame() && Time.time - shiftDownTime <= runHoldThreshold;

        if (state == MoveState.Dashing && Time.time >= dashEndTime)
            EndDash(dashEndSpeed);

        TryStartDash(wishDir, grounded);
        TryJump();

        // After a jump this frame we're no longer "on the ground" for movement purposes
        bool onGround = grounded && verticalVelocity <= 0f;

        TryStartSlide(wishDir, onGround);

        bool wantsRun = AllowRun
            && state == MoveState.Normal
            && wishDir.sqrMagnitude > 0.01f
            && dashAction.IsPressed()
            && Time.time - shiftDownTime > runHoldThreshold;
        if (wantsRun != isRunning)
        {
            isRunning = wantsRun;
            if (isRunning) onRunStart?.Invoke();
            else onRunEnd?.Invoke();
        }

        switch (state)
        {
            case MoveState.Dashing:
                horizontalVelocity = dashDirection * dashSpeed;
                break;
            case MoveState.Sliding:
                UpdateSlide(wishDir, onGround, dt);
                break;
            default:
                UpdateNormalMovement(wishDir, onGround, dt);
                break;
        }

        // Wall slide: airborne, falling, pushing into a wall we touched in the last moment
        isWallSliding = wallSlideFallSpeed > 0f
            && state == MoveState.Normal
            && !grounded
            && verticalVelocity < 0f
            && Time.time - lastWallTime <= 0.1f
            && Vector3.Dot(wishDir, -wallNormal) > 0.1f;

        UpdateVertical(grounded, dt);

        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        CollisionFlags flags = controller.Move(velocity * dt);

        // Hit a wall? Keep only the speed we actually moved at, so we slide along it
        if ((flags & CollisionFlags.Sides) != 0)
        {
            Vector3 actual = controller.velocity;
            actual.y = 0f;
            horizontalVelocity = actual;
        }
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;

        UpdateCamera(dt);

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ───────────────────────── Look ─────────────────────────

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = lookAction.ReadValue<Vector2>() * lookSensitivity;
        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        // Rotation is applied in UpdateCamera so roll can be added
    }

    // ───────────────────────── Ground movement ─────────────────────────

    private void UpdateNormalMovement(Vector3 wishDir, bool onGround, float dt)
    {
        float speed = horizontalVelocity.magnitude;
        bool hasInput = wishDir.sqrMagnitude > 0.01f;
        float targetSpeed = isRunning ? runSpeed : moveSpeed;

        if (speed > targetSpeed + 0.1f && (hasInput || !onGround))
        {
            // Carrying momentum: bleed it off slowly, let input bend it
            float decay = onGround ? groundOverspeedDecay : airOverspeedDecay;
            float newSpeed = Mathf.MoveTowards(speed, targetSpeed, decay * dt);
            Vector3 dir = horizontalVelocity / speed;
            if (hasInput)
                dir = Vector3.RotateTowards(dir, wishDir.normalized, momentumSteerRate * dt, 0f);
            horizontalVelocity = dir * newSpeed;
        }
        else
        {
            float accel = onGround ? groundAcceleration : airAcceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wishDir * targetSpeed, accel * dt);
        }
    }

    private void UpdateVertical(bool grounded, float dt)
    {
        if (state == MoveState.Dashing)
        {
            // Ground dashes stay glued to the floor, air dashes go flat
            verticalVelocity = dashGrounded ? groundStickVelocity : 0f;
            return;
        }

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = state == MoveState.Sliding ? slideStickVelocity : groundStickVelocity;

        verticalVelocity += gravity * GravityScale(grounded) * dt;

        if (isWallSliding)
            verticalVelocity = Mathf.Max(verticalVelocity, -wallSlideFallSpeed);
    }

    private float GravityScale(bool grounded)
    {
        if (verticalVelocity > 0f)
        {
            if (canCutJump && !jumpAction.IsPressed()) return jumpCutScale;
            return verticalVelocity < apexThreshold ? apexGravityScale : 1f;
        }

        canCutJump = false;
        if (grounded) return 1f;
        return -verticalVelocity < apexThreshold ? apexGravityScale : fallGravityScale;
    }

    private void HandleLanding()
    {
        float fallSpeed = -verticalVelocity;
        LastLandSpeed = fallSpeed;
        if (fallSpeed > 3f) onLand?.Invoke();

        if (fallSpeed > landDipMinSpeed)
        {
            float t = Mathf.Clamp01((fallSpeed - landDipMinSpeed) / 10f);
            landOffset = -t * landDipAmount;
            pitchKick = landPitchKick * t;
        }
    }

    // ───────────────────────── Jump ─────────────────────────

    private void TryJump()
    {
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
        if (!wantsJump) return;

        if (!canJump)
        {
            TryWallJump();
            return;
        }

        if (state == MoveState.Dashing)
        {
            // Dash-jump: cancel the dash but keep most of the speed
            EndDash(dashSpeed * dashJumpCarry);
        }
        else if (state == MoveState.Sliding)
        {
            if (!CanStand()) return; // can't jump out from under a low ceiling
            ExitSlide();
            horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity * slideJumpBoost, maxSlideSpeed);
        }

        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        canCutJump = true;
        pitchKick = jumpPitchKick;
        lastGroundedTime = -10f;
        lastJumpPressedTime = -10f;
        onJump?.Invoke();
    }

    // ───────────────────────── Wall jump ─────────────────────────

    // Called by Unity whenever the CharacterController bumps into something during Move
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (Mathf.Abs(hit.normal.y) > 0.3f) return; // floors/ceilings/steep ramps aren't walls
        if ((obstacleMask.value & (1 << hit.gameObject.layer)) == 0) return;

        Vector3 n = hit.normal;
        n.y = 0f;
        if (n.sqrMagnitude < 0.01f) return;

        wallNormal = n.normalized;
        lastWallTime = Time.time;
    }

    private void TryWallJump()
    {
        if (!AllowWallJump) return;
        if (Time.time - lastWallTime > wallCoyoteTime) return;
        if (Time.time < nextWallJumpTime) return;
        if (maxWallJumps > 0 && wallJumpsUsed >= maxWallJumps) return;

        float carried = horizontalVelocity.magnitude;

        if (state == MoveState.Sliding)
        {
            if (!CanStand()) return;
            ExitSlide();
        }
        else if (state == MoveState.Dashing)
        {
            EndDash(0f); // we override the velocity below
        }

        // Kick away from the wall, plus along it in the direction we're looking
        Vector3 along = Vector3.ProjectOnPlane(transform.forward, wallNormal);
        along.y = 0f;

        Vector3 dir = wallNormal * wallJumpAwayWeight;
        if (along.sqrMagnitude > 0.01f) dir += along.normalized * wallJumpForwardWeight;
        dir.Normalize();

        float speed = Mathf.Min(Mathf.Max(wallJumpSpeed, carried * wallJumpSpeedKeep), dashSpeed);
        horizontalVelocity = dir * speed;
        verticalVelocity = Mathf.Sqrt(wallJumpHeight * -2f * gravity);
        canCutJump = false;
        pitchKick = jumpPitchKick;

        wallJumpsUsed++;
        nextWallJumpTime = Time.time + wallJumpCooldown;
        lastWallTime = -10f;
        lastJumpPressedTime = -10f;
        if (wallJumpRefreshesDash) airDashesUsed = 0;

        fovPunch = Mathf.Max(fovPunch, wallJumpFovPunch);
        onWallJump?.Invoke();
    }

    // ───────────────────────── Dash ─────────────────────────

    private void TryStartDash(Vector3 wishDir, bool grounded)
    {
        if (!dashTapped || !AllowDash) return;
        if (state == MoveState.Dashing || Time.time < nextDashTime) return;
        if (!grounded && airDashesUsed >= maxAirDashes) return;
        if (state == MoveState.Sliding && !CanStand()) return;

        if (state == MoveState.Sliding) ExitSlide();

        dashDirection = wishDir.sqrMagnitude > 0.01f ? wishDir.normalized : transform.forward;
        dashGrounded = grounded;
        if (!grounded) airDashesUsed++;

        state = MoveState.Dashing;
        dashEndTime = Time.time + dashDuration;
        nextDashTime = Time.time + dashCooldown;
        horizontalVelocity = dashDirection * dashSpeed;
        verticalVelocity = grounded ? groundStickVelocity : 0f;

        // Juice
        fovPunch = dashFovPunch;
        trauma = Mathf.Min(1f, trauma + dashShake);
        if (dashTrail != null) dashTrail.emitting = true;
        if (dashBurst != null) dashBurst.Play();
        onDash?.Invoke();
    }

    private void EndDash(float carrySpeed)
    {
        state = MoveState.Normal;
        horizontalVelocity = dashDirection * carrySpeed;
        if (dashTrail != null) dashTrail.emitting = false;
    }

    // ───────────────────────── Slide ─────────────────────────

    private void TryStartSlide(Vector3 wishDir, bool onGround)
    {
        if (state != MoveState.Normal || !onGround || !AllowSlide || Time.time < nextSlideTime) return;
        if (Time.time - lastSlidePressedTime > slideBufferTime) return; // buffered, so pressing just before landing works

        Vector3 dir = horizontalVelocity.magnitude > 1f ? horizontalVelocity.normalized : wishDir.normalized;
        if (dir.sqrMagnitude < 0.01f) return; // standing still with no input: no slide

        lastSlidePressedTime = -10f;
        state = MoveState.Sliding;

        float speed = Mathf.Max(horizontalVelocity.magnitude, slideStartSpeed);
        horizontalVelocity = dir * speed;

        SetCrouched(true);
        if (slideSparks != null) slideSparks.Play();
        onSlideStart?.Invoke();
    }

    private void UpdateSlide(Vector3 wishDir, bool onGround, float dt)
    {
        // Gentle steering
        if (wishDir.sqrMagnitude > 0.01f && horizontalVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 dir = Vector3.RotateTowards(horizontalVelocity.normalized, wishDir.normalized, slideSteerRate * dt, 0f);
            horizontalVelocity = dir * horizontalVelocity.magnitude;
        }

        if (onGround)
        {
            // Friction
            float speed = Mathf.MoveTowards(horizontalVelocity.magnitude, 0f, slideFriction * dt);
            horizontalVelocity = horizontalVelocity.normalized * speed;

            // Downhill = free speed
            if (Physics.Raycast(FeetPosition() + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 0.8f,
                    obstacleMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, hit.normal);
                downhill.y = 0f;
                horizontalVelocity += downhill * slideSlopeAccel * dt;
            }

            horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, maxSlideSpeed);
        }

        bool released = !slideAction.IsPressed();
        bool tooSlow = onGround && horizontalVelocity.magnitude < slideMinSpeed;

        if (released || tooSlow)
        {
            if (CanStand())
            {
                ExitSlide();
            }
            else if (onGround && horizontalVelocity.magnitude < slideCrawlSpeed)
            {
                // Stuck under a low ceiling: keep crawling so you can get out
                Vector3 dir = horizontalVelocity.sqrMagnitude > 0.001f ? horizontalVelocity.normalized : transform.forward;
                horizontalVelocity = dir * slideCrawlSpeed;
            }
        }
    }

    private void ExitSlide()
    {
        state = MoveState.Normal;
        nextSlideTime = Time.time + slideCooldown;
        SetCrouched(false);
        if (slideSparks != null) slideSparks.Stop();
        onSlideEnd?.Invoke();
    }

    private void SetCrouched(bool crouched)
    {
        float h = crouched ? slideHeight : standingHeight;
        float diff = standingHeight - h;
        controller.height = h;
        // Keep the capsule's feet where they are
        controller.center = new Vector3(standingCenter.x, standingCenter.y - diff * 0.5f, standingCenter.z);
    }

    private bool CanStand()
    {
        float diff = standingHeight - controller.height;
        if (diff <= 0.001f) return true;

        float radius = controller.radius;
        Vector3 topSphere = FeetPosition() + Vector3.up * (controller.height - radius);
        return !Physics.SphereCast(topSphere, radius * 0.95f, Vector3.up, out _, diff + 0.02f,
            obstacleMask, QueryTriggerInteraction.Ignore);
    }

    private Vector3 FeetPosition()
    {
        return transform.position + controller.center - Vector3.up * (controller.height * 0.5f);
    }

    // ───────────────────────── Camera juice ─────────────────────────

    private static bool CrossedPhase(float prev, float cur, bool wrapped, float target)
    {
        return wrapped ? (target > prev || target <= cur) : (prev < target && cur >= target);
    }

    private void UpdateCamera(float dt)
    {
        float speed = horizontalVelocity.magnitude;

        // FOV: widens with speed, extra while running, punch on dash / wall jump
        fovPunch = Mathf.Lerp(fovPunch, 0f, 1f - Mathf.Exp(-fovPunchDecay * dt));
        float speedT = Mathf.InverseLerp(moveSpeed, dashSpeed, speed);
        float targetFov = baseFov + speedT * maxFovKick + (isRunning ? runFovBonus : 0f) + fovPunch;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSmoothing * dt));

        // Roll: lean into sideways movement (strafe, side dashes, curved slides)
        float lateral = Vector3.Dot(horizontalVelocity, transform.right);
        float targetRoll = -Mathf.Clamp(lateral / dashSpeed, -1f, 1f) * maxRoll;

        // Lean away from the wall while wall sliding (flip the sign of wallSlideRoll if it tilts the wrong way)
        if (isWallSliding)
            targetRoll += Vector3.Dot(-wallNormal, transform.right) * wallSlideRoll;
        roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-rollSmoothing * dt));

        // Head bob: grounded walking/running only, scaled by speed
        bool bobbing = controller.isGrounded && state == MoveState.Normal && speed > 0.5f;
        bobBlend = Mathf.Lerp(bobBlend, bobbing ? Mathf.Clamp01(speed / runSpeed) : 0f, 1f - Mathf.Exp(-10f * dt));
        float prevPhase = bobPhase;
        bobPhase += speed * bobStride * dt;
        bool wrapped = bobPhase > Mathf.PI * 2f;
        if (wrapped) bobPhase -= Mathf.PI * 2f;

        // Footstep each time the bob bottoms out (twice per cycle)
        if (bobbing && (CrossedPhase(prevPhase, bobPhase, wrapped, Mathf.PI * 0.75f)
                     || CrossedPhase(prevPhase, bobPhase, wrapped, Mathf.PI * 1.75f)))
            onFootstep?.Invoke();
        float bobY = Mathf.Sin(bobPhase * 2f) * bobVertical * bobBlend;
        float bobX = Mathf.Cos(bobPhase) * bobHorizontal * bobBlend;
        float bobTilt = Mathf.Cos(bobPhase) * bobRoll * bobBlend;

        // Pitch: slight forward lean while running, plus kicks from jumps and landings
        leanPitch = Mathf.Lerp(leanPitch, isRunning ? runLeanPitch : 0f, 1f - Mathf.Exp(-6f * dt));
        pitchKick = Mathf.Lerp(pitchKick, 0f, 1f - Mathf.Exp(-pitchKickRecovery * dt));

        // Inertia: the camera lags behind sudden speed changes (dash start, jump, wall hit)
        Vector3 vel = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);
        swayVelocity = Vector3.Lerp(swayVelocity, vel, 1f - Mathf.Exp(-swayFollow * dt));
        Vector3 lag = transform.InverseTransformDirection(swayVelocity - vel);
        lag.y *= 0.5f;
        Vector3 sway = Vector3.ClampMagnitude(lag * swayAmount, swayMax);

        // Height: drop during slide, dip on hard landings
        float targetY = state == MoveState.Sliding ? -slideCameraDrop : 0f;
        cameraYOffset = Mathf.Lerp(cameraYOffset, targetY, 1f - Mathf.Exp(-cameraHeightSmoothing * dt));
        landOffset = Mathf.Lerp(landOffset, 0f, 1f - Mathf.Exp(-landDipRecovery * dt));

        // Shake
        trauma = Mathf.MoveTowards(trauma, 0f, shakeDecay * dt);
        Vector3 shake = trauma > 0f ? Random.insideUnitSphere * (shakeAmplitude * trauma * trauma) : Vector3.zero;

        Vector3 offset = new Vector3(bobX, cameraYOffset + landOffset + bobY, 0f) + sway;
        cameraPivot.localPosition = cameraBaseLocalPos + offset + shake;
        cameraPivot.localRotation = Quaternion.Euler(pitch + pitchKick + leanPitch, 0f, roll + bobTilt);
    }
}