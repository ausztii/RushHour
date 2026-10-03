using UnityEngine;

/// <summary>
/// Temporary protection for the pod, granted by Civic Grant collectibles. While active,
/// hitting a hazard consumes the grant and destroys the hazard instead of ending the run.
/// Lives on the pod root so hazards can find it via GetComponentInParent.
/// </summary>
public class ShieldSystem : MonoBehaviour
{
    [Header("Shield Settings")]
    [Tooltip("How long (in seconds) a collected grant lasts.")]
    public float shieldDuration = 5f;

    [Header("Visual")]
    [Tooltip("Child object used as the glowing shield bubble. Toggled on/off with the grant.")]
    public GameObject shieldVisual;

    [Tooltip("How fast the shield bubble pulses while active.")]
    public float pulseSpeed = 6f;

    [Tooltip("How much the shield bubble scales while pulsing.")]
    [Range(0f, 0.3f)]
    public float pulseAmount = 0.06f;

    [Header("Feedback")]
    [Tooltip("Camera shake amplitude when a grant absorbs an impact.")]
    [SerializeField] private float absorbShakeAmplitude = 0.16f;

    [Tooltip("Camera shake duration (realtime seconds) when a grant absorbs an impact.")]
    [SerializeField] private float absorbShakeDuration = 0.25f;

    [Header("References")]
    [Tooltip("HUD, so a granted shield can announce itself and a consumed one can flash.")]
    [SerializeField] private HudController hud;

    [Tooltip("Camera shake driver for the absorb thump.")]
    [SerializeField] private CameraShake cameraShake;

    private float shieldTimer;
    private Vector3 shieldVisualBaseScale = Vector3.one;
    private bool hasAnnounced;

    /// <summary>True while the pod cannot be destroyed by a hazard.</summary>
    public bool IsShielded => shieldTimer > 0f;

    /// <summary>Seconds of protection remaining.</summary>
    public float RemainingTime => shieldTimer;

    private void Awake()
    {
        if (shieldVisual != null)
        {
            shieldVisualBaseScale = shieldVisual.transform.localScale;
            shieldVisual.SetActive(false);
        }
    }

    private void Update()
    {
        if (shieldTimer <= 0f) return;

        shieldTimer -= Time.deltaTime;

        if (shieldTimer <= 0f)
        {
            shieldTimer = 0f;
            hasAnnounced = false;
            SetVisual(false);
            return;
        }

        // Gentle pulse so the shield reads as an active grant
        if (shieldVisual != null && shieldVisual.activeSelf)
        {
            float pulse = 1f + (Mathf.Sin(Time.time * pulseSpeed) * pulseAmount);
            shieldVisual.transform.localScale = shieldVisualBaseScale * pulse;
        }
    }

    /// <summary>Grants protection for the default duration.</summary>
    public void ActivateShield()
    {
        ActivateShield(shieldDuration);
    }

    /// <summary>
    /// Grants protection for a specific duration.
    /// Refreshes an active shield rather than stacking it.
    /// </summary>
    public void ActivateShield(float duration)
    {
        shieldTimer = Mathf.Max(shieldTimer, duration);
        SetVisual(true);

        if (hud != null && !hasAnnounced)
        {
            hasAnnounced = true;
            hud.ShowBanner("CIVIC GRANT",
                "Emergency maintenance shield  ·  " + Mathf.RoundToInt(duration) + "s of cover");
        }
    }

    /// <summary>
    /// Consumes the shield on impact. Returns true if protection was active.
    /// </summary>
    public bool ConsumeShield()
    {
        if (!IsShielded) return false;

        shieldTimer = 0f;
        hasAnnounced = false;
        SetVisual(false);

        if (hud != null) hud.FlashGrant();
        if (cameraShake != null) cameraShake.Shake(absorbShakeAmplitude, absorbShakeDuration);

        return true;
    }

    /// <summary>Removes any active protection.</summary>
    public void ResetShield()
    {
        shieldTimer = 0f;
        hasAnnounced = false;
        SetVisual(false);
    }

    private void SetVisual(bool visible)
    {
        if (shieldVisual == null) return;

        shieldVisual.SetActive(visible);

        if (!visible)
        {
            shieldVisual.transform.localScale = shieldVisualBaseScale;
        }
    }
}
