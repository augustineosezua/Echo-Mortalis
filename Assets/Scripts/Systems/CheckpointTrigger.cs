using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CheckpointTrigger : MonoBehaviour
{
    private const string DefaultCheckpointMessage = "Checkpoint reached.";

    [SerializeField] private bool oneShot = true;
    [SerializeField] private string checkpointMessage = "";
    [SerializeField] private string checkpointCueId = "checkpoint";

    private bool activated;

    void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated && oneShot)
            return;

        PlayerMovement playerMovement = other.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null)
            return;

        GameObject player = playerMovement.gameObject;
        Health health = player.GetComponent<Health>();
        PlayerWeaponController weaponController = player.GetComponent<PlayerWeaponController>();

        int hp = health != null ? Mathf.CeilToInt(health.CurrentHealth) : CheckpointSystem.DefaultSavedHealth;
        int weaponIndex = weaponController != null ? weaponController.CurrentWeaponIndex : CheckpointSystem.DefaultSavedWeaponIndex;
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;

        CheckpointSystem.Save(sceneIndex, player.transform.position, hp, weaponIndex);
        activated = true;

        string messageToShow = string.IsNullOrWhiteSpace(checkpointMessage)
            ? DefaultCheckpointMessage
            : checkpointMessage;

        CenterScreenMessageUI.Show(messageToShow, 0.12f, 0.85f, 0.2f);

        // The default checkpoint cue is a spoken line, so we replace it with text.
        if (!string.IsNullOrWhiteSpace(checkpointCueId) &&
            !string.Equals(checkpointCueId, "checkpoint"))
        {
            AudioManager.TryPlaySfx(checkpointCueId);
        }
    }
}
