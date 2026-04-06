using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CreditsController : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private int mainMenuSceneBuildIndex = 0;
    [SerializeField] private float skipEnableDelay = 1.1f;
    [SerializeField] private float scrollStartDelay = 0.6f;
    [SerializeField] private float scrollSpeed = 76f;
    [SerializeField] private float holdAfterScroll = 1.3f;
    [SerializeField] private float fadeInDuration = 0.45f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    [Header("Text")]
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private string title = "Echo Mortalis";
    [SerializeField] private string subtitle = "A CISC 226 action-platformer";
    [SerializeField] private string skipPrompt = "Press any key to return to the menu";
    [SerializeField, TextArea(24, 60)] private string creditsText =
        "<size=150%><b>Developers</b></size>\n" +
        "Augustine\n" +
        "Abdel\n" +
        "Noah\n" +
        "Mark\n\n" +
        "<size=150%><b>Player Character (itch.io)</b></size>\n" +
        "Fantasy Knight Free Pixelart Animated Character\n" +
        "aamatniekss.itch.io/fantasy-knight-free-pixelart-animated-character\n\n" +
        "<size=150%><b>Tilesets (itch.io)</b></size>\n" +
        "0x72 DungeonTileset II - Zone 1 supplement\n" +
        "RottingPixels Dungeon Platformer Tileset - Zone 1 alt\n" +
        "Free Swamp 2D Tileset Pixel Art (Craftpix) - Zone 2\n" +
        "Pixel Fantasy Caves (Szadi art) - Zone 3\n" +
        "Mossy Cavern (Maaot) - Zone 3 background\n\n" +
        "<size=150%><b>Enemy Sprites (itch.io)</b></size>\n" +
        "NightBorne Warrior (Free) by CreativeKind - Zone 1 ranged enemy\n" +
        "Necromancer (Free) by CreativeKind - Zone 2 ranged caster\n" +
        "Flying Demon 2D Pixel Art by Mattz Art - Zone 2 flying enemy\n" +
        "Animated Pixel Slime by rvros - Zone 1 ground roamer\n" +
        "Pixel Art Skeletons Pack by MonoPixelArt - Zone 2 melee patrol\n" +
        "Bringer of Death (Free) by Clembod - Zone 3 boss\n\n" +
        "<size=150%><b>Sound Effects (itch.io)</b></size>\n" +
        "RPG Essentials SFX - Free! by Leohpaz\n" +
        "Kronbits 200+ Retro SFX by Kronbits\n" +
        "Minifantasy Dungeon Audio Pack by Leohpaz\n\n" +
        "<size=150%><b>Built With</b></size>\n" +
        "Unity 6\n" +
        "TextMesh Pro\n\n" +
        "<size=150%><b>Final Thanks</b></size>\n" +
        "Thank you for playing.";

    [Header("Style")]
    [SerializeField] private Color backgroundColor = new Color(0.03f, 0.05f, 0.07f, 1f);
    [SerializeField] private Color panelColor = new Color(0.04f, 0.08f, 0.09f, 0.9f);
    [SerializeField] private Color panelAccentColor = new Color(0.63f, 0.82f, 0.72f, 0.85f);
    [SerializeField] private Color titleColor = new Color(0.96f, 0.95f, 0.89f, 1f);
    [SerializeField] private Color subtitleColor = new Color(0.74f, 0.86f, 0.82f, 1f);
    [SerializeField] private Color bodyColor = new Color(0.88f, 0.92f, 0.9f, 1f);
    [SerializeField] private Color hintColor = new Color(0.73f, 0.8f, 0.78f, 1f);
    [SerializeField] private Vector2 panelSize = new Vector2(1220f, 760f);
    [SerializeField] private Vector2 viewportSize = new Vector2(980f, 350f);

    [Header("Scene References")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Image backdropImage;
    [SerializeField] private Image glowTopImage;
    [SerializeField] private Image glowBottomImage;
    [SerializeField] private Image panelImage;
    [SerializeField] private Image accentTopImage;
    [SerializeField] private Image accentBottomImage;
    [SerializeField] private Image dividerImage;
    [SerializeField] private Image viewportImage;
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private RectTransform creditsContentRect;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI subtitleLabel;
    [SerializeField] private TextMeshProUGUI creditsLabel;
    [SerializeField] private TextMeshProUGUI hintLabel;

    private Vector2 creditsStartPosition;
    private Vector2 creditsEndPosition;
    private bool canSkip;
    private bool exiting;

    void Awake()
    {
        EnsureCamera();

        if (!RefreshScenePresentation())
        {
            Debug.LogWarning(
                "CreditsController could not find the Credits scene UI references.",
                this);
        }
    }

    void Start()
    {
        CheckpointSystem.Reset();
        GamePersistence.Reset();
        ConfigureScrollBounds();

        if (rootCanvasGroup != null)
            rootCanvasGroup.alpha = 0f;

        if (hintLabel != null)
            hintLabel.alpha = 0f;

        StartCoroutine(PresentationRoutine());
    }

    void Update()
    {
        if (hintLabel != null && canSkip && !exiting)
            hintLabel.alpha = 0.45f + (0.55f * Mathf.PingPong(Time.unscaledTime * 1.35f, 1f));

        if (!canSkip || exiting || !ShouldSkip())
            return;

        StartCoroutine(ReturnToMenuRoutine(true));
    }

    public bool RefreshScenePresentation()
    {
        CacheSceneReferences();
        if (rootCanvasGroup == null)
            return false;

        ApplySceneState();
        ConfigureScrollBounds();
        return true;
    }

    private IEnumerator PresentationRoutine()
    {
        yield return FadeCanvas(0f, 1f, fadeInDuration);

        float distance = Vector2.Distance(creditsStartPosition, creditsEndPosition);
        float scrollDuration = distance <= 0.01f
            ? 0f
            : distance / Mathf.Max(1f, scrollSpeed);
        float totalDuration = scrollStartDelay + scrollDuration + holdAfterScroll;
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (!canSkip && elapsed >= skipEnableDelay)
                canSkip = true;

            float scrollT = scrollDuration <= 0f
                ? 1f
                : Mathf.Clamp01((elapsed - scrollStartDelay) / scrollDuration);
            if (creditsContentRect != null)
                creditsContentRect.anchoredPosition = Vector2.Lerp(creditsStartPosition, creditsEndPosition, scrollT);

            yield return null;
        }

        if (!exiting)
            yield return ReturnToMenuRoutine(false);
    }

    private IEnumerator ReturnToMenuRoutine(bool playAcceptSfx)
    {
        if (exiting)
            yield break;

        exiting = true;
        canSkip = false;

        if (playAcceptSfx)
            AudioManager.TryPlaySfx("ui_accept");

        yield return FadeCanvas(rootCanvasGroup != null ? rootCanvasGroup.alpha : 1f, 0f, fadeOutDuration);

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) &&
            Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
            yield break;
        }

        SceneManager.LoadScene(Mathf.Max(0, mainMenuSceneBuildIndex));
    }

    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (rootCanvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            rootCanvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        rootCanvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rootCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        rootCanvasGroup.alpha = to;
    }

    private bool ShouldSkip()
    {
        return Input.anyKeyDown ||
            Input.GetMouseButtonDown(0) ||
            Input.GetMouseButtonDown(1) ||
            Input.GetKeyDown(KeyCode.JoystickButton0) ||
            Input.GetKeyDown(KeyCode.JoystickButton1);
    }

    private void ConfigureScrollBounds()
    {
        if (viewportRect == null || creditsContentRect == null || creditsLabel == null)
            return;

        Canvas.ForceUpdateCanvases();

        float contentWidth = Mathf.Max(160f, viewportRect.rect.width - 72f);
        float contentHeight = Mathf.Max(120f, creditsLabel.preferredHeight + 8f);
        creditsContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
        creditsContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

        RectTransform labelRect = creditsLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
        labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

        creditsStartPosition = new Vector2(0f, -contentHeight - 24f);
        creditsEndPosition = new Vector2(0f, viewportRect.rect.height + 24f);
        creditsContentRect.anchoredPosition = creditsStartPosition;
    }

    private void EnsureCamera()
    {
        Camera sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            GameObject cameraObject = GameObject.Find("Main Camera");
            if (cameraObject == null)
                cameraObject = new GameObject("Main Camera");

            cameraObject.tag = "MainCamera";
            sceneCamera = cameraObject.GetComponent<Camera>();
            if (sceneCamera == null)
                sceneCamera = cameraObject.AddComponent<Camera>();

            if (cameraObject.GetComponent<AudioListener>() == null)
                cameraObject.AddComponent<AudioListener>();
        }

        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = 5f;
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = backgroundColor;
        sceneCamera.nearClipPlane = 0.3f;
        sceneCamera.farClipPlane = 1000f;
    }

    private void CacheSceneReferences()
    {
        Transform canvasTransform = transform.Find("CreditsCanvas");
        if (canvasTransform == null)
            return;

        if (rootCanvasGroup == null)
            rootCanvasGroup = canvasTransform.GetComponent<CanvasGroup>();

        if (panelRect == null)
            panelRect = FindRectTransform(canvasTransform, "CreditsPanel");
        if (backdropImage == null)
            backdropImage = FindImage(canvasTransform, "Backdrop");
        if (glowTopImage == null)
            glowTopImage = FindImage(canvasTransform, "GlowTop");
        if (glowBottomImage == null)
            glowBottomImage = FindImage(canvasTransform, "GlowBottom");
        if (panelImage == null)
            panelImage = FindImage(canvasTransform, "CreditsPanel");
        if (accentTopImage == null)
            accentTopImage = FindImage(canvasTransform, "CreditsPanel/AccentTop");
        if (accentBottomImage == null)
            accentBottomImage = FindImage(canvasTransform, "CreditsPanel/AccentBottom");
        if (dividerImage == null)
            dividerImage = FindImage(canvasTransform, "CreditsPanel/Divider");
        if (viewportImage == null)
            viewportImage = FindImage(canvasTransform, "CreditsPanel/Viewport");
        if (viewportRect == null)
            viewportRect = FindRectTransform(canvasTransform, "CreditsPanel/Viewport");
        if (creditsContentRect == null)
            creditsContentRect = FindRectTransform(canvasTransform, "CreditsPanel/Viewport/CreditsContent");
        if (titleLabel == null)
            titleLabel = FindText(canvasTransform, "CreditsPanel/Title");
        if (subtitleLabel == null)
            subtitleLabel = FindText(canvasTransform, "CreditsPanel/Subtitle");
        if (creditsLabel == null)
            creditsLabel = FindText(canvasTransform, "CreditsPanel/Viewport/CreditsContent/CreditsText");
        if (hintLabel == null)
            hintLabel = FindText(canvasTransform, "CreditsPanel/Hint");
    }

    private void ApplySceneState()
    {
        TMP_FontAsset resolvedTitleFont = ResolveFont(titleFont);
        TMP_FontAsset resolvedBodyFont = ResolveFont(bodyFont);

        ConfigureImage(backdropImage, Vector2.zero, new Vector2(1920f, 1080f), backgroundColor);
        ConfigureImage(glowTopImage, new Vector2(0f, 294f), new Vector2(1600f, 160f), new Color(panelAccentColor.r, panelAccentColor.g, panelAccentColor.b, 0.08f));
        ConfigureImage(glowBottomImage, new Vector2(0f, -312f), new Vector2(1700f, 220f), new Color(panelAccentColor.r, panelAccentColor.g, panelAccentColor.b, 0.05f));
        ConfigureImage(panelImage, Vector2.zero, panelSize, panelColor);
        ConfigureImage(accentTopImage, new Vector2(0f, (panelSize.y * 0.5f) - 24f), new Vector2(panelSize.x - 72f, 4f), panelAccentColor);
        ConfigureImage(accentBottomImage, new Vector2(0f, (-panelSize.y * 0.5f) + 24f), new Vector2(panelSize.x - 72f, 4f), panelAccentColor);
        ConfigureImage(dividerImage, new Vector2(0f, 110f), new Vector2(panelSize.x - 180f, 2.5f), new Color(panelAccentColor.r, panelAccentColor.g, panelAccentColor.b, 0.72f));
        ConfigureImage(viewportImage, new Vector2(0f, -16f), viewportSize, new Color(0f, 0f, 0f, 0.18f));

        if (panelRect != null)
            SetRect(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, panelSize);
        if (viewportRect != null)
            SetRect(viewportRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), viewportSize);
        if (creditsContentRect != null)
            SetRect(creditsContentRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(viewportSize.x - 72f, 220f));

        ConfigureText(
            titleLabel,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 236f),
            new Vector2(panelSize.x - 120f, 110f),
            title,
            resolvedTitleFont,
            82f,
            titleColor,
            TextAlignmentOptions.Center,
            FontStyles.SmallCaps);
        if (titleLabel != null)
            titleLabel.characterSpacing = 3f;

        ConfigureText(
            subtitleLabel,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 154f),
            new Vector2(panelSize.x - 180f, 60f),
            subtitle,
            resolvedBodyFont,
            30f,
            subtitleColor,
            TextAlignmentOptions.Center,
            FontStyles.Normal);
        if (subtitleLabel != null)
            subtitleLabel.fontWeight = FontWeight.Regular;

        ConfigureText(
            creditsLabel,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            new Vector2(viewportSize.x - 72f, 220f),
            creditsText,
            resolvedBodyFont,
            32f,
            bodyColor,
            TextAlignmentOptions.TopGeoAligned,
            FontStyles.Normal);
        if (creditsLabel != null)
        {
            creditsLabel.textWrappingMode = TextWrappingModes.Normal;
            creditsLabel.overflowMode = TextOverflowModes.Overflow;
            creditsLabel.lineSpacing = 8f;
        }

        ConfigureText(
            hintLabel,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -286f),
            new Vector2(panelSize.x - 140f, 44f),
            skipPrompt,
            resolvedBodyFont,
            25f,
            hintColor,
            TextAlignmentOptions.Center,
            FontStyles.Italic);
    }

    private void ConfigureImage(Image image, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        if (image == null)
            return;

        SetRect(
            image.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            anchoredPosition,
            size);
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        image.color = color;
    }

    private void ConfigureText(
        TextMeshProUGUI label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        string text,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        FontStyles fontStyle)
    {
        if (label == null)
            return;

        SetRect(label.rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, size);
        label.font = font;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = fontStyle;
        label.text = text;
        label.raycastTarget = false;
        label.outlineWidth = 0.18f;
        label.outlineColor = new Color(0f, 0f, 0f, 0.88f);
    }

    private static void SetRect(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
    }

    private static Image FindImage(Transform root, string path)
    {
        Transform target = root.Find(path);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private static RectTransform FindRectTransform(Transform root, string path)
    {
        return root.Find(path) as RectTransform;
    }

    private static TextMeshProUGUI FindText(Transform root, string path)
    {
        Transform target = root.Find(path);
        return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
    }

    private static TMP_FontAsset ResolveFont(TMP_FontAsset preferredFont)
    {
        return preferredFont != null ? preferredFont : TMP_Settings.defaultFontAsset;
    }
}
