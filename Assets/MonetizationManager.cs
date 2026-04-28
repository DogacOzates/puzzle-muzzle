using System;
using UnityEngine;

public class MonetizationManager : MonoBehaviour
{
    public const string NoAdsProductId = "com.dogac.puzzlemuzzle.removeads";

    private const bool NoAdsPurchasesEnabled = true;
    private const string NoAdsPurchasedKey = "monetization.noads.purchased";
    private const int AdFreeLevels = 10;
    private const string DefaultNoAdsPrice = "$4.99";

    private LevelGateAdsBridge adsBridge;
    private NoAdsIapBridge iapBridge;

    public bool IsNoAdsAvailable => NoAdsPurchasesEnabled;
    public bool IsNoAdsPurchased { get; private set; }
    public bool IsStoreReady => iapBridge != null && iapBridge.IsStoreReady;
    public string NoAdsPrice { get; private set; } = DefaultNoAdsPrice;
    public string NoAdsButtonLabel => "No Ads\n" + NoAdsPrice;

    public event Action NoAdsStateChanged;
    public event Action NoAdsPriceChanged;

    public void Initialize()
    {
        IsNoAdsPurchased = PlayerPrefs.GetInt(NoAdsPurchasedKey, 0) == 1;

        adsBridge = new LevelGateAdsBridge();
        adsBridge.Initialize();
        if (!IsNoAdsPurchased)
        {
            adsBridge.LoadLevelGateAd();
            adsBridge.LoadRewardedHintAd();
        }

        if (NoAdsPurchasesEnabled)
        {
            iapBridge = new NoAdsIapBridge(NoAdsProductId, OnNoAdsPurchased, OnNoAdsPriceUpdated);
            iapBridge.Initialize();
        }
    }

    public bool ShouldShowLevelGateAd(int currentLevelIndex)
    {
        if (IsNoAdsPurchased)
            return false;

        int lvl = currentLevelIndex + 1; // 1-based level number just completed

        if (lvl <= 10)   return false;           // levels 1-10: no ads
        if (lvl <= 100)  return lvl % 5 == 0;    // levels 11-100: every 5 levels
        if (lvl <= 150)  return lvl % 4 == 0;    // levels 101-150: every 4 levels
        if (lvl <= 200)  return lvl % 3 == 0;    // levels 151-200: every 3 levels
        return lvl % 2 == 0;                     // levels 201-300: every 2 levels
    }

    public void ShowScheduledLevelGateAdIfNeeded(int currentLevelIndex, Action onFinished)
    {
        if (!ShouldShowLevelGateAd(currentLevelIndex))
        {
            onFinished?.Invoke();
            return;
        }

        adsBridge.ShowLevelGateAd(() =>
        {
            if (!IsNoAdsPurchased)
                adsBridge.LoadLevelGateAd();
            onFinished?.Invoke();
        });
    }

    public void PurchaseNoAds()
    {
        if (!NoAdsPurchasesEnabled)
        {
            Debug.Log("No Ads purchase is temporarily disabled for builds.");
            return;
        }

        if (IsNoAdsPurchased)
            return;

        iapBridge.Purchase();
    }

    public void ShowRewardedHintAdIfNeeded(Action onRewardEarned)
    {
        if (IsNoAdsPurchased)
        {
            onRewardEarned?.Invoke();
            return;
        }

        adsBridge.ShowRewardedHintAd(onRewardEarned);
    }

    private void OnNoAdsPurchased()
    {
        if (IsNoAdsPurchased)
            return;

        IsNoAdsPurchased = true;
        PlayerPrefs.SetInt(NoAdsPurchasedKey, 1);
        PlayerPrefs.Save();
        NoAdsStateChanged?.Invoke();
    }

    private void OnNoAdsPriceUpdated(string price)
    {
        if (string.IsNullOrEmpty(price))
            return;

        NoAdsPrice = price;
        NoAdsPriceChanged?.Invoke();
    }
}
