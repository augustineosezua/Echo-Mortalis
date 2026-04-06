using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWorldHealthBar : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(1.2f, 0.16f);
    [SerializeField] private float verticalPadding = 0.18f;
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.88f);
    [SerializeField] private Color fillColor = new Color(0.22f, 0.82f, 0.34f, 1f);
    [SerializeField] private int sortingOrderOffset = 4;

    private Health health;
    private SpriteRenderer targetRenderer;
    private Transform barRoot;
    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;
    private static Sprite whiteSprite;
    private float displayedCurrentHealth = float.MinValue;
    private float displayedMaxHealth = float.MinValue;

    void Awake()
    {
        ResolveReferences();
        BuildBar();
        RefreshPlacement();
        RefreshSorting();

        if (health != null)
            UpdateFill(health.CurrentHealth, health.MaxHealth);
    }

    void OnEnable()
    {
        ResolveReferences();

        if (health != null)
        {
            health.OnHealthChanged += UpdateFill;
            UpdateFill(health.CurrentHealth, health.MaxHealth);
        }
    }

    void OnDisable()
    {
        if (health != null)
            health.OnHealthChanged -= UpdateFill;

        SetVisible(false);
    }

    void OnDestroy()
    {
        if (health != null)
            health.OnHealthChanged -= UpdateFill;

        if (barRoot != null)
            Destroy(barRoot.gameObject);
    }

    void LateUpdate()
    {
        RefreshFillFromHealth();
        RefreshPlacement();
        RefreshSorting();
    }

    private void ResolveReferences()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void BuildBar()
    {
        if (barRoot != null)
            return;

        barRoot = new GameObject("PlayerWorldHealthBar").transform;

        backgroundRenderer = CreateBarPart("Background", backgroundColor, 0f);
        backgroundRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);

        fillRenderer = CreateBarPart("Fill", fillColor, -0.01f);
        fillRenderer.transform.localScale = new Vector3(size.x, size.y * 0.65f, 1f);
    }

    private SpriteRenderer CreateBarPart(string name, Color color, float localZ)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(barRoot, false);
        child.localPosition = new Vector3(0f, 0f, localZ);

        SpriteRenderer renderer = child.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        return renderer;
    }

    private void RefreshPlacement()
    {
        if (barRoot == null)
            return;

        if (targetRenderer != null)
        {
            Bounds bounds = targetRenderer.bounds;
            barRoot.position = new Vector3(bounds.center.x, bounds.max.y + verticalPadding, targetRenderer.transform.position.z);
            return;
        }

        barRoot.position = transform.position + new Vector3(0f, 1f + verticalPadding, 0f);
    }

    private void RefreshSorting()
    {
        if (targetRenderer == null)
            return;

        int backgroundOrder = targetRenderer.sortingOrder + sortingOrderOffset;
        int fillOrder = backgroundOrder + 1;

        if (backgroundRenderer != null)
        {
            backgroundRenderer.sortingLayerID = targetRenderer.sortingLayerID;
            backgroundRenderer.sortingOrder = backgroundOrder;
        }

        if (fillRenderer != null)
        {
            fillRenderer.sortingLayerID = targetRenderer.sortingLayerID;
            fillRenderer.sortingOrder = fillOrder;
        }
    }

    private void RefreshFillFromHealth()
    {
        if (health == null)
            ResolveReferences();

        if (health == null)
            return;

        if (Mathf.Approximately(displayedCurrentHealth, health.CurrentHealth) &&
            Mathf.Approximately(displayedMaxHealth, health.MaxHealth))
        {
            return;
        }

        UpdateFill(health.CurrentHealth, health.MaxHealth);
    }

    private void UpdateFill(float current, float max)
    {
        if (backgroundRenderer == null || fillRenderer == null)
            return;

        displayedCurrentHealth = current;
        displayedMaxHealth = max;

        float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        bool visible = isActiveAndEnabled && current > 0f && max > 0f;
        SetVisible(visible);

        float fillWidth = Mathf.Max(0.0001f, size.x * fraction);
        fillRenderer.transform.localScale = new Vector3(fillWidth, size.y * 0.65f, 1f);
        fillRenderer.transform.localPosition = new Vector3(-(size.x - fillWidth) * 0.5f, 0f, -0.01f);
    }

    private void SetVisible(bool visible)
    {
        if (backgroundRenderer != null)
            backgroundRenderer.enabled = visible;

        if (fillRenderer != null)
            fillRenderer.enabled = visible;
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
}
