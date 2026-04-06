using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-425)]
[DisallowMultipleComponent]
public class PlayerDeathToPurgatory : MonoBehaviour
{
    [SerializeField] private int purgatorySceneBuildIndex = 4;
    [SerializeField] private float loadDelay = 1.2f;
    [SerializeField] private bool enableFallDeath = true;
    [SerializeField] private float fallDeathY = -10f;

    private Health playerHealth;
    private Transform playerTransform;
    private bool subscribed;
    private bool loading;

    private IEnumerator Start()
    {
        yield return null;
        SubscribeToPlayerDeath();
    }

    void OnDisable()
    {
        UnsubscribeFromPlayerDeath();
        loading = false;
    }

    void OnDestroy()
    {
        UnsubscribeFromPlayerDeath();
    }

    void Update()
    {
        TryHandleFallDeath();
    }

    private void SubscribeToPlayerDeath()
    {
        if (subscribed)
            return;

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement == null)
        {
            Debug.LogWarning("PlayerDeathToPurgatory could not find the player in the active scene.", this);
            return;
        }

        playerHealth = playerMovement.GetComponent<Health>();
        playerTransform = playerMovement.transform;
        if (playerHealth == null)
        {
            Debug.LogWarning("PlayerDeathToPurgatory could not find a Health component on the player.", this);
            return;
        }

        playerHealth.OnDied += HandlePlayerDied;
        subscribed = true;
    }

    private void UnsubscribeFromPlayerDeath()
    {
        if (!subscribed)
            return;

        if (playerHealth != null)
            playerHealth.OnDied -= HandlePlayerDied;

        playerHealth = null;
        playerTransform = null;
        subscribed = false;
    }

    private void HandlePlayerDied()
    {
        if (loading)
            return;

        if (purgatorySceneBuildIndex < 0)
        {
            Debug.LogWarning("PlayerDeathToPurgatory has no valid Purgatory scene build index configured.", this);
            return;
        }

        loading = true;
        GamePersistence.SetPurgatoryReturn(SceneManager.GetActiveScene().buildIndex);
        StartCoroutine(LoadPurgatoryAfterDelay());
    }

    private void TryHandleFallDeath()
    {
        if (!enableFallDeath || loading || !subscribed || playerHealth == null || playerTransform == null)
            return;

        if (playerHealth.IsDead || playerTransform.position.y > fallDeathY)
            return;

        playerHealth.TakeDamage(Mathf.Max(1f, playerHealth.CurrentHealth), Vector2.down, 0f);
    }

    private IEnumerator LoadPurgatoryAfterDelay()
    {
        if (loadDelay > 0f)
        {
            float elapsed = 0f;
            while (elapsed < loadDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        SceneManager.LoadScene(purgatorySceneBuildIndex);
    }
}
