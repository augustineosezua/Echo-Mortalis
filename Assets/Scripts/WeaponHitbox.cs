using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WeaponHitbox : MonoBehaviour
{
    public float damage = 20f;
    public float knockbackForce = 6f;
    public LayerMask targetLayers;
    [SerializeField] private float hitboxScaleMultiplier = 1.45f;
    [SerializeField] private float overlapFallbackRadius = 0.2f;
    [SerializeField] private float recoilMultiplier = 1.6f;

    private Transform ownerRoot;
    private bool hasHitThisSwing;
    private Vector3 originalScale;
    private bool hasScaled;

    void Awake()
    {
        originalScale = transform.localScale;
    }

    public void SetOwner(Transform owner)
    {
        ownerRoot = owner;
    }

    public void BeginSwing()
    {
        hasHitThisSwing = false;
        ApplyHitboxScale();
        gameObject.SetActive(true);
        TryOverlapFallback();
    }

    public void EndSwing()
    {
        ResetHitboxScale();
        gameObject.SetActive(false);
    }

    public void SetSwingScale(float scale, float overlapRadius = -1f)
    {
        hitboxScaleMultiplier = Mathf.Max(1f, scale);
        if (overlapRadius >= 0f)
            overlapFallbackRadius = overlapRadius;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHit(Collider2D other)
    {
        if (other == null || IsGroundTarget(other))
            return;

        Health ownerHealth = ownerRoot != null
            ? ownerRoot.GetComponentInParent<Health>()
            : null;

        if (ownerRoot != null)
        {
            if (ownerHealth != null && ownerHealth.IsDead)
                return;
        }

        if (hasHitThisSwing)
            return;

        if (ownerRoot != null &&
            (other.transform == ownerRoot ||
             other.transform.IsChildOf(ownerRoot) ||
             ownerRoot.IsChildOf(other.transform)))
            return;

        if (((1 << other.gameObject.layer) & targetLayers) == 0)
            return;

        Health targetHealth = other.GetComponentInParent<Health>();
        if (targetHealth == null)
            return;
        if (ownerHealth != null && targetHealth == ownerHealth)
            return;

        if (ownerRoot == null)
            Debug.Log($"[Combat] Unknown source dealt {damage} damage to {other.name}");
        else
            Debug.Log($"[Combat] {ownerRoot.name} dealt {damage} damage to {other.name}");

        Vector2 knockbackDirection = Vector2.zero;
        if (ownerRoot != null)
            knockbackDirection = (other.transform.position - ownerRoot.position).normalized;
        if (knockbackDirection == Vector2.zero)
            knockbackDirection = transform.right;

        targetHealth.TakeDamage(damage, knockbackDirection, knockbackForce * recoilMultiplier, ownerRoot);
        AudioManager.TryPlaySfx("sword_hit", 1f, Random.Range(0.97f, 1.04f));
        hasHitThisSwing = true;
        ResetHitboxScale();
        gameObject.SetActive(false);
    }

    private bool IsGroundTarget(Collider2D other)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        int terrainLayer = LayerMask.NameToLayer("Terrain");
        return (groundLayer >= 0 && other.gameObject.layer == groundLayer) ||
            (terrainLayer >= 0 && other.gameObject.layer == terrainLayer);
    }

    private void TryOverlapFallback()
    {
        if (hasHitThisSwing || overlapFallbackRadius <= 0f)
            return;

        if (transform == null)
            return;

        Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, overlapFallbackRadius, targetLayers);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (overlaps[i] == null)
                continue;

            TryHit(overlaps[i]);
            if (hasHitThisSwing)
                break;
        }
    }

    void OnDisable()
    {
        ResetHitboxScale();
    }

    private void ApplyHitboxScale()
    {
        if (hasScaled || transform == null)
            return;

        originalScale = transform.localScale;
        transform.localScale = originalScale * Mathf.Max(1f, hitboxScaleMultiplier);
        hasScaled = true;
    }

    private void ResetHitboxScale()
    {
        if (!hasScaled)
            return;

        transform.localScale = originalScale;
        hasScaled = false;
    }
}
