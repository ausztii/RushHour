using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Edit-mode probe: reads the StartScreen scene's button wiring, the StartScreenController's
/// serialized references, the overlay's initial active state, and the build list order.
/// Read-only. Compiled by execute_script into a throwaway root-level assembly.
/// </summary>
public static class VerifyStartScreenWiring
{
    static Type FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch { continue; }
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i].Name == name) return types[i];
            }
        }
        return null;
    }

    static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    public static string Execute()
    {
        var sb = new StringBuilder();

        Scene scene = SceneManager.GetActiveScene();
        sb.AppendLine("ACTIVE SCENE: " + scene.name + "  (" + scene.path + ")");
        sb.AppendLine();

        // ---- Buttons and their persistent onClick wiring ----
        var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine("=== BUTTONS (" + buttons.Length + ") ===");
        foreach (var b in buttons)
        {
            sb.AppendLine("'" + PathOf(b.transform) + "'  activeInHierarchy=" + b.gameObject.activeInHierarchy
                + "  interactable=" + b.interactable);

            var so = new UnityEditor.SerializedObject(b);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls == null)
            {
                sb.AppendLine("    <no persistent-calls property found>");
                continue;
            }
            sb.AppendLine("    persistentCalls = " + calls.arraySize);
            for (int i = 0; i < calls.arraySize; i++)
            {
                var c = calls.GetArrayElementAtIndex(i);
                var tgt = c.FindPropertyRelative("m_Target").objectReferenceValue;
                var mth = c.FindPropertyRelative("m_MethodName").stringValue;
                var mode = c.FindPropertyRelative("m_Mode").intValue;
                var state = c.FindPropertyRelative("m_CallState").intValue;
                var asmName = c.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;
                sb.AppendLine("      [" + i + "] target=" + (tgt == null ? "<NULL>" : tgt.name)
                    + "  type=" + asmName + "  method=" + mth
                    + "  mode=" + mode + "  callState=" + state);
            }
        }
        sb.AppendLine();

        // ---- StartScreenController references ----
        sb.AppendLine("=== StartScreenController ===");
        Type scType = FindType("StartScreenController");
        sb.AppendLine("type = " + (scType == null ? "<MISSING>" : scType.FullName));
        if (scType != null)
        {
            var comps = UnityEngine.Object.FindObjectsByType(scType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine("instances = " + comps.Length);
            var panelField = scType.GetField("howToPlayPanel", BindingFlags.Public | BindingFlags.Instance);
            var nameField = scType.GetField("gameplaySceneName", BindingFlags.Public | BindingFlags.Instance);
            foreach (var comp in comps)
            {
                var mb = (Component)comp;
                var panel = panelField != null ? panelField.GetValue(comp) as GameObject : null;
                var sname = nameField != null ? nameField.GetValue(comp) as string : null;
                sb.AppendLine("  on '" + PathOf(mb.transform) + "'");
                sb.AppendLine("    howToPlayPanel = " + (panel == null ? "<NULL>" : PathOf(panel.transform)));
                sb.AppendLine("    howToPlayPanel.activeSelf = " + (panel == null ? "n/a" : panel.activeSelf.ToString()));
                sb.AppendLine("    gameplaySceneName = '" + sname + "'");
            }
            foreach (var m in new[] { "PlayGame", "ShowHowToPlay", "HideHowToPlay" })
            {
                sb.AppendLine("  method " + m + " present = " + (scType.GetMethod(m) != null));
            }
        }
        sb.AppendLine();

        // ---- Overlay object state ----
        sb.AppendLine("=== OVERLAY ===");
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name != "HowToPlayPanel") continue;
            if (go.scene != scene) continue;
            sb.AppendLine("HowToPlayPanel found at '" + PathOf(go.transform) + "'  activeSelf=" + go.activeSelf
                + "  activeInHierarchy=" + go.activeInHierarchy);
        }
        sb.AppendLine();

        // ---- Build list ----
        sb.AppendLine("=== BUILD SCENES ===");
        int idx = 0;
        foreach (var s in UnityEditor.EditorBuildSettings.scenes)
        {
            sb.AppendLine("  [" + (idx++) + "] " + s.path + "  enabled=" + s.enabled);
        }

        return sb.ToString();
    }
}
