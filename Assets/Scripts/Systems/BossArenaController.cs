using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class BossArenaController : MonoBehaviour
{
    [SerializeField] private BossEchoNexus boss;
    [SerializeField] private Collider2D leftBarrier;
    [SerializeField] private Collider2D rightBarrier;
    [SerializeField] private SpriteRenderer leftBarrierRenderer;
    [SerializeField] private SpriteRenderer rightBarrierRenderer;

    [Header("Presentation")]
    [SerializeField] private string bossTitle = "Bringer of Death";
    [SerializeField] private string introMessage = "The nexus answers.";
    [SerializeField] private string victoryMessage = "The path beyond is clear.";
    [SerializeField] private float introLockBuffer = 0.08f;

    [Header("Victory")]
    [SerializeField] private float victoryDelay = 2.25f;
    [SerializeField] private int creditsSceneBuildIndex = 5;

    private Collider2D entryTrigger;
    private bool encounterStarted;
    private bool resolvingVictory;

    void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    void Awake()
    {
        entryTrigger = GetComponent<Collider2D>();
        SetBarrierState(false);
        BossHealthBar.Hide(true);
    }

    void OnEnable()
    {
        if (boss != null)
            boss.BossDefeated += HandleBossDefeated;
    }

    void OnDisable()
    {
        if (boss != null)
            boss.BossDefeated -= HandleBossDefeated;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (encounterStarted)
            return;

        PlayerMovement playerMovement = other.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null)
            return;

        encounterStarted = true;
        if (entryTrigger != null)
            entryTrigger.enabled = false;

        StartCoroutine(BeginEncounterRoutine(playerMovement));
    }

    private IEnumerator BeginEncounterRoutine(PlayerMovement playerMovement)
    {
        SetBarrierState(true);
        SetPlayerInputLocked(playerMovement, true);

        if (!string.IsNullOrWhiteSpace(introMessage))
            CenterScreenMessageUI.Show(introMessage, 0.12f, 0.9f, 0.2f);

        if (boss != null)
        {
            BossHealthBar.ShowFor(boss.HealthComponent, bossTitle);
            boss.BeginEncounter();
        }

        float introDuration = boss != null ? boss.IntroDuration : 0.85f;
        yield return WaitForSecondsRealtimeSafe(Mathf.Max(0.05f, introDuration + introLockBuffer));

        SetPlayerInputLocked(playerMovement, false);
    }

    private void HandleBossDefeated(BossEchoNexus defeatedBoss)
    {
        if (resolvingVictory)
            return;

        resolvingVictory = true;
        SetBarrierState(false);
        BossHealthBar.Hide();

        if (!string.IsNullOrWhiteSpace(victoryMessage))
            CenterScreenMessageUI.Show(victoryMessage, 0.12f, 1.05f, 0.2f);

        StartCoroutine(LoadCreditsRoutine());
    }

    private IEnumerator LoadCreditsRoutine()
    {
        yield return WaitForSecondsRealtimeSafe(victoryDelay);

        if (creditsSceneBuildIndex < 0)
        {
            Debug.LogWarning("BossArenaController has no valid Credits scene build index configured.", this);
            yield break;
        }

        SceneManager.LoadScene(creditsSceneBuildIndex);
    }

    private void SetPlayerInputLocked(PlayerMovement playerMovement, bool isLocked)
    {
        if (playerMovement != null)
            playerMovement.SetInputLocked(isLocked);

        PlayerWeaponController weaponController = playerMovement != null
            ? playerMovement.GetComponent<PlayerWeaponController>()
            : null;
        if (weaponController != null)
            weaponController.SetInputLocked(isLocked);
    }

    private void SetBarrierState(bool active)
    {
        if (leftBarrier != null)
            leftBarrier.enabled = active;
        if (rightBarrier != null)
            rightBarrier.enabled = active;
        if (leftBarrierRenderer != null)
            leftBarrierRenderer.enabled = active;
        if (rightBarrierRenderer != null)
            rightBarrierRenderer.enabled = active;
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
