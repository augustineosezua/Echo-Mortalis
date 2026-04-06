using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossHealthBar : MonoBehaviour
{
    [Header("Style")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private Color titleColor = new Color(0.95f, 0.96f, 0.94f, 1f);
    [SerializeField] private Color panelColor = new Color(0.03f, 0.05f, 0.07f, 0.9f);
    [SerializeField] private Color barBackgroundColor = new Color(0.12f, 0.14f, 0.16f, 0.96f);
    [SerializeField] private Color fillColor = new Color(0.86f, 0.2f, 0.18f, 1f);
    [SerializeField] private Vector2 panelSize = new Vector2(980f, 120f);
    [SerializeField] private float titleFontSize = 34f;
    [SerializeField] private float fadeDuration = 0.18f;

    private static BossHealthBar instance;
    private static Sprite whiteSprite;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI valueLabel;
    private RectTransform fillAreaRect;
    private RectTransform fillRect;
    private Coroutine fadeRoutine;
    private Health targetHealth;
    private string displayTitle = "Boss";
    private float displayedCurrentHealth;
    private float displayedMaxHealth = 1f;
    private float displayedFraction = 1f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUi();
        HideImmediate();
    }

    void OnDestroy()
    {
        UnbindTarget();

        if (instance == this)
            instance = null;
    }

    void LateUpdate()
    {
        RefreshDisplayFromTarget();
        ApplyFillVisual();
    }

    public static void ShowFor(Health target, string title)
    {
        if (target == null)
            return;

        BossHealthBar bossHealthBar = GetOrCreateInstance();
        if (bossHealthBar == null)
            return;

        bossHealthBar.BindTarget(target, title);
        bossHealthBar.FadeTo(1f);
    }

    public static void Hide(bool immediate = false)
    {
        if (instance == null)
            return;

        if (immediate)
        {
            instance.HideImmediate();
            return;
        }

        instance.FadeTo(0f);
    }

    private void BindTarget(Health newTarget, string title)
    {
        UnbindTarget();
        targetHealth = newTarget;
        UpdateTitle(title);
        RefreshDisplayFromTarget();
        ApplyFillVisual();
    }

    private void UnbindTarget()
    {
        targetHealth = null;
    }

    private void UpdateTitle(string title)
    {
        displayTitle = string.IsNullOrWhiteSpace(title) ? "Boss" : title;

        if (titleLabel != null)
            titleLabel.text = displayTitle;
    }

    private void RefreshDisplayFromTarget()
    {
        if (targetHealth == null)
            return;

        displayedCurrentHealth = targetHealth.CurrentHealth;
        displayedMaxHealth = Mathf.Max(0f, targetHealth.MaxHealth);
        displayedFraction = displayedMaxHealth > 0f
            ? Mathf.Clamp01(displayedCurrentHealth / displayedMaxHealth)
            : 0f;
    }

    private void ApplyFillVisual()
    {
        if (fillRect == null || fillAreaRect == null)
            return;

        float targetWidth = Mathf.Max(0f, fillAreaRect.rect.width * displayedFraction);
        fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);

        if (valueLabel != null)
        {
            int currentHp = Mathf.Max(0, Mathf.CeilToInt(displayedCurrentHealth));
            int maxHp = Mathf.Max(0, Mathf.CeilToInt(displayedMaxHealth));
            valueLabel.text = $"{currentHp} / {maxHp}";
        }
    }

    private void FadeTo(float targetAlpha)
    {
        if (canvasGroup == null)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeRoutine = null;
    }

    private void HideImmediate()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = null;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private void BuildUi()
    {
        Sprite uiSprite = GetWhiteSprite();

        GameObject canvasObject = new GameObject("BossHealthBarCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 980;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -32f);
        panelRect.sizeDelta = panelSize;

        canvasGroup = panelObject.GetComponent<CanvasGroup>();

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = uiSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.color = panelColor;

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(panelObject.transform, false);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.48f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(26f, -8f);
        titleRect.offsetMax = new Vector2(-26f, -16f);

        titleLabel = titleObject.GetComponent<TextMeshProUGUI>();
        titleLabel.font = titleFont != null ? titleFont : TMP_Settings.defaultFontAsset;
        titleLabel.fontSize = titleFontSize;
        titleLabel.color = titleColor;
        titleLabel.alignment = TextAlignmentOptions.Center;
        titleLabel.fontStyle = FontStyles.SmallCaps;
        titleLabel.text = "Boss";

        GameObject barBackdropObject = new GameObject("BarBackdrop", typeof(RectTransform), typeof(Image));
        barBackdropObject.transform.SetParent(panelObject.transform, false);

        RectTransform barBackdropRect = barBackdropObject.GetComponent<RectTransform>();
        barBackdropRect.anchorMin = new Vector2(0f, 0f);
        barBackdropRect.anchorMax = new Vector2(1f, 0.45f);
        barBackdropRect.offsetMin = new Vector2(34f, 24f);
        barBackdropRect.offsetMax = new Vector2(-34f, -18f);

        Image backdropImage = barBackdropObject.GetComponent<Image>();
        backdropImage.sprite = uiSprite;
        backdropImage.type = Image.Type.Simple;
        backdropImage.color = barBackgroundColor;

        GameObject fillAreaObject = new GameObject("FillArea", typeof(RectTransform));
        fillAreaObject.transform.SetParent(barBackdropObject.transform, false);

        fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(6f, 6f);
        fillAreaRect.offsetMax = new Vector2(-6f, -6f);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(fillAreaObject.transform, false);

        fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.sprite = uiSprite;
        fillImage.color = fillColor;
        fillImage.type = Image.Type.Simple;

        GameObject valueObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        valueObject.transform.SetParent(barBackdropObject.transform, false);

        RectTransform valueRect = valueObject.GetComponent<RectTransform>();
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = Vector2.zero;
        valueRect.offsetMax = Vector2.zero;

        valueLabel = valueObject.GetComponent<TextMeshProUGUI>();
        valueLabel.font = titleFont != null ? titleFont : TMP_Settings.defaultFontAsset;
        valueLabel.fontSize = Mathf.Max(18f, titleFontSize * 0.55f);
        valueLabel.color = titleColor;
        valueLabel.alignment = TextAlignmentOptions.Center;
        valueLabel.text = "0 / 0";

        Canvas.ForceUpdateCanvases();
        ApplyFillVisual();
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        whiteSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            Texture2D.whiteTexture.width);
        return whiteSprite;
    }

    private static BossHealthBar GetOrCreateInstance()
    {
        if (instance != null)
            return instance;

        GameObject host = new GameObject("BossHealthBar");
        return host.AddComponent<BossHealthBar>();
    }
}
