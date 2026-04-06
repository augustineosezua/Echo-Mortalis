using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ZoneNameCard : MonoBehaviour
{
    [Header("Style")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private Color titleColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color subtitleColor = new Color(0.7f, 0.78f, 0.86f, 1f);
    [SerializeField] private Color panelColor = new Color(0.02f, 0.03f, 0.05f, 0.78f);
    [SerializeField] private Vector2 panelSize = new Vector2(560f, 120f);
    [SerializeField] private float titleFontSize = 34f;
    [SerializeField] private float subtitleFontSize = 18f;
    [SerializeField] private float holdDuration = 2f;
    [SerializeField] private float fadeDuration = 0.3f;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI subtitleLabel;
    private Coroutine activeRoutine;

    void Awake()
    {
        BuildUi();
        HideImmediate();
    }

    public void ShowCard(string title, string subtitle = "")
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(ShowCardRoutine(title, subtitle));
    }

    public void HideImmediate()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = null;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private IEnumerator ShowCardRoutine(string title, string subtitle)
    {
        if (titleLabel == null || subtitleLabel == null || canvasGroup == null)
            yield break;

        titleLabel.text = title;
        subtitleLabel.text = subtitle;
        subtitleLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(subtitle));

        yield return FadeCanvas(0f, 1f, fadeDuration);
        yield return WaitForSecondsRealtimeSafe(holdDuration);
        yield return FadeCanvas(1f, 0f, fadeDuration);
        activeRoutine = null;
    }

    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (canvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
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

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("ZoneNameCardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("CardPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = new Vector2(0f, -48f);

        canvasGroup = panelObject.GetComponent<CanvasGroup>();

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = panelColor;

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(panelObject.transform, false);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.4f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(24f, -6f);
        titleRect.offsetMax = new Vector2(-24f, -14f);

        titleLabel = titleObject.GetComponent<TextMeshProUGUI>();
        titleLabel.font = titleFont != null ? titleFont : TMP_Settings.defaultFontAsset;
        titleLabel.fontSize = titleFontSize;
        titleLabel.color = titleColor;
        titleLabel.alignment = TextAlignmentOptions.Center;
        titleLabel.fontStyle = FontStyles.SmallCaps;

        GameObject subtitleObject = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subtitleObject.transform.SetParent(panelObject.transform, false);

        RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0f, 0f);
        subtitleRect.anchorMax = new Vector2(1f, 0.48f);
        subtitleRect.offsetMin = new Vector2(24f, 10f);
        subtitleRect.offsetMax = new Vector2(-24f, -6f);

        subtitleLabel = subtitleObject.GetComponent<TextMeshProUGUI>();
        subtitleLabel.font = titleFont != null ? titleFont : TMP_Settings.defaultFontAsset;
        subtitleLabel.fontSize = subtitleFontSize;
        subtitleLabel.color = subtitleColor;
        subtitleLabel.alignment = TextAlignmentOptions.Center;
    }
}
