using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Health))]
public class EchoPurgatoryController : MonoBehaviour
{
    private const string VisualConsumerName = "EchoPurgatoryController";
    private const string MoveParameterName = "isMoving";
    private const string GroundedParameterName = "isGrounded";
    private const string AttackParameterName = "isAttacking";

    private static readonly int MoveParameterHash = Animator.StringToHash(MoveParameterName);
    private static readonly int GroundedParameterHash = Animator.StringToHash(GroundedParameterName);
    private static readonly int AttackParameterHash = Animator.StringToHash(AttackParameterName);

    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool IsAttacking => isAttacking || isAttackVisualActive;
    public bool IsDrivingAttackMovement => isDrivingAttackMovement;
    public int CurrentAttackToken => currentAttackToken;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private Animator visualAnimator;
    [SerializeField] private Color telegraphTint = new Color(0.84f, 1f, 0.95f, 1f);

    [Header("References")]
    [SerializeField] private WeaponHitbox meleeHitbox;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.8f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float preferredDistance = 1.7f;
    [SerializeField] private float distanceTolerance = 0.25f;
    [SerializeField] private float retreatDistance = 0.95f;
    [SerializeField] private float approachDistance = 3.2f;

    [Header("Attack Flow")]
    [SerializeField] private float initialAttackDelay = 0.7f;
    [SerializeField] private float attackCooldown = 1.15f;

    [Header("Slash")]
    [SerializeField] private float slashRange = 2.15f;
    [SerializeField] private float slashTelegraph = 0.3f;
    [SerializeField] private float slashSpeed = 8.25f;
    [SerializeField] private float slashDuration = 0.18f;
    [SerializeField] private float slashRecovery = 0.34f;
    [SerializeField] private float slashDamage = 16f;
    [SerializeField] private float slashKnockback = 5.5f;
    [SerializeField] private float slashHitboxScale = 2.2f;
    [SerializeField] private float slashOverlapRadius = 0.38f;

    [Header("Targeting")]
    [SerializeField] private LayerMask meleeTargetLayers = 1;

    [Header("Spawn Presentation")]
    [SerializeField] private float spawnDuration = 0.9f;
    [SerializeField] private float spawnStartScale = 0.78f;
    [SerializeField] private float spawnOvershootScale = 1.06f;
    [SerializeField] private Color spawnTint = new Color(0.48f, 1f, 0.9f, 1f);

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Health health;
    private EnemyHealthBar enemyHealthBar;
    private Transform player;
    private Health playerHealth;
    private Coroutine attackRoutine;
    private Color baseColor = Color.white;
    private Vector3 baseVisualScale = Vector3.one;
    private Vector3 meleeHomeLocalPosition;
    private float attackTimer;
    private int facingDirection = -1;
    private bool encounterActive;
    private bool isDead;
    private bool isAttacking;
    private bool isAttackVisualActive;
    private bool isDrivingAttackMovement;
    private bool isSpawning;
    private bool animatorHasMoveParameter;
    private bool animatorHasGroundedParameter;
    private bool animatorHasAttackParameter;
    private bool hasExternalKnightVisuals;
    private int currentAttackToken;

    void Reset()
    {
        ResolveReferences(false);
        EnsureDefaultTargetMasks();
    }

    void OnValidate()
    {
        ResolveReferences(false);
        EnsureDefaultTargetMasks();
        preferredDistance = Mathf.Max(0.5f, preferredDistance);
        distanceTolerance = Mathf.Max(0.05f, distanceTolerance);
        retreatDistance = Mathf.Clamp(retreatDistance, 0.1f, preferredDistance);
        approachDistance = Mathf.Max(preferredDistance + distanceTolerance, approachDistance);
        slashRange = Mathf.Max(preferredDistance, slashRange);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        health = GetComponent<Health>();
        enemyHealthBar = GetComponent<EnemyHealthBar>();
        hasExternalKnightVisuals = GetComponent<EchoKnightVisualController>() != null;

        ResolveReferences(true);
        EnsureDefaultTargetMasks();
        CacheAnimatorParameterSupport();
        PrimeVisualState();

        if (visualRenderer != null)
        {
            baseColor = visualRenderer.color;
            if (baseColor.a <= 0.001f)
            {
                baseColor.a = 1f;
                visualRenderer.color = baseColor;
            }

            baseVisualScale = visualRenderer.transform.localScale;
        }

        if (meleeHitbox != null)
        {
            meleeHomeLocalPosition = meleeHitbox.transform.localPosition;
            meleeHitbox.SetOwner(transform);
            meleeHitbox.EndSwing();
        }

        if (rb != null)
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;

        RefreshFacingVisual();
        UpdateAnimatorState(false);
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDied;

        CancelCombatState();
    }

    void Update()
    {
        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        ResolvePlayerReference();

        if (!CanThink())
        {
            UpdateAnimatorState(false);
            return;
        }

        FacePlayer();

        if (!isAttacking && attackRoutine == null && attackTimer <= 0f && CanStartSlashAttack())
            attackRoutine = StartCoroutine(AttackRoutine());

        bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.05f && !isDrivingAttackMovement;
        UpdateAnimatorState(isMoving);
    }

    void FixedUpdate()
    {
        if (rb == null || health == null || health.IsDead || isSpawning)
            return;

        if (!encounterActive || !HasCombatTarget())
        {
            StopHorizontalMotion();
            return;
        }

        if (isAttacking)
        {
            if (!isDrivingAttackMovement)
                StopHorizontalMotion();

            return;
        }

        MoveAroundPlayer();
    }

    public void BindPlayer(Transform target)
    {
        player = target;
        playerHealth = player != null ? player.GetComponent<Health>() : null;
    }

    public void SetEncounterActive(bool isActive)
    {
        encounterActive = isActive && !isDead && !isSpawning;

        if (!encounterActive)
        {
            StopHorizontalMotion();
            isAttackVisualActive = false;
            UpdateAnimatorState(false);
            return;
        }

        attackTimer = Mathf.Max(attackTimer, initialAttackDelay);
    }

    public void PrepareForSpawnPresentation()
    {
        isSpawning = true;
        encounterActive = false;
        CancelCombatState();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        if (bodyCollider != null)
            bodyCollider.enabled = false;

        ToggleHealthBar(false);

        if (visualRenderer != null)
        {
            Color hiddenColor = spawnTint;
            hiddenColor.a = 0f;
            visualRenderer.color = hiddenColor;
            visualRenderer.transform.localScale = baseVisualScale * Mathf.Max(0.1f, spawnStartScale);
        }
    }

    public IEnumerator PlaySpawnPresentation()
    {
        if (visualRenderer == null)
        {
            FinishSpawnPresentation();
            yield break;
        }

        Color startColor = spawnTint;
        startColor.a = 0f;
        Vector3 startScale = baseVisualScale * Mathf.Max(0.1f, spawnStartScale);
        Vector3 overshootScale = baseVisualScale * Mathf.Max(spawnStartScale, spawnOvershootScale);

        float elapsed = 0f;
        while (elapsed < spawnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, spawnDuration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            visualRenderer.color = Color.Lerp(startColor, baseColor, eased);
            visualRenderer.transform.localScale = t < 0.7f
                ? Vector3.Lerp(startScale, overshootScale, t / 0.7f)
                : Vector3.Lerp(overshootScale, baseVisualScale, (t - 0.7f) / 0.3f);

            yield return null;
        }

        visualRenderer.color = baseColor;
        visualRenderer.transform.localScale = baseVisualScale;
        FinishSpawnPresentation();
    }

    private void FinishSpawnPresentation()
    {
        isSpawning = false;

        if (rb != null)
            rb.simulated = true;

        if (bodyCollider != null)
            bodyCollider.enabled = true;

        ToggleHealthBar(true);
    }

    private void ResolveReferences(bool logAutoWire)
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

        if (meleeHitbox == null)
            meleeHitbox = GetComponentInChildren<WeaponHitbox>(true);
    }

    private void EnsureDefaultTargetMasks()
    {
        if (meleeTargetLayers.value == 0)
            meleeTargetLayers = 1 << 0;
    }

    private void PrimeVisualState()
    {
        if (visualAnimator != null && visualAnimator.enabled && visualRenderer != null && visualRenderer.sprite == null)
            visualAnimator.Update(0f);
    }

    private void CacheAnimatorParameterSupport()
    {
        animatorHasMoveParameter = HasBoolParameter(visualAnimator, MoveParameterName);
        animatorHasGroundedParameter = HasBoolParameter(visualAnimator, GroundedParameterName);
        animatorHasAttackParameter = HasBoolParameter(visualAnimator, AttackParameterName);
    }

    private void UpdateAnimatorState(bool isMoving)
    {
        if (hasExternalKnightVisuals || visualAnimator == null)
            return;

        if (animatorHasMoveParameter)
            visualAnimator.SetBool(MoveParameterHash, isMoving);

        if (animatorHasGroundedParameter)
            visualAnimator.SetBool(GroundedParameterHash, true);

        if (animatorHasAttackParameter)
            visualAnimator.SetBool(AttackParameterHash, isAttackVisualActive);
    }

    private void ResolvePlayerReference()
    {
        if (player != null)
            return;

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement == null)
            return;

        player = playerMovement.transform;
        playerHealth = playerMovement.GetComponent<Health>();
    }

    private bool CanThink()
    {
        return encounterActive &&
            !isDead &&
            !isSpawning &&
            health != null &&
            !health.IsDead &&
            HasCombatTarget();
    }

    private bool HasCombatTarget()
    {
        return player != null &&
            playerHealth != null &&
            !playerHealth.IsDead;
    }

    private bool CanStartSlashAttack()
    {
        if (player == null)
            return false;

        return Mathf.Abs(player.position.x - transform.position.x) <= slashRange;
    }

    private void MoveAroundPlayer()
    {
        float horizontalDelta = player.position.x - rb.position.x;
        float distance = Mathf.Abs(horizontalDelta);
        float targetSpeed = 0f;

        if (distance < retreatDistance)
        {
            targetSpeed = -Mathf.Sign(horizontalDelta) * moveSpeed;
        }
        else if (distance > approachDistance)
        {
            targetSpeed = Mathf.Sign(horizontalDelta) * moveSpeed;
        }
        else if (distance > preferredDistance + distanceTolerance)
        {
            targetSpeed = Mathf.Sign(horizontalDelta) * moveSpeed * 0.6f;
        }
        else if (distance < preferredDistance - distanceTolerance)
        {
            targetSpeed = -Mathf.Sign(horizontalDelta) * moveSpeed * 0.5f;
        }

        float nextVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(nextVelocityX, rb.linearVelocity.y);
        FacePlayer();
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        currentAttackToken++;

        if (CanStartSlashAttack())
            yield return PerformSlash();

        attackTimer = attackCooldown;
        isAttackVisualActive = false;
        isDrivingAttackMovement = false;
        isAttacking = false;
        UpdateAnimatorState(false);
        attackRoutine = null;
    }

    private IEnumerator PerformSlash()
    {
        FacePlayer();
        yield return Telegraph(slashTelegraph);
        if (!CanThink())
            yield break;

        AudioManager.TryPlaySfx("sword_swing", 0.9f, Random.Range(0.92f, 0.98f));
        BeginMeleeAttack(slashDamage, slashKnockback, slashHitboxScale, slashOverlapRadius);
        yield return DriveAttackMotion(facingDirection * slashSpeed, slashDuration);
        EndMeleeAttack();
        isAttackVisualActive = false;
        yield return WaitForSecondsSafe(slashRecovery);
    }

    private IEnumerator Telegraph(float duration)
    {
        isAttackVisualActive = true;
        UpdateAnimatorState(false);
        StopHorizontalMotion();

        if (visualRenderer == null || duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 4f);
            visualRenderer.color = Color.Lerp(baseColor, telegraphTint, pulse * 0.8f);
            yield return null;
        }

        visualRenderer.color = baseColor;
    }

    private void BeginMeleeAttack(float damage, float knockback, float hitboxScale, float overlapRadius)
    {
        if (meleeHitbox == null)
            return;

        AlignMeleeHitbox();
        meleeHitbox.SetOwner(transform);
        meleeHitbox.damage = damage;
        meleeHitbox.knockbackForce = knockback;
        meleeHitbox.targetLayers = meleeTargetLayers;
        meleeHitbox.SetSwingScale(hitboxScale, overlapRadius);
        meleeHitbox.BeginSwing();
    }

    private void EndMeleeAttack()
    {
        if (meleeHitbox != null)
            meleeHitbox.EndSwing();
    }

    private IEnumerator DriveAttackMotion(float horizontalSpeed, float duration)
    {
        isDrivingAttackMovement = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!CanThink())
                break;

            rb.linearVelocity = new Vector2(horizontalSpeed, rb.linearVelocity.y);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        isDrivingAttackMovement = false;
        StopHorizontalMotion();
    }

    private IEnumerator WaitForSecondsSafe(float duration)
    {
        if (duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void FacePlayer()
    {
        if (player == null)
            return;

        float horizontalDelta = player.position.x - transform.position.x;
        if (Mathf.Abs(horizontalDelta) <= 0.01f)
            return;

        facingDirection = horizontalDelta >= 0f ? 1 : -1;
        RefreshFacingVisual();
    }

    private void RefreshFacingVisual()
    {
        if (visualRenderer != null)
            visualRenderer.flipX = facingDirection < 0;

        AlignMeleeHitbox();
    }

    private void AlignMeleeHitbox()
    {
        if (meleeHitbox == null)
            return;

        Vector3 position = meleeHomeLocalPosition;
        position.x = Mathf.Abs(meleeHomeLocalPosition.x) * facingDirection;
        meleeHitbox.transform.localPosition = position;
    }

    private void StopHorizontalMotion()
    {
        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void HandleDied()
    {
        isDead = true;
        encounterActive = false;
        CancelCombatState();

        if (bodyCollider != null)
            bodyCollider.enabled = false;

        if (visualRenderer != null)
            visualRenderer.color = baseColor;
    }

    private void CancelCombatState()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = null;
        isAttacking = false;
        isAttackVisualActive = false;
        isDrivingAttackMovement = false;

        if (visualRenderer != null)
        {
            visualRenderer.color = baseColor;
            visualRenderer.transform.localScale = baseVisualScale;
        }

        EndMeleeAttack();
        StopHorizontalMotion();
        UpdateAnimatorState(false);
    }

    private void ToggleHealthBar(bool visible)
    {
        if (enemyHealthBar != null)
            enemyHealthBar.enabled = visible;

        Transform barRoot = transform.Find("EnemyHealthBar");
        if (barRoot != null)
            barRoot.gameObject.SetActive(visible);
    }

    private static bool HasBoolParameter(Animator sourceAnimator, string parameterName)
    {
        if (sourceAnimator == null)
            return false;

        AnimatorControllerParameter[] parameters = sourceAnimator.parameters;
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
}
