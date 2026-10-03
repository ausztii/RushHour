using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls pod movement with discrete lane position snapping, smooth transition interpolation,
/// auto-forward driving, dynamic banking, battery drain, and crash detection.
/// Supports both Unity's New Input System and Legacy Input Manager.
/// </summary>
public class CarController : MonoBehaviour
{
    [Header("Forward Driving")]
    [Tooltip("Constant forward speed along the Z axis (set to 0 for stationary testing).")]
    public float forwardSpeed = 10f;

    [Header("Lane Snapping")]
    [Tooltip("Center of the road along X axis (Lane 0).")]
    public float laneCenterX = 0f;

    [Tooltip("Distance between adjacent lanes along horizontal (X) axis.")]
    public float laneDistance = 2.5f;

    [Tooltip("Leftmost lane index (-1 for a standard 3-lane setup).")]
    public int minLaneIndex = -1;

    [Tooltip("Rightmost lane index (1 for a standard 3-lane setup).")]
    public int maxLaneIndex = 1;

    [Tooltip("Starting lane index (0 is Center).")]
    public int startingLaneIndex = 0;

    public enum TransitionType
    {
        SmoothDamp,
        Lerp
    }

    [Header("Smooth Transition")]
    [Tooltip("Smoothing algorithm used to transition to the snapped target lane.")]
    public TransitionType transitionType = TransitionType.SmoothDamp;

    [Tooltip("Time in seconds to reach the target lane (lower is snappier, e.g. 0.1 - 0.15s).")]
    [Range(0.02f, 0.4f)]
    public float smoothDampTime = 0.12f;

    [Tooltip("Speed multiplier when using Lerp transition mode.")]
    public float lerpSpeed = 15f;

    [Header("Dynamic Body Tilt (Juice)")]
    [Tooltip("Enable subtle car body tilt / banking when switching lanes.")]
    public bool enableBanking = true;

    [Tooltip("Max roll angle in degrees during lane transitions.")]
    [Range(0f, 20f)]
    public float maxBankAngle = 8f;

    [Tooltip("Speed of tilting in and out of turns.")]
    public float bankSpeed = 10f;

    public enum ResetMode
    {
        RestartScene,   // Reloads the active scene (standard arcade restart)
        ResetPosition   // Snaps car back to start lane & position, destroys active obstacles
    }

    [Header("Collision & Reset")]
    [Tooltip("How the car resets when colliding with an obstacle.")]
    public ResetMode resetMode = ResetMode.RestartScene;

    [Tooltip("Delay (in seconds) before resetting to give visual feedback on crash.")]
    [Range(0f, 3f)]
    public float resetDelay = 0.5f;

    [Tooltip("Tags that trigger a crash on contact.")]
    public string[] obstacleTags = new string[] { "Obstacle" };

    [Tooltip("Automatically setup Rigidbody and BoxCollider if missing at runtime.")]
    public bool autoSetupPhysics = true;

    [Header("Collider Calibration (Accurate Fit)")]
    [Tooltip("Precise collider dimensions matching a single lane.")]
    public Vector3 colliderSize = new Vector3(1.8f, 1.2f, 4.0f);

    [Tooltip("Offset of the collider relative to the car root.")]
    public Vector3 colliderCenter = new Vector3(0f, 0.6f, 0f);

    [Tooltip("Particle effect or prefab to instantiate on crash (optional).")]
    public GameObject crashVfxPrefab;

    [Tooltip("Audio clip to play on crash (optional).")]
    public AudioClip crashSound;

    [Header("Debug")]
    [Tooltip("Print key press detection and lane transitions to the Unity Console.")]
    public bool showDebugLogs = false;

    [Header("References")]
    [Tooltip("The pod's battery pack. Drains while riding, and a little more on every swerve.")]
    public BatterySystem battery;

    [Tooltip("Child transform holding the pod's bodywork. Banking tilts this, so the car root " +
             "(and the rear camera parented to it) stays level.")]
    public Transform visualRoot;

    [Tooltip("Drag the GameManager object here.")]
    public GameManager gameManager;

    // Internal State
    private int currentLaneIndex;
    private float targetX;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float xVelocity;
    private float currentBankAngle;
    private bool isAlive = true;
    private bool isResetting = false;

    public float CalculateLaneTargetX(int lane)
    {
        return laneCenterX + (lane * laneDistance);
    }

    private void Awake()
    {
        currentLaneIndex = Mathf.Clamp(startingLaneIndex, minLaneIndex, maxLaneIndex);
        targetX = CalculateLaneTargetX(currentLaneIndex);

        // Snap car immediately to starting lane
        Vector3 pos = transform.position;
        pos.x = targetX;
        transform.position = pos;

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (autoSetupPhysics)
        {
            EnsurePhysicsSetup();
        }
    }

    private void Start()
    {
        if (battery == null)
            battery = GetComponent<BatterySystem>();

        if (gameManager == null)
            gameManager = Object.FindFirstObjectByType<GameManager>();
    }

    /// <summary>
    /// Ensures car has a Rigidbody (Kinematic) and BoxCollider with exact lane dimensions.
    /// </summary>
    [ContextMenu("Setup Accurate Car Collider & Rigidbody")]
    public void EnsurePhysicsSetup()
    {
        // 1. Ensure Rigidbody exists (Kinematic so car moves via script without unintended physics forces)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        // 2. Ensure BoxCollider exists with explicit, accurate dimensions
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
        }

        // Apply clean calibrated dimensions (fits a single 2.5m lane with clearance)
        box.size = colliderSize;
        box.center = colliderCenter;
        box.isTrigger = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.gameObject);
    }

    private void HandleHit(GameObject hitObj)
    {
        if (!isAlive || isResetting || hitObj == null) return;

        if (!IsObstacle(hitObj)) return;

        // Verify spatial proximity to prevent false triggers across lanes or far ahead
        if (!IsSpatialHit(hitObj.transform)) return;

        // Let the obstacle resolve the impact, so shield consumption happens exactly once
        // even though both the car and the obstacle receive the trigger event.
        Obstacle obstacle = hitObj.GetComponentInParent<Obstacle>();
        if (obstacle == null) obstacle = hitObj.GetComponentInChildren<Obstacle>();

        if (obstacle != null)
        {
            obstacle.ResolveImpact(this);
        }
        else
        {
            OnCrash();
        }
    }

    /// <summary>
    /// How far ahead (in metres) an obstacle may legitimately be at the moment its
    /// trigger is reported. A trigger fires only on the physics tick where the two
    /// colliders FIRST overlap, so deltaZ there is already (car half-depth + obstacle
    /// half-depth) plus up to one tick of closing travel. A hard 2.5m cap was narrower
    /// than the 3.2m-deep Congestion hazard, whose first contact sits at 3.3m, so its
    /// impact was rejected (and never retried) and the pod drove straight through it.
    /// </summary>
    private float LongitudinalAllowance(Transform obstacleTransform)
    {
        Collider mine = GetComponent<Collider>();
        Collider theirs = obstacleTransform != null ? obstacleTransform.GetComponentInChildren<Collider>() : null;

        if (mine == null || theirs == null) return 2.5f;

        // Sum of the two half-depths closes the gap; +1m absorbs the worst case of one
        // physics tick of closing travel (25 m/s * 0.02s = 0.5m) with margin to spare.
        return mine.bounds.extents.z + theirs.bounds.extents.z + 1f;
    }

    private bool IsSpatialHit(Transform obstacleTransform)
    {
        if (obstacleTransform == null) return false;

        // 1. Longitudinal distance: must be near the car bumper (not 50m ahead!)
        float deltaZ = obstacleTransform.position.z - transform.position.z;
        if (deltaZ > LongitudinalAllowance(obstacleTransform) || deltaZ < -3.5f) return false;

        // 2. Lateral distance: must be in the same lane!
        float deltaX = Mathf.Abs(obstacleTransform.position.x - transform.position.x);
        if (deltaX > 1.6f) return false;

        return true;
    }

    private bool IsObstacle(GameObject hitObj)
    {
        if (hitObj == null) return false;

        // Check for Obstacle component in parent or children
        if (hitObj.GetComponentInParent<Obstacle>() != null || hitObj.GetComponentInChildren<Obstacle>() != null)
        {
            return true;
        }

        // Check tags safely
        if (obstacleTags != null)
        {
            foreach (string tag in obstacleTags)
            {
                if (!string.IsNullOrEmpty(tag))
                {
                    try
                    {
                        if (hitObj.CompareTag(tag)) return true;
                    }
                    catch { }
                }
            }
        }

        // Check common naming patterns
        string lower = hitObj.name.ToLower();
        if (lower.Contains("obstacle") || lower.Contains("hazard") || lower.Contains("barrier"))
        {
            return true;
        }

        return false;
    }

    private void Update()
    {
        if (!isAlive) return;

        HandleLaneInput();
        UpdateMovement();

        if (enableBanking)
        {
            UpdateBanking();
        }
    }

    /// <summary>
    /// Detects Left / Right arrow keys or A / D keys using the active Input System.
    /// </summary>
    private void HandleLaneInput()
    {
        bool leftPressed = false;
        bool rightPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            leftPressed = Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame;
            rightPressed = Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!leftPressed)
        {
            leftPressed = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        }
        if (!rightPressed)
        {
            rightPressed = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        }
#endif

        if (leftPressed)
        {
            if (showDebugLogs) Debug.Log($"[CarController] <color=yellow>Left Arrow / A key detected</color>");
            ShiftLane(-1);
        }
        else if (rightPressed)
        {
            if (showDebugLogs) Debug.Log($"[CarController] <color=yellow>Right Arrow / D key detected</color>");
            ShiftLane(1);
        }
    }

    /// <summary>
    /// Snaps target lane left (-1) or right (+1) and applies dodging fuel penalty.
    /// </summary>
    public void ShiftLane(int direction)
    {
        int newLane = Mathf.Clamp(currentLaneIndex + direction, minLaneIndex, maxLaneIndex);

        if (newLane != currentLaneIndex)
        {
            currentLaneIndex = newLane;
            targetX = CalculateLaneTargetX(currentLaneIndex);
            if (showDebugLogs) Debug.Log($"[CarController] <color=green>Shifted to Lane: {currentLaneIndex}</color> (Target X = {targetX:F2})");

            // Swerving is a deliberate extra draw on the pack
            if (battery != null)
            {
                battery.DrainExtra(battery.swerveCost);
            }
        }
        else
        {
            if (showDebugLogs) Debug.Log($"[CarController] <color=orange>Lane limit reached (Lane {currentLaneIndex})</color>");
        }
    }

    /// <summary>
    /// Smoothly transitions X position to target lane and translates forward along Z.
    /// </summary>
    private void UpdateMovement()
    {
        Vector3 currentPos = transform.position;
        float newX = currentPos.x;

        switch (transitionType)
        {
            case TransitionType.SmoothDamp:
                newX = Mathf.SmoothDamp(currentPos.x, targetX, ref xVelocity, smoothDampTime);
                break;

            case TransitionType.Lerp:
                newX = Mathf.Lerp(currentPos.x, targetX, Time.deltaTime * lerpSpeed);
                break;
        }

        // Auto forward movement
        float newZ = currentPos.z + (forwardSpeed * Time.deltaTime);

        transform.position = new Vector3(newX, currentPos.y, newZ);
    }

    /// <summary>
    /// Dynamically rolls the car body into lane switches and rights itself upon arriving.
    /// </summary>
    private void UpdateBanking()
    {
        // Bank the bodywork only. Rolling the car root would roll the rear camera with it.
        Transform bankTarget = visualRoot != null ? visualRoot : transform;

        float deltaX = targetX - transform.position.x;
        float targetBank = Mathf.Clamp(-deltaX * (maxBankAngle / Mathf.Max(laneDistance, 0.1f)), -maxBankAngle, maxBankAngle);

        currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, Time.deltaTime * bankSpeed);

        Vector3 euler = bankTarget.localEulerAngles;
        bankTarget.localEulerAngles = new Vector3(euler.x, euler.y, currentBankAngle);
    }

    /// <summary>
    /// Called when the car collides with an obstacle.
    /// </summary>
    public void OnCrash()
    {
        if (!isAlive || isResetting) return;
        isAlive = false;

        if (showDebugLogs) Debug.Log("<color=red><b>💥 CRASH! Car collided with an obstacle!</b></color>");

        // Audio feedback
        if (crashSound != null)
        {
            AudioSource.PlayClipAtPoint(crashSound, transform.position);
        }

        // Visual FX
        if (crashVfxPrefab != null)
        {
            Instantiate(crashVfxPrefab, transform.position, Quaternion.identity);
        }

        if (gameManager != null)
        {
            // The GameManager owns the end-of-run state (pause + result text + restart button),
            // so the car must not auto-reset out from under it.
            gameManager.TriggerFailure("Cargo compromised");
        }
        else
        {
            // No GameManager in the scene: fall back to the car's own reset behaviour.
            StartCoroutine(ResetRoutine());
        }
    }

    private System.Collections.IEnumerator ResetRoutine()
    {
        isResetting = true;

        if (resetDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(resetDelay);
        }

        if (resetMode == ResetMode.RestartScene)
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
        else
        {
            ResetCarState();
        }
    }

    /// <summary>
    /// Resets pod position, lane, charge, and eliminates active hazards without reloading the scene.
    /// </summary>
    public void ResetCarState()
    {
        currentLaneIndex = Mathf.Clamp(startingLaneIndex, minLaneIndex, maxLaneIndex);
        targetX = CalculateLaneTargetX(currentLaneIndex);

        Vector3 pos = initialPosition;
        pos.x = targetX;
        transform.position = pos;
        transform.rotation = initialRotation;

        currentBankAngle = 0f;
        xVelocity = 0f;

        // Refill the pack
        if (battery != null)
        {
            battery.ResetCharge();
        }

        // Clear active obstacles on the road so car doesn't immediately crash again
        ObstacleSpawner spawner = Object.FindFirstObjectByType<ObstacleSpawner>();
        if (spawner != null)
        {
            spawner.ClearAllObstacles();
            spawner.ResetSpawner();
        }
        else
        {
            Obstacle[] obstacles = Object.FindObjectsByType<Obstacle>(FindObjectsSortMode.None);
            foreach (Obstacle obs in obstacles)
            {
                if (obs != null) Destroy(obs.gameObject);
            }
        }

        isAlive = true;
        isResetting = false;
        Time.timeScale = 1f;

        if (showDebugLogs) Debug.Log("<color=green><b>✔ Car reset to starting position and lane. Ready!</b></color>");
    }

    public bool IsAlive() => isAlive;
    public int CurrentLane => currentLaneIndex;

    /// <summary>
    /// Visualize lane tracks and collision box in the Unity Editor Scene view.
    /// </summary>
    private void OnDrawGizmos()
    {
        // Draw green collision box around the car
        Gizmos.color = isAlive ? Color.green : Color.red;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(colliderCenter, colliderSize);
        Gizmos.matrix = oldMatrix;
    }

    private void OnDrawGizmosSelected()
    {
        float baseX = laneCenterX;
        float y = transform.position.y;
        float z = transform.position.z;

        for (int lane = minLaneIndex; lane <= maxLaneIndex; lane++)
        {
            float lx = baseX + (lane * laneDistance);
            Gizmos.color = (lane == currentLaneIndex) ? Color.green : new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawLine(new Vector3(lx, y, z - 20f), new Vector3(lx, y, z + 20f));
            Gizmos.DrawWireCube(new Vector3(lx, y, z), new Vector3(1.2f, 0.1f, 2.5f));
        }
    }
}
