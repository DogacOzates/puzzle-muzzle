using UnityEngine;
using System.Runtime.InteropServices;

public class HapticManager : MonoBehaviour
{
    public static HapticManager Instance { get; private set; }

    private const string HapticsKey = "settings.haptics";

    public bool IsHapticsEnabled
    {
        get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void _HapticLight();
    [DllImport("__Internal")] private static extern void _HapticSuccess();
#endif

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Light tap — call when a cell is collected.</summary>
    public void CellCollected()
    {
        if (!IsHapticsEnabled) return;
#if UNITY_IOS && !UNITY_EDITOR
        _HapticLight();
#endif
    }

    /// <summary>Success burst — call when a level is completed.</summary>
    public void LevelComplete()
    {
        if (!IsHapticsEnabled) return;
#if UNITY_IOS && !UNITY_EDITOR
        _HapticSuccess();
#endif
    }
}
