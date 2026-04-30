using System;
using UnityEngine;

#if GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
#endif

public class LevelGateAdsBridge
{
    // ── Ad Unit IDs ───────────────────────────────────────────────────────
    // Set USE_TEST_ADS = true while testing on device.
    // Switch to false and use your real IDs before App Store submission.
    private const bool USE_TEST_ADS = false;

    // Google's official test ad unit IDs (safe to use on any device)
    private const string TestInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
    private const string TestRewardedAdUnitId     = "ca-app-pub-3940256099942544/1712485313";

    // Your real production ad unit IDs
    private const string RealIosInterstitialAdUnitId = "ca-app-pub-2933494287812005/4279957716";
    private const string RealIosRewardedAdUnitId     = "ca-app-pub-2933494287812005/7161258613";

    private const string AndroidInterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    private const string AndroidRewardedAdUnitId     = "ca-app-pub-3940256099942544/5224354917";

#if GOOGLE_MOBILE_ADS
    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;
    private bool isInitialized;
    private bool pendingLoadAfterInitialization;
    private bool pendingRewardedLoadAfterInitialization;
    private bool interstitialLoadInProgress;
    private bool rewardedLoadInProgress;
    private Action pendingInterstitialShowCallback;
    private Action pendingRewardedShowCallback;
#endif

    public void Initialize()
    {
#if GOOGLE_MOBILE_ADS
        MobileAds.Initialize(_ =>
        {
            isInitialized = true;
            if (pendingLoadAfterInitialization)
            {
                pendingLoadAfterInitialization = false;
                LoadLevelGateAd();
            }
            if (pendingRewardedLoadAfterInitialization)
            {
                pendingRewardedLoadAfterInitialization = false;
                LoadRewardedHintAd();
            }
        });
#else
        Debug.Log("AdMob bridge is inactive. Import the Google Mobile Ads Unity plugin and add the GOOGLE_MOBILE_ADS scripting define to enable ads.");
#endif
    }

    public void LoadLevelGateAd()
    {
#if GOOGLE_MOBILE_ADS
        if (!isInitialized)
        {
            pendingLoadAfterInitialization = true;
            return;
        }

        if (string.IsNullOrEmpty(GetAdUnitId()))
            return;

        if (interstitialAd != null || interstitialLoadInProgress)
            return;

        interstitialLoadInProgress = true;
        InterstitialAd.Load(GetAdUnitId(), new AdRequest(), (ad, error) =>
        {
            interstitialLoadInProgress = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning("Failed to load level gate ad: " + (error != null ? error.ToString() : "unknown error"));
                Action fallback = pendingInterstitialShowCallback;
                pendingInterstitialShowCallback = null;
                fallback?.Invoke();
                return;
            }

            interstitialAd = ad;

            if (pendingInterstitialShowCallback != null)
            {
                Action callback = pendingInterstitialShowCallback;
                pendingInterstitialShowCallback = null;
                ShowLevelGateAd(callback);
            }
        });
#endif
    }

    public void LoadRewardedHintAd()
    {
#if GOOGLE_MOBILE_ADS
        if (!isInitialized)
        {
            pendingRewardedLoadAfterInitialization = true;
            return;
        }

        if (string.IsNullOrEmpty(GetRewardedAdUnitId()))
            return;

        if (rewardedAd != null || rewardedLoadInProgress)
            return;

        rewardedLoadInProgress = true;
        RewardedAd.Load(GetRewardedAdUnitId(), new AdRequest(), (ad, error) =>
        {
            rewardedLoadInProgress = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning("Failed to load rewarded hint ad: " + (error != null ? error.ToString() : "unknown error"));
                pendingRewardedShowCallback = null;
                return;
            }

            rewardedAd = ad;

            if (pendingRewardedShowCallback != null)
            {
                Action callback = pendingRewardedShowCallback;
                pendingRewardedShowCallback = null;
                ShowRewardedHintAd(callback);
            }
        });
#endif
    }

    public void ShowLevelGateAd(Action onFinished)
    {
#if GOOGLE_MOBILE_ADS
        if (interstitialAd == null || !interstitialAd.CanShowAd())
        {
            pendingInterstitialShowCallback = onFinished;
            LoadLevelGateAd();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Level gate ad is not ready yet. Loading it now.");
#endif
            return;
        }

        bool finished = false;
        void Complete()
        {
            if (finished)
                return;

            finished = true;
            interstitialAd.Destroy();
            interstitialAd = null;
            onFinished?.Invoke();
        }

        interstitialAd.OnAdFullScreenContentClosed += Complete;
        interstitialAd.OnAdFullScreenContentFailed += _ => Complete();
        interstitialAd.Show();
#else
        onFinished?.Invoke();
#endif
    }

    public void ShowRewardedHintAd(Action onRewardEarned)
    {
#if GOOGLE_MOBILE_ADS
        if (rewardedAd == null || !rewardedAd.CanShowAd())
        {
            pendingRewardedShowCallback = onRewardEarned;
            LoadRewardedHintAd();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Rewarded hint ad is not ready yet. Loading it now.");
#endif
            return;
        }

        bool handledClose = false;
        bool rewardGranted = false;

        void CleanupAndReload()
        {
            if (handledClose)
                return;

            handledClose = true;
            if (rewardGranted)
                onRewardEarned?.Invoke();
            rewardedAd.Destroy();
            rewardedAd = null;
            LoadRewardedHintAd();
        }

        rewardedAd.OnAdFullScreenContentClosed += CleanupAndReload;
        rewardedAd.OnAdFullScreenContentFailed += _ => CleanupAndReload();
        rewardedAd.Show(_ =>
        {
            if (rewardGranted)
                return;

            rewardGranted = true;
        });
#else
        onRewardEarned?.Invoke();
#endif
    }

#if GOOGLE_MOBILE_ADS
    private string GetAdUnitId()
    {
#if UNITY_ANDROID
        return AndroidInterstitialAdUnitId;
#elif UNITY_IOS
        return USE_TEST_ADS ? TestInterstitialAdUnitId : RealIosInterstitialAdUnitId;
#else
        return string.Empty;
#endif
    }

    private string GetRewardedAdUnitId()
    {
#if UNITY_ANDROID
        return AndroidRewardedAdUnitId;
#elif UNITY_IOS
        return USE_TEST_ADS ? TestRewardedAdUnitId : RealIosRewardedAdUnitId;
#else
        return string.Empty;
#endif
    }
#endif
}
