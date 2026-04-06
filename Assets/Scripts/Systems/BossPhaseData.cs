using System;
using UnityEngine;

[Serializable]
public class BossPhaseData
{
    [Header("Movement")]
    [Min(0f)] public float moveSpeed = 3.4f;
    [Min(0f)] public float acceleration = 18f;
    [Min(0f)] public float preferredDistance = 4.5f;
    [Min(0f)] public float distanceTolerance = 0.7f;

    [Header("Dash")]
    [Min(0f)] public float dashTriggerRange = 5.6f;
    [Min(0f)] public float dashCooldown = 2.15f;
    [Min(0f)] public float dashWindup = 0.42f;
    [Min(0f)] public float dashSpeed = 14f;
    [Min(0f)] public float dashDuration = 0.22f;
    [Min(1)] public int dashRepeatCount = 1;
    [Min(0f)] public float dashRepeatDelay = 0.14f;
    [Min(0f)] public float dashRecovery = 0.34f;
    [Min(0f)] public float dashDamage = 18f;
    [Min(0f)] public float dashKnockback = 6f;
    [Min(1f)] public float dashHitboxScale = 1.8f;
    [Min(0f)] public float dashOverlapRadius = 0.35f;

    [Header("Projectiles")]
    [Min(0f)] public float projectileMinRange = 3.25f;
    [Min(0f)] public float projectileMaxRange = 14f;
    [Min(0f)] public float projectileCooldown = 2.8f;
    [Min(0f)] public float projectileWindup = 0.58f;
    [Min(0f)] public float projectileRecovery = 0.28f;
    [Min(1)] public int projectileBurstCount = 1;
    [Min(0f)] public float projectileBurstInterval = 0.16f;
    [Min(1)] public int projectileCount = 5;
    [Range(0f, 180f)] public float projectileSpreadAngle = 42f;
    [Min(0f)] public float projectileSpeed = 8.5f;
    [Min(0f)] public float projectileDamage = 10f;
    [Min(0f)] public float projectileLifetime = 3f;
    [Min(0f)] public float projectileKnockback = 5f;
    public Color projectileColor = new Color(0.32f, 0.92f, 0.76f, 1f);

    [Header("Flow")]
    [Min(0f)] public float postAttackDecisionDelay = 0.14f;

    public void ClampValues()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        preferredDistance = Mathf.Max(0.5f, preferredDistance);
        distanceTolerance = Mathf.Max(0.05f, distanceTolerance);

        dashTriggerRange = Mathf.Max(0f, dashTriggerRange);
        dashCooldown = Mathf.Max(0f, dashCooldown);
        dashWindup = Mathf.Max(0f, dashWindup);
        dashSpeed = Mathf.Max(0f, dashSpeed);
        dashDuration = Mathf.Max(0f, dashDuration);
        dashRepeatCount = Mathf.Max(1, dashRepeatCount);
        dashRepeatDelay = Mathf.Max(0f, dashRepeatDelay);
        dashRecovery = Mathf.Max(0f, dashRecovery);
        dashDamage = Mathf.Max(0f, dashDamage);
        dashKnockback = Mathf.Max(0f, dashKnockback);
        dashHitboxScale = Mathf.Max(1f, dashHitboxScale);
        dashOverlapRadius = Mathf.Max(0f, dashOverlapRadius);

        projectileMinRange = Mathf.Max(0f, projectileMinRange);
        projectileMaxRange = Mathf.Max(projectileMinRange, projectileMaxRange);
        projectileCooldown = Mathf.Max(0f, projectileCooldown);
        projectileWindup = Mathf.Max(0f, projectileWindup);
        projectileRecovery = Mathf.Max(0f, projectileRecovery);
        projectileBurstCount = Mathf.Max(1, projectileBurstCount);
        projectileBurstInterval = Mathf.Max(0f, projectileBurstInterval);
        projectileCount = Mathf.Max(1, projectileCount);
        projectileSpreadAngle = Mathf.Clamp(projectileSpreadAngle, 0f, 180f);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileDamage = Mathf.Max(0f, projectileDamage);
        projectileLifetime = Mathf.Max(0f, projectileLifetime);
        projectileKnockback = Mathf.Max(0f, projectileKnockback);
        postAttackDecisionDelay = Mathf.Max(0f, postAttackDecisionDelay);
    }
}
