using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scratch harness (lives outside Assets/, never shipped): removes the retired fuel-era
/// scripts now that the battery system has replaced them.
/// </summary>
public static class DeleteFuelScripts
{
    public static string Execute()
    {
        string[] paths =
        {
            "Assets/Scripts/FuelSystem.cs",
            "Assets/Scripts/FuelPickup.cs",
        };

        StringBuilder log = new StringBuilder();

        foreach (string path in paths)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null;
            string result = AssetDatabase.DeleteAsset(path) ? "deleted" : "not found";
            log.AppendLine(path + " -> " + result + (exists ? "" : " (asset not registered)"));
        }

        AssetDatabase.Refresh();
        Debug.Log("[RushHour] " + log.ToString().TrimEnd());
        return log.ToString().TrimEnd();
    }
}
