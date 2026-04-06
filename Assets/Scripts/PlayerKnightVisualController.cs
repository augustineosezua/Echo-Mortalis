using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerWeaponController))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(KnightSpriteAnimator))]
public class PlayerKnightVisualController : MonoBehaviour
{
    [SerializeField] private KnightSpriteAnimator knightAnimator;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerWeaponController playerWeaponController;
    [SerializeField] private Health health;
    [SerializeField] private float hurtHoldDuration = 0.18f;

    private float hurtUntil = float.NegativeInfinity;
    private float lastKnownHealth;
    private int lastAttackToken = -1;
    private int lastTurnToken = -1;
    private int lastCrouchTransitionToken = -1;
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

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerWeaponController == null)
            playerWeaponController = GetComponent<PlayerWeaponController>();

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
        {
            bool movingDeath = playerMovement != null &&
                (Mathf.Abs(playerMovement.HorizontalSpeed) > 1f || Mathf.Abs(playerMovement.VerticalSpeed) > 1f);
            return movingDeath ? KnightAnimationClipId.Death : KnightAnimationClipId.DeathNoMovement;
        }

        if (Time.time < hurtUntil)
        {
            if (lastClip != KnightAnimationClipId.Hit)
                restartClip = true;

            return KnightAnimationClipId.Hit;
        }

        if (playerWeaponController != null && playerWeaponController.IsAttacking)
        {
            if (playerWeaponController.CurrentAttackToken != lastAttackToken)
            {
                lastAttackToken = playerWeaponController.CurrentAttackToken;
                restartClip = true;
            }

            return ResolveAttackClip(playerWeaponController.CurrentAttackVisual);
        }

        if (playerMovement == null)
            return KnightAnimationClipId.Idle;

        if (playerMovement.IsTurningAround)
        {
            if (playerMovement.TurnAnimationToken != lastTurnToken)
            {
                lastTurnToken = playerMovement.TurnAnimationToken;
                restartClip = true;
            }

            return KnightAnimationClipId.TurnAround;
        }

        if (playerMovement.IsGroundSliding)
            return KnightAnimationClipId.SlideAll;

        if (playerMovement.IsAirDashing)
            return KnightAnimationClipId.Dash;

        if (playerMovement.IsWallClimbing)
            return Mathf.Abs(playerMovement.VerticalInput) > 0.15f
                ? KnightAnimationClipId.WallClimb
                : KnightAnimationClipId.WallClimbNoMovement;

        if (playerMovement.IsWallHanging)
            return KnightAnimationClipId.WallHang;

        if (playerMovement.IsWallSliding)
            return KnightAnimationClipId.WallSlide;

        if (playerMovement.IsInCrouchTransition)
        {
            if (playerMovement.CrouchTransitionToken != lastCrouchTransitionToken)
            {
                lastCrouchTransitionToken = playerMovement.CrouchTransitionToken;
                restartClip = true;
            }

            return KnightAnimationClipId.CrouchTransition;
        }

        if (playerMovement.IsCrouching)
            return playerMovement.IsCrouchWalking ? KnightAnimationClipId.CrouchWalk : KnightAnimationClipId.Crouch;

        if (!playerMovement.IsGrounded)
        {
            if (playerMovement.VerticalSpeed > 0.2f)
                return KnightAnimationClipId.Jump;

            if (playerMovement.VerticalSpeed > -0.2f)
                return KnightAnimationClipId.JumpFallInbetween;

            return KnightAnimationClipId.Fall;
        }

        if (playerMovement.IsMovingHorizontally)
            return KnightAnimationClipId.Run;

        return KnightAnimationClipId.Idle;
    }

    private static KnightAnimationClipId ResolveAttackClip(PlayerWeaponController.KnightAttackVisualType attackVisual)
    {
        switch (attackVisual)
        {
            case PlayerWeaponController.KnightAttackVisualType.Attack:
                return KnightAnimationClipId.Attack;
            case PlayerWeaponController.KnightAttackVisualType.Attack2:
                return KnightAnimationClipId.Attack2;
            case PlayerWeaponController.KnightAttackVisualType.AttackNoMovement:
                return KnightAnimationClipId.AttackNoMovement;
            case PlayerWeaponController.KnightAttackVisualType.Attack2NoMovement:
                return KnightAnimationClipId.Attack2NoMovement;
            case PlayerWeaponController.KnightAttackVisualType.AttackCombo:
                return KnightAnimationClipId.AttackCombo;
            case PlayerWeaponController.KnightAttackVisualType.AttackComboNoMovement:
                return KnightAnimationClipId.AttackComboNoMovement;
            case PlayerWeaponController.KnightAttackVisualType.CrouchAttack:
                return KnightAnimationClipId.CrouchAttack;
            default:
                return KnightAnimationClipId.AttackNoMovement;
        }
    }
}
