using UnityEngine;

/// <summary>
/// Scrolls the road texture along the plane to create the illusion of high-speed driving.
/// Compatible with Universal Render Pipeline (URP) Lit/Unlit and Built-in shaders.
/// </summary>
[RequireComponent(typeof(Renderer))]
[AddComponentMenu("Vehicles/Road Scroller")]
public class RoadScroller : MonoBehaviour
{
    [Header("Scroll Configuration")]
    [Tooltip("Scroll speed in UV units per second. Positive values scroll road backwards towards the camera.")]
    [SerializeField] private float scrollSpeed = 1.2f;

    [Tooltip("Reverse scroll direction if the road is moving the wrong way.")]
    [SerializeField] private bool reverseDirection = false;

    [Header("Car Synchronization (Optional)")]
    [Tooltip("Optional reference to CarController. If set, scrolling halts when the car crashes.")]
    [SerializeField] private CarController carController;

    [Tooltip("Scale scroll speed proportionally with car forwardSpeed if CarController is assigned.")]
    [SerializeField] private bool syncWithCarSpeed = true;

    [Tooltip("Speed multiplier when synchronized with CarController.")]
    [SerializeField] private float speedMultiplier = 0.1f;

    [Header("Texture Properties")]
    [Tooltip("Shader property name for the main texture. URP uses '_BaseMap', Built-in uses '_MainTex'.")]
    [SerializeField] private string texturePropertyName = "_BaseMap";

    private Renderer roadRenderer;
    private Material roadMaterial;
    private float currentOffset = 0f;
    private int texturePropId;

    private void Awake()
    {
        roadRenderer = GetComponent<Renderer>();
        if (roadRenderer != null)
        {
            roadMaterial = roadRenderer.material;
        }

        // Cache property ID for performance
        texturePropId = Shader.PropertyToID(texturePropertyName);

        // Auto-find the car script if not assigned
        if (carController == null)
        {
            carController = Object.FindAnyObjectByType<CarController>();
        }
    }

    private void Update()
    {
        if (roadMaterial == null) return;

        // Stop scrolling if car has crashed
        if (carController != null && !carController.IsAlive())
        {
            return;
        }

        float effectiveSpeed = scrollSpeed;

        // Synchronize with car speed if requested
        if (syncWithCarSpeed && carController != null && carController.forwardSpeed > 0f)
        {
            effectiveSpeed = carController.forwardSpeed * speedMultiplier;
        }

        float direction = reverseDirection ? -1f : 1f;

        // Increment UV offset
        currentOffset = (currentOffset + (effectiveSpeed * direction * Time.deltaTime)) % 1f;

        // Apply offset to URP BaseMap and legacy MainTex
        Vector2 offsetVector = new Vector2(0f, currentOffset);

        if (roadMaterial.HasProperty(texturePropId))
        {
            roadMaterial.SetTextureOffset(texturePropId, offsetVector);
        }

        // Fallback for standard mainTextureOffset
        roadMaterial.mainTextureOffset = offsetVector;
    }

    /// <summary>
    /// Dynamically update scroll speed at runtime (e.g. boost pads or braking).
    /// </summary>
    public void SetScrollSpeed(float newSpeed)
    {
        scrollSpeed = newSpeed;
    }

    public float GetScrollSpeed() => scrollSpeed;

    private void OnDestroy()
    {
        // Clean up instantiated runtime material to prevent memory leaks
        if (roadMaterial != null)
        {
            Destroy(roadMaterial);
        }
    }
}
