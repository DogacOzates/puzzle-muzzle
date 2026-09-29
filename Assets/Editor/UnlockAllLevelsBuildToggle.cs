using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Toggles the UNLOCK_ALL_LEVELS scripting define for iOS.
// ON  → every level is selectable in the level select (for device testing / App Store screenshots).
// OFF → normal release behaviour. Always turn OFF before an App Store build!
public static class UnlockAllLevelsBuildToggle
{
    private const string Define = "UNLOCK_ALL_LEVELS";
    private const string MenuPath = "Debug/Test Build: Unlock All Levels";

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        var target = NamedBuildTarget.iOS;
        var defines = PlayerSettings.GetScriptingDefineSymbols(target)
            .Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        bool on = defines.Contains(Define);
        if (on) defines.Remove(Define); else defines.Add(Define);
        PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));

        Debug.Log(on
            ? "[UnlockAll] OFF — normal release build."
            : "[UnlockAll] ON — all levels unlocked in iOS builds. Turn OFF before the App Store build!");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        bool on = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS).Split(';').Contains(Define);
        Menu.SetChecked(MenuPath, on);
        return true;
    }
}
