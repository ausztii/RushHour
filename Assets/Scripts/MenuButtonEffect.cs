using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Adds a stylized hover effect to menu buttons, resembling the "Black Jacket" reference.
/// It scales and tilts the text slightly while revealing a "brush stroke" image behind it.
/// </summary>
public class MenuButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI References")]
    [Tooltip("The image component for the brush stroke highlight behind the text.")]
    public Image brushHighlight;
    [Tooltip("The RectTransform of the Text or TextMeshPro component.")]
    public RectTransform textTransform;

    [Header("Settings")]
    public float hoverScale = 1.05f;
    public float hoverTilt = 2f;
    public float transitionSpeed = 15f;
    public AudioClip hoverSound;
    public AudioClip clickSound;

    private AudioSource audioSource;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private bool isHovered = false;

    private void Awake()
    {
        if (textTransform != null)
        {
            originalScale = textTransform.localScale;
            originalRotation = textTransform.localRotation;
        }

        if (brushHighlight != null)
        {
            // Start with the brush highlight invisible
            brushHighlight.canvasRenderer.SetAlpha(0f);
        }

        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        if (textTransform != null)
        {
            Vector3 targetScale = isHovered ? originalScale * hoverScale : originalScale;
            Quaternion targetRotation = isHovered ? originalRotation * Quaternion.Euler(0, 0, hoverTilt) : originalRotation;

            textTransform.localScale = Vector3.Lerp(textTransform.localScale, targetScale, Time.deltaTime * transitionSpeed);
            textTransform.localRotation = Quaternion.Lerp(textTransform.localRotation, targetRotation, Time.deltaTime * transitionSpeed);
        }

        if (brushHighlight != null)
        {
            float targetAlpha = isHovered ? 1f : 0f;
            float currentAlpha = brushHighlight.canvasRenderer.GetAlpha();
            brushHighlight.canvasRenderer.SetAlpha(Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * transitionSpeed));
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (hoverSound != null)
        {
            audioSource.PlayOneShot(hoverSound);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
    }
}
