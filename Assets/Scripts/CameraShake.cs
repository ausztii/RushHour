using UnityEngine;

/// <summary>
/// Short, sharp camera shake for impacts. Driven by unscaled time so it still plays
/// during the post-impact slow-motion freeze (when Time.timeScale is near zero).
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Tooltip("Multiplier applied to every shake request.")]
    [SerializeField] private float intensityScale = 1f;

    private Vector3 baseLocalPosition;
    private float timer;
    private float duration;
    private float amplitude;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    /// <summary>Kicks off a shake. Replaces any shake already running.</summary>
    public void Shake(float amplitude, float duration)
    {
        this.amplitude = amplitude * intensityScale;
        this.duration = Mathf.Max(0.01f, duration);
        timer = this.duration;
    }

    private void LateUpdate()
    {
        if (timer <= 0f)
        {
            if (transform.localPosition != baseLocalPosition)
            {
                transform.localPosition = baseLocalPosition;
            }
            return;
        }

        timer -= Time.unscaledDeltaTime;
        float falloff = Mathf.Clamp01(timer / duration);
        transform.localPosition = baseLocalPosition + (Random.insideUnitSphere * amplitude * falloff);
    }
}
