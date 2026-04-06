using System;
using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath;
    [SerializeField] private bool flashOnDeath;
    [SerializeField] private bool explodeOnDeath = true;
    [SerializeField] private int flashCount = 4;
    [SerializeField] private float flashInterval = 0.08f;
    [SerializeField] private float disappearDelay = 0.2f;
    [SerializeField] private float deathDelayBeforeHide = 0.05f;
    [SerializeField] private float hitKnockbackForce = 7f;
    [SerializeField] private float hitKnockbackLift = 1f;
    [SerializeField] private float explosionScale = 1.3f;
    [SerializeField] private float explosionDuration = 0.45f;
    [SerializeField] private Color explosionTint = new Color(1f, 0.55f, 0.12f, 1f);
    [SerializeField] private Sprite explosionRingSprite;
    [Header("Kill Reward")]
    [SerializeField] private int playerHealOnKill = 0;
    [Header("Audio")]
    [SerializeField] private string hurtCueId = "";
    [SerializeField] private string deathCueId = "";

    public bool isPlayer = false;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;
    private bool isDying;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    private static Sprite fallbackExplosionSprite;

    void Awake()
    {
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, Vector2.zero, -1f, null);
    }

    public void TakeDamage(float amount, Vector2 knockbackDirection, float knockbackForce = -1f)
    {
        TakeDamage(amount, knockbackDirection, knockbackForce, null);
    }

    public void TakeDamage(float amount, Vector2 knockbackDirection, float knockbackForce, Transform damageSource)
    {
        if (IsDead || amount <= 0f)
            return;

        Health damageSourceHealth = damageSource != null
            ? damageSource.GetComponentInParent<Health>()
            : null;

        Debug.Log($"[Combat] {gameObject.name} took {amount} damage.");
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

        if (knockbackDirection.sqrMagnitude > 0f)
            ApplyKnockback(knockbackDirection, knockbackForce < 0f ? hitKnockbackForce : knockbackForce);

        if (CurrentHealth <= 0f)
        {
            TryGrantPlayerKillReward(damageSourceHealth);
            Die();
            return;
        }

        PlayHurtAudio();
    }

    private void Die()
    {
        if (isDying)
            return;

        isDying = true;

        Debug.Log($"[Health] Death: {gameObject.name} at {transform.position}");
        PlayDeathAudio();
        OnDied?.Invoke();

        if (flashOnDeath)
        {
            StartCoroutine(PlayDeathFlashRoutine());
        }
        else
        {
            StartCoroutine(HideOrDestroyAfterDelay());
        }
    }

    private IEnumerator PlayDeathFlashRoutine()
    {
        var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < Mathf.Max(1, flashCount); i++)
        {
            SetSpriteRenderersVisible(spriteRenderers, false);
            yield return new WaitForSeconds(flashInterval);
            SetSpriteRenderersVisible(spriteRenderers, true);
            yield return new WaitForSeconds(flashInterval);
        }

        if (disappearDelay > 0f)
            yield return new WaitForSeconds(disappearDelay);

        SpawnExplosion();

        if (deathDelayBeforeHide > 0f)
            yield return new WaitForSeconds(deathDelayBeforeHide);

        HideOrDestroy();
    }

    private void SetSpriteRenderersVisible(SpriteRenderer[] spriteRenderers, bool visible)
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].enabled = visible;
        }
    }

    private void HideOrDestroy()
    {
        if (destroyOnDeath)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void ApplyKnockback(Vector2 direction, float force)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            return;

        rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        rb.angularVelocity = 0f;
        rb.rotation = 0f;

        Vector2 normalized = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
        if (normalized == Vector2.zero)
            return;

        Vector2 impulse = normalized * force;
        impulse.y += hitKnockbackLift;
        rb.AddForce(impulse, ForceMode2D.Impulse);
    }

    private IEnumerator PlayExplosionFlash()
    {
        GameObject explosion = new GameObject("DeathExplosion");
        explosion.transform.position = transform.position;
        var renderer = explosion.AddComponent<SpriteRenderer>();
        renderer.sprite = GetExplosionSprite();

        renderer.color = explosionTint;
        SpriteRenderer sourceRenderer = GetComponentInChildren<SpriteRenderer>();
        if (sourceRenderer != null)
        {
            renderer.sortingLayerName = sourceRenderer.sortingLayerName;
            renderer.sortingOrder = sourceRenderer.sortingOrder + 1;
        }
        else
        {
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 100;
        }

        Vector3 baseScale = Vector3.one * explosionScale;
        float elapsed = 0f;
        while (elapsed < explosionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / explosionDuration);
            explosion.transform.localScale = Vector3.Lerp(baseScale, baseScale * 2f, t);
            Color color = renderer.color;
            color.a = Mathf.Lerp(1f, 0f, t);
            renderer.color = color;
            yield return null;
        }

        if (explosion != null)
            Destroy(explosion);
    }

    private void SpawnExplosion()
    {
        if (!explodeOnDeath)
            return;

        StartCoroutine(PlayExplosionFlash());
    }

    private IEnumerator HideOrDestroyAfterDelay()
    {
        if (disappearDelay > 0f)
            yield return new WaitForSeconds(disappearDelay);

        SpawnExplosion();

        if (deathDelayBeforeHide > 0f)
            yield return new WaitForSeconds(deathDelayBeforeHide);

        HideOrDestroy();
    }

    public void SetHealth(int amount)
    {
        CurrentHealth = Mathf.Clamp(amount, 0, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
            return;

        float healedHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        if (Mathf.Approximately(healedHealth, CurrentHealth))
            return;

        CurrentHealth = healedHealth;
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    private Sprite GetExplosionSprite()
    {
        if (explosionRingSprite != null)
            return explosionRingSprite;

        if (fallbackExplosionSprite != null)
            return fallbackExplosionSprite;

        Texture2D texture = Texture2D.whiteTexture;
        fallbackExplosionSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            64f
        );
        return fallbackExplosionSprite;
    }

    private void PlayHurtAudio()
    {
        string cueId = ResolveHurtCueId();
        if (!string.IsNullOrWhiteSpace(cueId))
            AudioManager.TryPlaySfx(cueId);
    }

    private void PlayDeathAudio()
    {
        string cueId = ResolveDeathCueId();
        if (!string.IsNullOrWhiteSpace(cueId))
            AudioManager.TryPlaySfx(cueId);
    }

    private string ResolveHurtCueId()
    {
        if (!string.IsNullOrWhiteSpace(hurtCueId))
            return hurtCueId;

        if (isPlayer)
            return "player_hurt";

        if (IsBossLike())
            return "boss_hit";

        return "slime_hurt";
    }

    private string ResolveDeathCueId()
    {
        if (!string.IsNullOrWhiteSpace(deathCueId))
            return deathCueId;

        if (isPlayer)
            return string.Empty;

        if (IsBossLike())
            return string.Empty;

        return "enemy_death";
    }

    private bool IsBossLike()
    {
        return CompareTag("Boss") ||
            name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void TryGrantPlayerKillReward(Health damageSourceHealth)
    {
        if (isPlayer || damageSourceHealth == null)
            return;

        if (!damageSourceHealth.isPlayer || damageSourceHealth.IsDead)
            return;

        int healAmount = damageSourceHealth.playerHealOnKill > 0
            ? damageSourceHealth.playerHealOnKill
            : playerHealOnKill;
        if (healAmount <= 0)
            return;

        damageSourceHealth.Heal(healAmount);
    }
}
