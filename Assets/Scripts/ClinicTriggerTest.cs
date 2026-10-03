using System;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Scratch harness (lives outside Assets/, never shipped). Isolates the Greenfield Clinic arrival
/// trigger: it raises deliveryDistance far out of reach so the 500 m odometer rule in
/// GameManager.Update cannot be the cause of the win, parks the pod just short of the drop-off bay,
/// and reports whether ClinicTrigger alone ended the run as the pod drives in.
///
/// Stateless by design - every method re-resolves the scene objects, because each execute_script
/// call compiles into a fresh assembly and static state does not survive between calls.
///
/// Usage in play mode: Execute() -> wait a few real seconds -> Report() -> Restore().
/// </summary>
public static class ClinicTriggerTest
{
    private const float ParkZ = 496f;        // 3.7 m short of the volume's near edge (z = 499.7)
    private const float OutOfReach = 99999f;
    private const float OriginalDeliveryDistance = 500f;

    public static string Execute()
    {
        StringBuilder sb = new StringBuilder();

        Component gm = FindComponent("GameManager");
        Component trigger = FindComponent("ClinicTrigger");
        Component spawner = FindComponent("ObstacleSpawner");
        GameObject car = GameObject.FindGameObjectWithTag("Player");

        if (gm == null) return "ERROR: GameManager component not found.";
        if (trigger == null) return "ERROR: ClinicTrigger component not found.";
        if (car == null) return "ERROR: no GameObject tagged 'Player'.";

        FieldInfo distanceField = gm.GetType().GetField("deliveryDistance", BindingFlags.Public | BindingFlags.Instance);
        if (distanceField == null) return "ERROR: deliveryDistance field not found.";
        distanceField.SetValue(gm, OutOfReach);

        if (spawner != null)
        {
            Behaviour b = spawner as Behaviour;
            if (b != null) b.enabled = false;
        }

        sb.AppendLine("=== ARMED ===");
        sb.AppendLine("deliveryDistance: " + OriginalDeliveryDistance + " -> " + OutOfReach + "  (odometer rule disabled)");
        sb.AppendLine("GameManager: " + gm.gameObject.name + "   ClinicTrigger: " + trigger.gameObject.name);
        sb.AppendLine("spawner disabled: " + (spawner != null));
        sb.AppendLine("car position before: " + car.transform.position);
        car.transform.position = new Vector3(0f, car.transform.position.y, ParkZ);
        sb.AppendLine("car position after:  " + car.transform.position + "   (volume starts at z = 499.7)");
        sb.AppendLine("pre-state: IsGameOver=" + IsGameOver(gm) + "  timeScale=" + Time.timeScale + "  remaining=" + Property(gm, "RemainingDistance"));
        sb.AppendLine("(pod now drives in under its own power - wait a few real seconds, then Report)");
        return sb.ToString();
    }

    public static string Report()
    {
        Component gm = FindComponent("GameManager");
        GameObject car = GameObject.FindGameObjectWithTag("Player");
        if (gm == null) return "ERROR: GameManager component not found.";
        if (car == null) return "ERROR: no GameObject tagged 'Player'.";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== AFTER WAIT ===");
        sb.AppendLine("car position:      " + car.transform.position + "   (past z = 510.3 means it crossed the volume)");
        sb.AppendLine("IsGameOver:        " + IsGameOver(gm));
        sb.AppendLine("timeScale:         " + Time.timeScale + "   (0 == run ended)");
        sb.AppendLine("deliveryDistance:  " + Property(gm, "deliveryDistance"));
        sb.AppendLine("RemainingDistance: " + Property(gm, "RemainingDistance"));
        sb.AppendLine();
        sb.AppendLine("--- HUD text in scene ---");
        sb.Append(HudTexts());
        return sb.ToString();
    }

    /// <summary>Fallback: proves the component logic without relying on the physics overlap.</summary>
    public static string Direct()
    {
        Component gm = FindComponent("GameManager");
        Component trigger = FindComponent("ClinicTrigger");
        GameObject car = GameObject.FindGameObjectWithTag("Player");
        if (gm == null || trigger == null || car == null) return "ERROR: scene objects not found.";

        Collider col = car.GetComponent<Collider>();
        if (col == null) return "ERROR: car has no Collider.";

        MethodInfo onTriggerEnter = trigger.GetType().GetMethod(
            "OnTriggerEnter", BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { typeof(Collider) }, null);
        if (onTriggerEnter == null) return "ERROR: ClinicTrigger.OnTriggerEnter(Collider) not found.";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== DIRECT INVOCATION ===");
        sb.AppendLine("collider: " + col.GetType().Name + " on '" + car.name + "'");
        sb.AppendLine("IsGameOver before: " + IsGameOver(gm));

        onTriggerEnter.Invoke(trigger, new object[] { col });
        sb.AppendLine("call 1 -> IsGameOver=" + IsGameOver(gm) + "  timeScale=" + Time.timeScale);

        onTriggerEnter.Invoke(trigger, new object[] { col });
        sb.AppendLine("call 2 -> IsGameOver=" + IsGameOver(gm) + "  timeScale=" + Time.timeScale + "   (hasFired guard, no exception)");

        sb.AppendLine();
        sb.AppendLine("--- HUD text in scene ---");
        sb.Append(HudTexts());
        return sb.ToString();
    }

    public static string Restore()
    {
        StringBuilder sb = new StringBuilder();

        Component gm = FindComponent("GameManager");
        if (gm != null)
        {
            FieldInfo distanceField = gm.GetType().GetField("deliveryDistance", BindingFlags.Public | BindingFlags.Instance);
            if (distanceField != null)
            {
                distanceField.SetValue(gm, OriginalDeliveryDistance);
                sb.AppendLine("deliveryDistance restored to " + OriginalDeliveryDistance);
            }
        }

        Component spawner = FindComponent("ObstacleSpawner");
        if (spawner != null)
        {
            Behaviour b = spawner as Behaviour;
            if (b != null) b.enabled = true;
            sb.AppendLine("spawner re-enabled");
        }

        Time.timeScale = 1f;
        sb.AppendLine("timeScale reset to 1");
        return sb.ToString();
    }

    private static bool IsGameOver(Component gm)
    {
        object v = Property(gm, "IsGameOver");
        return v is bool && (bool)v;
    }

    private static object Property(Component gm, string name)
    {
        if (gm == null) return null;
        PropertyInfo p = gm.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        return p == null ? null : p.GetValue(gm);
    }

    private static string HudTexts()
    {
        Type textType = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            textType = a.GetType("UnityEngine.UI.Text", false);
            if (textType != null) break;
        }
        if (textType == null) return "  (UnityEngine.UI.Text not resolvable)\n";

        PropertyInfo textProp = textType.GetProperty("text");
        StringBuilder sb = new StringBuilder();
        int count = 0;

        UnityEngine.SceneManagement.Scene scene =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Component[] texts = root.GetComponentsInChildren(textType, true) as Component[];
            foreach (Component c in texts)
            {
                string t = textProp.GetValue(c) as string;
                if (string.IsNullOrEmpty(t)) continue;
                count++;
                sb.AppendLine("  [" + (c.gameObject.activeInHierarchy ? "visible" : "hidden ") + "] "
                              + c.gameObject.name + ": " + t);
            }
        }

        if (count == 0) sb.AppendLine("  (no non-empty Text found)");
        return sb.ToString();
    }

    private static Component FindComponent(string typeName)
    {
        Type type = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = a.GetType(typeName, false);
            if (type != null) break;
        }
        if (type == null) return null;
        return UnityEngine.Object.FindFirstObjectByType(type) as Component;
    }
}
