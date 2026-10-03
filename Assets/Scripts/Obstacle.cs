using UnityEngine;

/// <summary>
/// Attached to hazard objects on Route 7 (stalled-construction barricades, deferred-maintenance
/// potholes, debris, congestion clusters).
/// Moves along -Z towards the player and resolves the impact when it reaches the pod.
/// Acts as the single authority for a car/obstacle collision, so a shield is only ever
/// consumed once per impact no matter which collider reports the trigger first.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Obstacle : MonoBehaviour
{
    /// <summary>How this hazard punishes the player on contact.</summary>
    public enum HazardKind
    {
        /// <summary>Ends the run, unless the car is shielded.</summary>
        Crash,

        /// <summary>Costs charge and nothing else - the pod drives straight over it.</summary>
        ChargePenalty
    }

    [Header("Hazard Type")]
    [Tooltip("Crash = ends the run (a Civic Grant absorbs it). ChargePenalty = only costs charge, never crashes.")]
    [SerializeField] private HazardKind hazardKind = HazardKind.Crash;

    [Tooltip("Charge drained when a ChargePenalty hazard (e.g. a pothole) is driven over.")]
    [SerializeField] private float chargePenalty = 12f;

    [Header("Movement (For Scrolling Road Mode)")]
    [Tooltip("If true, moves backwards towards the player along the Z axis.")]
    [SerializeField] private bool moveTowardsPlayer = true;

    [Tooltip("Movement speed towards player along -Z (e.g. 10 - 20 m/s).")]
    [SerializeField] private float moveSpeed = 15f;

    [Tooltip("Distance (in metres) behind the car at which this obstacle is destroyed.")]
    [SerializeField] private float despawnDistanceBehind = 15f;

    [Header("Impact Settings")]
    [Tooltip("Destroy this obstacle immediately upon colliding with the car.")]
    [SerializeField] private bool destroyOnImpact = false;

    private Transform carTransform;
    private bool hasResolved = false;

    /// <summary>True when this hazard only drains the pack instead of ending the run.</summary>
    public bool IsChargePenalty => hazardKind == HazardKind.ChargePenalty;

    private void Awake()
    {
        // Ensure this obstacle has a kinematic Rigidbody so Unity triggers collision/trigger events reliably
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Update()
    {
        if (!moveTowardsPlayer) return;

        // Move backwards towards the player
        transform.Translate(Vector3.back * moveSpeed * Time.deltaTime, Space.World);

        // Clean up once this obstacle is safely behind the car.
        // (Car-relative, because the car itself travels forward along +Z.)
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
        if (car != null)
        {
            carTransform = car.transform;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckCrash(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckCrash(collision.gameObject);
    }

    private void CheckCrash(GameObject target)
    {
        if (target == null) return;

        CarController car = target.GetComponentInParent<CarController>();
        if (car == null) car = target.GetComponentInChildren<CarController>();
        if (car == null) return;

        ResolveImpact(car);
    }

    /// <summary>
    /// Single authority for a car/obstacle impact. The car forwards its own trigger and
    /// collision events here, and <c>hasResolved</c> guarantees the impact only applies once.
    /// A ChargePenalty hazard (pothole) only costs the pack. A Crash hazard consumes an active
    /// Civic Grant if there is one, and otherwise ends the run.
    /// </summary>
    public void ResolveImpact(CarController car)
    {
        if (hasResolved || car == null || !car.IsAlive()) return;

        // Verify spatial proximity to prevent false triggers across lanes or far ahead
        if (!IsSpatialHit(car.transform)) return;

        hasResolved = true;

        // A hole in the road is not something a shield can protect against: burn charge and move on.
        if (hazardKind == HazardKind.ChargePenalty)
        {
            ApplyChargePenalty(car);
            Destroy(gameObject);
            return;
        }

        // Shielded? Absorb the impact, consume the shield, destroy the obstacle.
        ShieldSystem shield = car.GetComponentInParent<ShieldSystem>();
        if (shield == null) shield = car.GetComponentInChildren<ShieldSystem>();

        if (shield != null && shield.ConsumeShield())
        {
            Destroy(gameObject);
            return;
        }

        car.OnCrash();
        HandleImpactCleanup();
    }

    /// <summary>
    /// Drains the pod's pack for clipping a ChargePenalty hazard, without ending the run.
    /// </summary>
    private void ApplyChargePenalty(CarController car)
    {
        BatterySystem battery = car.GetComponentInParent<BatterySystem>();
        if (battery == null) battery = car.GetComponentInChildren<BatterySystem>();
        if (battery == null) return;

        battery.DrainExtra(chargePenalty);
    }

    /// <summary>
    /// Validates that the obstacle is genuinely within collision range of the car in both X and Z.
    /// Prevents phantom triggers, out-of-lane collisions, and premature crashes.
    /// </summary>
    /// <summary>
    /// How far the car may legitimately be at the moment this hazard's trigger is
    /// reported. A trigger fires only on the physics tick where the colliders FIRST
    /// overlap, so deltaZ there is already (hazard half-depth + car half-depth) plus up
    /// to one tick of closing travel. A hard 2.5m cap was narrower than this hazard's
    /// own first-contact distance for the 3.2m-deep Congestion (3.3m), so the impact was
    /// rejected - and an enter event is never retried, so nothing ever resolved.
    /// </summary>
    private float LongitudinalAllowance(Transform carTransform)
    {
        Collider mine = GetComponent<Collider>();
        Collider theirs = carTransform != null ? carTransform.GetComponentInChildren<Collider>() : null;

        if (mine == null || theirs == null) return 2.5f;

        // Sum of the two half-depths closes the gap; +1m absorbs the worst case of one
        // physics tick of closing travel (25 m/s * 0.02s = 0.5m) with margin to spare.
        return mine.bounds.extents.z + theirs.bounds.extents.z + 1f;
    }

    private bool IsSpatialHit(Transform carTransform)
    {
        if (carTransform == null) return false;

        // 1. Check longitudinal Z distance (must be close to the car bumper, not 50m ahead!)
        float deltaZ = transform.position.z - carTransform.position.z;
        if (deltaZ > LongitudinalAllowance(carTransform) || deltaZ < -3.5f)
        {
            return false;
        }

        // 2. Check lateral X distance (must be in the same lane!)
        // In a 2.5m lane setup, two adjacent lanes are 2.5m apart.
        // A car and hazard block only touch if deltaX <= 1.6m.
        float deltaX = Mathf.Abs(transform.position.x - carTransform.position.x);
        if (deltaX > 1.6f)
        {
            return false;
        }

        return true;
    }

    private void HandleImpactCleanup()
    {
        if (destroyOnImpact)
        {
            Destroy(gameObject);
        }
    }

    public void SetSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }

    /// <summary>
    /// Tells this obstacle which car to track for despawn distance.
    /// Set by the spawner; falls back to a scene lookup if left unset.
    /// </summary>
    public void SetCar(Transform car)
    {
        carTransform = car;
    }
}
