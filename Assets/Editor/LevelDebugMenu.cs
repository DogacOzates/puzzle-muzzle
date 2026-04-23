using UnityEditor;
using UnityEngine;

public static class LevelDebugMenu
{
    private const string SavedLevelIndexKey = "progress.savedLevelIndex";

    [MenuItem("Debug/Unlock All Levels")]
    private static void UnlockAllLevels()
    {
        int lastIndex = LevelDatabase.TotalLevels - 1;
        PlayerPrefs.SetInt(SavedLevelIndexKey, lastIndex);
        PlayerPrefs.Save();
        Debug.Log($"[LevelDebug] Unlocked all levels (index {lastIndex}).");
    }

    [MenuItem("Debug/Load Last Level (Play Mode)")]
    private static void LoadLastLevel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LevelDebug] Enter Play Mode first, then use this menu item.");
            return;
        }

        var gm = Object.FindAnyObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("[LevelDebug] GameManager not found.");
            return;
        }

        int lastIndex = LevelDatabase.TotalLevels - 1;
        // Set PlayerPrefs BEFORE calling SelectLevel so the unlock guard passes
        PlayerPrefs.SetInt(SavedLevelIndexKey, lastIndex);
        PlayerPrefs.Save();

        gm.SelectLevel(lastIndex);
        Debug.Log($"[LevelDebug] Loading Level {LevelDatabase.TotalLevels}.");
    }

    [MenuItem("Debug/Load Last Level (Play Mode)", true)]
    private static bool LoadLastLevelValidate() => Application.isPlaying;

    [MenuItem("Debug/Reset Progress")]
    private static void ResetProgress()
    {
        PlayerPrefs.SetInt(SavedLevelIndexKey, 0);
        PlayerPrefs.Save();
        Debug.Log("[LevelDebug] Progress reset to Level 1.");
    }
}

