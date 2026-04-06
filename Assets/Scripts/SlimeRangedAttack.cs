using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyRandomFollower))]
[RequireComponent(typeof(Health))]
public class SlimeRangedAttack : MonoBehaviour
{
    private const int FallbackProjectileTextureSize = 32;

    [Header("Attack")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private float minimumAttackRange = 0f;
    [SerializeField] private float maximumAttackRange = 6f;
    [SerializeField] private float attackCooldown = 1.8f;
    [SerializeField] private float attackWindup = 0.22f;
    [SerializeField] private float postAttackPause = 0.18f;

    [Header("Projectile")]
    [SerializeField] private float projectileSpeed = 7f;
    [SerializeField] private float projectileDamage = 10f;
    [SerializeField] private float projectileLifetime = 2.5f;
    [SerializeField] private float projectileKnockback = 4f;
    [SerializeField] private LayerMask projectileTargetLayers = default;
    [SerializeField] private Vector2 spawnOffset = new Vector2(0.45f, 0.45f);
    [SerializeField] private Vector2 projectileScale = new Vector2(0.32f, 0.32f);
    [SerializeField] private float projectileColliderRadius = 0.8f;
    [SerializeField] private Color projectileColor = new Color(0.62f, 0.95f, 0.62f, 1f);

    private EnemyRandomFollower follower;
    private Health health;
    private Transform player;
    private SpriteRenderer ownerRenderer;
    private Coroutine attackRoutine;
    private float cooldownTimer;
    private static Sprite fallbackProjectileSprite;

    void Awake()
    {
        follower = GetComponent<EnemyRandomFollower>();
        health = GetComponent<Health>();
        ownerRenderer = GetComponent<SpriteRenderer>();
        if (ownerRenderer == null)
            ownerRenderer = GetComponentInChildren<SpriteRenderer>();
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

        if (follower != null)
        {
            follower.PauseMovement(attackWindup + postAttackPause);
            follower.PlayAttackVisual();
        }

        yield return new WaitForSeconds(attackWindup);

        ResolvePlayer();
        if (player != null && projectilePrefab != null && health != null && !health.IsDead)
            FireProjectile();

        yield return new WaitForSeconds(postAttackPause);
        attackRoutine = null;
    }

    private void FireProjectile()
    {
        Vector2 spawnPosition = GetSpawnPosition();
        Vector2 direction = GetHorizontalFireDirection();

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
        projectile.Init(direction, transform);
        AudioManager.TryPlaySfx("slime_shot", 1f, Random.Range(0.97f, 1.05f));
    }

    private Vector2 GetHorizontalFireDirection()
    {
        ResolvePlayer();

        if (player == null)
            return Vector2.right;

        float deltaX = player.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= 0.001f)
            return Vector2.right;

        return deltaX >= 0f ? Vector2.right : Vector2.left;
    }

    private Vector2 GetSpawnPosition()
    {
        ResolvePlayer();
        float horizontalDirection = 1f;
        if (player != null)
        {
            float deltaX = player.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) > 0.001f)
                horizontalDirection = Mathf.Sign(deltaX);
        }

        return (Vector2)transform.position + new Vector2(spawnOffset.x * horizontalDirection, spawnOffset.y);
    }

    private void ConfigureProjectileVisual(Projectile projectile)
    {
        if (projectile == null)
            return;

        projectile.transform.localScale = new Vector3(projectileScale.x, projectileScale.y, 1f);

        SpriteRenderer projectileRenderer = projectile.GetComponent<SpriteRenderer>();
        if (projectileRenderer == null)
            return;

        projectileRenderer.enabled = true;

        if (projectileRenderer.sprite == null)
            projectileRenderer.sprite = GetFallbackProjectileSprite();

        if (ownerRenderer != null)
        {
            projectileRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
            projectileRenderer.sortingOrder = ownerRenderer.sortingOrder + 2;
        }

        projectileRenderer.color = projectileColor;

        CircleCollider2D projectileCollider = projectile.GetComponent<CircleCollider2D>();
        if (projectileCollider != null)
            projectileCollider.radius = Mathf.Max(0.05f, projectileColliderRadius);
    }

    private void ResolvePlayer()
    {
        if (player != null)
            return;

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
            player = playerMovement.transform;
    }

    private static Sprite GetFallbackProjectileSprite()
    {
        if (fallbackProjectileSprite != null)
            return fallbackProjectileSprite;

        Texture2D texture = new Texture2D(FallbackProjectileTextureSize, FallbackProjectileTextureSize, TextureFormat.RGBA32, false)
        {
            name = "SlimeProjectileFallback"
        };
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((FallbackProjectileTextureSize - 1) * 0.5f, (FallbackProjectileTextureSize - 1) * 0.5f);
        float radius = FallbackProjectileTextureSize * 0.5f - 1f;
        Vector2 highlightCenter = new Vector2(FallbackProjectileTextureSize * 0.34f, FallbackProjectileTextureSize * 0.7f);

        for (int y = 0; y < FallbackProjectileTextureSize; y++)
        {
            for (int x = 0; x < FallbackProjectileTextureSize; x++)
            {
                Vector2 pixelPosition = new Vector2(x, y);
                float distance = Vector2.Distance(pixelPosition, center) / radius;
                if (distance >= 1f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float highlight = Mathf.Clamp01(1f - Vector2.Distance(pixelPosition, highlightCenter) / (radius * 0.9f));
                float core = Mathf.Clamp01(1f - distance * 0.9f);
                float edgeFade = distance > 0.86f ? Mathf.Clamp01((1f - distance) / 0.14f) : 1f;
                float brightness = Mathf.Clamp01(0.28f + core * 0.42f + highlight * 0.36f);

                texture.SetPixel(x, y, new Color(brightness, brightness, brightness, edgeFade));
            }
        }

        texture.Apply();
        fallbackProjectileSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, FallbackProjectileTextureSize, FallbackProjectileTextureSize),
            new Vector2(0.5f, 0.5f),
            16f);
        return fallbackProjectileSprite;
    }
}
