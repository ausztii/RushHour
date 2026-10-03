using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns hazards in the 3 lanes ahead of the pod, with a spawn interval that ramps
/// down over time for difficulty scaling, plus occasional Civic Grant / grid recharge collectibles.
/// Ensures at least one lane is always open so every wave is dodgeable.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    [Header("Spawn Timing (Difficulty Ramp)")]
    [Tooltip("Seconds between waves at the start of the run.")]
    [SerializeField] private float startingSpawnInterval = 2.0f;

    [Tooltip("Fastest the waves ever get (reached after the ramp duration).")]
    [SerializeField] private float minimumSpawnInterval = 0.6f;

    [Tooltip("Seconds of play over which the interval ramps from starting to minimum.")]
    [SerializeField] private float difficultyRampDuration = 60f;

    [Tooltip("Delay before the very first wave spawns.")]
    [SerializeField] private float startDelay = 2.0f;

    [Tooltip("Speed given to spawned obstacles (should match road scroll feel).")]
    [SerializeField] private float obstacleSpeed = 15f;

    [Header("Spawn Position")]
    [Tooltip("Distance ahead of the car (along +Z) where waves appear.")]
    [SerializeField] private float spawnDistanceAhead = 50f;

    [Tooltip("Y height for spawned obstacles above the road.")]
    [SerializeField] private float spawnY = 0.5f;

    [Tooltip("Y height for spawned collectibles above the road.")]
    [SerializeField] private float pickupY = 1.0f;

    [Tooltip("Horizontal X positions for the 3 lanes (Left, Center, Right).")]
    [SerializeField] private float[] laneXPositions = new float[] { -2.5f, 0f, 2.5f };

    [Header("Prefabs")]
    [Tooltip("Obstacle prefabs. One is picked at random per spawn. If empty, a procedural hazard block is created.")]
    [SerializeField] private GameObject[] obstaclePrefabs;

    [Header("Collectibles")]
    [Tooltip("Chance (0-1) that a wave also drops a collectible into a lane left open.")]
    [Range(0f, 1f)]
    [SerializeField] private float collectibleSpawnChance = 0.35f;

    [Tooltip("Civic Grant prefab (absorbs one hazard, and grants brief protection).")]
    [SerializeField] private GameObject shieldPickupPrefab;

    [Tooltip("Grid recharge point prefab (puts charge back in the pack).")]
    [SerializeField] private GameObject chargePickupPrefab;

    [Tooltip("When a collectible spawns, the chance it is a Civic Grant rather than a recharge point.")]
    [Range(0f, 1f)]
    [SerializeField] private float shieldPickupWeight = 0.75f;

    [Header("Maintained Road (Clinic Approach)")]
    [Tooltip("Distance (metres) travelled from which Route 7 has been resurfaced: charge-penalty " +
             "hazards such as potholes stop appearing, so the run to the clinic cleans up.")]
    [SerializeField] private float maintainedRoadFromDistance = 380f;

    [Header("References")]
    [SerializeField] private CarController carController;

    [Tooltip("GameManager, so the spawner can tell how far down Route 7 the pod is.")]
    [SerializeField] private GameManager gameManager;

    private float timer = 0f;
    private float elapsedTime = 0f;
    private float currentSpawnInterval;
    private bool hasSpawnedFirstWave = false;
    private Material defaultHazardMaterial;

    /// <summary>Current seconds-between-waves, after difficulty ramping.</summary>
    public float CurrentSpawnInterval => currentSpawnInterval;

    private void Start()
    {
        currentSpawnInterval = startingSpawnInterval;
        timer = 0f;
        elapsedTime = 0f;
        hasSpawnedFirstWave = false;

        // Auto-find car reference
        if (carController == null) carController = Object.FindFirstObjectByType<CarController>();
        if (gameManager == null) gameManager = Object.FindFirstObjectByType<GameManager>();
    }

    private void Update()
    {
        // Don't spawn if car has crashed
        if (carController != null && !carController.IsAlive()) return;

        elapsedTime += Time.deltaTime;

        // Ramp the interval down from starting to minimum across difficultyRampDuration seconds
        float rampT = difficultyRampDuration > 0f
            ? Mathf.Clamp01(elapsedTime / difficultyRampDuration)
            : 1f;
        currentSpawnInterval = Mathf.Lerp(startingSpawnInterval, minimumSpawnInterval, rampT);

        timer += Time.deltaTime;

        float threshold = hasSpawnedFirstWave ? currentSpawnInterval : startDelay;

        if (timer >= threshold)
        {
            timer = 0f;
            hasSpawnedFirstWave = true;
            SpawnWave();
        }
    }

    /// <summary>
    /// Spawns 1 or 2 obstacles across the 3 lanes, guaranteeing at least 1 lane stays clear,
    /// and occasionally drops a collectible into one of the open lanes.
    /// </summary>
    public void SpawnWave()
    {
        float spawnZ = GetSpawnZ();

        // Pick 1 or 2 lanes to fill (never all 3!)
        List<int> availableLanes = new List<int> { 0, 1, 2 };
        int lanesToFill = (Random.value < 0.35f) ? 2 : 1; // 35% chance of 2 obstacles, 65% chance of 1

        for (int i = 0; i < lanesToFill; i++)
        {
            int chosenIndex = Random.Range(0, availableLanes.Count);
            int lane = availableLanes[chosenIndex];
            availableLanes.RemoveAt(chosenIndex);

            SpawnObstacle(new Vector3(laneXPositions[lane], spawnY, spawnZ));
        }

        // Collectible in a lane that was left open
        if (availableLanes.Count > 0 && Random.value < collectibleSpawnChance)
        {
            GameObject prefab = PickCollectiblePrefab();
            if (prefab != null)
            {
                int lane = availableLanes[Random.Range(0, availableLanes.Count)];
                Instantiate(prefab, new Vector3(laneXPositions[lane], pickupY, spawnZ), Quaternion.identity);
            }
        }
    }

    /// <summary>
    /// World Z just ahead of the car, so every wave spawns in front of it
    /// no matter how far down the road the car has travelled.
    /// </summary>
    private float GetSpawnZ()
    {
        Transform car = GetCarTransform();
        float baseZ = car != null ? car.position.z : transform.position.z;
        return baseZ + spawnDistanceAhead;
    }

    private Transform GetCarTransform()
    {
        return carController != null ? carController.transform : null;
    }

    private GameObject PickCollectiblePrefab()
    {
        if (shieldPickupPrefab == null) return chargePickupPrefab;
        if (chargePickupPrefab == null) return shieldPickupPrefab;

        return (Random.value < shieldPickupWeight) ? shieldPickupPrefab : chargePickupPrefab;
    }

    private void SpawnObstacle(Vector3 position)
    {
        GameObject obstacleObj;

        GameObject prefab = PickObstaclePrefab();

        if (prefab != null)
        {
            obstacleObj = Instantiate(prefab, position, Quaternion.identity);
        }
        else
        {
            // Procedural default hazard block
            obstacleObj = CreateProceduralHazardBlock(position);
        }

        // Configure movement
        Obstacle obsComp = obstacleObj.GetComponent<Obstacle>();
        if (obsComp != null)
        {
            obsComp.SetSpeed(obstacleSpeed);
            obsComp.SetCar(GetCarTransform());
        }
    }

    /// <summary>
    /// Picks this wave's hazard from the assigned prefabs. Past the resurfaced stretch of Route 7
    /// the potholes are gone, so only physical hazards remain - the road itself now tells the
    /// player they are nearly at the clinic. Single-pass reservoir pick, so the choice stays
    /// uniform over whichever prefabs are eligible.
    /// </summary>
    private GameObject PickObstaclePrefab()
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) return null;

        bool maintained = DistanceTravelled >= maintainedRoadFromDistance;

        GameObject picked = null;
        int candidates = 0;

        for (int i = 0; i < obstaclePrefabs.Length; i++)
        {
            GameObject candidate = obstaclePrefabs[i];
            if (candidate == null) continue;

            Obstacle obstacle = candidate.GetComponent<Obstacle>();
            if (maintained && obstacle != null && obstacle.IsChargePenalty) continue;

            candidates++;
            if (Random.Range(0, candidates) == 0) picked = candidate;
        }

        return picked;
    }

    /// <summary>Metres the pod has covered along Route 7, measured the way the GameManager does.</summary>
    private float DistanceTravelled
    {
        get
        {
            if (gameManager == null) gameManager = Object.FindFirstObjectByType<GameManager>();
            return gameManager != null ? gameManager.DistanceTravelled : 0f;
        }
    }

    /// <summary>
    /// Creates a clean, ready-to-use hazard block if no custom prefabs are assigned.
    /// </summary>
    private GameObject CreateProceduralHazardBlock(Vector3 position)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Hazard_Barrier";
        block.transform.position = position;
        block.transform.localScale = new Vector3(1.6f, 1.0f, 0.6f);

        // Setup BoxCollider as trigger
        BoxCollider col = block.GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Add kinematic Rigidbody so Unity triggers collision events reliably
        Rigidbody rb = block.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = block.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        // Tag as Obstacle (the tag is defined in the project; ignored if it is missing)
        try { block.tag = "Obstacle"; } catch { }

        // Add Obstacle component
        Obstacle obs = block.AddComponent<Obstacle>();
        obs.SetSpeed(obstacleSpeed);
        obs.SetCar(GetCarTransform());

        // Color hazard orange/red
        Renderer rend = block.GetComponent<Renderer>();
        if (rend != null)
        {
            if (defaultHazardMaterial == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Standard");
                defaultHazardMaterial = new Material(s);
                defaultHazardMaterial.color = new Color(0.95f, 0.35f, 0.1f); // Bright safety orange
            }
            rend.sharedMaterial = defaultHazardMaterial;
        }

        return block;
    }

    /// <summary>
    /// Destroys all currently active obstacles in the scene.
    /// </summary>
    public void ClearAllObstacles()
    {
        Obstacle[] obstacles = Object.FindObjectsByType<Obstacle>(FindObjectsSortMode.None);
        foreach (Obstacle obs in obstacles)
        {
            if (obs != null)
            {
                Destroy(obs.gameObject);
            }
        }
    }

    /// <summary>
    /// Resets the difficulty ramp and spawn timer back to the start of a run.
    /// </summary>
    public void ResetSpawner()
    {
        elapsedTime = 0f;
        timer = 0f;
        hasSpawnedFirstWave = false;
        currentSpawnInterval = startingSpawnInterval;
    }
}
