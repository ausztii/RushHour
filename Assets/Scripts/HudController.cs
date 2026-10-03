using UnityEngine;

/// <summary>
/// Single owner of everything the player reads: charge meter, route progress, the
/// district you are riding through, the route banner, the end-of-run result, and the
/// impact flash. Game logic never touches UI directly - it reports to this component.
/// </summary>
public class HudController : MonoBehaviour
{
    [System.Serializable]
    public class DistrictStop
    {
        [Tooltip("Distance travelled (metres) at which this district banner takes over.")]
        public float fromDistance;

        [Tooltip("Name shown under the distance readout.")]
        public string districtName;
    }

    [Header("Charge Meter")]
    [SerializeField] private BatterySystem battery;
    [SerializeField] private UnityEngine.UI.Image chargeFill;
    [SerializeField] private UnityEngine.UI.Text chargeValueText;
    [SerializeField] private UnityEngine.UI.Text lowChargeText;
    [Range(0f, 1f)]
    [SerializeField] private float lowChargeThreshold = 0.25f;
    [SerializeField] private Color chargeHighColor = new Color(0.30f, 0.88f, 0.45f);
    [SerializeField] private Color chargeMidColor = new Color(0.95f, 0.75f, 0.25f);
    [SerializeField] private Color chargeLowColor = new Color(0.95f, 0.28f, 0.22f);

    [Header("Route")]
    [SerializeField] private UnityEngine.UI.Text distanceText;
    [SerializeField] private UnityEngine.UI.Text districtText;
    [SerializeField] private DistrictStop[] districts;

    [Header("Route Banner")]
    [SerializeField] private CanvasGroup bannerGroup;
    [SerializeField] private UnityEngine.UI.Text bannerTitleText;
    [SerializeField] private UnityEngine.UI.Text bannerBodyText;

    [Header("Result")]
    [SerializeField] private UnityEngine.UI.Text resultTitleText;
    [SerializeField] private UnityEngine.UI.Text resultBodyText;
    [SerializeField] private GameObject restartButton;
    [SerializeField] private Color successColor = new Color(0.35f, 0.95f, 0.55f);
    [SerializeField] private Color failureColor = new Color(1f, 0.42f, 0.38f);

    [Header("Impact Flash")]
    [SerializeField] private UnityEngine.UI.Image flashOverlay;
    [SerializeField] private float flashFadeSpeed = 2.2f;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    private float bannerHoldTimer;
    private float bannerFadeTimer;
    private float bannerFadeDuration = 1f;
    private float flashAlpha;
    private Color flashColor = Color.clear;
    private int lastDistrictIndex = -1;
    private bool resultShown;

    private void Awake()
    {
        if (bannerGroup != null) bannerGroup.alpha = 0f;
        if (restartButton != null) restartButton.SetActive(false);
        if (lowChargeText != null) lowChargeText.gameObject.SetActive(false);

        SetVisible(resultTitleText, false);
        SetVisible(resultBodyText, false);

        if (flashOverlay != null)
        {
            Color c = flashOverlay.color;
            c.a = 0f;
            flashOverlay.color = c;
        }
    }

    private void Start()
    {
        // The brief the rider starts every run with.
        ShowBanner("ROUTE 7  ·  MEDICAL RUN",
            "Vaccines for the Greenfield Community Clinic  ·  shipment window closing  ·  500 m to go",
            4.5f, 1.5f);
    }

    private void Update()
    {
        UpdateCharge();
        UpdateRoute();
        UpdateBanner();
        UpdateFlash();
    }

    // ------------------------------------------------------------------ charge

    private void UpdateCharge()
    {
        if (battery == null) return;

        float norm = battery.NormalizedCharge;

        if (chargeFill != null)
        {
            chargeFill.fillAmount = norm;
            chargeFill.color = norm > 0.5f
                ? Color.Lerp(chargeMidColor, chargeHighColor, (norm - 0.5f) * 2f)
                : Color.Lerp(chargeLowColor, chargeMidColor, norm * 2f);
        }

        if (chargeValueText != null)
        {
            chargeValueText.text = Mathf.CeilToInt(norm * 100f) + "%";
        }

        if (lowChargeText != null)
        {
            bool low = norm <= lowChargeThreshold;

            if (lowChargeText.gameObject.activeSelf != low)
            {
                lowChargeText.gameObject.SetActive(low);
            }

            if (low)
            {
                float pulse = 0.55f + (0.45f * Mathf.Sin(Time.unscaledTime * 6f));
                lowChargeText.color = new Color(1f, 0.45f, 0.35f, pulse);
            }
        }
    }

    // ------------------------------------------------------------------- route

    private void UpdateRoute()
    {
        if (gameManager == null) return;

        if (distanceText != null)
        {
            distanceText.text = Mathf.FloorToInt(gameManager.DistanceTravelled) +
                                " / " + Mathf.RoundToInt(gameManager.deliveryDistance) + " m";
        }

        if (districtText == null || districts == null || districts.Length == 0) return;

        int index = 0;
        for (int i = 0; i < districts.Length; i++)
        {
            if (gameManager.DistanceTravelled >= districts[i].fromDistance) index = i;
        }

        if (index != lastDistrictIndex)
        {
            lastDistrictIndex = index;
            districtText.text = districts[index].districtName;
        }
    }

    // ------------------------------------------------------------- route banner

    /// <summary>Shows the route banner, holds, then fades it out.</summary>
    public void ShowBanner(string title, string body, float hold = 2.6f, float fade = 1f)
    {
        if (bannerGroup == null || resultShown) return;

        if (bannerTitleText != null) bannerTitleText.text = title;
        if (bannerBodyText != null) bannerBodyText.text = body;

        bannerFadeDuration = Mathf.Max(0.05f, fade);
        bannerHoldTimer = Mathf.Max(0f, hold);
        bannerFadeTimer = 0f;
        bannerGroup.alpha = 1f;
    }

    private void UpdateBanner()
    {
        if (bannerGroup == null) return;

        if (bannerHoldTimer > 0f)
        {
            bannerHoldTimer -= Time.unscaledDeltaTime;

            if (bannerHoldTimer <= 0f)
            {
                bannerFadeTimer = bannerFadeDuration;
            }
            return;
        }

        if (bannerFadeTimer > 0f)
        {
            bannerFadeTimer -= Time.unscaledDeltaTime;
            bannerGroup.alpha = Mathf.Clamp01(bannerFadeTimer / bannerFadeDuration);
        }
    }

    // ------------------------------------------------------------------ result

    /// <summary>End-of-run card. Replaces the banner and reveals the restart button.</summary>
    public void ShowResult(string title, string body, bool success)
    {
        resultShown = true;

        if (bannerGroup != null)
        {
            bannerHoldTimer = 0f;
            bannerFadeTimer = 0f;
            bannerGroup.alpha = 0f;
        }

        if (resultTitleText != null)
        {
            SetVisible(resultTitleText, true);
            resultTitleText.text = title;
            resultTitleText.color = success ? successColor : failureColor;
        }

        if (resultBodyText != null)
        {
            SetVisible(resultBodyText, true);
            resultBodyText.text = body;
        }

        if (restartButton != null) restartButton.SetActive(true);
    }

    // ------------------------------------------------------------- impact flash

    /// <summary>Red flash for a lost shipment.</summary>
    public void FlashImpact(float intensity)
    {
        flashColor = new Color(0.85f, 0.12f, 0.08f);
        flashAlpha = Mathf.Clamp01(intensity);
    }

    /// <summary>Cyan flash for a grant absorbing a hit.</summary>
    public void FlashGrant()
    {
        flashColor = new Color(0.25f, 0.85f, 1f);
        flashAlpha = 0.35f;
    }

    private void UpdateFlash()
    {
        if (flashOverlay == null || flashAlpha <= 0f) return;

        flashAlpha = Mathf.Max(0f, flashAlpha - (flashFadeSpeed * Time.unscaledDeltaTime));
        flashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
    }

    private static void SetVisible(UnityEngine.UI.Text text, bool visible)
    {
        if (text != null) text.gameObject.SetActive(visible);
    }
}
