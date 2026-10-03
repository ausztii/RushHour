using System;
using System.Reflection;

/// <summary>
/// Scratch harness (lives outside Assets/, never shipped): invokes the RushHour scene builder
/// by reflection so this file needs no compile-time reference to the editor assembly.
/// </summary>
public static class BuildRushHourScene
{
    public static string Execute()
    {
        Type builderType = null;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            builderType = assembly.GetType("RushHourSceneBuilder", false);
            if (builderType != null) break;
        }

        if (builderType == null) return "ERROR: RushHourSceneBuilder type not found.";

        MethodInfo execute = builderType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
        if (execute == null) return "ERROR: RushHourSceneBuilder.Execute() not found.";

        object result = execute.Invoke(null, null);
        return result == null ? "(no result)" : result.ToString();
    }
}
