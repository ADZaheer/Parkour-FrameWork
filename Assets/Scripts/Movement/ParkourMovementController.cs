using UnityEngine;

/// <summary>
/// CharacterController-based locomotion script tuned for fast-paced parkour gameplay.
/// Supports sprinting, crouching, double jumps, wall running, and wall jumps out of the box.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ParkourMovementController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Base walking speed while on the ground.")]
    public float walkSpeed = 5f;
    [Tooltip("Speed while holding the sprint key and moving forward.")]
    public float sprintSpeed = 8.5f;
    [Tooltip("Speed while crouched.")]
    public float crouchSpeed = 3f;
    [Tooltip("How quickly horizontal velocity responds to input.")]
    public float acceleration = 12f;
    [Range(0f, 1f)]
    [Tooltip("Multiplier applied to acceleration while airborne.")]
    public float airControl = 0.35f;

    [Header("Jumping")]
    [Tooltip("Height reached by the initial ground jump.")]
    public float jumpHeight = 1.6f;
    [Tooltip("Height reached by each additional air jump.")]
    public float doubleJumpHeight = 1.3f;
    [Tooltip("Number of air jumps available after leaving the ground.")]
    public int extraAirJumps = 1;

    [Header("Gravity")]
    [Tooltip("Gravity applied while airborne. A negative value pushes the player downward.")]
    public float gravity = -24f;
    [Tooltip("Small downward force applied while grounded to keep the controller snapped to the floor.")]
    public float groundedGravity = -4f;

    [Header("Crouch")]
    [Tooltip("Target height for the CharacterController while crouched.")]
    public float crouchHeight = 1.1f;
    [Tooltip("Speed used when interpolating between standing and crouching heights.")]
    public float crouchTransitionSpeed = 12f;

    [Header("Wall Running")]
    [Tooltip("Maximum distance to detect climbable walls to the left/right.")]
    public float wallCheckDistance = 0.75f;
    [Tooltip("Layers that are considered valid for wall running.")]
    public LayerMask wallMask = ~0;
    [Tooltip("Horizontal speed target while sliding along a wall.")]
    public float wallRunSpeed = 9f;
    [Tooltip("Gravity applied while wall running. Closer to zero means longer runs.")]
    public float wallRunGravity = -2.5f;
    [Tooltip("Upward velocity imparted when jumping off a wall.")]
    public float wallJumpUpVelocity = 9f;
    [Tooltip("Lateral velocity applied away from the contacted wall during a wall jump.")]
    public float wallJumpPushVelocity = 9f;
    [Tooltip("Minimum height above the ground before a wall run can begin.")]
    public float minWallRunHeight = 1.5f;

    [Header("Ground Detection")]
    [Tooltip("Optional transform for sphere-based ground checks. If omitted, CharacterController.isGrounded is used.")]
    public Transform groundCheck;
    [Tooltip("Radius for the optional ground check sphere.")]
    public float groundCheckRadius = 0.25f;
    [Tooltip("Layers considered ground when using the optional ground check.")]
    public LayerMask groundMask = ~0;

    [Header("Input")]
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;
    [Tooltip("Name of the horizontal axis inside Unity's Input Manager.")]
    public string horizontalAxis = "Horizontal";
    [Tooltip("Name of the vertical axis inside Unity's Input Manager.")]
    public string verticalAxis = "Vertical";
    [Tooltip("Name of the jump button inside Unity's Input Manager.")]
    public string jumpButton = "Jump";

    private CharacterController controller;
    private CharacterController Controller
    {
        get
        {
            if (controller == null)
            {
                controller = GetComponent<CharacterController>();
            }

            return controller;
        }
    }

    private Vector3 planarVelocity;
    private float verticalVelocity;
    private bool isGrounded;
    private bool isSprinting;
    private bool isCrouching;
    private bool isWallRunning;
    private Vector3 currentWallNormal;
    private Vector3 wallDirection;
    private int remainingAirJumps;

    private float defaultHeight;
    private Vector3 defaultCenter;

    /// <summary> Current world-space velocity of the character. </summary>
    public Vector3 CurrentVelocity => planarVelocity + Vector3.up * verticalVelocity;
    public bool IsGrounded => isGrounded;
    public bool IsWallRunning => isWallRunning;
    public bool IsCrouching => isCrouching;
    public bool IsSprinting => isSprinting;

    private void Awake()
    {
        CharacterController cc = Controller;
        defaultHeight = cc.height;
        defaultCenter = cc.center;
        crouchHeight = Mathf.Clamp(crouchHeight, 0.5f, defaultHeight);
        remainingAirJumps = extraAirJumps;
    }

    private void Update()
    {
        ReadInput(out Vector2 moveInput, out bool jumpPressed, out bool sprintHeld, out bool crouchHeld);

        UpdateGroundedState();
        UpdateSprintState(sprintHeld, moveInput);
        UpdateCrouchState(crouchHeld);
        HandleWallRun(moveInput);
        HandleJump(jumpPressed);
        ApplyMovement(moveInput);
        ApplyGravity();
        ApplyCharacterControllerMove();
    }

    private void ReadInput(out Vector2 moveInput, out bool jumpPressed, out bool sprintHeld, out bool crouchHeld)
    {
        float horizontal = Input.GetAxisRaw(horizontalAxis);
        float vertical = Input.GetAxisRaw(verticalAxis);
        moveInput = new Vector2(horizontal, vertical);
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        jumpPressed = Input.GetButtonDown(jumpButton);
        sprintHeld = Input.GetKey(sprintKey);
        crouchHeld = Input.GetKey(crouchKey);
    }

    private void UpdateGroundedState()
    {
        CharacterController cc = Controller;
        bool previousGrounded = isGrounded;
        if (groundCheck != null)
        {
            int mask = groundMask == 0 ? Physics.DefaultRaycastLayers : groundMask;
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, mask, QueryTriggerInteraction.Ignore);
        }
        else
        {
            isGrounded = cc.isGrounded;
        }

        if (isGrounded)
        {
            remainingAirJumps = extraAirJumps;
            if (verticalVelocity < 0f)
            {
                verticalVelocity = groundedGravity;
            }
            isWallRunning = false;
        }
        else if (previousGrounded)
        {
            // Leaving the ground resets vertical velocity so jumps feel responsive.
            if (verticalVelocity < 0f)
            {
                verticalVelocity = 0f;
            }
        }
    }

    private void UpdateSprintState(bool sprintHeld, Vector2 moveInput)
    {
        isSprinting = sprintHeld && moveInput.y > 0.1f && !isCrouching && !isWallRunning;
    }

    private void UpdateCrouchState(bool crouchHeld)
    {
        CharacterController cc = Controller;

        if (crouchHeld)
        {
            isCrouching = true;
        }
        else if (isCrouching)
        {
            if (HasHeadClearance())
            {
                isCrouching = false;
            }
        }

        float targetHeight = isCrouching ? crouchHeight : defaultHeight;
        Vector3 targetCenter = isCrouching
            ? new Vector3(defaultCenter.x, crouchHeight / 2f, defaultCenter.z)
            : defaultCenter;

        cc.height = Mathf.Lerp(cc.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
        cc.center = Vector3.Lerp(cc.center, targetCenter, crouchTransitionSpeed * Time.deltaTime);
    }

    private bool HasHeadClearance()
    {
        CharacterController cc = Controller;
        float radius = cc.radius - 0.05f;
        if (radius <= 0f)
        {
            radius = cc.radius * 0.95f;
        }

        float castDistance = defaultHeight - cc.height;
        if (castDistance <= 0f)
        {
            return true;
        }

        Vector3 origin = transform.position + cc.center + Vector3.up * (cc.height / 2f - radius);
        return !Physics.SphereCast(origin, radius, Vector3.up, out _, castDistance + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private void HandleWallRun(Vector2 moveInput)
    {
        if (isGrounded || IsNearGround(minWallRunHeight))
        {
            EndWallRun();
            return;
        }

        if (moveInput.y <= 0.1f)
        {
            EndWallRun();
            return;
        }

        if (TryGetWall(out RaycastHit hit))
        {
            if (!isWallRunning)
            {
                remainingAirJumps = extraAirJumps; // Refresh air jumps once when entering a wall run.
            }

            isWallRunning = true;
            currentWallNormal = hit.normal;
            wallDirection = Vector3.Cross(currentWallNormal, Vector3.up).normalized;
            if (Vector3.Dot(wallDirection, transform.forward) < 0f)
            {
                wallDirection = -wallDirection;
            }
        }
        else
        {
            EndWallRun();
        }
    }

    private bool TryGetWall(out RaycastHit hit)
    {
        CharacterController cc = Controller;
        Vector3 origin = transform.position + cc.center + Vector3.up * (cc.height * 0.5f);
        bool leftHit = Physics.Raycast(origin, -transform.right, out RaycastHit leftInfo, wallCheckDistance, wallMask, QueryTriggerInteraction.Ignore);
        bool rightHit = Physics.Raycast(origin, transform.right, out RaycastHit rightInfo, wallCheckDistance, wallMask, QueryTriggerInteraction.Ignore);

        if (leftHit && Vector3.Dot(leftInfo.normal, Vector3.up) < 0.3f)
        {
            hit = leftInfo;
            return true;
        }

        if (rightHit && Vector3.Dot(rightInfo.normal, Vector3.up) < 0.3f)
        {
            hit = rightInfo;
            return true;
        }

        hit = default;
        return false;
    }

    private void EndWallRun()
    {
        isWallRunning = false;
        wallDirection = Vector3.zero;
    }

    private void HandleJump(bool jumpPressed)
    {
        if (!jumpPressed)
        {
            return;
        }

        if (isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(Mathf.Max(jumpHeight, 0.01f) * -2f * gravity);
            return;
        }

        if (isWallRunning)
        {
            EndWallRun();
            Vector3 push = Vector3.ProjectOnPlane(currentWallNormal * wallJumpPushVelocity, Vector3.up);
            Vector3 planar = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
            planarVelocity = planar + push;
            verticalVelocity = wallJumpUpVelocity;
            remainingAirJumps = extraAirJumps;
            return;
        }

        if (remainingAirJumps > 0)
        {
            verticalVelocity = Mathf.Sqrt(Mathf.Max(doubleJumpHeight, 0.01f) * -2f * gravity);
            remainingAirJumps--;
        }
    }

    private void ApplyMovement(Vector2 moveInput)
    {
        Vector3 desiredDirection = transform.forward * moveInput.y + transform.right * moveInput.x;
        desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

        float targetSpeed = GetTargetSpeed();
        Vector3 desiredVelocity = desiredDirection * targetSpeed;

        float lerpRate = acceleration * (isGrounded || isWallRunning ? 1f : airControl);
        float lerpFactor = Mathf.Clamp01(lerpRate * Time.deltaTime);
        planarVelocity = Vector3.Lerp(planarVelocity, desiredVelocity, lerpFactor);

        if (isWallRunning)
        {
            Vector3 wallVelocity = wallDirection * wallRunSpeed;
            planarVelocity = Vector3.Lerp(planarVelocity, wallVelocity, Mathf.Clamp01(acceleration * Time.deltaTime));
        }

        planarVelocity.y = 0f;
    }

    private float GetTargetSpeed()
    {
        if (isWallRunning)
        {
            return wallRunSpeed;
        }

        if (isCrouching)
        {
            return crouchSpeed;
        }

        if (isSprinting)
        {
            return sprintSpeed;
        }

        return walkSpeed;
    }

    private void ApplyGravity()
    {
        if (isWallRunning)
        {
            verticalVelocity += wallRunGravity * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, wallRunGravity);
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    private void ApplyCharacterControllerMove()
    {
        Vector3 velocity = planarVelocity;
        velocity.y = verticalVelocity;
        Controller.Move(velocity * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (groundCheck != null)
        {
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        Color wallColor = isWallRunning ? Color.cyan : Color.yellow;
        Gizmos.color = wallColor;

        CharacterController cc = Controller;
        float height = cc != null ? cc.height * 0.5f : 1f;
        Vector3 origin = transform.position + (cc != null ? cc.center : Vector3.up * 0.5f);
        origin += Vector3.up * height * 0.5f;
        Gizmos.DrawLine(origin, origin + transform.right * wallCheckDistance);
        Gizmos.DrawLine(origin, origin - transform.right * wallCheckDistance);
    }

    private bool IsNearGround(float distance)
    {
        if (distance <= 0f)
        {
            return false;
        }

        int mask = groundMask == 0 ? Physics.DefaultRaycastLayers : groundMask;
        Vector3 origin = transform.position + Controller.center;
        origin.y += 0.1f;
        return Physics.Raycast(origin, Vector3.down, distance, mask, QueryTriggerInteraction.Ignore);
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        sprintSpeed = Mathf.Max(0f, sprintSpeed);
        crouchSpeed = Mathf.Max(0f, crouchSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        airControl = Mathf.Clamp01(airControl);
        jumpHeight = Mathf.Max(0f, jumpHeight);
        doubleJumpHeight = Mathf.Max(0f, doubleJumpHeight);
        extraAirJumps = Mathf.Max(0, extraAirJumps);
        if (gravity > -0.01f)
        {
            gravity = -0.01f;
        }
        if (groundedGravity > -0.01f)
        {
            groundedGravity = -0.01f;
        }
        crouchHeight = Mathf.Max(0.1f, crouchHeight);
        crouchTransitionSpeed = Mathf.Max(0f, crouchTransitionSpeed);
        wallCheckDistance = Mathf.Max(0.05f, wallCheckDistance);
        wallRunSpeed = Mathf.Max(0f, wallRunSpeed);
        if (wallRunGravity > -0.01f)
        {
            wallRunGravity = -0.01f;
        }
        wallJumpUpVelocity = Mathf.Max(0f, wallJumpUpVelocity);
        wallJumpPushVelocity = Mathf.Max(0f, wallJumpPushVelocity);
        minWallRunHeight = Mathf.Max(0f, minWallRunHeight);
        groundCheckRadius = Mathf.Max(0.01f, groundCheckRadius);
    }
}
