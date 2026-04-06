using System.Collections;
using UnityEngine;

public class PlayerWeaponController : MonoBehaviour
{
    public enum KnightAttackVisualType
    {
        None,
        Attack,
        Attack2,
        AttackNoMovement,
        Attack2NoMovement,
        AttackCombo,
        AttackComboNoMovement,
        CrouchAttack
    }

    private const string AttackParameterName = "isAttacking";
    private const string VisualConsumerName = "PlayerWeaponController";
    private static readonly int AttackParameterHash = Animator.StringToHash(AttackParameterName);

    public bool IsAttacking => attackRoutine != null;
    public int CurrentAttackToken => currentAttackToken;
    public KnightAttackVisualType CurrentAttackVisual => currentAttackVisual;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private Animator visualAnimator;

    [Header("Current Weapon")]
    public WeaponType currentWeapon = WeaponType.IronSword;

    [Header("Shared")]
    public LayerMask enemyLayers;

    [Header("Iron Sword")]
    public WeaponHitbox swordHitbox;
    public float swordDamage = 20f;
    public float swordSwingDuration = 0.15f;
    public float swordCooldown = 0.3f;
    [Tooltip("Scale multiplier used for the player sword hitbox during a swing.")]
    public float playerSwordHitboxScale = 2.35f;
    [Tooltip("Fallback overlap radius used to catch enemies that are nearly on top of the player.")]
    public float playerSwordOverlapFallbackRadius = 0.42f;

    [Header("Combat Feel")]
    public float attackDelayBetweenAttacks = 0.1f;
    public float comboQueueWindow = 0.22f;
    public float comboResetDelay = 0.55f;
    public float secondAttackDamageMultiplier = 1.08f;
    public float comboAttackDamageMultiplier = 1.2f;

    [Header("Attack Visual")]
    [SerializeField] private Sprite[] attackFrames = new Sprite[0];
    [SerializeField] private float attackFrameRate = 10f;
    [SerializeField] private bool disableAnimatorDuringAttack = true;
    [SerializeField] private bool returnToAnimatorAfterAttack = true;
    [SerializeField] private float fallbackAttackFlash = 0.18f;
    [SerializeField] private float attackScaleMultiplier = 0.84f;

    private PlayerMovement playerMovement;
    private float attackCooldownTimer;
    private Sprite defaultSprite;
    private Color defaultColor = Color.white;
    private Coroutine attackVisualRoutine;
    private Coroutine attackRoutine;
    private Transform attackVisualTransform;
    private Vector3 attackVisualOriginalScale;
    private bool isAttackVisualScaled;
    private bool inputLocked;
    private bool canDriveAnimatorAttack;
    private bool hasExternalKnightVisuals;
    private bool queuedComboAttack;
    private float queuedComboAttackExpiresAt;
    private float comboExpiresAt;
    private int currentComboStage;
    private int currentAttackToken;
    private KnightAttackVisualType currentAttackVisual;

    void Reset()
    {
        ResolveVisualReferences(false);
        canDriveAnimatorAttack = HasBoolParameter(visualAnimator, AttackParameterName);
    }

    void OnValidate()
    {
        ResolveVisualReferences(false);
        canDriveAnimatorAttack = HasBoolParameter(visualAnimator, AttackParameterName);
        playerSwordHitboxScale = Mathf.Max(1f, playerSwordHitboxScale);
        playerSwordOverlapFallbackRadius = Mathf.Max(0f, playerSwordOverlapFallbackRadius);
        swordSwingDuration = Mathf.Max(0.02f, swordSwingDuration);
        swordCooldown = Mathf.Max(0f, swordCooldown);
        comboQueueWindow = Mathf.Max(0.05f, comboQueueWindow);
        comboResetDelay = Mathf.Max(comboQueueWindow, comboResetDelay);
        attackDelayBetweenAttacks = Mathf.Max(0.02f, attackDelayBetweenAttacks);
        ValidateVisualSetup();
        ValidateAttackSetup();
    }

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        hasExternalKnightVisuals = GetComponent<PlayerKnightVisualController>() != null;

        ResolveVisualReferences(true);
        EnsureAnimatorEnabled("Awake");
        PrimeAnimatorVisualState();
        CaptureDefaultVisualState();
        canDriveAnimatorAttack = HasBoolParameter(visualAnimator, AttackParameterName);

        ValidateVisualSetup();
        ValidateAttackSetup();
    }

    void OnEnable()
    {
        RestoreIdleVisualState("OnEnable", false);
    }

    void OnDisable()
    {
        StopAttackSequence();
        StopAttackVisual();
    }

    void Update()
    {
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        if (attackRoutine == null && Time.time > comboExpiresAt)
            currentComboStage = 0;

        if (attackRoutine != null && Time.time > queuedComboAttackExpiresAt)
            queuedComboAttack = false;

        if (inputLocked)
            return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J))
            HandleAttackPressed();
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

    private void ValidateAttackSetup()
    {
        if (hasExternalKnightVisuals)
            return;

        if (!canDriveAnimatorAttack && (attackFrames == null || attackFrames.Length == 0))
        {
            Debug.LogWarning(
                $"{VisualConsumerName} has no assigned attackFrames and visualAnimator " +
                $"'{PlayerVisualReferenceUtility.DescribeComponent(visualAnimator)}' does not expose '{AttackParameterName}'. " +
                "Attacks will fall back to a brief color flash only.",
                this);
        }
    }

    private void EnsureAnimatorEnabled(string source)
    {
        if (hasExternalKnightVisuals || visualAnimator == null || visualAnimator.enabled)
            return;

        visualAnimator.enabled = true;
        Debug.LogWarning(
            $"{VisualConsumerName} enabled visualAnimator '{PlayerVisualReferenceUtility.DescribeTransform(visualAnimator.transform)}' " +
            $"during {source} because it was disabled.",
            this);
    }

    private void PrimeAnimatorVisualState()
    {
        if (hasExternalKnightVisuals || visualAnimator == null || !visualAnimator.enabled || visualRenderer == null)
            return;

        if (visualRenderer.sprite != null && visualRenderer.color.a > 0.001f)
            return;

        visualAnimator.Update(0f);
    }

    private void CaptureDefaultVisualState()
    {
        attackVisualTransform = visualRoot != null
            ? visualRoot
            : (visualRenderer != null ? visualRenderer.transform : transform);

        if (visualRenderer == null)
            return;

        if (visualRenderer.sprite != null)
            defaultSprite = visualRenderer.sprite;

        defaultColor = visualRenderer.color;
        if (defaultColor.a <= 0.001f)
        {
            defaultColor.a = 1f;
            visualRenderer.color = defaultColor;
        }
    }

    private void HandleAttackPressed()
    {
        if (attackRoutine != null)
        {
            QueueComboAttack();
            return;
        }

        if (attackCooldownTimer > 0f)
            return;

        attackRoutine = StartCoroutine(AttackRoutine());
    }

    private void QueueComboAttack()
    {
        if (currentWeapon != WeaponType.IronSword || currentComboStage >= 3)
            return;

        queuedComboAttack = true;
        queuedComboAttackExpiresAt = Time.time + comboQueueWindow;
    }

    private IEnumerator AttackRoutine()
    {
        while (true)
        {
            queuedComboAttack = false;
            queuedComboAttackExpiresAt = 0f;

            int nextStage = ResolveNextComboStage();
            currentAttackVisual = ResolveAttackVisual(nextStage);
            currentAttackToken++;

            if (!hasExternalKnightVisuals)
                PlayAttackVisual();

            if (currentWeapon == WeaponType.IronSword)
                yield return PerformSwordAttack(nextStage);

            comboExpiresAt = Time.time + comboResetDelay;

            bool canContinueCombo = false;
            if (nextStage < 3 && currentAttackVisual != KnightAttackVisualType.CrouchAttack)
            {
                float comboWait = Mathf.Max(comboQueueWindow, attackDelayBetweenAttacks);
                float elapsed = 0f;
                while (elapsed < comboWait)
                {
                    if (queuedComboAttack && Time.time <= queuedComboAttackExpiresAt)
                    {
                        canContinueCombo = true;
                        break;
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (!canContinueCombo)
                break;
        }

        currentComboStage = 0;
        currentAttackVisual = KnightAttackVisualType.None;
        attackRoutine = null;
        attackCooldownTimer = Mathf.Max(attackCooldownTimer, swordCooldown);
    }

    private IEnumerator PerformSwordAttack(int comboStage)
    {
        float damageMultiplier = ResolveDamageMultiplier(comboStage);
        AudioManager.TryPlaySfx("sword_swing", 1f, Random.Range(0.97f, 1.03f));

        BeginSwordSwing(damageMultiplier);
        yield return new WaitForSeconds(swordSwingDuration);
        EndSwordSwing();

        attackCooldownTimer = attackDelayBetweenAttacks;
    }

    private void BeginSwordSwing(float damageMultiplier)
    {
        if (swordHitbox == null)
            return;

        int dir = playerMovement != null ? playerMovement.FacingDirection : 1;
        Vector3 pos = swordHitbox.transform.localPosition;
        pos.x = Mathf.Abs(pos.x) * dir;
        swordHitbox.transform.localPosition = pos;

        swordHitbox.SetOwner(transform);
        swordHitbox.damage = swordDamage * damageMultiplier;
        swordHitbox.SetSwingScale(playerSwordHitboxScale, playerSwordOverlapFallbackRadius);
        swordHitbox.targetLayers = enemyLayers;
        swordHitbox.BeginSwing();
    }

    private void EndSwordSwing()
    {
        if (swordHitbox != null)
            swordHitbox.EndSwing();
    }

    private int ResolveNextComboStage()
    {
        bool crouching = playerMovement != null && playerMovement.IsCrouching;
        if (crouching)
        {
            currentComboStage = 1;
            return currentComboStage;
        }

        if (currentComboStage <= 0 || Time.time > comboExpiresAt)
            currentComboStage = 1;
        else
            currentComboStage = Mathf.Min(currentComboStage + 1, 3);

        return currentComboStage;
    }

    private KnightAttackVisualType ResolveAttackVisual(int comboStage)
    {
        bool crouching = playerMovement != null && playerMovement.IsCrouching;
        if (crouching)
            return KnightAttackVisualType.CrouchAttack;

        bool movingAttack = playerMovement != null &&
            (Mathf.Abs(playerMovement.MoveInput) > 0.1f || Mathf.Abs(playerMovement.HorizontalSpeed) > 0.6f);

        if (comboStage <= 1)
            return movingAttack ? KnightAttackVisualType.Attack : KnightAttackVisualType.AttackNoMovement;

        if (comboStage == 2)
            return movingAttack ? KnightAttackVisualType.Attack2 : KnightAttackVisualType.Attack2NoMovement;

        return movingAttack ? KnightAttackVisualType.AttackCombo : KnightAttackVisualType.AttackComboNoMovement;
    }

    private float ResolveDamageMultiplier(int comboStage)
    {
        if (currentAttackVisual == KnightAttackVisualType.CrouchAttack)
            return secondAttackDamageMultiplier;

        if (comboStage == 2)
            return secondAttackDamageMultiplier;

        if (comboStage >= 3)
            return comboAttackDamageMultiplier;

        return 1f;
    }

    private void PlayAttackVisual()
    {
        StopAttackVisual();
        attackVisualRoutine = StartCoroutine(PlayAttackVisualRoutine());
    }

    private IEnumerator PlayAttackVisualRoutine()
    {
        ApplyAttackVisualScale();

        if (TryStartAnimatorAttack())
        {
            yield return new WaitForSeconds(GetAttackVisualDuration());
            SetAnimatorAttackState(false);
        }
        else if (attackFrames != null && attackFrames.Length > 0)
        {
            if (disableAnimatorDuringAttack && visualAnimator != null)
                visualAnimator.enabled = false;

            float frameTime = Mathf.Max(1f / Mathf.Max(1f, attackFrameRate), 0.02f);
            if (visualRenderer != null)
                visualRenderer.color = defaultColor;

            for (int i = 0; i < attackFrames.Length; i++)
            {
                if (visualRenderer == null || attackFrames[i] == null)
                    continue;

                visualRenderer.sprite = attackFrames[i];
                yield return new WaitForSeconds(frameTime);
            }
        }
        else
        {
            if (disableAnimatorDuringAttack && visualAnimator != null)
                visualAnimator.enabled = false;

            if (visualRenderer != null)
            {
                Color flashColor = defaultColor;
                flashColor.a = Mathf.Max(flashColor.a, 1f);
                visualRenderer.color = flashColor;
                yield return new WaitForSeconds(fallbackAttackFlash);
            }
        }

        RestoreIdleVisualState("AttackComplete", false);
        ResetAttackVisualScale();
        attackVisualRoutine = null;
    }

    private void ApplyAttackVisualScale()
    {
        if (isAttackVisualScaled || attackVisualTransform == null)
            return;

        attackVisualOriginalScale = attackVisualTransform.localScale;
        float scale = Mathf.Max(0.1f, attackScaleMultiplier);
        attackVisualTransform.localScale = attackVisualOriginalScale * scale;
        isAttackVisualScaled = true;
    }

    private void ResetAttackVisualScale()
    {
        if (!isAttackVisualScaled || attackVisualTransform == null)
            return;

        attackVisualTransform.localScale = attackVisualOriginalScale;
        isAttackVisualScaled = false;
    }

    private void StopAttackVisual()
    {
        if (attackVisualRoutine != null)
            StopCoroutine(attackVisualRoutine);

        attackVisualRoutine = null;
        SetAnimatorAttackState(false);
        RestoreIdleVisualState("StopAttackVisual", false);
        ResetAttackVisualScale();
    }

    private void RestoreIdleVisualState(string source, bool verbose)
    {
        if (!hasExternalKnightVisuals && returnToAnimatorAfterAttack && visualAnimator != null && !visualAnimator.enabled)
            visualAnimator.enabled = true;

        if (visualRenderer == null)
            return;

        Color restoredColor = defaultColor;
        if (restoredColor.a <= 0.001f)
            restoredColor.a = 1f;

        visualRenderer.color = restoredColor;

        if (!canDriveAnimatorAttack && defaultSprite != null)
            visualRenderer.sprite = defaultSprite;

        if (visualRenderer.sprite == null && defaultSprite != null)
        {
            visualRenderer.sprite = defaultSprite;
            if (verbose)
            {
                Debug.LogWarning(
                    $"{VisualConsumerName} restored defaultSprite on visualRenderer " +
                    $"'{PlayerVisualReferenceUtility.DescribeTransform(visualRenderer.transform)}' during {source}.",
                    this);
            }
        }

        if (visualRenderer.color.a <= 0.001f)
        {
            Color repairedColor = visualRenderer.color;
            repairedColor.a = 1f;
            visualRenderer.color = repairedColor;
        }

        if (visualRenderer.sprite == null && visualAnimator != null && visualAnimator.enabled)
            visualAnimator.Update(0f);
    }

    private bool TryStartAnimatorAttack()
    {
        if (hasExternalKnightVisuals || !canDriveAnimatorAttack || visualAnimator == null)
            return false;

        EnsureAnimatorEnabled("Attack");

        if (!visualAnimator.isActiveAndEnabled)
            return false;

        RestoreIdleVisualState("AnimatorAttackStart", false);
        SetAnimatorAttackState(true);
        return true;
    }

    private void SetAnimatorAttackState(bool isAttacking)
    {
        if (hasExternalKnightVisuals || !canDriveAnimatorAttack || visualAnimator == null)
            return;

        visualAnimator.SetBool(AttackParameterHash, isAttacking);
    }

    private float GetAttackVisualDuration()
    {
        int frameCount = 0;
        if (attackFrames != null)
        {
            for (int i = 0; i < attackFrames.Length; i++)
            {
                if (attackFrames[i] != null)
                    frameCount++;
            }
        }

        if (frameCount > 0)
            return Mathf.Max(frameCount / Mathf.Max(1f, attackFrameRate), 0.02f);

        if (visualAnimator != null && visualAnimator.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = visualAnimator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name == "attack_animation")
                    return Mathf.Max(clips[i].length, 0.02f);
            }
        }

        return Mathf.Max(fallbackAttackFlash, 0.02f);
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

    private void StopAttackSequence()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = null;
        queuedComboAttack = false;
        currentComboStage = 0;
        currentAttackVisual = KnightAttackVisualType.None;
        EndSwordSwing();
    }

    public int CurrentWeaponIndex => 1;

    public void SetWeapon(int index)
    {
        currentWeapon = WeaponType.IronSword;
    }

    public void SetInputLocked(bool isLocked)
    {
        inputLocked = isLocked;

        if (!inputLocked)
            return;

        attackCooldownTimer = 0f;
        StopAttackSequence();
        StopAttackVisual();
    }
}
