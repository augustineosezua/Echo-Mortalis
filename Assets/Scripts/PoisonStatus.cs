using UnityEngine;

public class PoisonStatus : MonoBehaviour
{
    private Health health;
    private float damagePerTick;
    private float tickInterval;
    private float remainingTime;
    private float tickTimer;

    void Awake()
    {
        health = GetComponent<Health>();
    }

    public void Apply(float poisonDamagePerTick, float poisonTickInterval, float poisonDuration)
    {
        damagePerTick = poisonDamagePerTick;
        tickInterval = Mathf.Max(0.05f, poisonTickInterval);
        remainingTime = Mathf.Max(remainingTime, poisonDuration);
        tickTimer = 0f;
        enabled = true;
    }

    void Update()
    {
        if (health == null || health.IsDead || remainingTime <= 0f)
        {
            enabled = false;
            return;
        }

        remainingTime -= Time.deltaTime;
        tickTimer -= Time.deltaTime;

        if (tickTimer <= 0f)
        {
            health.TakeDamage(damagePerTick);
            tickTimer = tickInterval;
        }
    }
}
