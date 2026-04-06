using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    public float speed = 14f;
    public float lifetime = 2f;
    public float damage = 15f;
    public LayerMask targetLayers;
    public bool applyPoison;
    public float poisonDamagePerTick = 2f;
    public float poisonTickInterval = 0.5f;
    public float poisonDuration = 3f;
    public float knockbackForce = 7f;

    private Vector2 direction = Vector2.right;
    private Transform ownerRoot;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        Destroy(gameObject, lifetime);
    }

    public void Init(Vector2 moveDirection, Transform owner)
    {
        direction = moveDirection.normalized;
        ownerRoot = owner;
    }

    void FixedUpdate()
    {
        rb.linearVelocity = direction * speed;
    }

    void OnTriggerEnter2D(Collider2D other)
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

        if (ownerRoot != null &&
            (other.transform == ownerRoot ||
             other.transform.IsChildOf(ownerRoot) ||
             ownerRoot.IsChildOf(other.transform)))
            return;

        if (((1 << other.gameObject.layer) & targetLayers) == 0)
            return;

        Health targetHealth = other.GetComponentInParent<Health>();
        if (ownerHealth != null && targetHealth == ownerHealth)
            return;
        if (IsFriendlyTarget(targetHealth))
            return;

        Debug.Log($"[Combat] {(ownerRoot != null ? ownerRoot.name : "Projectile")} dealt {damage} damage to {other.name}");

        if (targetHealth != null)
        {
            Vector2 hitDirection = direction;
            if ((other.transform.position - transform.position).sqrMagnitude > 0f)
                hitDirection = (other.transform.position - transform.position).normalized;

            targetHealth.TakeDamage(damage, hitDirection, knockbackForce, ownerRoot);

            if (applyPoison)
            {
                PoisonStatus poison = targetHealth.GetComponent<PoisonStatus>();
                if (poison == null)
                    poison = targetHealth.gameObject.AddComponent<PoisonStatus>();

                poison.Apply(poisonDamagePerTick, poisonTickInterval, poisonDuration);
            }
        }

        Destroy(gameObject);
    }

    private bool IsGroundTarget(Collider2D other)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        int terrainLayer = LayerMask.NameToLayer("Terrain");
        return (groundLayer >= 0 && other.gameObject.layer == groundLayer) ||
            (terrainLayer >= 0 && other.gameObject.layer == terrainLayer);
    }

    private bool IsFriendlyTarget(Health targetHealth)
    {
        if (targetHealth == null || ownerRoot == null)
            return false;

        Health ownerHealth = ownerRoot.GetComponentInParent<Health>();
        if (ownerHealth == null)
            return false;

        return ownerHealth.isPlayer == targetHealth.isPlayer;
    }
}
