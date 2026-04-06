using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 8f;
    public bool followX = true;
    public bool followY = false;
    public float verticalOffset = 0.6f;
    public float verticalDeadZone = 0.85f;
    public bool clampHorizontally = false;
    public bool clampVertically = true;
    public bool fitOrthographicSizeToLevelBounds = true;
    public string levelRootName = "Zone1_Level";

    private Camera attachedCamera;
    private Bounds levelBounds;
    private bool hasLevelBounds;
    private bool hasSnappedToTarget;

    void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        ResolveLevelBounds();
        ApplyLevelFraming();
        ResolveTarget();

        if (target != null)
            SnapToTarget();
    }

    void Start()
    {
        ResolveTarget();
        if (!hasLevelBounds)
            ResolveLevelBounds();
        ApplyLevelFraming();
        SnapToTarget();
    }

    void LateUpdate()
    {
        if (target == null)
        {
            ResolveTarget();
            if (target != null)
                SnapToTarget();
        }

        if (target == null)
            return;

        Vector3 desired = transform.position;

        if (followX)
            desired.x = target.position.x;
        if (followY)
            desired.y = GetTrackedTargetY();

        desired = ClampToBounds(desired);
        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * smoothSpeed);
    }

    void ResolveTarget()
    {
        if (target != null)
            return;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            target = taggedPlayer.transform;
            return;
        }

        var player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
            target = player.transform;
    }

    void SnapToTarget()
    {
        if (target == null && !ShouldUseLevelAnchoredY())
            return;

        Vector3 snapped = transform.position;

        if (followX && target != null)
            snapped.x = target.position.x;
        if (ShouldUseLevelAnchoredY())
            snapped.y = levelBounds.center.y;
        else if (followY && target != null)
            snapped.y = target.position.y + verticalOffset;
        else if (!hasSnappedToTarget && target != null)
            snapped.y = target.position.y;

        transform.position = ClampToBounds(snapped);
        hasSnappedToTarget = true;
    }

    void ResolveLevelBounds()
    {
        hasLevelBounds = TryGetLevelBounds(out levelBounds);
    }

    void ApplyLevelFraming()
    {
        if (!ShouldUseLevelAnchoredY())
            return;

        attachedCamera.orthographicSize = Mathf.Max(0.01f, levelBounds.extents.y);

        Vector3 framedPosition = transform.position;
        framedPosition.y = levelBounds.center.y;
        transform.position = framedPosition;
    }

    bool TryGetLevelBounds(out Bounds bounds)
    {
        Tilemap[] tilemaps;
        Transform levelRoot = null;

        if (!string.IsNullOrWhiteSpace(levelRootName))
        {
            GameObject levelObject = GameObject.Find(levelRootName);
            if (levelObject != null)
                levelRoot = levelObject.transform;
        }

        if (levelRoot != null)
            tilemaps = levelRoot.GetComponentsInChildren<Tilemap>();
        else
            tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        bool foundBounds = false;
        bounds = default;

        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null || !tilemap.gameObject.activeInHierarchy)
                continue;

            Bounds localBounds = tilemap.localBounds;
            if (localBounds.size.sqrMagnitude <= 0.0001f)
                continue;

            Vector3 worldCenter = tilemap.transform.TransformPoint(localBounds.center);
            Vector3 worldSize = Vector3.Scale(localBounds.size, tilemap.transform.lossyScale);
            Bounds worldBounds = new Bounds(worldCenter, worldSize);

            if (!foundBounds)
            {
                bounds = worldBounds;
                foundBounds = true;
                continue;
            }

            bounds.Encapsulate(worldBounds.min);
            bounds.Encapsulate(worldBounds.max);
        }

        return foundBounds;
    }

    Vector3 ClampToBounds(Vector3 desired)
    {
        if (!hasLevelBounds)
            ResolveLevelBounds();

        if (!hasLevelBounds || attachedCamera == null || !attachedCamera.orthographic)
            return desired;

        float halfHeight = attachedCamera.orthographicSize;
        float halfWidth = halfHeight * attachedCamera.aspect;
        Vector3 current = transform.position;

        if (clampHorizontally)
            desired.x = ClampAxis(desired.x, current.x, levelBounds.min.x, levelBounds.max.x, halfWidth);

        if (clampVertically)
            desired.y = ClampAxis(desired.y, current.y, levelBounds.min.y, levelBounds.max.y, halfHeight);

        return desired;
    }

    bool ShouldUseLevelAnchoredY()
    {
        return !followY &&
            fitOrthographicSizeToLevelBounds &&
            hasLevelBounds &&
            attachedCamera != null &&
            attachedCamera.orthographic;
    }

    float ClampAxis(float desired, float current, float min, float max, float halfExtent)
    {
        float clampedMin = min + halfExtent;
        float clampedMax = max - halfExtent;

        if (clampedMin > clampedMax)
            return current;

        return Mathf.Clamp(desired, clampedMin, clampedMax);
    }

    float GetTrackedTargetY()
    {
        if (target == null)
            return transform.position.y;

        float targetY = target.position.y + verticalOffset;
        float deadZone = Mathf.Max(0f, verticalDeadZone);
        if (deadZone <= 0.0001f)
            return targetY;

        float currentY = transform.position.y;
        float delta = targetY - currentY;
        if (Mathf.Abs(delta) <= deadZone)
            return currentY;

        return targetY - Mathf.Sign(delta) * deadZone;
    }
}
