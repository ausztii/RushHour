using UnityEngine;

/// <summary>
/// The pod's diegetic lights, so the pack level and the stakes of the cargo read on the vehicle
/// itself and not only in the HUD: the rear strip dims as the pack empties, and the cargo seal
/// breathes while the shipment is aboard.
/// Purely cosmetic - it changes no run state and writes nothing but its own renderers, through
/// property blocks, so no material instance is leaked at runtime.
/// </summary>
public class PodSignals : MonoBehaviour
{
    [Header("Pack Light Strip (rear)")]
    [Tooltip("Renderer of the rear charge strip. Fully lit on a full pack, dim when nearly flat.")]
    [SerializeField] private Renderer chargeStrip;

    [Tooltip("Colour of the strip on a healthy pack.")]
    [SerializeField] private Color chargeHighColor = new Color(0.35f, 0.95f, 0.48f);

    [Tooltip("Colour the strip fades towards as the pack empties.")]
    [SerializeField] private Color chargeLowColor = new Color(1f, 0.30f, 0.22f);

    [Tooltip("Emission of the strip on a full pack.")]
    [SerializeField] private float chargeLitEmission = 2.8f;

    [Tooltip("Emission floor, so a nearly flat pack still glows faintly.")]
    [SerializeField] private float chargeDimEmission = 0.20f;

    [Header("Cargo Seal")]
    [Tooltip("Renderer of the sealed cargo panel. Pulses slowly while the shipment is aboard.")]
    [SerializeField] private Renderer cargoGlow;

    [SerializeField] private Color cargoColor = new Color(0.45f, 1f, 0.72f);
    [SerializeField] private float cargoPulseSpeed = 2.2f;
    [SerializeField] private float cargoPulseDepth = 0.45f;
    [SerializeField] private float cargoEmission = 1.30f;

    [Header("References")]
    [SerializeField] private BatterySystem battery;

    private MaterialPropertyBlock block;

    private void Awake()
    {
        block = new MaterialPropertyBlock();

        if (battery == null) battery = GetComponent<BatterySystem>();
        if (battery == null) battery = GetComponentInParent<BatterySystem>();
    }

    private void Update()
    {
        UpdateChargeStrip();
        UpdateCargoGlow();
    }

    private void UpdateChargeStrip()
    {
        if (chargeStrip == null) return;

        float norm = battery != null ? battery.NormalizedCharge : 1f;

        Color color = Color.Lerp(chargeLowColor, chargeHighColor, norm);
        ApplyEmission(chargeStrip, color, Mathf.Lerp(chargeDimEmission, chargeLitEmission, norm));
    }

    private void UpdateCargoGlow()
    {
        if (cargoGlow == null) return;

        // 1 +- depth, centred so the average brightness stays put as it breathes.
        float pulse = 1f + (Mathf.Sin(Time.time * cargoPulseSpeed) * cargoPulseDepth * 0.5f);
        ApplyEmission(cargoGlow, cargoColor, cargoEmission * pulse);
    }

    private void ApplyEmission(Renderer target, Color color, float emission)
    {
        target.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        block.SetColor("_EmissionColor", color * emission);
        target.SetPropertyBlock(block);
    }
}
