using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// Syncs campaign progress via iCloud Key-Value Store (NSUbiquitousKeyValueStore).
/// Requires the iCloud Key-Value entitlement in Xcode and the matching App ID capability.
/// The GameObject must be named exactly "iCloudSyncManager" (used by native UnitySendMessage).
/// </summary>
public class iCloudSyncManager : MonoBehaviour
{
    public static iCloudSyncManager Instance { get; private set; }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void _iCloudSetInt(string key, int value);
    [DllImport("__Internal")] private static extern int  _iCloudGetInt(string key, int defaultValue);
    [DllImport("__Internal")] private static extern void _iCloudStartSync();
#endif

    void Awake()
    {
        Instance = this;
#if UNITY_IOS && !UNITY_EDITOR
        _iCloudStartSync();
#endif
        MergeProgress();
    }

    /// <summary>
    /// Called from native code via UnitySendMessage when another device updates iCloud.
    /// </summary>
    public void OnExternalChange(string msg)
    {
        MergeProgress();
    }

    /// <summary>
    /// Merge iCloud and local progress, keeping the higher value on both sides.
    /// Only campaign progress (a monotonic index) is synced this way.
    /// </summary>
    private static void MergeProgress()
    {
        const string key = "progress.savedLevelIndex";
        int local  = PlayerPrefs.GetInt(key, 0);
        int cloud  = GetInt(key, 0);
        int merged = Mathf.Max(local, cloud);

        if (merged > local)
        {
            PlayerPrefs.SetInt(key, merged);
            PlayerPrefs.Save();
        }
        if (merged > cloud)
        {
            SetInt(key, merged);
        }
    }

    public static void SyncProgress(int levelIndex)
    {
        SetInt("progress.savedLevelIndex", levelIndex);
    }

    private static void SetInt(string key, int value)
    {
#if UNITY_IOS && !UNITY_EDITOR
        _iCloudSetInt(key, value);
#else
        PlayerPrefs.SetInt(key, value);
#endif
    }

    private static int GetInt(string key, int defaultValue)
    {
#if UNITY_IOS && !UNITY_EDITOR
        return _iCloudGetInt(key, defaultValue);
#else
        return PlayerPrefs.GetInt(key, defaultValue);
#endif
    }
}
