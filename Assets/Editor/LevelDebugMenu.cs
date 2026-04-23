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
        Debug.Log($"[LevelDebug] Unlocked all levels. SavedLevelIndex set to {lastIndex} (Level {LevelDatabase.TotalLevels}).");
    }

    [MenuItem("Debug/Reset Progress")]
    private static void ResetProgress()
    {
        PlayerPrefs.SetInt(SavedLevelIndexKey, 0);
        PlayerPrefs.Save();
        Debug.Log("[LevelDebug] Progress reset to Level 1.");
    }
}
