using System;
using UnityEditor;
using UnityEngine;

public static class RecordingPrep
{
    private const string SavedLevelIndexKey = "progress.savedLevelIndex";

    [MenuItem("Debug/Recording/Prep for Recording")]
    private static void PrepForRecording()
    {
        PlayerPrefs.SetInt("tutorial.v2.done", 1);
        PlayerPrefs.SetString("daily.lastClaim", DateTime.Now.ToString("yyyy-MM-dd"));
        PlayerPrefs.SetInt("review.given", 1);
        PlayerPrefs.SetInt(SavedLevelIndexKey, LevelDatabase.TotalLevels - 1);
        PlayerPrefs.Save();
        Debug.Log("[Recording] Tutorial skipped, daily/rate popups silenced, all levels unlocked.");
    }

    [MenuItem("Debug/Recording/Restore (fresh-install state)")]
    private static void Restore()
    {
        PlayerPrefs.DeleteKey("tutorial.v2.done");
        PlayerPrefs.DeleteKey("daily.lastClaim");
        PlayerPrefs.DeleteKey("daily.streak");
        PlayerPrefs.DeleteKey("review.given");
        PlayerPrefs.DeleteKey(SavedLevelIndexKey);
        PlayerPrefs.Save();
        Debug.Log("[Recording] Restored fresh-install state for the four recording-related flags.");
    }
}
