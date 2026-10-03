using UnityEngine;

/// <summary>
/// Owns the state of the run and nothing else: how far the pod has carried the shipment,
/// and whether the delivery was completed or lost. Every message the player reads is handed
/// to the HudController, which is the only component allowed to touch UI.
/// Attach to an empty GameObject named "GameManager".
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Delivery Goal")]
    [Tooltip("Reference to the player's pod Transform.")]
    public Transform car;

    [Tooltip("Target distance (in metres) the pod must travel to complete delivery.")]
    public float deliveryDistance = 500f;

    [Header("Failure Feel")]
    [Tooltip("Slowed-down time scale applied for the moment of impact, before the freeze.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float hitStopScale = 0.18f;

    [Tooltip("Realtime seconds spent in hit-stop before the run freezes outright.")]
    [SerializeField] private float hitStopDuration = 0.5f;

    [Tooltip("Camera shake amplitude kicked off when the shipment is lost.")]
    [SerializeField] private float failureShakeAmplitude = 0.32f;

    [Tooltip("Camera shake duration (realtime seconds) when the shipment is lost.")]
    [SerializeField] private float failureShakeDuration = 0.5f;

    [Header("Auto Restart")]
    [Tooltip("If true, automatically restarts the level after failure.")]
    public bool autoRestartOnFailure = false;

    [Tooltip("Delay in seconds (realtime) before auto-restarting.")]
    public float restartDelay = 1.0f;

    [Header("Navigation")]
    [Tooltip("Scene name of the title screen the player returns to out of a lost shipment.")]
    public string menuSceneName = "StartScreen";

    [Header("References")]
    [Tooltip("The HUD that presents every message to the player.")]
    [SerializeField] private HudController hud;

    [Tooltip("Camera shake driver, kicked on a lost shipment.")]
    [SerializeField] private CameraShake cameraShake;

    private Vector3 startPosition;
    private bool gameOver;

    /// <summary>True once the run has ended, win or lose.</summary>
    public bool IsGameOver => gameOver;

    /// <summary>Metres covered along the route so far.</summary>
    public float DistanceTravelled
    {
        get
        {
            if (car == null) return 0f;
            return Mathf.Max(0f, car.position.z - startPosition.z);
        }
    }

    /// <summary>Metres still to cover before the clinic.</summary>
    public float RemainingDistance => Mathf.Max(0f, deliveryDistance - DistanceTravelled);

    private void Start()
    {
        if (car == null)
        {
            CarController carController = Object.FindFirstObjectByType<CarController>();
            if (carController != null) car = carController.transform;
        }

        if (car != null)
        {
            startPosition = car.position;
        }

        if (hud == null) hud = Object.FindFirstObjectByType<HudController>();
        if (cameraShake == null) cameraShake = Object.FindFirstObjectByType<CameraShake>();
    }

    private void Update()
    {
        if (gameOver || car == null) return;

        if (DistanceTravelled >= deliveryDistance)
        {
            TriggerSuccess();
        }
    }

    /// <summary>The shipment reached the Outer District Clinic.</summary>
    public void TriggerSuccess()
    {
        if (gameOver) return;
        gameOver = true;

        if (hud != null)
        {
            hud.ShowResult(
                "DELIVERY COMPLETE",
                "Vaccines handed over at the Greenfield Community Clinic  ·  " +
                Mathf.RoundToInt(deliveryDistance) + " m on Route 7",
                true);
        }

        Time.timeScale = 0f;
    }

    /// <summary>
    /// The run ended badly: either the pod was wrecked ("Cargo compromised") or the
    /// pack ran flat ("Battery depleted").
    /// </summary>
    public void TriggerFailure(string reason)
    {
        if (gameOver) return;
        gameOver = true;

        if (hud != null)
        {
            hud.ShowResult(
                "SHIPMENT LOST",
                reason + "  ·  " + Mathf.RoundToInt(RemainingDistance) + " m from the clinic",
                false);

            hud.FlashImpact(0.5f);
        }

        if (cameraShake != null)
        {
            cameraShake.Shake(failureShakeAmplitude, failureShakeDuration);
        }

        if (autoRestartOnFailure)
        {
            StartCoroutine(AutoRestartCoroutine());
        }
        else
        {
            StartCoroutine(HitStopCoroutine());
        }
    }

    /// <summary>
    /// A brief slow-motion beat on impact, then a full freeze so the result card reads clearly.
    /// Realtime, because timeScale is exactly what is being manipulated.
    /// </summary>
    private System.Collections.IEnumerator HitStopCoroutine()
    {
        Time.timeScale = hitStopScale;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = 0f;
    }

    private System.Collections.IEnumerator AutoRestartCoroutine()
    {
        yield return new WaitForSecondsRealtime(restartDelay);
        RestartLevel();
    }

    /// <summary>
    /// Reloads the current scene. Hooked to the HUD's restart button.
    /// </summary>
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }

    /// <summary>
    /// Leaves the run for the title screen. Hooked to the HUD's return button, which is offered
    /// alongside the restart so a lost shipment is never a dead end.
    /// </summary>
    public void ReturnToMenu()
    {
        // The run freezes at zero on a lost shipment, and the title screen would come up frozen
        // with it if that were carried across the load.
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(menuSceneName);
    }
}
