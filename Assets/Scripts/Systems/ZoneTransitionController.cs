using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ZoneTransitionController : MonoBehaviour
{
    [SerializeField] private int targetSceneIndex = -1;
    [SerializeField] private string targetSceneName = string.Empty;
    [SerializeField] private bool requireAllEnemiesDefeated = true;
    [SerializeField] private string blockedMessage = "";
    [SerializeField] private string blockedCueId = string.Empty;
    [SerializeField] private string transitionCueId = "zone_transition";

    private bool isLoading;

    void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isLoading)
            return;

        PlayerMovement playerMovement = other.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null)
            return;

        if (requireAllEnemiesDefeated && HasAliveEnemies())
        {
            AudioManager.TryPlaySfx(blockedCueId);

            if (!string.IsNullOrWhiteSpace(blockedMessage))
                CenterScreenMessageUI.Show(blockedMessage, 0.12f, 0.85f, 0.2f);
            return;
        }

        if (!TryResolveTarget(out int resolvedSceneIndex, out string resolvedSceneName))
        {
            Debug.LogWarning("ZoneTransitionController has no valid target scene configured.", this);
            return;
        }

        isLoading = true;
        AudioManager.TryPlaySfx(transitionCueId);

        if (resolvedSceneIndex >= 0)
        {
            SceneManager.LoadScene(resolvedSceneIndex);
            return;
        }

        if (!string.IsNullOrWhiteSpace(resolvedSceneName))
        {
            SceneManager.LoadScene(resolvedSceneName);
            return;
        }

        isLoading = false;
    }

    private bool TryResolveTarget(out int resolvedSceneIndex, out string resolvedSceneName)
    {
        resolvedSceneIndex = -1;
        resolvedSceneName = string.Empty;

        if (targetSceneIndex >= 0)
        {
            resolvedSceneIndex = targetSceneIndex;
            return true;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
            return false;

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
            return false;

        resolvedSceneName = targetSceneName;
        return true;
    }

    private bool HasAliveEnemies()
    {
        Health[] healthComponents = FindObjectsByType<Health>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < healthComponents.Length; i++)
        {
            Health health = healthComponents[i];
            if (health == null || health.isPlayer || health.IsDead)
                continue;

            if (health.CompareTag("Enemy") || health.GetComponent<EnemyRandomFollower>() != null)
                return true;
        }

        return false;
    }
}
