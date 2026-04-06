using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    private const float GroundCastPadding = 0.02f;
    private const string VisualConsumerName = "PlayerMovement";

    public int FacingDirection => facingDirection;
    public float MoveInput => moveInput;
    public float VerticalInput => verticalInput;
    public bool IsGrounded => isGrounded;
    public bool IsAirDashing => isAirDashing;
    public bool IsGroundSliding => isGroundSliding;
    public bool IsWallSliding => isWallSliding;
    public bool IsWallHanging => isWallHanging;
    public bool IsWallClimbing => isWallClimbing;
    public bool IsCrouching => isCrouching;
    public bool IsCrouchWalking => isCrouching && Mathf.Abs(moveInput) > 0.01f;
    public bool IsMovingHorizontally => Mathf.Abs(HorizontalSpeed) > 0.05f || Mathf.Abs(moveInput) > 0.05f;
    public bool IsTurningAround => turnAroundTimer > 0f;
    public bool IsInCrouchTransition => crouchTransitionTimer > 0f;
    public float HorizontalSpeed => rb != null ? rb.linearVelocity.x : 0f;
    public float VerticalSpeed => rb != null ? rb.linearVelocity.y : 0f;
    public int TurnAnimationToken => turnAnimationToken;
    public int CrouchTransitionToken => crouchTransitionToken;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private Animator visualAnimator;

    [Header("Movement")]
    public float maxSpeed = 9f;
    public float acceleration = 40f;
    public float turnAcceleration = 90f;
    public float groundDeceleration = 55f;
    public float airAcceleration = 28f;
    public float airDeceleration = 20f;
    public float crouchWalkSpeedMultiplier = 0.42f;

    [Header("Jump")]
    public float jumpForce = 12f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    [Range(0.1f, 1f)] public float jumpCutMultiplier = 0.5f;
    public float fallGravityMultiplier = 2.2f;
    public float wallJumpHorizontalForce = 9f;
    public float wallJumpVerticalForce = 11f;
    public float wallJumpLockTime = 0.15f;

    [Header("Dash And Slide")]
    public float dashSpeed = 16f;
    public float dashTime = 0.15f;
    public float dashCooldown = 0.4f;
    public float slideSpeed = 13f;
    public float slideTime = 0.18f;

    [Header("Wall")]
    public float wallCheckDistance = 0.08f;
    public float wallSlideSpeed = 3.4f;
    public float wallClimbSpeed = 3.35f;

    [Header("Transitions")]
    public float turnAroundLockTime = 0.08f;
    public float crouchTransitionTime = 0.08f;

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public float groundCheckDepth = 0.08f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private float moveInput;
    private float verticalInput;
    private bool isGrounded;
    private bool isGroundSliding;
    private bool isAirDashing;
    private bool isWallSliding;
    private bool isWallHanging;
    private bool isWallClimbing;
    private bool isCrouching;
    private bool jumpReleased;
    private bool inputLocked;
    private bool hasGroundState;
    private bool hasExternalKnightVisuals;
    private float groundSlideTimer;
    private float dashTimer;
    private float dashCooldownTimer;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float wallJumpLockTimer;
    private float turnAroundTimer;
    private float crouchTransitionTimer;
    private int facingDirection = 1;
    private int wallDirection;
    private int turnAnimationToken;
    private int crouchTransitionToken;
    private float baseGravityScale;
    private ContactFilter2D groundContactFilter;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[6];
    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];

    void Reset()
    {
        ResolveVisualReferences(false);
        EnsureGroundLayerConfigured();
    }

    void OnValidate()
    {
        ResolveVisualReferences(false);
        EnsureGroundLayerConfigured();
        RefreshGroundContactFilter();
        ValidateVisualSetup();

        crouchWalkSpeedMultiplier = Mathf.Clamp(crouchWalkSpeedMultiplier, 0.05f, 1f);
        slideSpeed = Mathf.Max(1f, slideSpeed);
        slideTime = Mathf.Max(0.05f, slideTime);
        wallCheckDistance = Mathf.Max(0.02f, wallCheckDistance);
        wallSlideSpeed = Mathf.Max(0.25f, wallSlideSpeed);
        wallClimbSpeed = Mathf.Max(0.25f, wallClimbSpeed);
        turnAroundLockTime = Mathf.Max(0f, turnAroundLockTime);
        crouchTransitionTime = Mathf.Max(0f, crouchTransitionTime);
        wallJumpLockTime = Mathf.Max(0f, wallJumpLockTime);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        baseGravityScale = rb.gravityScale;
        hasExternalKnightVisuals = GetComponent<PlayerKnightVisualController>() != null;

        ResolveVisualReferences(true);
        EnsureGroundLayerConfigured();
        RefreshGroundContactFilter();
        EnsureAnimatorEnabled();
        ValidateVisualSetup();
    }

    void Start()
    {
        SnapToGroundOnSpawn();
        CheckGround();
        CheckWalls();
        PrimeIdleVisualState();
    }

    void Update()
    {
        if (!inputLocked)
        {
            CaptureInput();
            HandleJumpTimers();
            HandleDashInput();
        }

        UpdateAnimator();
    }

    void FixedUpdate()
    {
        CheckGround();
        CheckWalls();

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.fixedDeltaTime;

        if (wallJumpLockTimer > 0f)
            wallJumpLockTimer -= Time.fixedDeltaTime;

        if (turnAroundTimer > 0f)
            turnAroundTimer -= Time.fixedDeltaTime;

        if (crouchTransitionTimer > 0f)
            crouchTransitionTimer -= Time.fixedDeltaTime;

        if (inputLocked)
        {
            rb.gravityScale = baseGravityScale;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            ApplyBetterGravity();
            return;
        }

        if (isGroundSliding)
        {
            HandleGroundSlide();
            return;
        }

        if (isAirDashing)
        {
            HandleAirDash();
            return;
        }

        if (isWallClimbing)
        {
            HandleWallClimb();
            return;
        }

        ApplyHorizontalMovement();
        HandleJump();
        ApplyBetterGravity();
    }

    private void ResolveVisualReferences(bool logAutoWire)
    {
        var resolved = PlayerVisualReferenceUtility.Resolve(
            this,
            VisualConsumerName,
            visualRoot,
            visualRenderer,
            visualAnimator,
            logAutoWire);

        visualRoot = resolved.Root;
        visualRenderer = resolved.Renderer;
        visualAnimator = resolved.Animator;
    }

    private void ValidateVisualSetup()
    {
        PlayerVisualReferenceUtility.Validate(
            this,
            VisualConsumerName,
            visualRoot,
            visualRenderer,
            visualAnimator);
    }

    private void EnsureAnimatorEnabled()
    {
        if (hasExternalKnightVisuals || visualAnimator == null || visualAnimator.enabled)
            return;

        visualAnimator.enabled = true;
        Debug.LogWarning(
            $"{VisualConsumerName} enabled visualAnimator '{PlayerVisualReferenceUtility.DescribeTransform(visualAnimator.transform)}' " +
            "because it was disabled at startup.",
            this);
    }

    private void PrimeIdleVisualState()
    {
        if (visualRenderer == null)
            return;

        if (visualRenderer.color.a <= 0.001f)
        {
            Color repairedColor = visualRenderer.color;
            repairedColor.a = 1f;
            visualRenderer.color = repairedColor;
        }

        if (hasExternalKnightVisuals || visualRenderer.sprite != null || visualAnimator == null || !visualAnimator.enabled)
            return;

        visualAnimator.Update(0f);
    }

    private void UpdateAnimator()
    {
        if (hasExternalKnightVisuals || !hasGroundState || visualAnimator == null || !visualAnimator.isActiveAndEnabled)
            return;

        float horizontalSpeed = rb != null ? Mathf.Abs(rb.linearVelocity.x) : Mathf.Abs(moveInput);
        bool isMoving = horizontalSpeed > 0.05f || Mathf.Abs(moveInput) > 0.01f;

        visualAnimator.SetBool("isMoving", isGrounded && isMoving);
        visualAnimator.SetBool("isGrounded", isGrounded);
    }

    private void CaptureInput()
    {
        moveInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(moveInput) < 0.01f)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                moveInput = -1f;
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                moveInput = 1f;
        }

        if (Mathf.Abs(verticalInput) < 0.01f)
        {
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                verticalInput = -1f;
            else if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                verticalInput = 1f;
        }

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            int intendedFacing = moveInput > 0f ? 1 : -1;
            bool shouldTriggerTurn =
                intendedFacing != facingDirection &&
                isGrounded &&
                !isCrouching &&
                !isGroundSliding &&
                Mathf.Abs(rb.linearVelocity.x) > 0.8f &&
                turnAroundTimer <= 0f;

            facingDirection = intendedFacing;

            if (shouldTriggerTurn)
            {
                turnAroundTimer = turnAroundLockTime;
                turnAnimationToken++;
            }
        }

        bool shouldCrouch = isGrounded && verticalInput < -0.35f && !isGroundSliding && !isAirDashing;
        if (shouldCrouch != isCrouching)
        {
            isCrouching = shouldCrouch;
            crouchTransitionTimer = crouchTransitionTime;
            crouchTransitionToken++;
        }

        UpdateFacingVisual();

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            jumpBufferTimer = jumpBufferTime;

        if (Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.UpArrow))
            jumpReleased = true;
    }

    private void UpdateFacingVisual()
    {
        if (visualRenderer == null)
            return;

        visualRenderer.flipX = facingDirection < 0;
    }

    private void HandleJumpTimers()
    {
        if (jumpBufferTimer > 0f)
            jumpBufferTimer -= Time.deltaTime;

        if (!isGrounded)
            coyoteTimer -= Time.deltaTime;
    }

    private void HandleDashInput()
    {
        if (!Input.GetKeyDown(KeyCode.E) || dashCooldownTimer > 0f)
            return;

        bool canSlide = isGrounded &&
            !isCrouching &&
            (Mathf.Abs(moveInput) > 0.01f || Mathf.Abs(rb.linearVelocity.x) > 1f);

        if (canSlide)
            StartGroundSlide();
        else
            StartAirDash();
    }

    private void ApplyHorizontalMovement()
    {
        if (turnAroundTimer > 0f)
        {
            float brakingVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, 0f, turnAcceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(brakingVelocityX, rb.linearVelocity.y);
            return;
        }

        if (isWallSliding || isWallHanging)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float effectiveMoveInput = wallJumpLockTimer > 0f ? 0f : moveInput;
        float speedMultiplier = isCrouching ? crouchWalkSpeedMultiplier : 1f;
        float targetSpeed = effectiveMoveInput * maxSpeed * speedMultiplier;
        float accelRate;

        if (Mathf.Abs(effectiveMoveInput) > 0.01f)
        {
            bool turning = Mathf.Abs(rb.linearVelocity.x) > 0.05f &&
                Mathf.Sign(effectiveMoveInput) != Mathf.Sign(rb.linearVelocity.x);

            accelRate = isGrounded
                ? (turning ? turnAcceleration : acceleration)
                : airAcceleration;
        }
        else
        {
            accelRate = isGrounded ? groundDeceleration : airDeceleration;
        }

        float newVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);
    }

    private void HandleJump()
    {
        bool wantsJump = jumpBufferTimer > 0f;
        bool canGroundJump = coyoteTimer > 0f;
        bool canWallJump = wallDirection != 0 && !isGrounded && !isAirDashing && !isGroundSliding;

        if (wantsJump && canGroundJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            return;
        }

        if (wantsJump && canWallJump)
        {
            rb.linearVelocity = new Vector2(-wallDirection * wallJumpHorizontalForce, wallJumpVerticalForce);
            facingDirection = -wallDirection;
            wallJumpLockTimer = wallJumpLockTime;
            jumpBufferTimer = 0f;
            jumpReleased = false;
            isWallSliding = false;
            isWallHanging = false;
            isWallClimbing = false;
            UpdateFacingVisual();
            return;
        }

        if (jumpReleased && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

        jumpReleased = false;
    }

    private void CheckGround()
    {
        if (bodyCollider == null)
            return;

        Bounds bounds = bodyCollider.bounds;
        float probeDistance = Mathf.Max(groundCheckDepth + GroundCastPadding, 0.06f);
        int hitCount = bodyCollider.Cast(Vector2.down, groundContactFilter, groundHits, probeDistance);
        float nearestGroundDistance = float.MaxValue;
        bool castFoundGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (hit.collider == null)
                continue;

            castFoundGround = true;
            nearestGroundDistance = Mathf.Min(nearestGroundDistance, hit.distance);
        }

        bool touchingGround = bodyCollider.IsTouchingLayers(groundLayer);
        isGrounded = touchingGround || castFoundGround;
        hasGroundState = true;

        if (isGrounded)
            coyoteTimer = coyoteTime;
        else
            isCrouching = false;

        Vector3 debugStart = new Vector3(bounds.center.x, bounds.min.y, 0f);
        float debugDistance = nearestGroundDistance < float.MaxValue ? nearestGroundDistance : probeDistance;
        Debug.DrawLine(
            debugStart,
            debugStart + Vector3.down * debugDistance,
            isGrounded ? Color.green : Color.red);
    }

    private void CheckWalls()
    {
        wallDirection = 0;

        if (bodyCollider == null || isGrounded)
        {
            isWallSliding = false;
            isWallHanging = false;
            isWallClimbing = false;
            return;
        }

        bool touchingRightWall = HasWallHit(bodyCollider.Cast(Vector2.right, groundContactFilter, wallHits, wallCheckDistance));
        bool touchingLeftWall = HasWallHit(bodyCollider.Cast(Vector2.left, groundContactFilter, wallHits, wallCheckDistance));

        if (touchingRightWall)
            wallDirection = 1;
        else if (touchingLeftWall)
            wallDirection = -1;

        bool pressingIntoWall = wallDirection != 0 &&
            Mathf.Abs(moveInput) > 0.01f &&
            Mathf.Sign(moveInput) == wallDirection;

        isWallClimbing = pressingIntoWall && verticalInput > 0.2f && !isAirDashing && !isGroundSliding;
        isWallSliding = pressingIntoWall && !isWallClimbing && rb.linearVelocity.y < -0.05f;
        isWallHanging = pressingIntoWall && !isWallClimbing && !isWallSliding && Mathf.Abs(rb.linearVelocity.y) <= 0.05f;
    }

    private bool HasWallHit(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            if (wallHits[i].collider != null)
                return true;
        }

        return false;
    }

    private void EnsureGroundLayerConfigured()
    {
        if (groundLayer.value != 0)
            return;

        int fallbackMask = 0;
        int ground = LayerMask.NameToLayer("Ground");
        int terrain = LayerMask.NameToLayer("Terrain");

        if (ground >= 0)
            fallbackMask |= 1 << ground;
        if (terrain >= 0)
            fallbackMask |= 1 << terrain;

        if (fallbackMask == 0)
            fallbackMask = 1 << 6;

        if (fallbackMask != 0)
            groundLayer = fallbackMask;
    }

    private void RefreshGroundContactFilter()
    {
        groundContactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = groundLayer,
            useTriggers = false
        };
    }

    private void SnapToGroundOnSpawn()
    {
        if (bodyCollider == null || groundLayer.value == 0)
            return;

        float snapDistance = Mathf.Max(groundCheckDepth + 0.12f, 0.15f);
        int hitCount = bodyCollider.Cast(Vector2.down, groundContactFilter, groundHits, snapDistance);
        float nearestGroundDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (hit.collider == null)
                continue;

            nearestGroundDistance = Mathf.Min(nearestGroundDistance, hit.distance);
        }

        if (nearestGroundDistance == float.MaxValue || nearestGroundDistance <= 0f)
            return;

        float snapAmount = Mathf.Max(0f, nearestGroundDistance - GroundCastPadding);
        if (snapAmount <= 0f)
            return;

        transform.position += Vector3.down * snapAmount;
    }

    private void ApplyBetterGravity()
    {
        if (isWallClimbing)
        {
            rb.gravityScale = 0f;
            return;
        }

        if (isWallHanging)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(0f, 0f);
            return;
        }

        if (isWallSliding)
        {
            rb.gravityScale = baseGravityScale;
            if (rb.linearVelocity.y < -wallSlideSpeed)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideSpeed);
            return;
        }

        if (rb.linearVelocity.y < -0.01f)
            rb.gravityScale = baseGravityScale * fallGravityMultiplier;
        else
            rb.gravityScale = baseGravityScale;

        if (isGrounded && rb.linearVelocity.y < 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -1f);
    }

    private void StartGroundSlide()
    {
        isGroundSliding = true;
        isAirDashing = false;
        isCrouching = false;
        groundSlideTimer = slideTime;
        dashCooldownTimer = dashCooldown;
    }

    private void HandleGroundSlide()
    {
        rb.gravityScale = baseGravityScale;
        rb.linearVelocity = new Vector2(facingDirection * slideSpeed, rb.linearVelocity.y);
        groundSlideTimer -= Time.fixedDeltaTime;

        if (groundSlideTimer <= 0f || !isGrounded)
            isGroundSliding = false;
    }

    private void StartAirDash()
    {
        isAirDashing = true;
        isGroundSliding = false;
        dashTimer = dashTime;
        dashCooldownTimer = dashCooldown;
    }

    private void HandleAirDash()
    {
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(facingDirection * dashSpeed, 0f);
        dashTimer -= Time.fixedDeltaTime;

        if (dashTimer <= 0f)
        {
            isAirDashing = false;
            rb.gravityScale = baseGravityScale;
        }
    }

    private void HandleWallClimb()
    {
        rb.gravityScale = 0f;
        rb.linearVelocity = new Vector2(0f, wallClimbSpeed);
    }

    public void SetInputLocked(bool isLocked)
    {
        inputLocked = isLocked;

        if (!inputLocked)
            return;

        moveInput = 0f;
        verticalInput = 0f;
        jumpBufferTimer = 0f;
        jumpReleased = false;
        isGroundSliding = false;
        isAirDashing = false;
        isWallSliding = false;
        isWallHanging = false;
        isWallClimbing = false;
        isCrouching = false;
        groundSlideTimer = 0f;
        dashTimer = 0f;
        turnAroundTimer = 0f;
        crouchTransitionTimer = 0f;
        rb.gravityScale = baseGravityScale;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }
}
