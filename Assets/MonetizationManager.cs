using System;
using UnityEngine;

public class MonetizationManager : MonoBehaviour
{
    public const string NoAdsProductId      = "com.dogac.puzzlemuzzle.removeads";
    public const string HintStarterPackId   = "com.dogac.puzzlemuzzle.hints.starter";
    public const string HintPack5Id         = "com.dogac.puzzlemuzzle.hints.5";
    public const string HintPack20Id        = "com.dogac.puzzlemuzzle.hints.20";
    public const string HintPack60Id        = "com.dogac.puzzlemuzzle.hints.60";

    private const bool NoAdsPurchasesEnabled    = true;
    private const string NoAdsPurchasedKey      = "monetization.noads.purchased";
    private const string StarterPackUsedKey     = "hints.starter.used";
    private const string DefaultNoAdsPrice      = "$9.99";

    private LevelGateAdsBridge adsBridge;
    private NoAdsIapBridge iapBridge;

    public bool IsNoAdsAvailable  => NoAdsPurchasesEnabled;
    public bool IsNoAdsPurchased  { get; private set; }
    public bool IsStoreReady      => iapBridge != null && iapBridge.IsStoreReady;
    public string LastIapError    => iapBridge?.LastInitError ?? "iapBridge is null";
    public string NoAdsPrice      { get; private set; } = DefaultNoAdsPrice;
    public string NoAdsButtonLabel => "No Ads\n" + NoAdsPrice;

    // Hint pack prices (updated when IAP store is ready)
    public string HintStarterPrice { get; private set; } = "$0.99";
    public string HintPack5Price   { get; private set; } = "$0.99";
    public string HintPack20Price  { get; private set; } = "$2.99";
    public string HintPack60Price  { get; private set; } = "$5.99";
    public bool   IsStarterPackAvailable => PlayerPrefs.GetInt(StarterPackUsedKey, 0) == 0;

    public event Action NoAdsStateChanged;
    public event Action NoAdsPriceChanged;
    public event Action HintPackPricesChanged;

    public void Initialize()
    {
        IsNoAdsPurchased = PlayerPrefs.GetInt(NoAdsPurchasedKey, 0) == 1;

        adsBridge = new LevelGateAdsBridge();

        if (NoAdsPurchasesEnabled)
        {
            iapBridge = new NoAdsIapBridge(NoAdsProductId, OnNoAdsPurchased, OnNoAdsPriceUpdated);
            iapBridge.AddHintPack(HintStarterPackId, 15, OnStarterPackGranted, p => OnHintPackPrice(HintStarterPackId, p));
            iapBridge.AddHintPack(HintPack5Id,  5,  OnHintPackGranted, p => OnHintPackPrice(HintPack5Id,  p));
            iapBridge.AddHintPack(HintPack20Id, 20, OnHintPackGranted, p => OnHintPackPrice(HintPack20Id, p));
            iapBridge.AddHintPack(HintPack60Id, 60, OnHintPackGranted, p => OnHintPackPrice(HintPack60Id, p));
            iapBridge.Initialize();
        }
    }

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
        if (IsNoAdsPurchased) return false;
        int lvl = currentLevelIndex + 1;
        if (lvl <= 29) return false;
        int posInGroup = ((lvl - 1) % 300) + 1;
        return posInGroup <= 100 ? posInGroup % 10 == 0 : posInGroup % 5 == 0;
    }

    public void ShowScheduledLevelGateAdIfNeeded(int currentLevelIndex, Action onFinished)
    {
        if (!ShouldShowLevelGateAd(currentLevelIndex)) { onFinished?.Invoke(); return; }
        adsBridge.ShowLevelGateAd(() =>
        {
            if (!IsNoAdsPurchased) adsBridge.LoadLevelGateAd();
            onFinished?.Invoke();
        });
    }

    public void PurchaseNoAds()
    {
        if (!NoAdsPurchasesEnabled || IsNoAdsPurchased) return;
        iapBridge.Purchase();
    }

    public void PurchaseHintPack(string productId)
    {
        if (iapBridge == null) { Debug.LogWarning("IAP not initialized"); return; }
        iapBridge.PurchaseHintPack(productId);
    }

    public void RestorePurchases(Action<bool, string> onComplete)
    {
        if (iapBridge == null) { onComplete?.Invoke(false, "Store not available"); return; }
        iapBridge.RestorePurchases(onComplete);
    }

    public void ShowRewardedHintAd(Action onRewardEarned)
    {
        adsBridge.ShowRewardedHintAd(onRewardEarned);
    }

    // Called from hint button during gameplay — no-ads users get hint without watching
    public void ShowRewardedHintAdIfNeeded(Action onRewardEarned)
    {
        if (IsNoAdsPurchased) { onRewardEarned?.Invoke(); return; }
        adsBridge.ShowRewardedHintAd(onRewardEarned);
    }

    // ── event from hint packs granted (consumed via IAP) ────────────────────────
    // This is invoked by the IAP bridge; GameManager subscribes to handle granting.
    public event Action<int> HintPackGranted;

    private void OnHintPackGranted(int amount) => HintPackGranted?.Invoke(amount);

    private void OnStarterPackGranted(int amount)
    {
        PlayerPrefs.SetInt(StarterPackUsedKey, 1);
        PlayerPrefs.Save();
        HintPackGranted?.Invoke(amount);
    }

    private void OnNoAdsPurchased()
    {
        if (IsNoAdsPurchased) return;
        IsNoAdsPurchased = true;
        PlayerPrefs.SetInt(NoAdsPurchasedKey, 1);
        PlayerPrefs.Save();
        KeychainHelper.SetBool("noads.purchased", true);
        NoAdsStateChanged?.Invoke();
    }

    private void OnNoAdsPriceUpdated(string price)
    {
        if (string.IsNullOrEmpty(price)) return;
        if (price == "$0.01" || price == "0.01") return;
        NoAdsPrice = price;
        NoAdsPriceChanged?.Invoke();
    }

    private void OnHintPackPrice(string productId, string price)
    {
        if (string.IsNullOrEmpty(price) || price == "$0.01" || price == "0.01") return;
        if (productId == HintStarterPackId) HintStarterPrice = price;
        else if (productId == HintPack5Id)  HintPack5Price   = price;
        else if (productId == HintPack20Id) HintPack20Price  = price;
        else if (productId == HintPack60Id) HintPack60Price  = price;
        HintPackPricesChanged?.Invoke();
    }
}
