using UnityEngine;

/// <summary>
/// Drives the start screen's living backdrop: the lane dashes stream toward the camera so the
/// parked pod reads as already rolling down Route 7, and the pod breathes gently on its
/// suspension so the menu is never a still frame.
///
/// Presentation only. It owns no run state and nothing reads back what it writes, so it stays
/// clear of every system that GameManager, HudController and CarController own.
/// </summary>
public class StartScreenBackdrop : MonoBehaviour
{
    /// <summary>Lane dashes to recycle, assigned by the scene builder.</summary>
    public Transform[] laneDashes;

    /// <summary>The parked hero pod, bobbed on its suspension.</summary>
    public Transform heroPod;

    /// <summary>Metres per second the road markings travel toward the camera.</summary>
    public float scrollSpeed = 14f;

    /// <summary>How far a dash travels before it wraps back to the far end of its run.</summary>
    public float recycleLength = 180f;

    /// <summary>Z a recycled dash reappears at - the far end of the visible stretch.</summary>
    public float respawnZ = 100f;

    /// <summary>Vertical travel of the pod's idle bob, in metres.</summary>
    public float bobAmplitude = 0.035f;

    /// <summary>Idle bob cycles per second.</summary>
    public float bobFrequency = 0.9f;

    private Vector3[] basePositions;
    private Vector3 podBasePosition;

    private void Awake()
    {
        if (laneDashes != null)
        {
            basePositions = new Vector3[laneDashes.Length];
            for (int i = 0; i < laneDashes.Length; i++)
            {
                if (laneDashes[i] != null) basePositions[i] = laneDashes[i].localPosition;
            }
        }

        if (heroPod != null) podBasePosition = heroPod.localPosition;
    }

    private void Update()
    {
        float travel = scrollSpeed * Time.deltaTime;

        if (laneDashes != null)
        {
            for (int i = 0; i < laneDashes.Length; i++)
            {
                Transform dash = laneDashes[i];
                if (dash == null) continue;

                Vector3 position = dash.localPosition;
                position.z -= travel;

                // Wrapping by the full cycle rather than to the dash's own start keeps the
                // spacing intact, so two dashes never land on the same z.
                if (position.z < respawnZ) position.z += recycleLength;

                dash.localPosition = position;
            }
        }

        if (heroPod != null)
        {
            Vector3 position = podBasePosition;
            position.y += Mathf.Sin(Time.time * Mathf.PI * 2f * bobFrequency) * bobAmplitude;
            heroPod.localPosition = position;
        }
    }
}
