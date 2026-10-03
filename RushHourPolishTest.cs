using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scratch harness (lives outside Assets/, never shipped). Drives the RushHour polish features
/// deterministically in Play mode by exercising the real components rather than waiting on
/// random spawns and invisible input.
/// </summary>
public static class RushHourPolishTest
{
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // ------------------------------------------------------------------ reflection

    private static Type T(string name)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(name, false);
            if (t != null) return t;
        }
        return null;
    }

    private static object Get(object target, string name)
    {
        if (target == null) return null;

        Type t = target.GetType();
        FieldInfo f = t.GetField(name, All);
        if (f != null) return f.GetValue(target);

        PropertyInfo p = t.GetProperty(name, All);
        return p == null ? null : p.GetValue(target);
    }

    private static float F(object target, string name)
    {
        object v = Get(target, name);
        return v == null ? float.NaN : Convert.ToSingle(v);
    }

    private static string S(object target, string name)
    {
        object v = Get(target, name);
        return v == null ? "<null>" : v.ToString();
    }

    private static bool B(object target, string name)
    {
        object v = Get(target, name);
        return v is bool b && b;
    }

    private static object Call(object target, string name, params object[] args)
    {
        MethodInfo m = target.GetType().GetMethod(name, All, null, ArgTypes(args), null);
        return m == null ? null : m.Invoke(target, args);
    }

    private static Type[] ArgTypes(object[] args)
    {
        Type[] types = new Type[args.Length];
        for (int i = 0; i < args.Length; i++) types[i] = args[i].GetType();
        return types;
    }

    private static string ActiveOf(object target, string fieldName)
    {
        object o = Get(target, fieldName);
        UnityEngine.Object asObject = o as UnityEngine.Object;
        if (asObject == null || !asObject) return "?";

        GameObject go = asObject as GameObject;
        if (go == null)
        {
            Component component = asObject as Component;
            go = component == null ? null : component.gameObject;
        }

        return go == null ? "?" : go.activeSelf.ToString();
    }

    private static string TextOf(object hud, string fieldName)
    {
        object textComponent = Get(hud, fieldName);
        if (textComponent == null) return "<unassigned " + fieldName + ">";

        UnityEngine.Object asObject = textComponent as UnityEngine.Object;
        if (asObject == null) return "<not a UObject>";
        if (!asObject) return "<destroyed " + fieldName + ">";

        object text = Get(textComponent, "text");
        return text == null ? "<no text>" : text.ToString();
    }

    // ------------------------------------------------------------------ scene lookup

    private sealed class Ctx
    {
        public object gameManager;
        public object carController;
        public object battery;
        public object shield;
        public object hud;
        public object spawner;
        public Transform car;
        public Collider carCollider;
        public Type obstacleType;
        public Type shieldPickupType;
        public Type chargePickupType;
        public MethodInfo resolveImpact;
    }

    private static Ctx Lookup()
    {
        Ctx c = new Ctx();

        Type gmType = T("GameManager");
        Type ccType = T("CarController");
        Type batType = T("BatterySystem");
        Type shieldType = T("ShieldSystem");
        Type hudType = T("HudController");
        Type spawnerType = T("ObstacleSpawner");

        c.obstacleType = T("Obstacle");
        c.shieldPickupType = T("ShieldPickup");
        c.chargePickupType = T("ChargePickup");

        if (gmType == null || ccType == null || batType == null || c.obstacleType == null)
        {
            return null;
        }

        c.gameManager = UnityEngine.Object.FindFirstObjectByType(gmType);
        c.carController = UnityEngine.Object.FindFirstObjectByType(ccType);
        c.battery = UnityEngine.Object.FindFirstObjectByType(batType);
        c.shield = UnityEngine.Object.FindFirstObjectByType(shieldType);
        c.hud = UnityEngine.Object.FindFirstObjectByType(hudType);
        c.spawner = spawnerType == null ? null : UnityEngine.Object.FindFirstObjectByType(spawnerType);

        if (c.gameManager == null || c.carController == null || c.battery == null) return null;

        c.car = ((Component)c.carController).transform;
        c.carCollider = ((Component)c.carController).GetComponent<Collider>();
        c.resolveImpact = c.obstacleType.GetMethod("ResolveImpact", All);
        return c;
    }

    private static GameObject SpawnPrefab(string path, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return null;
        return UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
    }

    private static void FireTrigger(object pickupComponent, Collider other)
    {
        MethodInfo m = pickupComponent.GetType().GetMethod("OnTriggerEnter", All);
        if (m != null) m.Invoke(pickupComponent, new object[] { other });
    }

    // ================================================================== A: behaviour

    public static string Setup()
    {
        StringBuilder log = new StringBuilder();
        Ctx c = Lookup();

        if (c == null)
        {
            return "ERROR: could not resolve GameManager/CarController/BatterySystem/Obstacle in the scene.";
        }

        // Silence the spawner so the run is deterministic.
        if (c.spawner is Behaviour sb) sb.enabled = false;
        foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(c.obstacleType, FindObjectsSortMode.None))
        {
            UnityEngine.Object.Destroy(o);
        }

        // Charging to a known baseline.
        Call(c.battery, "ResetCharge");

        log.AppendLine("baseline charge=" + F(c.battery, "currentCharge") +
                       " gameOver=" + B(c.gameManager, "IsGameOver"));

        // ---------------------------------------------------- 1) pothole: charge only
        float before = F(c.battery, "currentCharge");
        GameObject pothole = SpawnPrefab("Assets/Prefabs/Obstacle_Pothole.prefab",
            new Vector3(c.car.position.x, 0.5f, c.car.position.z));

        if (pothole == null)
        {
            log.AppendLine("1 POtholeChargePenalty: FAIL (prefab missing)");
        }
        else
        {
            object potholeObstacle = pothole.GetComponent(c.obstacleType);
            c.resolveImpact.Invoke(potholeObstacle, new object[] { c.carController });
            float after = F(c.battery, "currentCharge");

            log.AppendLine("1 POtholeChargePenalty: charge " + before.ToString("0.#") + " -> " + after.ToString("0.#") +
                           " (delta " + (after - before).ToString("0.#") + ", want -12), gameOver=" +
                           B(c.gameManager, "IsGameOver") + " want False");
        }

        // ------------------------------------------------- 2) shield pickup: banner
        GameObject grant = SpawnPrefab("Assets/Prefabs/ShieldPickup.prefab", c.car.position);
        if (grant == null)
        {
            log.AppendLine("2 ShieldPickup: FAIL (prefab missing)");
        }
        else
        {
            FireTrigger(grant.GetComponent(c.shieldPickupType), c.carCollider);
            log.AppendLine("2 ShieldPickup: shielded=" + B(c.shield, "IsShielded") +
                           ", bannerTitle=\"" + TextOf(c.hud, "bannerTitleText") + "\"");
        }

        // ---------------------------------------- 3) shield absorbs the next impact
        GameObject barrier = SpawnPrefab("Assets/Prefabs/Obstacle_Barrier.prefab",
            new Vector3(c.car.position.x, 0.5f, c.car.position.z));

        if (barrier == null)
        {
            log.AppendLine("3 ShieldAbsorb: FAIL (prefab missing)");
        }
        else
        {
            float chargeBefore = F(c.battery, "currentCharge");
            c.resolveImpact.Invoke(barrier.GetComponent(c.obstacleType), new object[] { c.carController });

            log.AppendLine("3 ShieldAbsorb: shieldedAfter=" + B(c.shield, "IsShielded") +
                           " want False, gameOver=" + B(c.gameManager, "IsGameOver") +
                           " want False, carAlive=" + (Call(c.carController, "IsAlive") is bool alive && alive) +
                           ", chargeUnchanged=" + (Math.Abs(F(c.battery, "currentCharge") - chargeBefore) < 0.01f));
        }

        // ---------------------------------------------------- 4) recharge point
        Call(c.battery, "Drain", 28f);
        float drained = F(c.battery, "currentCharge");

        GameObject recharge = SpawnPrefab("Assets/Prefabs/ChargePickup.prefab", c.car.position);
        if (recharge == null)
        {
            log.AppendLine("4 ChargePickup: FAIL (prefab missing)");
        }
        else
        {
            FireTrigger(recharge.GetComponent(c.chargePickupType), c.carCollider);
            log.AppendLine("4 ChargePickup: charge " + drained.ToString("0.#") + " -> " +
                           F(c.battery, "currentCharge").ToString("0.#") + " (want +30), bannerTitle=\"" +
                           TextOf(c.hud, "bannerTitleText") + "\"");
        }

        // ------------------------------------------------------ 5) swerve cost
        float preSwerve = F(c.battery, "currentCharge");
        int laneBefore = Convert.ToInt32(Get(c.carController, "CurrentLane"));
        object shiftResult = Call(c.carController, "ShiftLane", 1);
        int laneAfter = Convert.ToInt32(Get(c.carController, "CurrentLane"));

        log.AppendLine("5 SwerveCost: charge " + preSwerve.ToString("0.#") + " -> " +
                       F(c.battery, "currentCharge").ToString("0.#") + " (want -1), lane " +
                       laneBefore + " -> " + laneAfter);

        // ------------------------------------------- 6) banking uses Visual only
        // Force the real banking update several times so the tilt settles, then compare frames.
        MethodInfo banking = c.carController.GetType().GetMethod("UpdateBanking", All);
        if (banking != null)
        {
            for (int i = 0; i < 60; i++) banking.Invoke(c.carController, null);
        }

        Transform visual = c.car.Find("Visual");
        Transform camera = c.car.Find("Camera");

        log.AppendLine("6 Banking: Visual.z=" + (visual == null ? "?" : visual.localEulerAngles.z.ToString("0.#")) +
                       " (want != 0), Car.z=" + c.car.localEulerAngles.z.ToString("0.#") + " (want 0)" +
                       ", Camera.z=" + (camera == null ? "?" : camera.localEulerAngles.z.ToString("0.#")) + " (want 0)" +
                       ", Camera.rot=" + (camera == null ? "?" : camera.localEulerAngles.ToString("0.#")));

        log.AppendLine("left-running: charge=" + F(c.battery, "currentCharge").ToString("0.#") +
                       " gameOver=" + B(c.gameManager, "IsGameOver"));

        return log.ToString().TrimEnd();
    }

    // ============================================================ B: teardown checks

    public static string Mid()
    {
        StringBuilder log = new StringBuilder();
        Ctx c = Lookup();
        if (c == null) return "ERROR: scene lookup failed.";

        int obstacles = UnityEngine.Object.FindObjectsByType(c.obstacleType, FindObjectsSortMode.None).Length;
        log.AppendLine("obstacles left in scene=" + obstacles + " (want 0: pothole + absorbed barrier removed)");
        log.AppendLine("shielded=" + B(c.shield, "IsShielded") + " (want False)");

        // Teleport the pod to the finish so the next Update fires the win.
        Vector3 p = c.car.position;
        c.car.position = new Vector3(p.x, p.y, 500f);
        log.AppendLine("teleported car to z=500, car.z active=" + c.car.position.z.ToString("0.##"));

        return log.ToString().TrimEnd();
    }

    public static string Report()
    {
        StringBuilder log = new StringBuilder();
        Ctx c = Lookup();
        if (c == null) return "ERROR: scene lookup failed.";

        log.AppendLine("gameOver=" + B(c.gameManager, "IsGameOver") + " (want True)");
        log.AppendLine("timeScale=" + Time.timeScale.ToString("0.###") + " (want 0)");
        log.AppendLine("distanceTravelled=" + F(c.gameManager, "DistanceTravelled").ToString("0.#"));
        log.AppendLine("resultTitle=\"" + TextOf(c.hud, "resultTitleText") + "\"");
        log.AppendLine("resultBody=\"" + TextOf(c.hud, "resultBodyText") + "\"");

        object button = Get(c.hud, "restartButton");
        GameObject buttonGo = button as GameObject;
        log.AppendLine("restartButton active=" + (buttonGo == null ? "?" : buttonGo.activeSelf.ToString()) + " (want True)");
        log.AppendLine("bannerAlpha=" + F(Get(c.hud, "bannerGroup"), "alpha").ToString("0.##") + " (want 0)");

        return log.ToString().TrimEnd();
    }

    // ============================================================ C: battery depleted

    public static string Deplete()
    {
        StringBuilder log = new StringBuilder();
        Ctx c = Lookup();
        if (c == null) return "ERROR: scene lookup failed.";

        if (c.spawner is Behaviour sb) sb.enabled = false;
        foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(c.obstacleType, FindObjectsSortMode.None))
        {
            UnityEngine.Object.Destroy(o);
        }

        Call(c.battery, "Drain", 500f);

        log.AppendLine("charge=" + F(c.battery, "currentCharge").ToString("0.#") + " (want 0)");
        log.AppendLine("isDepleted=" + B(c.battery, "IsDepleted") + " (want True)");
        log.AppendLine("gameOver=" + B(c.gameManager, "IsGameOver") + " (want True)");
        log.AppendLine("resultTitle=\"" + TextOf(c.hud, "resultTitleText") + "\" (want SHIPMENT LOST)");
        log.AppendLine("resultBody=\"" + TextOf(c.hud, "resultBodyText") + "\" (want Battery depleted ...)");

        return log.ToString().TrimEnd();
    }

    // ============================================================ D: clean win

    public static string Win()
    {
        StringBuilder log = new StringBuilder();
        Ctx c = Lookup();
        if (c == null) return "ERROR: scene lookup failed.";

        // Hard-stop every spawner in the scene, whatever object it lives on.
        Type spawnerType = T("ObstacleSpawner");
        int spawners = 0;

        if (spawnerType != null)
        {
            foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(spawnerType, FindObjectsSortMode.None))
            {
                Behaviour b = o as Behaviour;
                if (b == null) continue;
                b.enabled = false;
                spawners++;
            }
        }

        // Clear the road of hazards and collectibles.
        int cleared = 0;
        Type[] sweep = new Type[] { c.obstacleType, c.shieldPickupType, c.chargePickupType };

        for (int i = 0; i < sweep.Length; i++)
        {
            if (sweep[i] == null) continue;
            foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsByType(sweep[i], FindObjectsSortMode.None))
            {
                UnityEngine.Object.Destroy(o);
                cleared++;
            }
        }

        MethodInfo resetShield = c.shield != null ? c.shield.GetType().GetMethod("ResetShield", All) : null;
        if (resetShield != null) resetShield.Invoke(c.shield, null);

        float baseZ = c.car.position.z - F(c.gameManager, "DistanceTravelled");
        Vector3 p = c.car.position;
        c.car.position = new Vector3(p.x, p.y, baseZ + 520f);
        Physics.SyncTransforms();

        log.AppendLine("spawners disabled=" + spawners + ", hazards/collectibles cleared=" + cleared);
        log.AppendLine("car.z=" + c.car.position.z.ToString("0.#") + ", distance=" +
                       F(c.gameManager, "DistanceTravelled").ToString("0.#") + " (want >= 500)");
        log.AppendLine("gameOver now=" + B(c.gameManager, "IsGameOver") + ", timeScale=" + Time.timeScale.ToString("0.###"));

        return log.ToString().TrimEnd();
    }

    // ============================================================ E: district stops

    public static string Districts()
    {
        StringBuilder log = new StringBuilder();
        Ctx c = Lookup();
        if (c == null) return "ERROR: scene lookup failed.";

        MethodInfo updateRoute = c.hud.GetType().GetMethod("UpdateRoute", All);
        if (updateRoute == null) return "ERROR: HudController.UpdateRoute not found.";

        float baseZ = c.car.position.z - F(c.gameManager, "DistanceTravelled");
        float[] stops = new float[] { 0f, 130f, 270f, 410f };

        for (int i = 0; i < stops.Length; i++)
        {
            Vector3 p = c.car.position;
            c.car.position = new Vector3(p.x, p.y, baseZ + stops[i]);
            updateRoute.Invoke(c.hud, null);

            log.AppendLine("d=" + stops[i] + "m -> district=\"" + TextOf(c.hud, "districtText") +
                           "\", distance=\"" + TextOf(c.hud, "distanceText") + "\"");
        }

        // The end-of-run card must have replaced the route banner, and the card must be visible.
        log.AppendLine("bannerAlpha=" + F(Get(c.hud, "bannerGroup"), "alpha").ToString("0.##") +
                       " (want 0), bannerTitle=\"" + TextOf(c.hud, "bannerTitleText") + "\"");
        log.AppendLine("resultTitle active=" + ActiveOf(c.hud, "resultTitleText") +
                       ", resultBody active=" + ActiveOf(c.hud, "resultBodyText") +
                       ", restartButton active=" + ActiveOf(c.hud, "restartButton"));

        return log.ToString().TrimEnd();
    }
}
