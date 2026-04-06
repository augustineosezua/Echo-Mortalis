using UnityEngine;

[DisallowMultipleComponent]
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(0.9f, 0.12f);
    [SerializeField] private float verticalPadding = 0.12f;
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.9f);
    [SerializeField] private Color fillColor = new Color(0.88f, 0.16f, 0.2f, 1f);
    [SerializeField] private int sortingOrderOffset = 3;

    private Health health;
    private SpriteRenderer targetRenderer;
    private Transform barRoot;
    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;
    private static Sprite whiteSprite;

    void Awake()
    {
        health = GetComponent<Health>();
        targetRenderer = FindPrimarySpriteRenderer();

        BuildBar();
        RefreshPlacement();
        RefreshSorting();

        if (health != null)
            UpdateFill(health.CurrentHealth, health.MaxHealth);
    }

    void OnEnable()
    {
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
    }

    void LateUpdate()
    {
        RefreshPlacement();
        RefreshSorting();
    }

    private void BuildBar()
    {
        if (barRoot != null)
            return;

        barRoot = new GameObject("EnemyHealthBar").transform;
        barRoot.SetParent(transform, false);

        backgroundRenderer = CreateBarPart("Background", backgroundColor, 0f);
        backgroundRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);

        fillRenderer = CreateBarPart("Fill", fillColor, -0.01f);
        fillRenderer.transform.localScale = new Vector3(size.x, size.y * 0.6f, 1f);
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
        if (barRoot == null || targetRenderer == null)
            return;

        float spriteTop = 1f;
        if (targetRenderer.sprite != null)
            spriteTop = Mathf.Max(0.1f, targetRenderer.bounds.max.y - transform.position.y);

        barRoot.localPosition = new Vector3(0f, spriteTop + verticalPadding, 0f);
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

    private void UpdateFill(float current, float max)
    {
        if (backgroundRenderer == null || fillRenderer == null)
            return;

        float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        bool visible = current > 0f && max > 0f;
        backgroundRenderer.enabled = visible;
        fillRenderer.enabled = visible;

        float fillWidth = Mathf.Max(0.0001f, size.x * fraction);
        fillRenderer.transform.localScale = new Vector3(fillWidth, size.y * 0.6f, 1f);
        fillRenderer.transform.localPosition = new Vector3(-(size.x - fillWidth) * 0.5f, 0f, -0.01f);
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

    private SpriteRenderer FindPrimarySpriteRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer selfRenderer = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject == gameObject)
            {
                selfRenderer = candidate;
                continue;
            }

            return candidate;
        }

        return selfRenderer;
    }
}
