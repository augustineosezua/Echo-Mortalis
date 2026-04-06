using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBar : MonoBehaviour
{
    private RectTransform fillRect;
    private Health playerHealth;
    private GameObject hudRoot;

    void Awake()
    {
        BuildUI();
    }

    void Start()
    {
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player == null)
            return;

        playerHealth = player.GetComponent<Health>();
        if (playerHealth == null)
            return;

        SetFill(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        playerHealth.OnHealthChanged += SetFill;
    }

    void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= SetFill;
        if (hudRoot != null)
            Destroy(hudRoot);
    }

    private void SetFill(float current, float max)
    {
        if (fillRect == null)
            return;

        fillRect.anchorMax = new Vector2(max > 0f ? Mathf.Clamp01(current / max) : 0f, 1f);
    }

    private void BuildUI()
    {
        Sprite white = MakeWhiteSprite();

        // Canvas must be on its own root — attaching to the player object breaks UI layout
        hudRoot = new GameObject("PlayerHUD");
        Object.DontDestroyOnLoad(hudRoot);

        Canvas canvas = hudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = hudRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        hudRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject bgGO = new GameObject("HB_Background");
        bgGO.transform.SetParent(hudRoot.transform, false);

        RectTransform bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 1f);
        bgRect.anchorMax = new Vector2(0f, 1f);
        bgRect.pivot     = new Vector2(0f, 1f);
        bgRect.anchoredPosition = new Vector2(20f, -20f);
        bgRect.sizeDelta        = new Vector2(220f, 28f);

        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite = white;
        bgImg.color  = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        GameObject fillGO = new GameObject("HB_Fill");
        fillGO.transform.SetParent(bgGO.transform, false);

        fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);

        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.sprite = white;
        fillImg.color  = new Color(0.22f, 0.82f, 0.34f, 1f);
    }

    private static Sprite MakeWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
