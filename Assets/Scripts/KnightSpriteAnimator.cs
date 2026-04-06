using UnityEngine;

[DisallowMultipleComponent]
public class KnightSpriteAnimator : MonoBehaviour
{
    private const string VisualConsumerName = "KnightSpriteAnimator";

    [SerializeField] private KnightSpriteSet spriteSet;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator legacyAnimator;
    [SerializeField] private bool disableLegacyAnimator = true;

    private Sprite fallbackSprite;
    private KnightAnimationClipId currentClip = KnightAnimationClipId.None;
    private float clipElapsed;

    public KnightAnimationClipId CurrentClip => currentClip;
    public KnightSpriteSet SpriteSet => spriteSet;

    void Reset()
    {
        ResolveReferences(false);
    }

    void OnValidate()
    {
        ResolveReferences(false);
    }

    void Awake()
    {
        ResolveReferences(true);

        if (spriteRenderer != null)
            fallbackSprite = spriteRenderer.sprite;

        DisableLegacyAnimatorIfNeeded();
    }

    void OnEnable()
    {
        DisableLegacyAnimatorIfNeeded();
        ApplyFrameForCurrentClip();
    }

    void LateUpdate()
    {
        DisableLegacyAnimatorIfNeeded();

        if (currentClip == KnightAnimationClipId.None || spriteSet == null || spriteRenderer == null)
            return;

        KnightSpriteClip clip = spriteSet.GetClip(currentClip);
        if (!clip.HasFrames)
        {
            if (fallbackSprite != null)
                spriteRenderer.sprite = fallbackSprite;
            return;
        }

        clipElapsed += Time.deltaTime;
        ApplyFrame(clip, clipElapsed);
    }

    public void SetSpriteSet(KnightSpriteSet value)
    {
        spriteSet = value;
    }

    public void Play(KnightAnimationClipId clipId, bool restart = false)
    {
        if (clipId == KnightAnimationClipId.None)
        {
            currentClip = KnightAnimationClipId.None;
            clipElapsed = 0f;
            if (spriteRenderer != null && fallbackSprite != null)
                spriteRenderer.sprite = fallbackSprite;
            return;
        }

        if (!restart && clipId == currentClip)
            return;

        currentClip = clipId;
        clipElapsed = 0f;
        DisableLegacyAnimatorIfNeeded();
        ApplyFrameForCurrentClip();
    }

    public float GetClipDuration(KnightAnimationClipId clipId)
    {
        if (spriteSet == null || clipId == KnightAnimationClipId.None)
            return 0f;

        KnightSpriteClip clip = spriteSet.GetClip(clipId);
        return clip.Duration;
    }

    public bool IsCurrentClipComplete()
    {
        if (spriteSet == null || currentClip == KnightAnimationClipId.None)
            return true;

        KnightSpriteClip clip = spriteSet.GetClip(currentClip);
        if (!clip.HasFrames || clip.loop)
            return false;

        return clipElapsed >= clip.Duration;
    }

    private void ResolveReferences(bool logAutoWire)
    {
        var resolved = PlayerVisualReferenceUtility.Resolve(
            this,
            VisualConsumerName,
            transform,
            spriteRenderer,
            legacyAnimator,
            logAutoWire);

        spriteRenderer = resolved.Renderer;
        legacyAnimator = resolved.Animator;
    }

    private void DisableLegacyAnimatorIfNeeded()
    {
        if (!disableLegacyAnimator || legacyAnimator == null || !legacyAnimator.enabled)
            return;

        legacyAnimator.enabled = false;
    }

    private void ApplyFrameForCurrentClip()
    {
        if (spriteSet == null || spriteRenderer == null)
            return;

        KnightSpriteClip clip = spriteSet.GetClip(currentClip);
        if (!clip.HasFrames)
        {
            if (fallbackSprite != null)
                spriteRenderer.sprite = fallbackSprite;
            return;
        }

        ApplyFrame(clip, clipElapsed);
    }

    private void ApplyFrame(KnightSpriteClip clip, float elapsed)
    {
        if (!clip.HasFrames || spriteRenderer == null)
            return;

        int frameIndex = Mathf.FloorToInt(elapsed * Mathf.Max(0.01f, clip.framesPerSecond));
        if (clip.loop)
            frameIndex %= clip.frames.Length;
        else
            frameIndex = Mathf.Min(frameIndex, clip.frames.Length - 1);

        Sprite frame = clip.frames[Mathf.Clamp(frameIndex, 0, clip.frames.Length - 1)];
        if (frame != null)
            spriteRenderer.sprite = frame;
    }
}
