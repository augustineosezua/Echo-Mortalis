using System.Collections.Generic;
using UnityEngine;

public class EnemyContactDamage : MonoBehaviour
{
    public float damagePerHit = 4f;
    public float hitCooldown = 0.6f;
    public float extraAttackDelay = 0.35f;
    [Range(0f, 1f)] public float initialAttackDelay = 0.001f;
    public float knockbackForce = 7f;
    public LayerMask targetLayers = ~0;
    [Header("Player Contact Rules")]
    [SerializeField] private bool requireAggressiveMovementForPlayerDamage = true;
    [SerializeField] private float minimumAggressiveSpeed = 0.45f;
    [Range(-1f, 1f)] [SerializeField] private float minimumApproachDot = 0.1f;

    private IEnemyAttackVisual attackVisual;
    private Rigidbody2D ownerRigidbody;
    private readonly Dictionary<int, float> targetCooldowns = new Dictionary<int, float>();

    void Awake()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemyAttackVisual visual)
            {
                attackVisual = visual;
                break;
            }
        }

        ownerRigidbody = GetComponentInParent<Rigidbody2D>();
        if (ownerRigidbody == null)
            ownerRigidbody = GetComponent<Rigidbody2D>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamage(collision.collider);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryDamage(collision.collider);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        TryClearTargetCooldown(collision.collider);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        TryClearTargetCooldown(other);
    }

    private void TryDamage(Collider2D other)
    {
        if (other == null || IsGroundTarget(other))
            return;

        if (!other.gameObject.activeInHierarchy)
            return;

        if (targetLayers.value != 0 && ((1 << other.gameObject.layer) & targetLayers.value) == 0)
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health == null || health.IsDead)
            return;

        Health attackerHealth = GetComponentInParent<Health>();
        if (attackerHealth != null && attackerHealth.isPlayer == health.isPlayer)
            return;

        int targetKey = health.GetInstanceID();
        if (!targetCooldowns.TryGetValue(targetKey, out float nextAvailableTime))
        {
            targetCooldowns[targetKey] = Time.time + initialAttackDelay;
            return;
        }

        if (Time.time < nextAvailableTime)
            return;

        bool canDamageTarget = CanApplyDamageToTarget(health);
        attackVisual?.PlayAttackVisual();
        targetCooldowns[targetKey] = Time.time + hitCooldown + extraAttackDelay;
        if (!canDamageTarget)
            return;

        string sourceName = gameObject.name;
        if (attackerHealth != null)
            sourceName = attackerHealth.name;

        Debug.Log($"[Combat] {sourceName} dealt {damagePerHit} contact damage to {other.name}");

        Vector2 knockbackDirection = transform.position.x < other.transform.position.x ? Vector2.right : Vector2.left;
        health.TakeDamage(damagePerHit, knockbackDirection, knockbackForce, transform);
    }

    private bool CanApplyDamageToTarget(Health targetHealth)
    {
        if (targetHealth == null || !targetHealth.isPlayer)
            return true;

        if (!requireAggressiveMovementForPlayerDamage)
            return true;

        if (ownerRigidbody == null)
            return false;

        Vector2 velocity = ownerRigidbody.linearVelocity;
        if (velocity.sqrMagnitude < minimumAggressiveSpeed * minimumAggressiveSpeed)
            return false;

        Vector2 toTarget = (Vector2)(targetHealth.transform.position - transform.position);
        if (toTarget.sqrMagnitude <= 0.0001f)
            return false;

        float approachDot = Vector2.Dot(velocity.normalized, toTarget.normalized);
        return approachDot >= minimumApproachDot;
    }

    private bool IsGroundTarget(Collider2D other)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        int terrainLayer = LayerMask.NameToLayer("Terrain");
        return (groundLayer >= 0 && other.gameObject.layer == groundLayer) ||
            (terrainLayer >= 0 && other.gameObject.layer == terrainLayer);
    }

    private void TryClearTargetCooldown(Collider2D other)
    {
        if (other == null)
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health == null)
            return;

        int targetKey = health.GetInstanceID();
        targetCooldowns.Remove(targetKey);
    }
}
