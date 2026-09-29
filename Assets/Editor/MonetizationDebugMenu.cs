using UnityEditor;
using UnityEngine;

public static class MonetizationDebugMenu
{
    [MenuItem("Debug/Reset No Ads + Starter Pack Purchases")]
    private static void ResetPurchases()
    {
        PlayerPrefs.DeleteKey("monetization.noads.purchased");
        PlayerPrefs.DeleteKey("hints.starter.used");
        PlayerPrefs.Save();
        Debug.Log("[MonetizationDebug] Reset No Ads + Starter Pack flags. " +
                  (Application.isPlaying ? "Stop and re-enter Play Mode to apply." : "Enter Play Mode to test."));
    }

    [MenuItem("Debug/Reset Daily Reward (show again)")]
    private static void ResetDailyReward()
    {
        PlayerPrefs.DeleteKey("daily.lastClaim");
        PlayerPrefs.Save();
        Debug.Log("[MonetizationDebug] Daily reward reset — it will pop up ~1.5s after entering Play Mode.");
    }
}
