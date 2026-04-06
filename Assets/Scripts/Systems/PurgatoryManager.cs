using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class PurgatoryManager : MonoBehaviour
{
    private const string DefaultPlayerSpawnName = "PlayerSpawn";
    private const string DefaultEchoSpawnName = "EchoSpawn";

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [FormerlySerializedAs("enemyPrefab")]
    [SerializeField] private GameObject echoPrefab;

    [Header("Scene References")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private Transform echoSpawnPoint;

    [Header("Fallback Spawns")]
    [FormerlySerializedAs("playerSpawnPosition")]
    [SerializeField] private Vector3 fallbackPlayerSpawnPosition = new Vector3(-3.8f, -1.15f, 0f);
    [FormerlySerializedAs("enemySpawnPosition")]
    [SerializeField] private Vector3 fallbackEchoSpawnPosition = new Vector3(3.8f, -1.15f, 0f);

    [Header("Camera")]
    [SerializeField] private Color cameraBackgroundColor = new Color(0.03f, 0.07f, 0.09f, 1f);

    [Header("Messages")]
    [SerializeField] private string introMessage = "";
    [SerializeField] private string victoryMessage = "";
    [SerializeField] private string failureMessage = "";
    [SerializeField] private float introHold = 1.1f;
    [SerializeField] private float resultHold = 1.1f;
    [SerializeField] private float messageFade = 0.2f;

    private CenterScreenMessageUI messageUi;
    private GameObject playerInstance;
    private GameObject echoInstance;
    private PlayerMovement playerMovement;
    private PlayerWeaponController playerWeaponController;
    private EchoPurgatoryController echoController;
    private Health playerHealth;
    private Health echoHealth;
    private bool resolved;

    void Awake()
    {
        messageUi = GetComponent<CenterScreenMessageUI>();
        if (messageUi == null)
            messageUi = gameObject.AddComponent<CenterScreenMessageUI>();

        ResolveSpawnPoints();
    }

    private IEnumerator Start()
    {
        GamePersistence.ResolvePurgatory(false);
        ConfigureCamera();
        SpawnCombatants();
        SubscribeToHealthEvents();

        yield return RunIntroSequence();
    }

    void OnDestroy()
    {
        UnsubscribeFromHealthEvents();
    }

    private void ResolveSpawnPoints()
    {
        if (playerSpawnPoint == null)
            playerSpawnPoint = FindNamedTransform(DefaultPlayerSpawnName);

        if (echoSpawnPoint == null)
            echoSpawnPoint = FindNamedTransform(DefaultEchoSpawnName);
    }

    private static Transform FindNamedTransform(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.transform : null;
    }

    private void ConfigureCamera()
    {
        if (Camera.main != null)
            Camera.main.backgroundColor = cameraBackgroundColor;
    }

    private void SpawnCombatants()
    {
        if (playerPrefab == null || echoPrefab == null)
        {
            Debug.LogWarning("PurgatoryManager is missing one or more prefab references.", this);
            return;
        }

        Vector3 playerSpawnPosition = playerSpawnPoint != null ? playerSpawnPoint.position : fallbackPlayerSpawnPosition;
        Vector3 echoSpawnPosition = echoSpawnPoint != null ? echoSpawnPoint.position : fallbackEchoSpawnPosition;

        playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            playerInstance = playerMovement.gameObject;
            playerInstance.transform.position = playerSpawnPosition;
        }
        else
        {
            playerInstance = Instantiate(playerPrefab, playerSpawnPosition, Quaternion.identity);
            playerMovement = playerInstance.GetComponent<PlayerMovement>();
        }

        playerInstance.name = "PurgatoryPlayer";

        PlayerHealthBar playerHud = playerInstance.GetComponent<PlayerHealthBar>();
        if (playerHud != null)
            Destroy(playerHud);

        echoController = FindFirstObjectByType<EchoPurgatoryController>();
        if (echoController != null)
        {
            echoInstance = echoController.gameObject;
            echoInstance.transform.position = echoSpawnPosition;
        }
        else
        {
            echoInstance = Instantiate(echoPrefab, echoSpawnPosition, Quaternion.identity);
            echoController = echoInstance.GetComponent<EchoPurgatoryController>();
        }

        echoInstance.name = "PurgatoryEcho";

        playerWeaponController = playerInstance.GetComponent<PlayerWeaponController>();
        playerHealth = playerInstance.GetComponent<Health>();
        echoHealth = echoInstance.GetComponent<Health>();

        if (playerHealth != null)
            playerHealth.SetHealth(Mathf.CeilToInt(playerHealth.MaxHealth));

        if (echoHealth != null)
            echoHealth.SetHealth(Mathf.CeilToInt(echoHealth.MaxHealth));

        if (echoController != null)
            echoController.BindPlayer(playerInstance.transform);
    }

    private IEnumerator RunIntroSequence()
    {
        if (playerInstance == null || echoInstance == null)
            yield break;

        LockGameplay(true);

        if (echoController != null)
            echoController.PrepareForSpawnPresentation();

        yield return null;

        if (messageUi != null && !string.IsNullOrWhiteSpace(introMessage))
            yield return messageUi.ShowMessage(introMessage, introHold, messageFade);

        if (echoController != null)
            yield return echoController.PlaySpawnPresentation();

        LockGameplay(false);
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

    private void HandlePlayerDied()
    {
        if (resolved)
            return;

        resolved = true;
        StartCoroutine(HandleFailureSequence());
    }

    private void HandleEchoDied()
    {
        if (resolved)
            return;

        resolved = true;
        StartCoroutine(HandleVictorySequence());
    }

    private IEnumerator HandleVictorySequence()
    {
        LockGameplay(true);

        if (messageUi != null && !string.IsNullOrWhiteSpace(victoryMessage))
            yield return messageUi.ShowMessage(victoryMessage, resultHold, messageFade, false);

        yield return WaitForSecondsRealtimeSafe(0.25f);

        int returnSceneIndex = GamePersistence.ReturningFromPurgatory_ZoneIndex;
        GamePersistence.ResolvePurgatory(true);

        if (returnSceneIndex > 0)
        {
            SceneManager.LoadScene(returnSceneIndex);
            yield break;
        }

        CheckpointSystem.Reset();
        GamePersistence.Reset();
        SceneManager.LoadScene(0);
    }

    private IEnumerator HandleFailureSequence()
    {
        LockGameplay(true);

        if (messageUi != null && !string.IsNullOrWhiteSpace(failureMessage))
            yield return messageUi.ShowMessage(failureMessage, resultHold, messageFade, false);

        yield return WaitForSecondsRealtimeSafe(0.25f);

        CheckpointSystem.Reset();
        GamePersistence.Reset();
        SceneManager.LoadScene(0);
    }

    private void LockGameplay(bool isLocked)
    {
        if (playerMovement != null)
            playerMovement.SetInputLocked(isLocked);

        if (playerWeaponController != null)
            playerWeaponController.SetInputLocked(isLocked);

        if (echoController != null)
            echoController.SetEncounterActive(!isLocked);
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
