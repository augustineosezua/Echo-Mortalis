using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class EnemyGroundSpriteAnimator : MonoBehaviour, IEnemyAttackVisual
{
    private enum VisualState
    {
        Idle,
        Walk,
        Attack,
        Hurt,
        Death
    }

    [Header("Animation")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private float idleFramesPerSecond = 6f;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private float walkFramesPerSecond = 10f;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private float attackFramesPerSecond = 14f;
    [SerializeField] private Sprite[] alternateAttackFrames;
    [SerializeField] private float alternateAttackFramesPerSecond = 14f;
    [SerializeField] private Sprite[] hurtFrames;
    [SerializeField] private float hurtFramesPerSecond = 14f;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private float deathFramesPerSecond = 8f;
    [SerializeField] private float movementAnimationThreshold = 0.05f;
    [SerializeField] private float hurtHoldDuration = 0.18f;
    [SerializeField] private bool anchorFeetToRoot = true;
    [SerializeField] private float groundedPivotOffset = 0f;

    [Header("Attack Visual")]
    [SerializeField] private Color attackTint = new Color(1f, 0.3f, 0.26f, 1f);
    [SerializeField] private float attackFlashDuration = 0.16f;
    [SerializeField] private float attackScalePulse = 1.06f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Health health;
    private Color baseColor = Color.white;
    private Vector3 baseScale = Vector3.one;
    private Vector3 baseLocalPosition = Vector3.zero;
    private Sprite fallbackSprite;
    private Coroutine attackVisualRoutine;
    private float attackAnimationUntil = float.NegativeInfinity;
    private float hurtAnimationUntil = float.NegativeInfinity;
    private float lastKnownHealth;
    private float movementAnimationThresholdSqr;
    private float animationElapsed;
    private VisualState visualState;
    private bool playAlternateAttackNext;
    private Sprite[] currentAttackFrames;
    private float currentAttackFramesPerSecond;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = FindPrimarySpriteRenderer();

        health = GetComponent<Health>();

        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
            baseScale = spriteRenderer.transform.localScale;
            baseLocalPosition = spriteRenderer.transform.localPosition;
            fallbackSprite = spriteRenderer.sprite;
        }

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

        attackAnimationUntil = float.NegativeInfinity;
        hurtAnimationUntil = float.NegativeInfinity;
        visualState = health != null && health.IsDead ? VisualState.Death : VisualState.Idle;
        animationElapsed = 0f;
        currentAttackFrames = attackFrames;
        currentAttackFramesPerSecond = attackFramesPerSecond;
        playAlternateAttackNext = false;
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
        if (health != null && health.MaxHealth > 0f)
            lastKnownHealth = health.CurrentHealth > 0f ? health.CurrentHealth : health.MaxHealth;
    }

    void Update()
    {
        UpdateAnimation();
    }

    public void PlayAttackVisual()
    {
        if (!isActiveAndEnabled || spriteRenderer == null || (health != null && health.IsDead))
            return;

        SelectAttackFramesForNextAttack(out currentAttackFrames, out currentAttackFramesPerSecond);
        attackAnimationUntil = Time.time + GetAnimationDuration(currentAttackFrames, currentAttackFramesPerSecond);
        if (visualState == VisualState.Attack)
            animationElapsed = 0f;

        if (attackVisualRoutine != null)
            StopCoroutine(attackVisualRoutine);

        attackVisualRoutine = StartCoroutine(PlayAttackFlashRoutine());
    }

    private IEnumerator PlayAttackFlashRoutine()
    {
        float elapsed = 0f;
        Vector3 pulseScale = baseScale * Mathf.Max(1f, attackScalePulse);

        while (elapsed < attackFlashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, attackFlashDuration));
            float eased = 1f - Mathf.Abs((t * 2f) - 1f);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(baseColor, attackTint, eased);
                spriteRenderer.transform.localScale = Vector3.Lerp(baseScale, pulseScale, eased);
            }

            yield return null;
        }

        ResetAttackVisual();
        attackVisualRoutine = null;
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

        if (rb != null && new Vector2(rb.linearVelocity.x, 0f).sqrMagnitude > movementAnimationThresholdSqr && HasFrames(walkFrames))
            return VisualState.Walk;

        return VisualState.Idle;
    }

    private void ApplyAnimationFrame(VisualState state, float elapsed)
    {
        Sprite[] frames = GetFramesForState(state);
        if (!HasFrames(frames))
        {
            if (fallbackSprite != null)
            {
                spriteRenderer.sprite = fallbackSprite;
                ApplyGroundedAnchor(fallbackSprite);
            }
            return;
        }

        float framesPerSecond = Mathf.Max(0.01f, GetFrameRateForState(state));
        int frameIndex = Mathf.FloorToInt(elapsed * framesPerSecond);
        if (IsLoopingState(state))
            frameIndex %= frames.Length;
        else
            frameIndex = Mathf.Min(frameIndex, frames.Length - 1);

        Sprite nextFrame = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        spriteRenderer.sprite = nextFrame;
        ApplyGroundedAnchor(nextFrame);
    }

    private Sprite[] GetFramesForState(VisualState state)
    {
        switch (state)
        {
            case VisualState.Walk:
                return walkFrames;
            case VisualState.Attack:
                return HasFrames(currentAttackFrames) ? currentAttackFrames : attackFrames;
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
            case VisualState.Walk:
                return walkFramesPerSecond;
            case VisualState.Attack:
                return HasFrames(currentAttackFrames) ? currentAttackFramesPerSecond : attackFramesPerSecond;
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
        return state == VisualState.Idle || state == VisualState.Walk;
    }

    private float GetAnimationDuration(Sprite[] frames, float framesPerSecond)
    {
        if (!HasFrames(frames))
            return 0f;

        return frames.Length / Mathf.Max(0.01f, framesPerSecond);
    }

    private void SelectAttackFramesForNextAttack(out Sprite[] frames, out float framesPerSecond)
    {
        bool hasPrimary = HasFrames(attackFrames);
        bool hasAlternate = HasFrames(alternateAttackFrames);

        if (!hasPrimary && hasAlternate)
        {
            frames = alternateAttackFrames;
            framesPerSecond = alternateAttackFramesPerSecond;
            return;
        }

        if (hasPrimary && hasAlternate)
        {
            bool useAlternate = playAlternateAttackNext;
            frames = useAlternate ? alternateAttackFrames : attackFrames;
            framesPerSecond = useAlternate ? alternateAttackFramesPerSecond : attackFramesPerSecond;
            playAlternateAttackNext = !playAlternateAttackNext;
            return;
        }

        frames = attackFrames;
        framesPerSecond = attackFramesPerSecond;
    }

    private static bool HasFrames(Sprite[] frames)
    {
        return frames != null && frames.Length > 0;
    }

    private void ApplyGroundedAnchor(Sprite sprite)
    {
        if (!anchorFeetToRoot || spriteRenderer == null || sprite == null)
            return;

        float scaleY = Mathf.Abs(spriteRenderer.transform.localScale.y);
        float anchoredY = baseLocalPosition.y + (sprite.bounds.extents.y * scaleY) + groundedPivotOffset;
        spriteRenderer.transform.localPosition = new Vector3(baseLocalPosition.x, anchoredY, baseLocalPosition.z);
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
}
