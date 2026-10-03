using UnityEngine;

/// <summary>
/// A grid recharge point: one of the small pilot investments Veridia's council approved
/// after years of petitions. Riding through it puts charge back in the pod's pack.
/// Removes itself once the pod is past, so misses don't accumulate across a run.
/// </summary>
public class ChargePickup : MonoBehaviour
{
    [Tooltip("Charge restored on pickup, in percent.")]
    [SerializeField] private float chargeBonus = 30f;

    [Tooltip("Speed of the up/down float.")]
    [SerializeField] private float bobSpeed = 2f;

    [Tooltip("How far the beacon floats up and down, in metres.")]
    [SerializeField] private float bobAmplitude = 0.22f;

    [Tooltip("How much the beacon breathes while it waits.")]
    [SerializeField] private float pulseAmount = 0.07f;

    [Tooltip("Speed of the breathing pulse.")]
    [SerializeField] private float pulseSpeed = 3f;

    [Tooltip("Distance behind the pod at which an uncollected beacon removes itself.")]
    [SerializeField] private float despawnDistanceBehind = 15f;

    private Vector3 startPosition;
    private Vector3 baseScale;
    private Transform carTransform;

    private void Start()
    {
        startPosition = transform.position;
        baseScale = transform.localScale;
        FindCar();
    }

    private void Update()
    {
        float t = Time.time;

        float y = startPosition.y + (Mathf.Sin(t * bobSpeed) * bobAmplitude);
        transform.position = new Vector3(startPosition.x, y, startPosition.z);

        float pulse = 1f + (Mathf.Sin(t * pulseSpeed) * pulseAmount);
        transform.localScale = baseScale * pulse;

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
        BatterySystem battery = other.GetComponentInParent<BatterySystem>();
        if (battery == null) return;

        battery.AddCharge(chargeBonus);
        Destroy(gameObject);
    }
}
