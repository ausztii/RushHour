using UnityEngine;

/// <summary>
/// The electric delivery pod's battery pack. Range is the real constraint on this run:
/// the pack drains continuously while the pod is moving, and every swerve the crumbling
/// roads force you into costs a little more than the straight line would have.
/// </summary>
public class BatterySystem : MonoBehaviour
{
    [Header("Pack")]
    [Tooltip("Battery capacity, in percent.")]
    public float maxCharge = 100f;

    [Tooltip("Charge currently left in the pack, in percent.")]
    public float currentCharge = 100f;

    [Header("Drain")]
    [Tooltip("Percent of pack drained per second of normal riding.")]
    public float drainPerSecond = 1.5f;

    [Tooltip("Extra charge a single lane swerve costs. Kept small on purpose - a swerve is a nudge, not a detour.")]
    public float swerveCost = 1f;

    [Header("References")]
    [Tooltip("GameManager that reports the shipment lost when the pack runs dry.")]
    public GameManager gameManager;

    [Tooltip("HUD, so a recharge can announce itself.")]
    [SerializeField] private HudController hud;

    private bool isDepleted;

    public bool IsDepleted => isDepleted;
    public bool HasCharge => !isDepleted && currentCharge > 0f;

    /// <summary>Charge as 0-1, for meters and bar fills.</summary>
    public float NormalizedCharge => maxCharge <= 0f ? 0f : Mathf.Clamp01(currentCharge / maxCharge);

    private void Start()
    {
        currentCharge = maxCharge;

        if (gameManager == null)
        {
            gameManager = Object.FindAnyObjectByType<GameManager>();
        }
    }

    private void Update()
    {
        if (isDepleted) return;

        Drain(drainPerSecond * Time.deltaTime);
    }

    /// <summary>Takes charge out of the pack. Running dry loses the shipment.</summary>
    public void Drain(float amount)
    {
        if (isDepleted || amount <= 0f) return;

        currentCharge = Mathf.Max(0f, currentCharge - amount);

        if (currentCharge <= 0f)
        {
            isDepleted = true;
            if (gameManager != null)
            {
                gameManager.TriggerFailure("Battery depleted");
            }
        }
    }

    /// <summary>Drain for a deliberate extra draw, such as a lane swerve.</summary>
    public void DrainExtra(float amount)
    {
        Drain(amount);
    }

    /// <summary>Puts charge back in the pack (grid recharge points).</summary>
    public void AddCharge(float amount)
    {
        if (isDepleted || amount <= 0f) return;

        currentCharge = Mathf.Min(maxCharge, currentCharge + amount);

        if (hud != null)
        {
            hud.ShowBanner("RECHARGE POINT", "Grid pilot investment secured  ·  +" + Mathf.RoundToInt(amount) + "%");
        }
    }

    /// <summary>Refills the pack and clears the depleted latch.</summary>
    public void ResetCharge()
    {
        isDepleted = false;
        currentCharge = maxCharge;
    }
}
