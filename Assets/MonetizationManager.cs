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
    public string LastIapError => iapBridge?.LastInitError ?? "iapBridge is null";
    public string NoAdsPrice { get; private set; } = DefaultNoAdsPrice;
    public string NoAdsButtonLabel => "No Ads\n" + NoAdsPrice;

    public event Action NoAdsStateChanged;
    public event Action NoAdsPriceChanged;

    public void Initialize()
    {
        IsNoAdsPurchased = PlayerPrefs.GetInt(NoAdsPurchasedKey, 0) == 1;

        adsBridge = new LevelGateAdsBridge();
        // Ads are initialized later via InitializeAds() after ATT permission is granted.

        if (NoAdsPurchasesEnabled)
        {
            iapBridge = new NoAdsIapBridge(NoAdsProductId, OnNoAdsPurchased, OnNoAdsPriceUpdated);
            iapBridge.Initialize();
        }
    }

    // Called from GameManager after the ATT dialog has been shown.
    public void InitializeAds()
    {
        adsBridge.Initialize();
        if (!IsNoAdsPurchased)
        {
            adsBridge.LoadLevelGateAd();
            adsBridge.LoadRewardedHintAd();
        }
    }

    public bool ShouldShowLevelGateAd(int currentLevelIndex)
    {
        if (IsNoAdsPurchased)
            return false;

        int lvl = currentLevelIndex + 1; // 1-based level number just completed

        if (lvl <= 29) return false; // first 29 levels: no ads

        // Each shape group is 300 levels. Within each group:
        //   first 100 levels → every 10,  next 200 levels → every 5
        int posInGroup = ((lvl - 1) % 300) + 1; // 1..300 within the current group
        return posInGroup <= 100 ? posInGroup % 10 == 0 : posInGroup % 5 == 0;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("No Ads purchase is temporarily disabled for builds.");
#endif
            return;
        }

        if (IsNoAdsPurchased)
            return;

        iapBridge.Purchase();
    }

    public void RestorePurchases(Action<bool, string> onComplete)
    {
        if (iapBridge == null)
        {
            onComplete?.Invoke(false, "Store not available");
            return;
        }
        iapBridge.RestorePurchases(onComplete);
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
        KeychainHelper.SetBool("noads.purchased", true);
        NoAdsStateChanged?.Invoke();
    }

    private void OnNoAdsPriceUpdated(string price)
    {
        if (string.IsNullOrEmpty(price))
            return;

        // Unity IAP returns "$0.01" as a placeholder in the Editor. Ignore it.
        if (price == "$0.01" || price == "0.01")
            return;

        NoAdsPrice = price;
        NoAdsPriceChanged?.Invoke();
    }
}
