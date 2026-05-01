using System;
using System.Runtime.InteropServices;

/// <summary>
/// Wraps iOS App Tracking Transparency (ATT) permission request.
/// Must be called before initializing AdMob to comply with Apple guidelines.
/// </summary>
public static class ATTManager
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _RequestATT(ATTCallback callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ATTCallback(int status);

    private static Action<int> _pendingCallback;

    [AOT.MonoPInvokeCallback(typeof(ATTCallback))]
    private static void OnNativeATTResult(int status)
    {
        var cb = _pendingCallback;
        _pendingCallback = null;
        cb?.Invoke(status);
    }
#endif

    // ATTrackingManagerAuthorizationStatus values:
    // 0 = NotDetermined, 1 = Restricted, 2 = Denied, 3 = Authorized
    public static void RequestAuthorization(Action<int> onComplete = null)
    {
#if UNITY_IOS && !UNITY_EDITOR
        _pendingCallback = onComplete;
        _RequestATT(OnNativeATTResult);
#else
        onComplete?.Invoke(3); // Authorized on Editor / non-iOS
#endif
    }
}
