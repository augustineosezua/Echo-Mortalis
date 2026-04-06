using UnityEngine;

[DisallowMultipleComponent]
public class ParallaxBackground : MonoBehaviour
{
    [Header("Parallax")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] [Range(0f, 1f)] private float parallaxFactor = 0.5f;
    [SerializeField] private bool moveOnX = true;
    [SerializeField] private bool moveOnY;

    private Vector3 startingPosition;
    private Vector3 startingCameraPosition;

    void Start()
    {
        // Falling back to Camera.main keeps setup simple for a single-camera 2D scene.
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // We remember the original positions so the background movement stays relative and predictable.
        startingPosition = transform.position;

        if (cameraTransform != null)
            startingCameraPosition = cameraTransform.position;
    }

    void LateUpdate()
    {
        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            // If the main camera appears later we capture its current position as the new reference point.
            cameraTransform = mainCamera.transform;
            startingCameraPosition = cameraTransform.position;
        }

        // The camera offset tells us how far the background should drift from its starting point.
        Vector3 cameraOffset = cameraTransform.position - startingCameraPosition;
        Vector3 parallaxPosition = startingPosition;

        if (moveOnX)
            parallaxPosition.x += cameraOffset.x * parallaxFactor;

        if (moveOnY)
            parallaxPosition.y += cameraOffset.y * parallaxFactor;

        // We keep the original Z so the layer never changes its depth in the scene.
        transform.position = parallaxPosition;
    }
}
