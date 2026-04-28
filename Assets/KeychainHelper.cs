using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// Thin wrapper around iOS Keychain so purchase flags survive app reinstall.
/// Falls back to PlayerPrefs in the Editor and on non-iOS platforms.
/// </summary>
public static class KeychainHelper
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern bool _KeychainGetBool(string key);
    [DllImport("__Internal")] private static extern void _KeychainSetBool(string key, bool value);

    public static bool  GetBool(string key)             => _KeychainGetBool(key);
    public static void  SetBool(string key, bool value) => _KeychainSetBool(key, value);
#else
    public static bool  GetBool(string key)             => PlayerPrefs.GetInt(key, 0) == 1;
    public static void  SetBool(string key, bool value) { PlayerPrefs.SetInt(key, value ? 1 : 0); PlayerPrefs.Save(); }
#endif
}
