using UnityEngine;

internal static class PlayerVisualReferenceUtility
{
    internal readonly struct VisualReferences
    {
        internal VisualReferences(
            Transform root,
            SpriteRenderer renderer,
            Animator animator,
            bool rootAutoWired,
            bool rendererAutoWired,
            bool animatorAutoWired)
        {
            Root = root;
            Renderer = renderer;
            Animator = animator;
            RootAutoWired = rootAutoWired;
            RendererAutoWired = rendererAutoWired;
            AnimatorAutoWired = animatorAutoWired;
        }

        internal Transform Root { get; }
        internal SpriteRenderer Renderer { get; }
        internal Animator Animator { get; }
        internal bool RootAutoWired { get; }
        internal bool RendererAutoWired { get; }
        internal bool AnimatorAutoWired { get; }
        internal bool UsedAutoWire => RootAutoWired || RendererAutoWired || AnimatorAutoWired;
    }

    internal static VisualReferences Resolve(
        Component owner,
        string consumerName,
        Transform visualRoot,
        SpriteRenderer visualRenderer,
        Animator visualAnimator,
        bool logAutoWire)
    {
        Transform resolvedRoot = visualRoot;
        SpriteRenderer resolvedRenderer = visualRenderer;
        Animator resolvedAnimator = visualAnimator;

        bool rootAutoWired = resolvedRoot == null;
        bool rendererAutoWired = resolvedRenderer == null;
        bool animatorAutoWired = resolvedAnimator == null;

        Transform primarySearchRoot = resolvedRoot != null ? resolvedRoot : owner.transform;

        if (resolvedAnimator == null)
            resolvedAnimator = FindBestAnimator(primarySearchRoot, resolvedRenderer);

        if (resolvedRenderer == null)
            resolvedRenderer = FindBestSpriteRenderer(primarySearchRoot, resolvedAnimator);

        if (resolvedAnimator == null)
            resolvedAnimator = FindBestAnimator(owner.transform, resolvedRenderer);

        if (resolvedRenderer == null)
            resolvedRenderer = FindBestSpriteRenderer(owner.transform, resolvedAnimator);

        if (resolvedRoot == null)
            resolvedRoot = ResolveVisualRoot(owner.transform, resolvedAnimator, resolvedRenderer);

        var resolved = new VisualReferences(
            resolvedRoot,
            resolvedRenderer,
            resolvedAnimator,
            rootAutoWired,
            rendererAutoWired,
            animatorAutoWired);

        if (logAutoWire && resolved.UsedAutoWire)
        {
            Debug.Log(
                $"{consumerName} auto-wired visualRoot='{DescribeTransform(resolved.Root)}', " +
                $"visualRenderer='{DescribeComponent(resolved.Renderer)}', " +
                $"visualAnimator='{DescribeComponent(resolved.Animator)}'.",
                owner);
        }

        return resolved;
    }

    internal static void Validate(
        Component owner,
        string consumerName,
        Transform visualRoot,
        SpriteRenderer visualRenderer,
        Animator visualAnimator)
    {
        if (visualRenderer == null)
        {
            Debug.LogWarning(
                $"{consumerName} could not resolve a SpriteRenderer under '{DescribeTransform(owner.transform)}'. " +
                "The player will not render.",
                owner);
            return;
        }

        if (visualAnimator == null)
        {
            Debug.LogWarning(
                $"{consumerName} could not resolve an Animator under '{DescribeTransform(owner.transform)}'. " +
                "Idle and movement animation states will not drive the visible sprite.",
                owner);
        }

        if (visualRoot != null && !IsAncestorOrSelf(visualRoot, visualRenderer.transform))
        {
            Debug.LogWarning(
                $"{consumerName} visualRoot '{DescribeTransform(visualRoot)}' does not contain visualRenderer " +
                $"'{DescribeTransform(visualRenderer.transform)}'.",
                owner);
        }

        if (visualRoot != null && visualAnimator != null && !IsAncestorOrSelf(visualRoot, visualAnimator.transform))
        {
            Debug.LogWarning(
                $"{consumerName} visualRoot '{DescribeTransform(visualRoot)}' does not contain visualAnimator " +
                $"'{DescribeTransform(visualAnimator.transform)}'.",
                owner);
        }

        if (visualAnimator != null && !CanAnimatorTargetRenderer(visualAnimator, visualRenderer))
        {
            Debug.LogWarning(
                $"{consumerName} resolved visualAnimator '{DescribeTransform(visualAnimator.transform)}' and " +
                $"visualRenderer '{DescribeTransform(visualRenderer.transform)}' on mismatched paths. " +
                "This animator cannot directly drive that renderer's sprite or color.",
                owner);
        }

        if (visualRenderer.sprite == null)
        {
            Debug.LogWarning(
                $"{consumerName} resolved visualRenderer '{DescribeTransform(visualRenderer.transform)}' but its sprite is null. " +
                "The player may be invisible until an animation assigns a sprite.",
                owner);
        }

        if (visualRenderer.color.a <= 0.001f)
        {
            Debug.LogWarning(
                $"{consumerName} resolved visualRenderer '{DescribeTransform(visualRenderer.transform)}' with alpha 0. " +
                "A fully transparent sprite will be invisible.",
                owner);
        }
    }

    internal static bool CanAnimatorTargetRenderer(Animator animator, SpriteRenderer renderer)
    {
        if (animator == null || renderer == null)
            return false;

        return IsAncestorOrSelf(animator.transform, renderer.transform);
    }

    internal static string DescribeTransform(Transform target)
    {
        if (target == null)
            return "<null>";

        return GetHierarchyPath(target);
    }

    internal static string DescribeComponent(Component component)
    {
        if (component == null)
            return "<null>";

        return GetHierarchyPath(component.transform);
    }

    private static Transform ResolveVisualRoot(Transform ownerRoot, Animator animator, SpriteRenderer renderer)
    {
        if (animator != null && renderer != null)
        {
            if (animator.transform == renderer.transform)
                return animator.transform;

            if (IsAncestorOrSelf(animator.transform, renderer.transform))
                return animator.transform;
        }

        if (renderer != null)
            return renderer.transform;

        if (animator != null)
            return animator.transform;

        return ownerRoot;
    }

    private static SpriteRenderer FindBestSpriteRenderer(Transform searchRoot, Animator preferredAnimator)
    {
        SpriteRenderer[] candidates = searchRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (candidates == null || candidates.Length == 0)
            return null;

        SpriteRenderer best = null;
        int bestScore = int.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            SpriteRenderer candidate = candidates[i];
            if (candidate == null)
                continue;

            int score = RankRendererCandidate(searchRoot, candidate, preferredAnimator);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static Animator FindBestAnimator(Transform searchRoot, SpriteRenderer preferredRenderer)
    {
        Animator[] candidates = searchRoot.GetComponentsInChildren<Animator>(true);
        if (candidates == null || candidates.Length == 0)
            return null;

        Animator best = null;
        int bestScore = int.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            Animator candidate = candidates[i];
            if (candidate == null)
                continue;

            int score = RankAnimatorCandidate(searchRoot, candidate, preferredRenderer);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static int RankRendererCandidate(Transform searchRoot, SpriteRenderer candidate, Animator preferredAnimator)
    {
        int score = GetDepthFrom(searchRoot, candidate.transform) * 10;

        if (!candidate.gameObject.activeSelf)
            score += 25;

        if (!candidate.enabled)
            score += 50;

        if (candidate.sprite == null)
            score += 10;

        if (candidate.color.a <= 0.001f)
            score += 20;

        if (preferredAnimator != null)
        {
            if (candidate.transform == preferredAnimator.transform)
            {
                score -= 1000;
            }
            else if (IsAncestorOrSelf(preferredAnimator.transform, candidate.transform))
            {
                score -= 750;
            }
            else if (IsAncestorOrSelf(candidate.transform, preferredAnimator.transform))
            {
                score += 400;
            }
            else
            {
                score += 800;
            }
        }
        else if (candidate.transform == searchRoot)
        {
            score -= 100;
        }

        return score;
    }

    private static int RankAnimatorCandidate(Transform searchRoot, Animator candidate, SpriteRenderer preferredRenderer)
    {
        int score = GetDepthFrom(searchRoot, candidate.transform) * 10;

        if (!candidate.gameObject.activeSelf)
            score += 25;

        if (!candidate.enabled)
            score += 50;

        if (candidate.runtimeAnimatorController == null)
            score += 50;

        if (preferredRenderer != null)
        {
            if (candidate.transform == preferredRenderer.transform)
            {
                score -= 1000;
            }
            else if (IsAncestorOrSelf(candidate.transform, preferredRenderer.transform))
            {
                score -= 750;
            }
            else if (IsAncestorOrSelf(preferredRenderer.transform, candidate.transform))
            {
                score += 400;
            }
            else
            {
                score += 800;
            }
        }
        else if (candidate.transform == searchRoot)
        {
            score -= 100;
        }

        return score;
    }

    private static int GetDepthFrom(Transform ancestor, Transform target)
    {
        if (ancestor == null || target == null)
            return 100;

        int depth = 0;
        Transform current = target;

        while (current != null && current != ancestor)
        {
            current = current.parent;
            depth++;
        }

        if (current == ancestor)
            return depth;

        return 100;
    }

    private static bool IsAncestorOrSelf(Transform ancestor, Transform target)
    {
        if (ancestor == null || target == null)
            return false;

        Transform current = target;
        while (current != null)
        {
            if (current == ancestor)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return "<null>";

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
