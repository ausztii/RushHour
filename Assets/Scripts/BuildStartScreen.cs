using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Scratch harness for the aura-unity execute_script tool. Root-level, outside Assets, so Unity
/// never compiles it into the project - the tool compiles it into its own throwaway assembly and
/// reaches the two builder types by reflection, because that assembly has no reference to
/// Assembly-CSharp-Editor.
///
/// Runs the playable-scene builder first so the shared post-processing profile is created with
/// its real overrides, then the start-screen builder, which reuses that same profile.
/// </summary>
public static class BuildStartScreenHarness
{
    public static void Execute()
    {
        InvokeBuilder("RushHourSceneBuilder", "Execute");
        InvokeBuilder("StartScreenBuilder", "Execute");
    }

    private static void InvokeBuilder(string typeName, string methodName)
    {
        Type type = null;
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Type candidate = null;
            try { candidate = assemblies[i].GetType(typeName, false); }
            catch { candidate = null; }

            if (candidate != null)
            {
                type = candidate;
                break;
            }
        }

        if (type == null)
        {
            Debug.LogError("[harness] type '" + typeName + "' not found in any loaded assembly.");
            return;
        }

        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            Debug.LogError("[harness] " + typeName + "." + methodName + " not found.");
            return;
        }

        object result;
        try
        {
            result = method.Invoke(null, null);
        }
        catch (Exception e)
        {
            Debug.LogError("[harness] " + typeName + "." + methodName + " threw: " + (e.InnerException ?? e));
            return;
        }

        Debug.Log("[harness] " + typeName + " -> " + result);
    }
}
