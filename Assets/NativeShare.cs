using UnityEngine;
using System.Runtime.InteropServices;

public static class NativeShare
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _ShowShareSheet(string text);
#endif

    public static void Share(string text)
    {
#if UNITY_IOS && !UNITY_EDITOR
        _ShowShareSheet(text);
#else
        GUIUtility.systemCopyBuffer = text;
        Debug.Log("[NativeShare] Copied to clipboard: " + text);
#endif
    }
}
