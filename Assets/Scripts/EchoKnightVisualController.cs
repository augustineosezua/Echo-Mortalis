using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EchoPurgatoryController))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(KnightSpriteAnimator))]
public class EchoKnightVisualController : MonoBehaviour
{
    [SerializeField] private KnightSpriteAnimator knightAnimator;
    [SerializeField] private EchoPurgatoryController echoController;
    [SerializeField] private Health health;
    [SerializeField] private float hurtHoldDuration = 0.2f;

    private float hurtUntil = float.NegativeInfinity;
    private float lastKnownHealth;
    private int lastAttackToken = -1;
    private KnightAnimationClipId lastClip = KnightAnimationClipId.None;

    void Reset()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    void Awake()
    {
        ResolveReferences();
        if (health != null)
            lastKnownHealth = health.MaxHealth > 0f ? health.MaxHealth : 0f;
    }

    void OnEnable()
    {
        ResolveReferences();

        if (health != null)
        {
            health.OnHealthChanged += HandleHealthChanged;
            health.OnDied += HandleDied;
            lastKnownHealth = health.CurrentHealth > 0f ? health.CurrentHealth : health.MaxHealth;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDied -= HandleDied;
        }
    }

    void Update()
    {
        if (knightAnimator == null)
            return;

        bool restartClip = false;
        KnightAnimationClipId requestedClip = ResolveRequestedClip(ref restartClip);

        if (requestedClip == KnightAnimationClipId.None)
            requestedClip = KnightAnimationClipId.Idle;

        if (requestedClip != lastClip)
        {
            restartClip = true;
            lastClip = requestedClip;
        }

        knightAnimator.Play(requestedClip, restartClip);
    }

    private void ResolveReferences()
    {
        if (knightAnimator == null)
            knightAnimator = GetComponent<KnightSpriteAnimator>();

        if (echoController == null)
            echoController = GetComponent<EchoPurgatoryController>();

        if (health == null)
            health = GetComponent<Health>();
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        float previousHealth = lastKnownHealth > 0f ? lastKnownHealth : maxHealth;
        lastKnownHealth = currentHealth;

        if (currentHealth <= 0f || currentHealth >= previousHealth || knightAnimator == null)
            return;

        float clipDuration = knightAnimator.GetClipDuration(KnightAnimationClipId.Hit);
        hurtUntil = Time.time + Mathf.Max(hurtHoldDuration, clipDuration);
    }

    private void HandleDied()
    {
        lastClip = KnightAnimationClipId.None;
    }

    private KnightAnimationClipId ResolveRequestedClip(ref bool restartClip)
    {
        if (health != null && health.IsDead)
            return KnightAnimationClipId.Death;

        if (Time.time < hurtUntil)
            return KnightAnimationClipId.Hit;

        if (echoController != null && echoController.CurrentAttackToken != lastAttackToken && echoController.IsAttacking)
        {
            lastAttackToken = echoController.CurrentAttackToken;
            restartClip = true;
        }

        if (echoController != null && echoController.IsAttacking)
        {
            bool aggressiveMotion = Mathf.Abs(echoController.Velocity.x) > 0.8f || echoController.IsDrivingAttackMovement;
            return aggressiveMotion ? KnightAnimationClipId.Attack : KnightAnimationClipId.AttackNoMovement;
        }

        if (echoController != null && Mathf.Abs(echoController.Velocity.x) > 0.08f)
            return KnightAnimationClipId.Run;

        return KnightAnimationClipId.Idle;
    }
}
