using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class EnemyRandomFollower : MonoBehaviour, IEnemyAttackVisual
{
    private const string MoveParameterName = "isMoving";
    private const string DeadParameterName = "isDead";
    private const string DeathStateName = "Base Layer.Death";
    private static readonly int MoveParameterHash = Animator.StringToHash(MoveParameterName);
    private static readonly int DeadParameterHash = Animator.StringToHash(DeadParameterName);
    private static readonly int DeathStateHash = Animator.StringToHash(DeathStateName);

    [Header("Target")]
    public Transform player;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float acceleration = 25f;
    public float stopDistance = 1f;
    [Tooltip("Back away when the player is closer than this. 0 = disabled.")]
    public float retreatDistance = 0f;
    public LayerMask groundLayer;
    public float groundCheckDepth = 0.08f;

    [Header("Screen Bounds")]
    [Range(0f, 0.45f)] public float screenPadding = 0.03f;
    [SerializeField] private bool spriteFacesRight = true;

    [Header("Randomness")]
    public float randomOffsetRadius = 2f;
    public float randomOffsetUpdateTime = 1f;

    [Header("Encounter Gating")]
    [Tooltip("If greater than 0, the enemy will stay idle until the player gets this close.")]
    [SerializeField] private float activationDistance = 0f;

    [Header("Fail Safes")]
    [SerializeField] private bool enableFallDeath = true;
    [SerializeField] private float fallDeathY = -12f;

    [Header("Enemy Identity")]
    [Tooltip("Tint used as a subtle ID pulse to make the enemy visually distinct.")]
    [SerializeField] private Color identityTintColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float identityPulseSpeed = 2.5f;
    [SerializeField] private float identityPulseIntensity = 0.35f;
    [SerializeField] private bool showIdentityPulse = true;

    [Header("Attack Visual")]
    [Tooltip("Attack animation frames from the attack spritesheet.")]
    [SerializeField] private Sprite[] attackFrames = new Sprite[0];
    [SerializeField] private float attackFrameRate = 10f;
    [SerializeField] private bool disableAnimatorDuringAttack = true;
    [SerializeField] private bool returnToAnimatorAfterAttack = true;
    [SerializeField] private float fallbackAttackFlash = 0.18f;

    [Header("Spawn Presentation")]
    [SerializeField] private float spawnDuration = 0.85f;
    [SerializeField] private float spawnStartScale = 0.86f;
    [SerializeField] private float spawnOvershootScale = 1.05f;
    [SerializeField] private Color spawnTint = new Color(0.7f, 0.88f, 1f, 1f);

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private Animator animator;
    private float randomOffsetX;
    private float randomOffsetTimer;
    private Color baseSpriteColor = Color.white;
    private Sprite defaultSprite;
    private Coroutine identityPulseRoutine;
    private Coroutine attackVisualRoutine;
    private bool isPlayingAttackVisual;
    private EnemyContactDamage contactDamage;
    private bool restoreContactDamageOnWake;
    private Health health;
    private Canvas[] childCanvases;
    private Vector3 baseVisualScale = Vector3.one;
    private bool isDormant;
    private bool hasActivatedByProximity;
    private float movementPauseTimer;
    private bool animatorHasMoveParameter;
    private bool animatorHasDeadParameter;

    public bool IsDormant => isDormant;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = FindPrimarySpriteRenderer();
        bodyCollider = EnsureBodyCollider();
        animator = GetComponent<Animator>();
        contactDamage = GetComponent<EnemyContactDamage>();
        restoreContactDamageOnWake = contactDamage != null && contactDamage.enabled;
        health = GetComponent<Health>();
        childCanvases = GetComponentsInChildren<Canvas>(true);
        if (rb != null)
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        mainCamera = Camera.main;
        CacheAnimatorParameterSupport();

        if (spriteRenderer != null)
        {
            baseSpriteColor = spriteRenderer.color;
            defaultSprite = spriteRenderer.sprite;
            baseVisualScale = spriteRenderer.transform.localScale;
        }

        hasActivatedByProximity = activationDistance <= 0f;

    }

    private SpriteRenderer FindPrimarySpriteRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer selfRenderer = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject == gameObject)
            {
                selfRenderer = candidate;
                continue;
            }

            return candidate;
        }

        return selfRenderer;
    }

    private Collider2D EnsureBodyCollider()
    {
        BoxCollider2D rootCollider = GetComponent<BoxCollider2D>();
        if (rootCollider == null)
        {
            rootCollider = gameObject.AddComponent<BoxCollider2D>();
            ApplyFallbackColliderShape(rootCollider);
            Debug.LogWarning("EnemyRandomFollower: Added a missing root BoxCollider2D body collider to the enemy.", this);
            return rootCollider;
        }

        if (rootCollider.size.sqrMagnitude <= 0.0001f)
            ApplyFallbackColliderShape(rootCollider);

        return rootCollider;
    }

    private void ApplyFallbackColliderShape(BoxCollider2D collider)
    {
        if (collider == null)
            return;

        collider.isTrigger = false;

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Bounds spriteBounds = spriteRenderer.sprite.bounds;
            float width = Mathf.Max(0.35f, spriteBounds.size.x * 0.6f);
            float height = Mathf.Max(0.3f, spriteBounds.size.y * 0.45f);
            collider.size = new Vector2(width, height);
            collider.offset = new Vector2(0f, height * 0.5f);
            return;
        }

        collider.size = new Vector2(0.7f, 0.5f);
        collider.offset = new Vector2(0f, 0.25f);
    }

    void OnEnable()
    {
        SubscribeToHealthEvents();

        if (animator != null && animatorHasDeadParameter)
            animator.SetBool(DeadParameterHash, false);

        if (!isDormant && showIdentityPulse && spriteRenderer != null)
            identityPulseRoutine = StartCoroutine(PlayEnemyIdentityPulse());
    }

    void OnDisable()
    {
        UnsubscribeFromHealthEvents();
        StopVisualEffects();
    }

    void OnDestroy()
    {
        UnsubscribeFromHealthEvents();
        StopVisualEffects();
    }

    public void PlayAttackVisual()
    {
        if (isDormant || (health != null && health.IsDead))
            return;

        if (attackVisualRoutine != null)
            StopCoroutine(attackVisualRoutine);

        attackVisualRoutine = StartCoroutine(PlayAttackVisualRoutine());
    }

    public void PrepareForSpawnPresentation()
    {
        if (spriteRenderer == null)
            return;

        StopVisualEffects();
        SetDormant(true);

        Color hiddenColor = spawnTint;
        hiddenColor.a = 0f;
        spriteRenderer.color = hiddenColor;
        spriteRenderer.transform.localScale = baseVisualScale * Mathf.Max(0.1f, spawnStartScale);
    }

    public IEnumerator PlaySpawnPresentation()
    {
        if (spriteRenderer == null)
        {
            SetDormant(false);
            yield break;
        }

        StopVisualEffects();

        bool restoreAnimator = animator != null && animator.enabled;
        if (animator != null)
            animator.enabled = false;

        Color startColor = spawnTint;
        startColor.a = 0f;
        Color targetColor = baseSpriteColor;
        Vector3 startScale = baseVisualScale * Mathf.Max(0.1f, spawnStartScale);
        Vector3 overshootScale = baseVisualScale * Mathf.Max(spawnStartScale, spawnOvershootScale);

        float elapsed = 0f;
        while (elapsed < spawnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / spawnDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            spriteRenderer.color = Color.Lerp(startColor, targetColor, eased);
            spriteRenderer.transform.localScale = t < 0.75f
                ? Vector3.Lerp(startScale, overshootScale, t / 0.75f)
                : Vector3.Lerp(overshootScale, baseVisualScale, (t - 0.75f) / 0.25f);

            yield return null;
        }

        spriteRenderer.color = targetColor;
        spriteRenderer.transform.localScale = baseVisualScale;

        if (animator != null)
            animator.enabled = restoreAnimator;

        SetDormant(false);
    }

    public void SetDormant(bool dormant)
    {
        isDormant = dormant;
        movementPauseTimer = 0f;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SetAnimatorMoving(false);

        if (contactDamage != null)
        {
            if (dormant)
            {
                contactDamage.enabled = false;
            }
            else if (restoreContactDamageOnWake)
            {
                contactDamage.enabled = true;
            }
        }

        if (bodyCollider != null)
            bodyCollider.enabled = !dormant;

        if (childCanvases != null)
        {
            for (int i = 0; i < childCanvases.Length; i++)
            {
                if (childCanvases[i] != null && childCanvases[i].gameObject != gameObject)
                    childCanvases[i].enabled = !dormant;
            }
        }

        if (dormant)
        {
            if (identityPulseRoutine != null)
            {
                StopCoroutine(identityPulseRoutine);
                identityPulseRoutine = null;
            }
        }
        else if (showIdentityPulse && spriteRenderer != null && identityPulseRoutine == null && !isPlayingAttackVisual)
        {
            identityPulseRoutine = StartCoroutine(PlayEnemyIdentityPulse());
        }
    }

    public void PauseMovement(float duration)
    {
        movementPauseTimer = Mathf.Max(movementPauseTimer, duration);

        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        SetAnimatorMoving(false);
    }

    private void StopVisualEffects()
    {
        if (identityPulseRoutine != null)
            StopCoroutine(identityPulseRoutine);
        identityPulseRoutine = null;

        if (attackVisualRoutine != null)
            StopCoroutine(attackVisualRoutine);
        attackVisualRoutine = null;
        isPlayingAttackVisual = false;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = baseSpriteColor;
            if (defaultSprite != null)
            {
                spriteRenderer.sprite = defaultSprite;
            }
        }
    }

    private IEnumerator PlayAttackVisualRoutine()
    {
        isPlayingAttackVisual = true;
        SetAnimatorMoving(false);

        if (identityPulseRoutine != null)
        {
            StopCoroutine(identityPulseRoutine);
            identityPulseRoutine = null;
        }

        if (disableAnimatorDuringAttack && animator != null)
            animator.enabled = false;

        if (attackFrames != null && attackFrames.Length > 0)
        {
            float frameTime = Mathf.Max(1f / Mathf.Max(1f, attackFrameRate), 0.02f);
            if (spriteRenderer != null)
                spriteRenderer.color = baseSpriteColor;

            for (int i = 0; i < attackFrames.Length; i++)
            {
                if (spriteRenderer == null || attackFrames[i] == null)
                    continue;

                spriteRenderer.sprite = attackFrames[i];
                yield return new WaitForSeconds(frameTime);
            }
        }
        else
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = identityTintColor;
                yield return new WaitForSeconds(fallbackAttackFlash);
                spriteRenderer.color = baseSpriteColor;
            }
        }

        if (spriteRenderer != null && defaultSprite != null)
            spriteRenderer.sprite = defaultSprite;

        if (returnToAnimatorAfterAttack && animator != null)
            animator.enabled = true;

        if (showIdentityPulse && spriteRenderer != null)
            identityPulseRoutine = StartCoroutine(PlayEnemyIdentityPulse());

        isPlayingAttackVisual = false;
        attackVisualRoutine = null;
    }

    private IEnumerator PlayEnemyIdentityPulse()
    {
        if (spriteRenderer == null)
            yield break;

        float timer = 0f;
        while (spriteRenderer != null && showIdentityPulse && !isPlayingAttackVisual)
        {
            timer += Time.deltaTime * identityPulseSpeed;
            float pulse = (Mathf.Sin(timer) + 1f) * 0.5f;
            spriteRenderer.color = Color.Lerp(baseSpriteColor, identityTintColor, pulse * identityPulseIntensity);
            yield return null;
        }
    }

    void Start()
    {
        if (player == null)
        {
            PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (playerMovement != null)
                player = playerMovement.transform;
        }

        PickNewRandomOffset();
    }

    void FixedUpdate()
    {
        if (rb != null)
            rb.angularVelocity = 0f;

        TryHandleFallDeath();

        if (health != null && health.IsDead)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            SetAnimatorMoving(false);
            return;
        }

        if (isDormant)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            SetAnimatorMoving(false);
            return;
        }

        if (movementPauseTimer > 0f)
        {
            movementPauseTimer = Mathf.Max(0f, movementPauseTimer - Time.fixedDeltaTime);
            if (rb != null)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            SetAnimatorMoving(false);
            if (player != null)
                UpdateFacingVisual(0f, player.position.x - rb.position.x);
            return;
        }

        if (player == null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetAnimatorMoving(false);
            return;
        }

        if (!HasActivatedByProximity())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetAnimatorMoving(false);
            return;
        }

        randomOffsetTimer -= Time.fixedDeltaTime;
        if (randomOffsetTimer <= 0f)
            PickNewRandomOffset();

        float rawDeltaX = player.position.x - rb.position.x;
        float distToPlayer = Mathf.Abs(rawDeltaX);

        if (retreatDistance > 0f && distToPlayer < retreatDistance)
        {
            // Too close — back away from the player.
            float retreatDir = rawDeltaX != 0f ? -Mathf.Sign(rawDeltaX) : 1f;
            float newVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, retreatDir * moveSpeed, acceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);
            UpdateFacingVisual(newVelocityX, rawDeltaX);
        }
        else
        {
            float targetX = player.position.x + randomOffsetX;
            float xDistance = targetX - rb.position.x;
            MoveHorizontally(xDistance);
        }

        SetAnimatorMoving(Mathf.Abs(rb.linearVelocity.x) > 0.05f);
        ClampToScreen();
    }

    private void TryHandleFallDeath()
    {
        if (!enableFallDeath || health == null || health.IsDead || transform.position.y > fallDeathY)
            return;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        health.TakeDamage(Mathf.Max(1f, health.CurrentHealth), Vector2.down, 0f);
    }

    void MoveHorizontally(float xDistance)
    {
        float targetSpeedX = Mathf.Abs(xDistance) <= stopDistance ? 0f : Mathf.Sign(xDistance) * moveSpeed;
        float newVelocityX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeedX, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);
        UpdateFacingVisual(targetSpeedX, xDistance);
    }

    void UpdateFacingVisual(float horizontalSpeed, float xDistance)
    {
        if (spriteRenderer == null)
            return;

        if (Mathf.Abs(horizontalSpeed) <= 0.01f)
        {
            if (player != null)
                ApplyFacing((player.position.x - rb.position.x) < 0f);
            return;
        }

        if (Mathf.Abs(xDistance) > 0.01f)
            ApplyFacing(xDistance < 0f);
        else
            ApplyFacing(horizontalSpeed < 0f);
    }

    private void ApplyFacing(bool faceLeft)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.flipX = spriteFacesRight ? faceLeft : !faceLeft;
    }

    void ClampToScreen()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Vector3 viewport = mainCamera.WorldToViewportPoint(rb.position);
        float clampedX = Mathf.Clamp(viewport.x, screenPadding, 1f - screenPadding);

        if (Mathf.Approximately(clampedX, viewport.x))
            return;

        float camDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 clampedWorld = mainCamera.ViewportToWorldPoint(new Vector3(clampedX, viewport.y, camDistance));
        rb.position = new Vector2(clampedWorld.x, rb.position.y);

        Vector2 v = rb.linearVelocity;
        v.x = 0f;
        rb.linearVelocity = v;
    }

    void PickNewRandomOffset()
    {
        randomOffsetX = Random.Range(-randomOffsetRadius, randomOffsetRadius);
        randomOffsetTimer = randomOffsetUpdateTime;
    }

    private bool HasActivatedByProximity()
    {
        if (hasActivatedByProximity || activationDistance <= 0f)
            return true;

        if (player == null)
            return false;

        if (Vector2.Distance(rb.position, player.position) <= activationDistance)
            hasActivatedByProximity = true;

        return hasActivatedByProximity;
    }

    private void SubscribeToHealthEvents()
    {
        if (health != null)
            health.OnDied += HandleDied;
    }

    private void UnsubscribeFromHealthEvents()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (identityPulseRoutine != null)
        {
            StopCoroutine(identityPulseRoutine);
            identityPulseRoutine = null;
        }

        if (attackVisualRoutine != null)
        {
            StopCoroutine(attackVisualRoutine);
            attackVisualRoutine = null;
        }

        isPlayingAttackVisual = false;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (contactDamage != null)
            contactDamage.enabled = false;

        if (spriteRenderer != null)
            spriteRenderer.color = baseSpriteColor;

        if (animator != null)
        {
            if (!animator.enabled)
                animator.enabled = true;

            SetAnimatorMoving(false);
            if (animatorHasDeadParameter)
                animator.SetBool(DeadParameterHash, true);

            animator.Play(DeathStateHash, 0, 0f);
            animator.Update(0f);
        }
    }

    private void CacheAnimatorParameterSupport()
    {
        animatorHasMoveParameter = HasBoolParameter(animator, MoveParameterName);
        animatorHasDeadParameter = HasBoolParameter(animator, DeadParameterName);
    }

    private void SetAnimatorMoving(bool isMoving)
    {
        if (animator != null && animatorHasMoveParameter)
            animator.SetBool(MoveParameterHash, isMoving);
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

#if UNITY_EDITOR
    void Reset()
    {
        EnsureBodyColliderInEditor();
    }

    void OnValidate()
    {
        EnsureBodyColliderInEditor();
    }

    private void EnsureBodyColliderInEditor()
    {
        if (Application.isPlaying)
            return;

        BoxCollider2D rootCollider = GetComponent<BoxCollider2D>();
        if (rootCollider == null)
        {
            rootCollider = gameObject.AddComponent<BoxCollider2D>();
            Debug.LogWarning("EnemyRandomFollower: Restored a missing root BoxCollider2D body collider in the editor.", this);
        }

        if (rootCollider.size.sqrMagnitude <= 0.0001f)
            ApplyFallbackColliderShape(rootCollider);
    }
#endif
}
