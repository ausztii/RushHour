using UnityEngine;

/// <summary>
/// The Greenfield Community Clinic drop-off: driving into the covered bay completes the delivery.
/// Arrival is the win condition, so the odometer is only the backstop.
/// Uses the same pattern as the hazard and collectible triggers - a kinematic Rigidbody on the
/// volume - so the trigger fires reliably against the pod's kinematic body, and defers to the
/// GameManager, which owns the run state and decides what the player is told.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ClinicTrigger : MonoBehaviour
{
    [Tooltip("GameManager that owns the run. TriggerSuccess is fired the moment the pod arrives.")]
    public GameManager gameManager;

    private bool hasFired;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        if (gameManager == null)
        {
            gameManager = Object.FindAnyObjectByType<GameManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasFired || other == null) return;

        CarController car = other.GetComponentInParent<CarController>();
        if (car == null) car = other.GetComponentInChildren<CarController>();
        if (car == null) return;

        hasFired = true;

        if (gameManager != null)
        {
            gameManager.TriggerSuccess();
        }
    }
}
