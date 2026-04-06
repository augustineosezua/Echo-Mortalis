using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-500)]
public class EchoEncounterController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";

    [Header("Messages")]
    [SerializeField] private string introMessage = "";
    [SerializeField] private string lossMessage = "";
    [SerializeField] private string victoryMessage = "";

    [Header("Timing")]
    [SerializeField] private float introMessageDuration = 1.4f;
    [SerializeField] private float introMessageFade = 0.22f;
    [SerializeField] private float introGapBeforeSpawn = 0.15f;
    [SerializeField] private float outcomeMessageDuration = 1.75f;
    [SerializeField] private float outcomeMessageFade = 0.2f;
    [SerializeField] private float sceneTransitionDelay = 0.2f;

    [Header("Victory Flow")]
    [SerializeField] private bool loadSceneOnVictory;
    [SerializeField] private string victorySceneName = string.Empty;
    [SerializeField] private int victorySceneBuildIndex = -1;

    private CenterScreenMessageUI messageUi;
    private PlayerMovement playerMovement;
    private PlayerWeaponController playerWeaponController;
    private Health playerHealth;
    private EnemyRandomFollower echoEnemy;
    private Health echoHealth;
    private bool encounterResolved;
    private Coroutine outcomeRoutine;

    void Awake()
    {
        CacheSceneReferences();
        if (!HasRequiredReferences())
        {
            Destroy(gameObject);
            return;
        }

        messageUi = gameObject.AddComponent<CenterScreenMessageUI>();
        echoEnemy.PrepareForSpawnPresentation();
    }

    private IEnumerator Start()
    {
        if (!HasRequiredReferences() || messageUi == null)
            yield break;

        SubscribeToHealthEvents();
        yield return RunIntroSequence();
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        UnsubscribeFromHealthEvents();
    }

    private void CacheSceneReferences()
    {
        playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
            playerHealth = playerMovement.GetComponent<Health>();

        playerWeaponController = FindFirstObjectByType<PlayerWeaponController>();
        echoEnemy = FindFirstObjectByType<EnemyRandomFollower>();
        if (echoEnemy != null)
            echoHealth = echoEnemy.GetComponent<Health>();
    }

    private bool HasRequiredReferences()
    {
        return playerMovement != null
            && playerWeaponController != null
            && playerHealth != null
            && echoEnemy != null
            && echoHealth != null;
    }

    private void SubscribeToHealthEvents()
    {
        if (playerHealth != null)
            playerHealth.OnDied += HandlePlayerDied;

        if (echoHealth != null)
            echoHealth.OnDied += HandleEchoDied;
    }

    private void UnsubscribeFromHealthEvents()
    {
        if (playerHealth != null)
            playerHealth.OnDied -= HandlePlayerDied;

        if (echoHealth != null)
            echoHealth.OnDied -= HandleEchoDied;
    }

    private IEnumerator RunIntroSequence()
    {
        LockGameplay(true);
        Time.timeScale = 0f;

        if (!string.IsNullOrWhiteSpace(introMessage))
            yield return messageUi.ShowMessage(introMessage, introMessageDuration, introMessageFade);
        yield return WaitForSecondsRealtimeSafe(introGapBeforeSpawn);
        yield return echoEnemy.PlaySpawnPresentation();

        Time.timeScale = 1f;
        LockGameplay(false);
    }

    private void HandlePlayerDied()
    {
        if (encounterResolved)
            return;

        encounterResolved = true;
        if (outcomeRoutine != null)
            StopCoroutine(outcomeRoutine);

        GamePersistence.SetPurgatoryReturn(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        outcomeRoutine = StartCoroutine(HandlePlayerLossSequence());
    }

    private IEnumerator HandlePlayerLossSequence()
    {
        LockGameplay(true);

        if (messageUi != null && !string.IsNullOrWhiteSpace(lossMessage))
        {
            messageUi.ShowMessage(lossMessage, Mathf.Min(outcomeMessageDuration, 1.1f), outcomeMessageFade, false);
        }

        yield return WaitForSecondsRealtimeSafe(1.2f);
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(4);
    }

    private void HandleEchoDied()
    {
        if (encounterResolved)
            return;

        encounterResolved = true;
        if (outcomeRoutine != null)
            StopCoroutine(outcomeRoutine);

        outcomeRoutine = StartCoroutine(HandleVictorySequence());
    }

    private IEnumerator HandleVictorySequence()
    {
        LockGameplay(true);
        if (messageUi != null && !string.IsNullOrWhiteSpace(victoryMessage))
            yield return messageUi.ShowMessage(victoryMessage, outcomeMessageDuration, outcomeMessageFade, false);

        yield return WaitForSecondsRealtimeSafe(sceneTransitionDelay);

        Time.timeScale = 1f;
        if (TryLoadVictoryScene())
            yield break;

        LockGameplay(false);
    }

    private void LockGameplay(bool isLocked)
    {
        if (playerMovement != null)
            playerMovement.SetInputLocked(isLocked);

        if (playerWeaponController != null)
            playerWeaponController.SetInputLocked(isLocked);

        if (echoEnemy != null)
            echoEnemy.SetDormant(isLocked);
    }

    private void ReloadGameplayScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void LoadMainMenuScene()
    {
        if (Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            SceneManager.LoadScene(MainMenuSceneName);
        else
            SceneManager.LoadScene(0);
    }

    private bool TryLoadVictoryScene()
    {
        if (!loadSceneOnVictory)
            return false;

        if (victorySceneBuildIndex >= 0)
        {
            SceneManager.LoadScene(victorySceneBuildIndex);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(victorySceneName))
        {
            SceneManager.LoadScene(victorySceneName);
            return true;
        }

        LoadMainMenuScene();
        return true;
    }

    private IEnumerator WaitForSecondsRealtimeSafe(float duration)
    {
        if (duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
