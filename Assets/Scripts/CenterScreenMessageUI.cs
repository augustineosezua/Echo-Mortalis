using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CenterScreenMessageUI : MonoBehaviour
{
    [Header("Style")]
    [SerializeField] private TMP_FontAsset messageFont;
    [SerializeField] private Color textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color panelColor = new Color(0.02f, 0.03f, 0.05f, 0.82f);
    [SerializeField] private Vector2 panelSize = new Vector2(980f, 180f);
    [SerializeField] private float fontSize = 62f;

    private static CenterScreenMessageUI _instance;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI messageLabel;
    private Coroutine activeRoutine;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        BuildUi();
        HideImmediate();
    }

    public static void Show(string message, float fadeIn, float hold, float fadeOut)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        CenterScreenMessageUI instance = GetOrCreateInstance();
        if (instance == null)
            return;

        instance.ShowMessage(message, hold, fadeIn, fadeOut > 0);
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public Coroutine ShowMessage(string message, float holdDuration, float fadeDuration = 0.2f, bool fadeOut = true)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(ShowMessageRoutine(message, holdDuration, fadeDuration, fadeOut));
        return activeRoutine;
    }

    public void HideImmediate()
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = null;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator ShowMessageRoutine(string message, float holdDuration, float fadeDuration, bool fadeOut)
    {
        if (messageLabel == null || canvasGroup == null)
            yield break;

        messageLabel.text = message;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        yield return FadeCanvas(0f, 1f, fadeDuration);
        yield return WaitForSecondsRealtimeSafe(holdDuration);

        if (fadeOut)
            yield return FadeCanvas(1f, 0f, fadeDuration);
        else
            canvasGroup.alpha = 1f;

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
        GameObject canvasObject = new GameObject("EncounterMessageCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject groupObject = new GameObject("MessageGroup", typeof(RectTransform), typeof(CanvasGroup));
        groupObject.transform.SetParent(canvasObject.transform, false);

        RectTransform groupRect = groupObject.GetComponent<RectTransform>();
        groupRect.anchorMin = Vector2.zero;
        groupRect.anchorMax = Vector2.one;
        groupRect.offsetMin = Vector2.zero;
        groupRect.offsetMax = Vector2.zero;

        canvasGroup = groupObject.GetComponent<CanvasGroup>();

        GameObject panelObject = new GameObject("MessagePanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(groupObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = new Vector2(0f, 70f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = panelColor;

        GameObject textObject = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(35f, 20f);
        textRect.offsetMax = new Vector2(-35f, -20f);

        messageLabel = textObject.GetComponent<TextMeshProUGUI>();
        messageLabel.font = messageFont != null ? messageFont : TMP_Settings.defaultFontAsset;
        messageLabel.alignment = TextAlignmentOptions.Center;
        messageLabel.color = textColor;
        messageLabel.fontSize = fontSize;
        messageLabel.fontStyle = FontStyles.SmallCaps;
        messageLabel.textWrappingMode = TextWrappingModes.NoWrap;
        messageLabel.outlineWidth = 0.18f;
        messageLabel.outlineColor = new Color(0f, 0f, 0f, 0.95f);
    }

    private static CenterScreenMessageUI GetOrCreateInstance()
    {
        if (_instance != null)
            return _instance;

        GameObject host = new GameObject("CenterScreenMessageUI");
        return host.AddComponent<CenterScreenMessageUI>();
    }
}
