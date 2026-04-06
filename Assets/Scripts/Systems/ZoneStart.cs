using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-450)]
[DisallowMultipleComponent]
[RequireComponent(typeof(ZoneNameCard))]
public class ZoneStart : MonoBehaviour
{
    [SerializeField] private string zoneTitle = "";
    [SerializeField] private string zoneSubtitle = "";
    [SerializeField] private float cardDelay = 0.25f;
    [SerializeField] private bool showCardOnStart = false;
    [SerializeField] private bool saveSpawnAsFallbackCheckpoint = true;
    [SerializeField] private bool restoreCheckpointOnPurgatoryReturn = true;

    private ZoneNameCard zoneNameCard;

    void Awake()
    {
        zoneNameCard = GetComponent<ZoneNameCard>();
    }

    private IEnumerator Start()
    {
        yield return null;

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement == null)
            yield break;

        GameObject player = playerMovement.gameObject;
        int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;

        bool shouldRestoreCheckpoint = restoreCheckpointOnPurgatoryReturn &&
            GamePersistence.ShouldRestoreCheckpointForScene(activeSceneIndex);

        if (shouldRestoreCheckpoint)
        {
            if (CheckpointSystem.HasCheckpointForScene(activeSceneIndex))
                CheckpointSystem.RestorePlayerTo(player);
            else if (saveSpawnAsFallbackCheckpoint)
                SaveCheckpoint(activeSceneIndex, player);

            GamePersistence.Reset();
        }
        else if (GamePersistence.IsReturningFromPurgatory &&
                 GamePersistence.ReturningFromPurgatory_ZoneIndex == activeSceneIndex &&
                 !GamePersistence.PurgatoryWon)
        {
            GamePersistence.Reset();

            if (saveSpawnAsFallbackCheckpoint && !CheckpointSystem.HasCheckpointForScene(activeSceneIndex))
                SaveCheckpoint(activeSceneIndex, player);
        }
        else if (saveSpawnAsFallbackCheckpoint &&
                 !CheckpointSystem.HasCheckpointForScene(activeSceneIndex))
        {
            SaveCheckpoint(activeSceneIndex, player);
        }

        if (!showCardOnStart || zoneNameCard == null || string.IsNullOrWhiteSpace(zoneTitle))
            yield break;

        yield return WaitForSecondsRealtimeSafe(cardDelay);
        zoneNameCard.ShowCard(zoneTitle, zoneSubtitle);
    }

    private void SaveCheckpoint(int zoneIndex, GameObject player)
    {
        if (player == null)
            return;

        Health health = player.GetComponent<Health>();
        PlayerWeaponController weaponController = player.GetComponent<PlayerWeaponController>();

        int hp = health != null ? Mathf.CeilToInt(health.CurrentHealth) : CheckpointSystem.DefaultSavedHealth;
        int weaponIndex = weaponController != null ? weaponController.CurrentWeaponIndex : CheckpointSystem.DefaultSavedWeaponIndex;

        CheckpointSystem.Save(zoneIndex, player.transform.position, hp, weaponIndex);
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
