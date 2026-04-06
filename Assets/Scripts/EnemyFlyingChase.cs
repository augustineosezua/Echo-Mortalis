using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class EnemyFlyingChase : MonoBehaviour, IEnemyAttackVisual
{
    private const string MoveParameterName = "isMoving";
    private const string DeadParameterName = "isDead";
    private static readonly int MoveParameterHash = Animator.StringToHash(MoveParameterName);
    private static readonly int DeadParameterHash = Animator.StringToHash(DeadParameterName);

    private enum VisualState
    {
        Idle,
        Flying,
        Attack,
        Hurt,
        Death
    }

    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private float activationDistance = 14f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.25f;
    [SerializeField] private float acceleration = 16f;
    [SerializeField] private float preferredHorizontalDistance = 2.25f;
    [SerializeField] private float minimumHorizontalDistance = 1.1f;
    [SerializeField] private float preferredHeightAbovePlayer = 2f;
    [SerializeField] private float verticalDeadZone = 0.2f;
    [SerializeField] private float hoverBobAmplitude = 0.35f;
    [SerializeField] private float hoverBobFrequency = 1.8f;
    [SerializeField] private float swoopDistance = 2.5f;
    [SerializeField] private float swoopSpeedMultiplier = 1.2f;
    [SerializeField] private bool spriteFacesRight = false;

    [Header("Animation")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private float idleFramesPerSecond = 6f;
    [SerializeField] private Sprite[] flyingFrames;
    [SerializeField] private float flyingFramesPerSecond = 10f;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private float attackFramesPerSecond = 18f;
    [SerializeField] private Sprite[] hurtFrames;
    [SerializeField] private float hurtFramesPerSecond = 16f;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private float deathFramesPerSecond = 30f;
    [SerializeField] private float movementAnimationThreshold = 0.15f;
    [SerializeField] private float hurtHoldDuration = 0.16f;

    [Header("Attack Visual")]
    [SerializeField] private Color attackTint = new Color(1f, 0.58f, 0.2f, 1f);
    [SerializeField] private float attackFlashDuration = 0.14f;
    [SerializeField] private float attackScalePulse = 1.08f;

    [Header("Fail Safes")]
    [SerializeField] private bool enableFallDeath = true;
    [SerializeField] private float fallDeathY = -12f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private EnemyContactDamage contactDamage;
    private Health health;
    private Color baseColor = Color.white;
    private Vector3 baseScale = Vector3.one;
    private Sprite fallbackSprite;
    private Coroutine attackVisualRoutine;
    private bool activatedByDistance;
    private bool animatorHasMoveParameter;
    private bool animatorHasDeadParameter;
    private float attackAnimationUntil = float.NegativeInfinity;
    private float hurtAnimationUntil = float.NegativeInfinity;
    private float lastKnownHealth;
    private float movementAnimationThresholdSqr;
    private float animationElapsed;
    private VisualState visualState;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        animator = GetComponent<Animator>();
        contactDamage = GetComponent<EnemyContactDamage>();
        health = GetComponent<Health>();

        if (rb != null)
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;

        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
            baseScale = spriteRenderer.transform.localScale;
            fallbackSprite = spriteRenderer.sprite;
        }

        CacheAnimatorParameters();
        activatedByDistance = activationDistance <= 0f;
        movementAnimationThresholdSqr = movementAnimationThreshold * movementAnimationThreshold;
        lastKnownHealth = health != null && health.MaxHealth > 0f ? health.MaxHealth : 0f;
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.OnHealthChanged += HandleHealthChanged;
            health.OnDied += HandleDied;
            if (health.MaxHealth > 0f)
                lastKnownHealth = health.CurrentHealth > 0f ? health.CurrentHealth : health.MaxHealth;
        }

        if (animator != null && animatorHasDeadParameter)
            animator.SetBool(DeadParameterHash, false);

        attackAnimationUntil = float.NegativeInfinity;
        hurtAnimationUntil = float.NegativeInfinity;
        visualState = VisualState.Idle;
        animationElapsed = 0f;
        ApplyAnimationFrame(visualState, 0f);
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDied -= HandleDied;
        }

        ResetAttackVisual();
    }

    void Start()
    {
        ResolvePlayer();
        if (health != null && health.MaxHealth > 0f)
            lastKnownHealth = health.CurrentHealth > 0f ? health.CurrentHealth : health.MaxHealth;
    }

    void Update()
    {
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        rb.angularVelocity = 0f;
        TryHandleFallDeath();

        if (health != null && health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            SetAnimatorMoving(false);
            UpdateFacing();
            return;
        }

        ResolvePlayer();
        if (player == null || !HasActivatedByDistance())
        {
            rb.linearVelocity = Vector2.zero;
            SetAnimatorMoving(false);
            return;
        }

        Vector2 targetPosition = GetDesiredHoverPosition();
        Vector2 delta = targetPosition - rb.position;
        float speedMultiplier = delta.magnitude <= swoopDistance ? swoopSpeedMultiplier : 1f;

        if (Mathf.Abs(delta.y) <= verticalDeadZone)
            delta.y = 0f;

        Vector2 desiredVelocity = delta.sqrMagnitude <= 0.01f
            ? Vector2.zero
            : delta.normalized * moveSpeed * speedMultiplier;

        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime);

        if (delta.sqrMagnitude <= 0.09f)
            rb.linearVelocity *= 0.92f;

        UpdateFacing();
        SetAnimatorMoving(rb.linearVelocity.sqrMagnitude > 0.05f);
    }

    private void TryHandleFallDeath()
    {
        if (!enableFallDeath || health == null || health.IsDead || transform.position.y > fallDeathY)
            return;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        health.TakeDamage(Mathf.Max(1f, health.CurrentHealth), Vector2.down, 0f);
    }

    public void PlayAttackVisual()
    {
        if (!isActiveAndEnabled || spriteRenderer == null || (health != null && health.IsDead))
            return;

        attackAnimationUntil = Time.time + GetAnimationDuration(attackFrames, attackFramesPerSecond);
        if (visualState == VisualState.Attack)
            animationElapsed = 0f;

        if (attackVisualRoutine != null)
            StopCoroutine(attackVisualRoutine);

        attackVisualRoutine = StartCoroutine(PlayAttackFlashRoutine());
    }

    private IEnumerator PlayAttackFlashRoutine()
    {
        float elapsed = 0f;
        Color startColor = attackTint;
        Vector3 pulseScale = baseScale * Mathf.Max(1f, attackScalePulse);

        while (elapsed < attackFlashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, attackFlashDuration));
            float eased = 1f - Mathf.Abs((t * 2f) - 1f);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(baseColor, startColor, eased);
                spriteRenderer.transform.localScale = Vector3.Lerp(baseScale, pulseScale, eased);
            }

            yield return null;
        }

        ResetAttackVisual();
        attackVisualRoutine = null;
    }

    private Vector2 GetDesiredHoverPosition()
    {
        Vector2 playerPosition = player.position;
        float horizontalSign = Mathf.Sign(playerPosition.x - rb.position.x);
        if (Mathf.Approximately(horizontalSign, 0f))
            horizontalSign = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;

        float horizontalOffset = preferredHorizontalDistance;
        if (Mathf.Abs(playerPosition.x - rb.position.x) < minimumHorizontalDistance)
            horizontalOffset = minimumHorizontalDistance;

        float bobOffset = Mathf.Sin(Time.time * hoverBobFrequency) * hoverBobAmplitude;
        return new Vector2(
            playerPosition.x - (horizontalSign * horizontalOffset),
            playerPosition.y + preferredHeightAbovePlayer + bobOffset);
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            player = taggedPlayer.transform;
            return;
        }

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
            player = playerMovement.transform;
    }

    private bool HasActivatedByDistance()
    {
        if (activatedByDistance || activationDistance <= 0f)
            return true;

        if (player == null)
            return false;

        if (Vector2.Distance(rb.position, player.position) <= activationDistance)
            activatedByDistance = true;

        return activatedByDistance;
    }

    private void UpdateFacing()
    {
        if (spriteRenderer == null || player == null)
            return;

        bool faceLeft = player.position.x < transform.position.x;
        spriteRenderer.flipX = spriteFacesRight ? faceLeft : !faceLeft;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        float previousHealth = lastKnownHealth > 0f ? lastKnownHealth : maxHealth;
        lastKnownHealth = currentHealth;

        if (currentHealth <= 0f || currentHealth >= previousHealth)
            return;

        hurtAnimationUntil = Time.time + Mathf.Max(hurtHoldDuration, GetAnimationDuration(hurtFrames, hurtFramesPerSecond));
        if (visualState == VisualState.Hurt)
            animationElapsed = 0f;
    }

    private void HandleDied()
    {
        if (attackVisualRoutine != null)
        {
            StopCoroutine(attackVisualRoutine);
            attackVisualRoutine = null;
        }

        ResetAttackVisual();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (contactDamage != null)
            contactDamage.enabled = false;

        if (bodyCollider != null)
            bodyCollider.enabled = false;

        SetAnimatorMoving(false);
        if (animator != null && animatorHasDeadParameter)
            animator.SetBool(DeadParameterHash, true);

        visualState = VisualState.Death;
        animationElapsed = 0f;
        ApplyAnimationFrame(visualState, 0f);
    }

    private void ResetAttackVisual()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = baseColor;
        spriteRenderer.transform.localScale = baseScale;
    }

    private void CacheAnimatorParameters()
    {
        animatorHasMoveParameter = HasBoolParameter(MoveParameterName);
        animatorHasDeadParameter = HasBoolParameter(DeadParameterName);
    }

    private void SetAnimatorMoving(bool isMoving)
    {
        if (animator != null && animatorHasMoveParameter)
            animator.SetBool(MoveParameterHash, isMoving);
    }

    private void UpdateAnimation()
    {
        if (spriteRenderer == null)
            return;

        VisualState nextState = GetVisualState();
        if (nextState != visualState)
        {
            visualState = nextState;
            animationElapsed = 0f;
        }
        else
        {
            animationElapsed += Time.deltaTime;
        }

        ApplyAnimationFrame(visualState, animationElapsed);
    }

    private VisualState GetVisualState()
    {
        if (health != null && health.IsDead)
            return VisualState.Death;

        if (Time.time < hurtAnimationUntil && HasFrames(hurtFrames))
            return VisualState.Hurt;

        if (Time.time < attackAnimationUntil && HasFrames(attackFrames))
            return VisualState.Attack;

        if (rb != null && rb.linearVelocity.sqrMagnitude > movementAnimationThresholdSqr && HasFrames(flyingFrames))
            return VisualState.Flying;

        return VisualState.Idle;
    }

    private void ApplyAnimationFrame(VisualState state, float elapsed)
    {
        Sprite[] frames = GetFramesForState(state);
        if (!HasFrames(frames))
        {
            if (fallbackSprite != null)
                spriteRenderer.sprite = fallbackSprite;
            return;
        }

        float framesPerSecond = Mathf.Max(0.01f, GetFrameRateForState(state));
        int frameIndex = Mathf.FloorToInt(elapsed * framesPerSecond);
        if (IsLoopingState(state))
            frameIndex %= frames.Length;
        else
            frameIndex = Mathf.Min(frameIndex, frames.Length - 1);

        spriteRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
    }

    private Sprite[] GetFramesForState(VisualState state)
    {
        switch (state)
        {
            case VisualState.Flying:
                return flyingFrames;
            case VisualState.Attack:
                return attackFrames;
            case VisualState.Hurt:
                return hurtFrames;
            case VisualState.Death:
                return deathFrames;
            default:
                return idleFrames;
        }
    }

    private float GetFrameRateForState(VisualState state)
    {
        switch (state)
        {
            case VisualState.Flying:
                return flyingFramesPerSecond;
            case VisualState.Attack:
                return attackFramesPerSecond;
            case VisualState.Hurt:
                return hurtFramesPerSecond;
            case VisualState.Death:
                return deathFramesPerSecond;
            default:
                return idleFramesPerSecond;
        }
    }

    private bool IsLoopingState(VisualState state)
    {
        return state == VisualState.Idle || state == VisualState.Flying;
    }

    private float GetAnimationDuration(Sprite[] frames, float framesPerSecond)
    {
        if (!HasFrames(frames))
            return 0f;

        return frames.Length / Mathf.Max(0.01f, framesPerSecond);
    }

    private bool HasFrames(Sprite[] frames)
    {
        return frames != null && frames.Length > 0;
    }

    private bool HasBoolParameter(string parameterName)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Bool &&
                parameters[i].name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.84f, 0.25f, 0.18f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, swoopDistance);

        Gizmos.color = new Color(0.36f, 0.74f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, activationDistance);
    }
#endif
}
