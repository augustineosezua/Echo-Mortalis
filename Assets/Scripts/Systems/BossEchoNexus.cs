using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Health))]
public class BossEchoNexus : MonoBehaviour
{
    public enum EncounterState
    {
        Idle,
        Intro,
        Phase1,
        Transition,
        Phase2,
        Death
    }

    private enum VisualState
    {
        Idle,
        Walk,
        Attack,
        Cast,
        Spell,
        Hurt,
        Death
    }

    private enum AttackPattern
    {
        None,
        Dash,
        Spell
    }

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private bool spriteFacesRight = false;
    [SerializeField] private Color introTint = new Color(0.72f, 1f, 0.88f, 1f);
    [SerializeField] private Color dashTelegraphTint = new Color(1f, 0.58f, 0.3f, 1f);
    [SerializeField] private Color spellTelegraphTint = new Color(0.34f, 0.96f, 0.82f, 1f);
    [SerializeField] private Color phaseTwoTint = new Color(0.56f, 1f, 0.78f, 1f);
    [SerializeField, Range(0f, 1f)] private float phaseTwoTintStrength = 0.42f;
    [SerializeField] private float introScalePulse = 1.08f;
    [SerializeField] private float telegraphScalePulse = 1.06f;
    [SerializeField] private Vector2 spellStrikeScale = new Vector2(13f, 13f);
    [SerializeField] private float spellStrikeRadius = 1.05f;
    [SerializeField] private float spellStrikeGroundOffset = 0.04f;

    [Header("References")]
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private WeaponHitbox dashHitbox;
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Sprite projectileSprite;
    [SerializeField] private Vector2 projectileScale = new Vector2(0.5f, 0.5f);
    [SerializeField] private float projectileColliderRadius = 0.36f;
    [SerializeField] private LayerMask projectileTargetLayers;

    [Header("Encounter")]
    [SerializeField] private float initialAttackDelay = 1f;
    [SerializeField] private float introDuration = 0.95f;
    [SerializeField] private float phaseTransitionDuration = 1.1f;
    [SerializeField, Range(0.1f, 0.9f)] private float phaseTwoHealthFraction = 0.5f;
    [SerializeField] private float movementAnimationThreshold = 0.05f;
    [SerializeField] private float hurtHoldDuration = 0.18f;

    [Header("Tactics")]
    [SerializeField, Range(0f, 1f)] private float phase1SpellWeight = 0.28f;
    [SerializeField, Range(0f, 1f)] private float phase2SpellWeight = 0.52f;
    [SerializeField, Range(0.1f, 1f)] private float repeatAttackWeightPenalty = 0.4f;
    [SerializeField, Min(1)] private int maxConsecutiveSameAttack = 2;
    [SerializeField, Range(0f, 1f)] private float phase1EvadeChance = 0.45f;
    [SerializeField, Range(0f, 1f)] private float phase2EvadeChance = 0.75f;
    [SerializeField] private Vector2 phase1EvadeDurationRange = new Vector2(0.14f, 0.28f);
    [SerializeField] private Vector2 phase2EvadeDurationRange = new Vector2(0.22f, 0.44f);
    [SerializeField] private Vector2 phase1EvadeSpeedMultiplierRange = new Vector2(0.95f, 1.18f);
    [SerializeField] private Vector2 phase2EvadeSpeedMultiplierRange = new Vector2(1.08f, 1.35f);
    [SerializeField, Range(0f, 1f)] private float phase1CrossThroughChance = 0.24f;
    [SerializeField, Range(0f, 1f)] private float phase2CrossThroughChance = 0.44f;

    [Header("Phases")]
    [SerializeField] private BossPhaseData phase1 = new BossPhaseData();
    [SerializeField] private BossPhaseData phase2 = new BossPhaseData
    {
        moveSpeed = 4.1f,
        acceleration = 22f,
        preferredDistance = 4f,
        distanceTolerance = 0.6f,
        dashTriggerRange = 6.4f,
        dashCooldown = 1.55f,
        dashWindup = 0.32f,
        dashSpeed = 16f,
        dashDuration = 0.24f,
        dashRepeatCount = 2,
        dashRepeatDelay = 0.18f,
        dashRecovery = 0.28f,
        dashDamage = 22f,
        dashKnockback = 7f,
        dashHitboxScale = 2.05f,
        dashOverlapRadius = 0.42f,
        projectileMinRange = 2.6f,
        projectileMaxRange = 15f,
        projectileCooldown = 2.1f,
        projectileWindup = 0.46f,
        projectileRecovery = 0.22f,
        projectileBurstCount = 2,
        projectileBurstInterval = 0.18f,
        projectileCount = 7,
        projectileSpreadAngle = 68f,
        projectileSpeed = 9.7f,
        projectileDamage = 12f,
        projectileLifetime = 3.2f,
        projectileKnockback = 5.6f,
        projectileColor = new Color(0.22f, 0.98f, 0.78f, 1f),
        postAttackDecisionDelay = 0.08f
    };

    [Header("Animation")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private float idleFramesPerSecond = 6f;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private float walkFramesPerSecond = 10f;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private float attackFramesPerSecond = 14f;
    [SerializeField] private Sprite[] castFrames;
    [SerializeField] private float castFramesPerSecond = 12f;
    [SerializeField] private Sprite[] spellFrames;
    [SerializeField] private float spellFramesPerSecond = 14f;
    [SerializeField] private Sprite[] hurtFrames;
    [SerializeField] private float hurtFramesPerSecond = 12f;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private float deathFramesPerSecond = 10f;

    public float IntroDuration => introDuration;
    public Health HealthComponent => health;
    public EncounterState State => state;

    public event Action<BossEchoNexus> EncounterStarted;
    public event Action<BossEchoNexus> PhaseTwoStarted;
    public event Action<BossEchoNexus> BossDefeated;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Health health;
    private Transform player;
    private Health playerHealth;
    private Coroutine stateRoutine;
    private Color baseColor = Color.white;
    private Vector3 baseVisualScale = Vector3.one;
    private Vector3 dashHitboxHomeLocalPosition;
    private Vector3 projectileSpawnHomeLocalPosition;
    private float dashCooldownTimer;
    private float projectileCooldownTimer;
    private float actionRecoveryTimer;
    private float hurtUntil;
    private float lastKnownHealth;
    private float movementAnimationThresholdSqr;
    private float animationElapsed;
    private int facingDirection = -1;
    private bool encounterRequested;
    private bool encounterActive;
    private bool isPerformingAttack;
    private bool isDrivingAttackMovement;
    private bool phaseTwoUnlocked;
    private bool isDead;
    private bool hasForcedVisualState;
    private VisualState forcedVisualState;
    private VisualState visualState;
    private EncounterState state;
    private AttackPattern lastAttackPattern;
    private int consecutiveAttackRepeatCount;
    private GameObject activeSpellStrikeObject;
    private SpriteRenderer activeSpellStrikeRenderer;

    void Reset()
    {
        ResolveReferences();
        EnsureDefaultLayers();
    }

    void OnValidate()
    {
        ResolveReferences();
        EnsureDefaultLayers();

        phase1 ??= new BossPhaseData();
        phase2 ??= new BossPhaseData();
        phase1.ClampValues();
        phase2.ClampValues();

        introDuration = Mathf.Max(0f, introDuration);
        phaseTransitionDuration = Mathf.Max(0f, phaseTransitionDuration);
        initialAttackDelay = Mathf.Max(0f, initialAttackDelay);
        movementAnimationThreshold = Mathf.Max(0f, movementAnimationThreshold);
        hurtHoldDuration = Mathf.Max(0f, hurtHoldDuration);
        phaseTwoHealthFraction = Mathf.Clamp(phaseTwoHealthFraction, 0.1f, 0.9f);
        phaseTwoTintStrength = Mathf.Clamp01(phaseTwoTintStrength);
        projectileScale.x = Mathf.Max(0.05f, projectileScale.x);
        projectileScale.y = Mathf.Max(0.05f, projectileScale.y);
        projectileColliderRadius = Mathf.Max(0.05f, projectileColliderRadius);
        spellStrikeScale.x = Mathf.Max(0.05f, spellStrikeScale.x);
        spellStrikeScale.y = Mathf.Max(0.05f, spellStrikeScale.y);
        spellStrikeRadius = Mathf.Max(0.05f, spellStrikeRadius);
        phase1SpellWeight = Mathf.Clamp01(phase1SpellWeight);
        phase2SpellWeight = Mathf.Clamp01(phase2SpellWeight);
        repeatAttackWeightPenalty = Mathf.Clamp(repeatAttackWeightPenalty, 0.1f, 1f);
        maxConsecutiveSameAttack = Mathf.Max(1, maxConsecutiveSameAttack);
        phase1EvadeChance = Mathf.Clamp01(phase1EvadeChance);
        phase2EvadeChance = Mathf.Clamp01(phase2EvadeChance);
        phase1CrossThroughChance = Mathf.Clamp01(phase1CrossThroughChance);
        phase2CrossThroughChance = Mathf.Clamp01(phase2CrossThroughChance);
        phase1EvadeDurationRange = ClampRange(phase1EvadeDurationRange, 0f);
        phase2EvadeDurationRange = ClampRange(phase2EvadeDurationRange, 0f);
        phase1EvadeSpeedMultiplierRange = ClampRange(phase1EvadeSpeedMultiplierRange, 0f);
        phase2EvadeSpeedMultiplierRange = ClampRange(phase2EvadeSpeedMultiplierRange, 0f);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        health = GetComponent<Health>();

        ResolveReferences();
        EnsureDefaultLayers();

        if (rb != null)
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;

        if (visualRoot == null && visualRenderer != null)
            visualRoot = visualRenderer.transform;

        if (visualRenderer != null)
        {
            baseColor = visualRenderer.color;
            if (baseColor.a <= 0.001f)
            {
                baseColor.a = 1f;
                visualRenderer.color = baseColor;
            }

            if (visualRoot != null)
                baseVisualScale = visualRoot.localScale;

            if (visualRenderer.sprite == null && HasFrames(idleFrames))
                visualRenderer.sprite = idleFrames[0];
        }

        if (dashHitbox != null)
        {
            dashHitboxHomeLocalPosition = dashHitbox.transform.localPosition;
            dashHitbox.SetOwner(transform);
            dashHitbox.EndSwing();
        }

        if (projectileSpawnPoint != null)
            projectileSpawnHomeLocalPosition = projectileSpawnPoint.localPosition;

        movementAnimationThresholdSqr = movementAnimationThreshold * movementAnimationThreshold;
        lastKnownHealth = health != null && health.MaxHealth > 0f
            ? (health.CurrentHealth > 0f ? health.CurrentHealth : health.MaxHealth)
            : 0f;

        state = EncounterState.Idle;
        visualState = VisualState.Idle;
        RefreshFacingVisual();
        ApplyAnimationFrame(visualState, 0f);
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.OnHealthChanged += HandleHealthChanged;
            health.OnDied += HandleDied;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDied -= HandleDied;
        }

        CancelCombatState();
    }

    void Update()
    {
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;
        if (projectileCooldownTimer > 0f)
            projectileCooldownTimer -= Time.deltaTime;
        if (actionRecoveryTimer > 0f)
            actionRecoveryTimer -= Time.deltaTime;

        ResolvePlayer();

        if (!isDead && state == EncounterState.Phase1 && !phaseTwoUnlocked && ShouldTriggerPhaseTwo())
            StartPhaseTwoTransition();

        if (ShouldChooseAttack())
            TryStartAttack(GetActivePhaseData());

        if (!isDrivingAttackMovement)
            FacePlayer();

        UpdateAnimation();
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        if (isDead || state == EncounterState.Death)
        {
            StopHorizontalMotion();
            return;
        }

        if (!encounterActive || !HasCombatTarget())
        {
            if (!isDrivingAttackMovement)
                StopHorizontalMotion();
            return;
        }

        if (isPerformingAttack)
        {
            if (!isDrivingAttackMovement)
                StopHorizontalMotion();
            return;
        }

        MoveTowardPreferredDistance(GetActivePhaseData());
    }

    public void BeginEncounter()
    {
        if (encounterRequested || isDead)
            return;

        encounterRequested = true;
        if (stateRoutine != null)
            StopCoroutine(stateRoutine);

        stateRoutine = StartCoroutine(BeginEncounterRoutine());
    }

    private IEnumerator BeginEncounterRoutine()
    {
        state = EncounterState.Intro;
        encounterActive = false;
        isPerformingAttack = false;
        isDrivingAttackMovement = false;
        StopHorizontalMotion();
        SetForcedVisualState(VisualState.Cast);

        yield return PlayTelegraphRoutine(introDuration, introTint, introScalePulse);

        ClearForcedVisualState();
        state = EncounterState.Phase1;
        encounterActive = true;
        dashCooldownTimer = initialAttackDelay;
        projectileCooldownTimer = initialAttackDelay * 0.65f;
        actionRecoveryTimer = 0f;
        lastAttackPattern = AttackPattern.None;
        consecutiveAttackRepeatCount = 0;
        EncounterStarted?.Invoke(this);
        stateRoutine = null;
    }

    private IEnumerator DashAttackRoutine(BossPhaseData activePhase)
    {
        dashCooldownTimer = activePhase.dashCooldown;
        isPerformingAttack = true;
        SetForcedVisualState(VisualState.Attack);

        for (int repeatIndex = 0; repeatIndex < Mathf.Max(1, activePhase.dashRepeatCount); repeatIndex++)
        {
            FacePlayer();
            yield return PlayTelegraphRoutine(activePhase.dashWindup, dashTelegraphTint, telegraphScalePulse);

            if (!CanContinueAttack())
                break;

            BeginDashStrike(activePhase);

            float elapsed = 0f;
            while (elapsed < activePhase.dashDuration)
            {
                if (!CanContinueAttack())
                    break;

                elapsed += Time.fixedDeltaTime;
                if (rb != null)
                    rb.linearVelocity = new Vector2(facingDirection * activePhase.dashSpeed, rb.linearVelocity.y);

                yield return new WaitForFixedUpdate();
            }

            EndDashStrike();

            float recovery = repeatIndex < activePhase.dashRepeatCount - 1
                ? activePhase.dashRepeatDelay
                : activePhase.dashRecovery;

            if (recovery > 0f)
                yield return new WaitForSeconds(recovery);
        }

        EndDashStrike();
        ClearForcedVisualState();
        yield return PerformEvasiveRepositionRoutine(activePhase);
        isPerformingAttack = false;
        actionRecoveryTimer = activePhase.postAttackDecisionDelay;
        stateRoutine = null;
    }

    private IEnumerator SpellAttackRoutine(BossPhaseData activePhase)
    {
        projectileCooldownTimer = activePhase.projectileCooldown;
        isPerformingAttack = true;
        StopHorizontalMotion();
        FacePlayer();
        SetForcedVisualState(VisualState.Cast);

        Vector2 strikePosition = ResolveSpellStrikePosition();
        CreateSpellStrikeVisual(strikePosition);
        yield return PlayTelegraphedSpellStrikeRoutine(activePhase, strikePosition);

        DestroyActiveSpellStrike();

        ClearForcedVisualState();
        ResetVisualTint();
        yield return PerformEvasiveRepositionRoutine(activePhase);
        isPerformingAttack = false;
        actionRecoveryTimer = activePhase.postAttackDecisionDelay;
        stateRoutine = null;
    }

    private void StartPhaseTwoTransition()
    {
        if (phaseTwoUnlocked || isDead || stateRoutine != null)
            return;

        phaseTwoUnlocked = true;
        if (stateRoutine != null)
            StopCoroutine(stateRoutine);

        EndDashStrike();
        stateRoutine = StartCoroutine(PhaseTwoTransitionRoutine());
    }

    private IEnumerator PhaseTwoTransitionRoutine()
    {
        state = EncounterState.Transition;
        encounterActive = false;
        isPerformingAttack = false;
        StopHorizontalMotion();
        AudioManager.TryPlaySfx("boss_phase_shift");
        CenterScreenMessageUI.Show("The nexus fractures.", 0.12f, 0.9f, 0.2f);

        SetForcedVisualState(VisualState.Cast);
        yield return PlayTelegraphRoutine(phaseTransitionDuration, spellTelegraphTint, introScalePulse * 1.03f);
        ClearForcedVisualState();

        state = EncounterState.Phase2;
        encounterActive = true;
        ResetVisualTint();
        dashCooldownTimer = Mathf.Min(dashCooldownTimer, 0.45f);
        projectileCooldownTimer = Mathf.Min(projectileCooldownTimer, 0.3f);
        actionRecoveryTimer = 0.12f;
        lastAttackPattern = AttackPattern.None;
        consecutiveAttackRepeatCount = 0;
        PhaseTwoStarted?.Invoke(this);
        stateRoutine = null;
    }

    private bool TryStartAttack(BossPhaseData activePhase)
    {
        if (activePhase == null)
            return false;

        float horizontalDistance = GetHorizontalDistanceToPlayer();
        float distanceToPlayer = GetDistanceToPlayer();
        AttackPattern attackPattern = ChooseAttackPattern(activePhase, horizontalDistance, distanceToPlayer);
        switch (attackPattern)
        {
            case AttackPattern.Dash:
                RegisterAttackPattern(AttackPattern.Dash);
                stateRoutine = StartCoroutine(DashAttackRoutine(activePhase));
                return true;
            case AttackPattern.Spell:
                RegisterAttackPattern(AttackPattern.Spell);
                stateRoutine = StartCoroutine(SpellAttackRoutine(activePhase));
                return true;
            default:
                return false;
        }
    }

    private AttackPattern ChooseAttackPattern(BossPhaseData activePhase, float horizontalDistance, float distanceToPlayer)
    {
        bool canDash = CanPerformDash(activePhase, horizontalDistance);
        bool canSpell = CanPerformSpell(activePhase, distanceToPlayer);
        if (!canDash && !canSpell)
            return AttackPattern.None;

        if (canDash && !canSpell)
            return AttackPattern.Dash;

        if (!canDash && canSpell)
            return AttackPattern.Spell;

        if (lastAttackPattern == AttackPattern.Dash && consecutiveAttackRepeatCount >= maxConsecutiveSameAttack)
            return AttackPattern.Spell;

        if (lastAttackPattern == AttackPattern.Spell && consecutiveAttackRepeatCount >= maxConsecutiveSameAttack)
            return AttackPattern.Dash;

        float spellWeight = state == EncounterState.Phase2 ? phase2SpellWeight : phase1SpellWeight;
        float dashWeight = Mathf.Max(0.05f, 1f - spellWeight);
        spellWeight = Mathf.Max(0.05f, spellWeight);

        if (distanceToPlayer > activePhase.preferredDistance + activePhase.distanceTolerance)
        {
            spellWeight *= 1.15f;
            dashWeight *= 0.9f;
        }
        else
        {
            dashWeight *= 1.16f;
            spellWeight *= 0.86f;
        }

        if (lastAttackPattern == AttackPattern.Dash)
            dashWeight *= repeatAttackWeightPenalty;
        else if (lastAttackPattern == AttackPattern.Spell)
            spellWeight *= repeatAttackWeightPenalty;

        float totalWeight = dashWeight + spellWeight;
        if (totalWeight <= 0.0001f)
            return UnityEngine.Random.value < 0.5f ? AttackPattern.Dash : AttackPattern.Spell;

        float pick = UnityEngine.Random.value * totalWeight;
        return pick <= dashWeight ? AttackPattern.Dash : AttackPattern.Spell;
    }

    private bool CanPerformDash(BossPhaseData activePhase, float horizontalDistance)
    {
        return horizontalDistance <= activePhase.dashTriggerRange && dashCooldownTimer <= 0f;
    }

    private bool CanPerformSpell(BossPhaseData activePhase, float distanceToPlayer)
    {
        if (!IsSpellAvailableInCurrentPhase())
            return false;

        if (projectileCooldownTimer > 0f)
            return false;

        if (distanceToPlayer < activePhase.projectileMinRange || distanceToPlayer > activePhase.projectileMaxRange)
            return false;

        float configuredSpellWeight = state == EncounterState.Phase2 ? phase2SpellWeight : phase1SpellWeight;
        return configuredSpellWeight > 0.001f;
    }

    private bool IsSpellAvailableInCurrentPhase()
    {
        return state == EncounterState.Phase2 || state == EncounterState.Phase1;
    }

    private void RegisterAttackPattern(AttackPattern attackPattern)
    {
        if (attackPattern == AttackPattern.None)
            return;

        if (attackPattern == lastAttackPattern)
            consecutiveAttackRepeatCount++;
        else
        {
            lastAttackPattern = attackPattern;
            consecutiveAttackRepeatCount = 1;
        }
    }

    private IEnumerator PerformEvasiveRepositionRoutine(BossPhaseData activePhase)
    {
        if (!HasCombatTarget() || rb == null || activePhase == null)
            yield break;

        float evadeChance = state == EncounterState.Phase2 ? phase2EvadeChance : phase1EvadeChance;
        if (evadeChance <= 0f || UnityEngine.Random.value > evadeChance)
            yield break;

        Vector2 durationRange = state == EncounterState.Phase2 ? phase2EvadeDurationRange : phase1EvadeDurationRange;
        Vector2 speedMultiplierRange = state == EncounterState.Phase2 ? phase2EvadeSpeedMultiplierRange : phase1EvadeSpeedMultiplierRange;
        float crossThroughChance = state == EncounterState.Phase2 ? phase2CrossThroughChance : phase1CrossThroughChance;

        float duration = UnityEngine.Random.Range(durationRange.x, durationRange.y);
        float speedMultiplier = UnityEngine.Random.Range(speedMultiplierRange.x, speedMultiplierRange.y);
        if (duration <= 0.001f || speedMultiplier <= 0.001f)
            yield break;

        bool crossThrough = UnityEngine.Random.value < crossThroughChance;
        float evadeDirection = ResolveEvadeDirection(crossThrough);
        float targetSpeed = Mathf.Max(0.1f, activePhase.moveSpeed * speedMultiplier);
        float acceleration = Mathf.Max(0.1f, activePhase.acceleration * 1.2f);

        isDrivingAttackMovement = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!CanContinueAttack())
                break;

            FacePlayer();
            float desiredVelocityX = evadeDirection * targetSpeed;
            float newVelocityX = Mathf.MoveTowards(
                rb.linearVelocity.x,
                desiredVelocityX,
                acceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        isDrivingAttackMovement = false;
        StopHorizontalMotion();
    }

    private float ResolveEvadeDirection(bool crossThrough)
    {
        if (!HasCombatTarget())
            return facingDirection == 0 ? 1f : facingDirection;

        float deltaX = player.position.x - transform.position.x;
        float towardPlayer = Mathf.Abs(deltaX) > 0.001f
            ? Mathf.Sign(deltaX)
            : (facingDirection == 0 ? 1f : facingDirection);
        float awayFromPlayer = -towardPlayer;
        return crossThrough ? towardPlayer : awayFromPlayer;
    }

    private void BeginDashStrike(BossPhaseData activePhase)
    {
        if (dashHitbox == null)
            return;

        isDrivingAttackMovement = true;

        Vector3 localPosition = dashHitboxHomeLocalPosition;
        localPosition.x = Mathf.Abs(dashHitboxHomeLocalPosition.x) * facingDirection;
        dashHitbox.transform.localPosition = localPosition;
        dashHitbox.damage = activePhase.dashDamage;
        dashHitbox.knockbackForce = activePhase.dashKnockback;
        dashHitbox.targetLayers = ResolvePlayerLayerMask();
        dashHitbox.SetSwingScale(activePhase.dashHitboxScale, activePhase.dashOverlapRadius);
        dashHitbox.BeginSwing();

        AudioManager.TryPlaySfx("sword_swing", 0.92f, UnityEngine.Random.Range(0.9f, 0.97f));
    }

    private void EndDashStrike()
    {
        isDrivingAttackMovement = false;

        if (dashHitbox != null)
            dashHitbox.EndSwing();

        StopHorizontalMotion();
    }

    private IEnumerator PlayTelegraphedSpellStrikeRoutine(BossPhaseData activePhase, Vector2 strikePosition)
    {
        float windupDuration = Mathf.Max(0.08f, activePhase.projectileWindup);
        float elapsed = 0f;
        while (elapsed < windupDuration)
        {
            if (!CanContinueAttack())
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / windupDuration);
            float pulse = Mathf.Sin(t * Mathf.PI);
            ApplyVisualPulse(spellTelegraphTint, pulse, telegraphScalePulse);
            UpdateSpellStrikeVisual(activePhase, t, 1f);
            yield return null;
        }

        ResetVisualTint();

        if (CanContinueAttack())
            ResolveSpellStrikeHit(activePhase, strikePosition);

        float recoveryDuration = Mathf.Max(0f, activePhase.projectileRecovery);
        if (recoveryDuration <= 0f)
            yield break;

        float recoveryElapsed = 0f;
        while (recoveryElapsed < recoveryDuration)
        {
            recoveryElapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(recoveryElapsed / recoveryDuration);
            UpdateSpellStrikeVisual(activePhase, 1f, fade);
            yield return null;
        }
    }

    private void CreateSpellStrikeVisual(Vector2 strikePosition)
    {
        DestroyActiveSpellStrike();

        activeSpellStrikeObject = new GameObject("BossSpellStrike");
        activeSpellStrikeObject.transform.position = strikePosition;
        activeSpellStrikeObject.transform.localScale = new Vector3(spellStrikeScale.x, spellStrikeScale.y, 1f);
        activeSpellStrikeRenderer = activeSpellStrikeObject.AddComponent<SpriteRenderer>();

        if (visualRenderer == null)
            return;

        activeSpellStrikeRenderer.sortingLayerID = visualRenderer.sortingLayerID;
        activeSpellStrikeRenderer.sortingOrder = visualRenderer.sortingOrder + 2;
        activeSpellStrikeRenderer.flipX = false;
        UpdateSpellStrikeVisual(GetActivePhaseData(), 0f, 0.85f);
    }

    private void UpdateSpellStrikeVisual(BossPhaseData activePhase, float normalizedTime, float alphaMultiplier)
    {
        if (activeSpellStrikeRenderer == null)
            return;

        Sprite[] frames = spellFrames;
        if (HasFrames(frames))
        {
            int frameIndex = Mathf.Min(
                Mathf.FloorToInt(Mathf.Clamp01(normalizedTime) * frames.Length),
                frames.Length - 1);
            activeSpellStrikeRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        }

        Color spellColor = Color.Lerp(GetRestingVisualColor(), activePhase.projectileColor, 0.65f);
        spellColor.a = Mathf.Clamp01(alphaMultiplier);
        activeSpellStrikeRenderer.color = spellColor;

        float pulse = 1.05f + 0.15f * Mathf.Sin(Mathf.Clamp01(normalizedTime) * Mathf.PI * 2f);
        if (activeSpellStrikeObject != null)
            activeSpellStrikeObject.transform.localScale =
                new Vector3(spellStrikeScale.x * pulse, spellStrikeScale.y * pulse, 1f);
    }

    private void ResolveSpellStrikeHit(BossPhaseData activePhase, Vector2 strikePosition)
    {
        if (!HasCombatTarget() || !IsPlayerInsideSpellStrike(strikePosition))
            return;

        float damage = Mathf.Max(0f, playerHealth.CurrentHealth * 0.5f);
        if (damage <= 0f)
            return;

        Vector2 knockbackDirection = player.position - (Vector3)strikePosition;
        if (knockbackDirection.sqrMagnitude <= 0.0001f)
            knockbackDirection = new Vector2(facingDirection, 0.4f);

        playerHealth.TakeDamage(damage, knockbackDirection.normalized, activePhase.projectileKnockback, transform);
        AudioManager.TryPlaySfx("boss_hit", 0.92f, UnityEngine.Random.Range(0.94f, 1.02f));
    }

    private bool IsPlayerInsideSpellStrike(Vector2 strikePosition)
    {
        if (!HasCombatTarget())
            return false;

        float radius = Mathf.Max(0.05f, spellStrikeRadius);
        Collider2D playerCollider = ResolvePlayerCollider();
        if (playerCollider == null)
            return Vector2.Distance(player.position, strikePosition) <= radius;

        Vector2 closestPoint = playerCollider.ClosestPoint(strikePosition);
        return (closestPoint - strikePosition).sqrMagnitude <= radius * radius;
    }

    private Vector2 ResolveSpellStrikePosition()
    {
        if (!HasCombatTarget())
            return transform.position;

        Collider2D playerCollider = ResolvePlayerCollider();
        if (playerCollider != null)
        {
            Bounds bounds = playerCollider.bounds;
            return new Vector2(bounds.center.x, bounds.min.y + spellStrikeGroundOffset);
        }

        return new Vector2(player.position.x, player.position.y + spellStrikeGroundOffset);
    }

    private Collider2D ResolvePlayerCollider()
    {
        if (player == null)
            return null;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider != null)
            return playerCollider;

        return player.GetComponentInChildren<Collider2D>();
    }

    private void DestroyActiveSpellStrike()
    {
        if (activeSpellStrikeObject != null)
            Destroy(activeSpellStrikeObject);

        activeSpellStrikeObject = null;
        activeSpellStrikeRenderer = null;
    }

    private bool ShouldChooseAttack()
    {
        return encounterActive &&
            !isDead &&
            !isPerformingAttack &&
            stateRoutine == null &&
            actionRecoveryTimer <= 0f &&
            HasCombatTarget() &&
            (state == EncounterState.Phase1 || state == EncounterState.Phase2);
    }

    private bool CanContinueAttack()
    {
        return !isDead && health != null && !health.IsDead && HasCombatTarget();
    }

    private bool ShouldTriggerPhaseTwo()
    {
        if (health == null || health.MaxHealth <= 0f)
            return false;

        return health.CurrentHealth <= health.MaxHealth * phaseTwoHealthFraction;
    }

    private BossPhaseData GetActivePhaseData()
    {
        return state == EncounterState.Phase2 ? phase2 : phase1;
    }

    private void MoveTowardPreferredDistance(BossPhaseData activePhase)
    {
        if (rb == null || !HasCombatTarget())
            return;

        float deltaX = player.position.x - transform.position.x;
        float absDelta = Mathf.Abs(deltaX);
        float desiredVelocityX = 0f;

        if (absDelta > activePhase.preferredDistance + activePhase.distanceTolerance)
        {
            desiredVelocityX = Mathf.Sign(deltaX) * activePhase.moveSpeed;
        }
        else if (absDelta < activePhase.preferredDistance - activePhase.distanceTolerance)
        {
            desiredVelocityX = -Mathf.Sign(deltaX) * activePhase.moveSpeed;
        }

        float newVelocityX = Mathf.MoveTowards(
            rb.linearVelocity.x,
            desiredVelocityX,
            activePhase.acceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector2(newVelocityX, rb.linearVelocity.y);
    }

    private void StopHorizontalMotion()
    {
        if (rb == null)
            return;

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void FacePlayer()
    {
        if (!HasCombatTarget())
            return;

        float deltaX = player.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) > 0.001f)
            facingDirection = deltaX < 0f ? -1 : 1;

        RefreshFacingVisual();
    }

    private void RefreshFacingVisual()
    {
        if (visualRenderer == null)
        {
            RefreshAttachmentAnchors();
            return;
        }

        bool shouldFlip = spriteFacesRight
            ? facingDirection < 0
            : facingDirection > 0;

        visualRenderer.flipX = shouldFlip;
        RefreshAttachmentAnchors();
    }

    private void RefreshAttachmentAnchors()
    {
        if (projectileSpawnPoint == null)
            return;

        Vector3 localPosition = projectileSpawnHomeLocalPosition;
        localPosition.x = Mathf.Abs(projectileSpawnHomeLocalPosition.x) * facingDirection;
        projectileSpawnPoint.localPosition = localPosition;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        float previousHealth = lastKnownHealth > 0f ? lastKnownHealth : maxHealth;
        lastKnownHealth = currentHealth;

        if (currentHealth <= 0f || currentHealth >= previousHealth)
            return;

        hurtUntil = Time.time + hurtHoldDuration;
    }

    private void HandleDied()
    {
        if (isDead)
            return;

        isDead = true;
        encounterActive = false;
        state = EncounterState.Death;
        CancelCombatState();

        if (bodyCollider != null)
            bodyCollider.enabled = false;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SetForcedVisualState(VisualState.Death);
        ApplyAnimationFrame(VisualState.Death, 0f);
        BossDefeated?.Invoke(this);
    }

    private void CancelCombatState()
    {
        if (stateRoutine != null)
            StopCoroutine(stateRoutine);

        stateRoutine = null;
        encounterActive = false;
        isPerformingAttack = false;
        lastAttackPattern = AttackPattern.None;
        consecutiveAttackRepeatCount = 0;
        EndDashStrike();
        ClearForcedVisualState();
        DestroyActiveSpellStrike();
        ResetVisualTint();
    }

    private IEnumerator PlayTelegraphRoutine(float duration, Color tint, float scalePulse)
    {
        if (duration <= 0f)
        {
            ResetVisualTint();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI);
            ApplyVisualPulse(tint, pulse, scalePulse);
            yield return null;
        }

        ResetVisualTint();
    }

    private void ApplyVisualPulse(Color tint, float pulse, float scalePulse)
    {
        Color restingColor = GetRestingVisualColor();
        if (visualRenderer != null)
            visualRenderer.color = Color.Lerp(restingColor, tint, pulse * 0.85f);

        if (visualRoot != null)
            visualRoot.localScale = Vector3.Lerp(baseVisualScale, baseVisualScale * Mathf.Max(1f, scalePulse), pulse);
    }

    private void ResetVisualTint()
    {
        if (visualRenderer != null)
            visualRenderer.color = GetRestingVisualColor();

        if (visualRoot != null)
            visualRoot.localScale = baseVisualScale;
    }

    private Color GetRestingVisualColor()
    {
        if (state != EncounterState.Phase2)
            return baseColor;

        return Color.Lerp(baseColor, phaseTwoTint, phaseTwoTintStrength);
    }

    private void SetForcedVisualState(VisualState nextState)
    {
        hasForcedVisualState = true;
        forcedVisualState = nextState;
        animationElapsed = 0f;
    }

    private void ClearForcedVisualState()
    {
        hasForcedVisualState = false;
    }

    private void UpdateAnimation()
    {
        if (visualRenderer == null)
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
        if (isDead || (health != null && health.IsDead))
            return VisualState.Death;

        if (hasForcedVisualState)
            return forcedVisualState;

        if (Time.time < hurtUntil && HasFrames(hurtFrames))
            return VisualState.Hurt;

        if (rb != null && new Vector2(rb.linearVelocity.x, 0f).sqrMagnitude > movementAnimationThresholdSqr && HasFrames(walkFrames))
            return VisualState.Walk;

        return VisualState.Idle;
    }

    private void ApplyAnimationFrame(VisualState nextState, float elapsed)
    {
        if (visualRenderer == null)
            return;

        Sprite[] frames = GetFramesForState(nextState);
        if (!HasFrames(frames))
            return;

        float framesPerSecond = Mathf.Max(0.01f, GetFramesPerSecond(nextState));
        int frameIndex = Mathf.FloorToInt(elapsed * framesPerSecond);
        if (IsLoopingState(nextState))
            frameIndex %= frames.Length;
        else
            frameIndex = Mathf.Min(frameIndex, frames.Length - 1);

        visualRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
    }

    private Sprite[] GetFramesForState(VisualState nextState)
    {
        switch (nextState)
        {
            case VisualState.Walk:
                return walkFrames;
            case VisualState.Attack:
                return attackFrames;
            case VisualState.Cast:
                return castFrames;
            case VisualState.Spell:
                return spellFrames;
            case VisualState.Hurt:
                return hurtFrames;
            case VisualState.Death:
                return deathFrames;
            default:
                return idleFrames;
        }
    }

    private float GetFramesPerSecond(VisualState nextState)
    {
        switch (nextState)
        {
            case VisualState.Walk:
                return walkFramesPerSecond;
            case VisualState.Attack:
                return attackFramesPerSecond;
            case VisualState.Cast:
                return castFramesPerSecond;
            case VisualState.Spell:
                return spellFramesPerSecond;
            case VisualState.Hurt:
                return hurtFramesPerSecond;
            case VisualState.Death:
                return deathFramesPerSecond;
            default:
                return idleFramesPerSecond;
        }
    }

    private bool IsLoopingState(VisualState nextState)
    {
        return nextState == VisualState.Idle ||
            nextState == VisualState.Walk ||
            nextState == VisualState.Spell;
    }

    private void ResolvePlayer()
    {
        if (player != null && player.gameObject.activeInHierarchy)
            return;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            player = taggedPlayer.transform;
            playerHealth = taggedPlayer.GetComponent<Health>();
            return;
        }

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            player = playerMovement.transform;
            playerHealth = playerMovement.GetComponent<Health>();
        }
    }

    private bool HasCombatTarget()
    {
        return player != null &&
            playerHealth != null &&
            !playerHealth.IsDead &&
            player.gameObject.activeInHierarchy;
    }

    private float GetHorizontalDistanceToPlayer()
    {
        return HasCombatTarget() ? Mathf.Abs(player.position.x - transform.position.x) : float.PositiveInfinity;
    }

    private float GetDistanceToPlayer()
    {
        return HasCombatTarget() ? Vector2.Distance(player.position, transform.position) : float.PositiveInfinity;
    }

    private LayerMask ResolvePlayerLayerMask()
    {
        if (projectileTargetLayers.value != 0)
            return projectileTargetLayers;

        int playerLayer = player != null ? player.gameObject.layer : LayerMask.NameToLayer("Default");
        if (playerLayer < 0)
            playerLayer = 0;

        return 1 << playerLayer;
    }

    private void EnsureDefaultLayers()
    {
        if (projectileTargetLayers.value != 0)
            return;

        projectileTargetLayers = 1 << 0;
    }

    private void ResolveReferences()
    {
        if (visualRenderer == null)
            visualRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (visualRoot == null && visualRenderer != null)
            visualRoot = visualRenderer.transform;

        if (dashHitbox == null)
            dashHitbox = GetComponentInChildren<WeaponHitbox>(true);

        if (projectileSpawnPoint == null)
        {
            Transform candidate = transform.Find("ProjectileSpawn");
            if (candidate != null)
                projectileSpawnPoint = candidate;
        }
    }

    private static bool HasFrames(Sprite[] frames)
    {
        return frames != null && frames.Length > 0;
    }

    private static Vector2 ClampRange(Vector2 range, float minValue)
    {
        float min = Mathf.Max(minValue, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return new Vector2(min, max);
    }
}
