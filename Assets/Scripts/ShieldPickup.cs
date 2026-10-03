using UnityEngine;

/// <summary>
/// Collectible shield pickup (a "smart-city infrastructure investment").
/// Grants the car temporary invincibility on pickup and then removes itself.
/// </summary>
public class ShieldPickup : MonoBehaviour
{
    [Tooltip("Seconds of invincibility granted by this pickup.")]
    [SerializeField] private float shieldDuration = 5f;

    [Tooltip("Rotation speed for collectible visual spinning.")]
    [SerializeField] private float rotationSpeed = 90f;

    [Tooltip("How far (in metres) the pickup bobs up and down.")]
    [SerializeField] private float bobAmplitude = 0.25f;

    [Tooltip("Speed of the up/down bob.")]
    [SerializeField] private float bobSpeed = 2f;

    [Tooltip("Distance (in metres) behind the car at which an uncollected pickup removes itself.")]
    [SerializeField] private float despawnDistanceBehind = 15f;

    private Vector3 startPosition;
    private Transform carTransform;

    private void Start()
    {
        startPosition = transform.position;
        FindCar();
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        float y = startPosition.y + (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
        transform.position = new Vector3(transform.position.x, y, transform.position.z);

        // Clean up once the car is safely past, so misses don't leak objects across the run.
        if (carTransform == null) FindCar();
        if (carTransform != null && transform.position.z < carTransform.position.z - despawnDistanceBehind)
        {
            Destroy(gameObject);
        }
    }

    private void FindCar()
    {
        if (carTransform != null) return;

        CarController car = Object.FindAnyObjectByType<CarController>();
        if (car != null) carTransform = car.transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        ShieldSystem shield = other.GetComponentInParent<ShieldSystem>();
        if (shield == null) return;

        shield.ActivateShield(shieldDuration);
        Destroy(gameObject);
    }
}
