using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public class EnemyProjectileAttack : MonoBehaviour
{
    public enum AttackAimMode
    {
        DirectToPlayer = 0,
        HorizontalOnly = 1
    }

    [Header("Attack")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private float minimumAttackRange = 0f;
    [SerializeField] private float maximumAttackRange = 8f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackWindup = 0.25f;
    [SerializeField] private float postAttackPause = 0.12f;
    [SerializeField] private bool pauseGroundMovementDuringAttack = true;
    [SerializeField] private float movementPausePadding = 0.05f;
    [SerializeField] private MonoBehaviour attackVisualOverride;

    [Header("Projectile")]
    [SerializeField] private AttackAimMode aimMode = AttackAimMode.DirectToPlayer;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileDamage = 10f;
    [SerializeField] private float projectileLifetime = 2.5f;
    [SerializeField] private float projectileKnockback = 4f;
    [SerializeField] private LayerMask projectileTargetLayers = default;
    [SerializeField] private Vector2 spawnOffset = new Vector2(0.45f, 0.45f);
    [SerializeField] private Vector2 projectileScale = new Vector2(0.4f, 0.4f);
    [SerializeField] private float projectileColliderRadius = 0.4f;
    [SerializeField] private Color projectileColor = Color.white;
    [SerializeField] private Sprite projectileSprite;

    [Header("Audio")]
    [SerializeField] private string attackCueId = "slime_shot";
    [SerializeField] private float attackCuePitchJitter = 0.04f;

    private EnemyRandomFollower follower;
    private Health health;
    private Transform player;
    private SpriteRenderer ownerRenderer;
    private Coroutine attackRoutine;
    private float cooldownTimer;
    private IEnemyAttackVisual attackVisual;

    void Awake()
    {
        follower = GetComponent<EnemyRandomFollower>();
        health = GetComponent<Health>();
        ownerRenderer = FindPrimarySpriteRenderer();

        attackVisual = ResolveAttackVisual();
    }

    void OnDisable()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = null;
    }

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (attackRoutine != null || projectilePrefab == null || health == null || health.IsDead)
            return;

        if (follower != null && follower.IsDormant)
            return;

        ResolvePlayer();
        if (player == null)
            return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer < minimumAttackRange || distanceToPlayer > maximumAttackRange || cooldownTimer > 0f)
            return;

        attackRoutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        cooldownTimer = attackCooldown;

        if (pauseGroundMovementDuringAttack && follower != null)
            follower.PauseMovement(attackWindup + postAttackPause + movementPausePadding);

        attackVisual?.PlayAttackVisual();
        yield return new WaitForSeconds(attackWindup);

        ResolvePlayer();
        if (player != null && projectilePrefab != null && health != null && !health.IsDead)
            FireProjectile();

        yield return new WaitForSeconds(postAttackPause);
        attackRoutine = null;
    }

    private void FireProjectile()
    {
        Vector2 direction = GetFireDirection();
        Vector2 spawnPosition = GetSpawnPosition(direction);

        Projectile projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        projectile.speed = projectileSpeed;
        projectile.damage = projectileDamage;
        projectile.lifetime = projectileLifetime;
        projectile.knockbackForce = projectileKnockback;
        projectile.applyPoison = false;
        projectile.targetLayers = projectileTargetLayers.value != 0
            ? projectileTargetLayers
            : (LayerMask)(1 << player.gameObject.layer);

        ConfigureProjectileVisual(projectile);
        projectile.transform.right = direction;
        projectile.Init(direction, transform);

        if (!string.IsNullOrWhiteSpace(attackCueId))
            AudioManager.TryPlaySfx(attackCueId, 1f, Random.Range(1f - attackCuePitchJitter, 1f + attackCuePitchJitter));
    }

    private Vector2 GetFireDirection()
    {
        ResolvePlayer();
        if (player == null)
            return Vector2.right;

        Vector2 delta = player.position - transform.position;
        if (aimMode == AttackAimMode.HorizontalOnly)
        {
            if (Mathf.Abs(delta.x) <= 0.001f)
                return Vector2.right;

            return delta.x >= 0f ? Vector2.right : Vector2.left;
        }

        if (delta.sqrMagnitude <= 0.0001f)
            return ownerRenderer != null && ownerRenderer.flipX ? Vector2.left : Vector2.right;

        return delta.normalized;
    }

    private Vector2 GetSpawnPosition(Vector2 direction)
    {
        float horizontalDirection = Mathf.Abs(direction.x) > 0.001f ? Mathf.Sign(direction.x) : 1f;
        return (Vector2)transform.position + new Vector2(spawnOffset.x * horizontalDirection, spawnOffset.y);
    }

    private void ConfigureProjectileVisual(Projectile projectile)
    {
        if (projectile == null)
            return;

        projectile.transform.localScale = new Vector3(projectileScale.x, projectileScale.y, 1f);

        SpriteRenderer projectileRenderer = projectile.GetComponent<SpriteRenderer>();
        if (projectileRenderer != null)
        {
            projectileRenderer.enabled = true;
            if (projectileSprite != null)
                projectileRenderer.sprite = projectileSprite;

            if (ownerRenderer != null)
            {
                projectileRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
                projectileRenderer.sortingOrder = ownerRenderer.sortingOrder + 2;
            }

            projectileRenderer.color = projectileColor;
        }

        CircleCollider2D projectileCollider = projectile.GetComponent<CircleCollider2D>();
        if (projectileCollider != null)
            projectileCollider.radius = Mathf.Max(0.05f, projectileColliderRadius);
    }

    private IEnemyAttackVisual ResolveAttackVisual()
    {
        if (attackVisualOverride is IEnemyAttackVisual overrideVisual)
            return overrideVisual;

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null || ReferenceEquals(behaviours[i], this))
                continue;

            if (behaviours[i] is IEnemyAttackVisual visual)
                return visual;
        }

        return null;
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
