using System;
using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    private const string SavedLevelIndexKey = "progress.savedLevelIndex";

    public bool IsLevelComplete { get; private set; }

    private GridManager gridManager;
    private InputHandler inputHandler;
    private UIManager uiManager;
    private MonetizationManager monetizationManager;
    private TutorialController tutorialController;
    private iCloudSyncManager iCloudSync;
    private GameCenterManager gameCenterManager;
    private HapticManager hapticManager;
    private int currentLevelIndex = 0;
    private bool isLevelTransitionRunning;

    public bool IsTutorialRunning => tutorialController != null && tutorialController.IsRunning;

    private static readonly Color BgColor = new Color(0.97f, 0.95f, 0.92f);
    private const float CompletedLevelPreviewDuration   = 0.6f;
    private const float CompletedLevelDisappearWaveStep = 0.045f;
    private const float CompletedLevelDisappearDuration = 0.42f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInitialize()
    {
        if (FindAnyObjectByType<GameManager>() == null)
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }
    }

    void Awake()
    {
        // Cap at 60fps — prevents 120Hz ProMotion devices (iPhone 16/16 Pro) from
        // running at 120fps, which doubles GPU/CPU load and causes thermal throttling.
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        // Safe to refresh every Play now: levels are lazy-loaded by campaign group,
        // so this no longer rebuilds all 1200 levels at startup.
        LevelDatabase.InvalidateCache();

        SetupCamera();

        var audioObj = new GameObject("AudioManager");
        audioObj.transform.SetParent(transform);
        audioObj.AddComponent<AudioManager>();

        var gridObj = new GameObject("GridManager");
        gridObj.transform.SetParent(transform);
        gridManager = gridObj.AddComponent<GridManager>();

        var inputObj = new GameObject("InputHandler");
        inputObj.transform.SetParent(transform);
        inputHandler = inputObj.AddComponent<InputHandler>();
        inputHandler.Initialize(gridManager, this);

        var monetizationObj = new GameObject("MonetizationManager");
        monetizationObj.transform.SetParent(transform);
        monetizationManager = monetizationObj.AddComponent<MonetizationManager>();
        monetizationManager.Initialize();

        // iCloudSyncManager must Awake before LoadSavedLevelIndex so merged progress is read.
        var iCloudObj = new GameObject("iCloudSyncManager");
        iCloudObj.transform.SetParent(transform);
        iCloudSync = iCloudObj.AddComponent<iCloudSyncManager>();

        var gcObj = new GameObject("GameCenterManager");
        gcObj.transform.SetParent(transform);
        gameCenterManager = gcObj.AddComponent<GameCenterManager>();

        var hapticObj = new GameObject("HapticManager");
        hapticObj.transform.SetParent(transform);
        hapticManager = hapticObj.AddComponent<HapticManager>();

        var themeObj = new GameObject("ThemeManager");
        themeObj.transform.SetParent(transform);
        themeObj.AddComponent<ThemeManager>();
        ThemeManager.OnThemeChanged += OnThemeChanged;

        var uiObj = new GameObject("UIManager");
        uiObj.transform.SetParent(transform);
        uiManager = uiObj.AddComponent<UIManager>();
        uiManager.Initialize();
        monetizationManager.NoAdsStateChanged += RefreshMonetizationUI;
        monetizationManager.NoAdsStateChanged += () =>
        {
            if (monetizationManager.IsNoAdsPurchased) uiManager?.HideOfferBanner();
            RefreshHintBadge();
        };
        monetizationManager.NoAdsPriceChanged += RefreshMonetizationUI;
        monetizationManager.HintPackGranted   += OnHintPackGranted;
        RefreshMonetizationUI();

        // Restore free hint badge if any hints were accumulated
        RefreshHintBadge();

        StartCoroutine(CheckDailyRewardCoroutine());

        currentLevelIndex = LoadSavedLevelIndex();
        LoadLevel(currentLevelIndex);
        StartCoroutine(RequestATTThenInitAds());
    }

    // ATT must be shown after the app window is fully ready.
    // A short delay ensures iOS doesn't silently drop the dialog.
    private IEnumerator CheckDailyRewardCoroutine()
    {
        yield return new WaitForSeconds(1.5f);

        string today     = DateTime.Now.ToString("yyyy-MM-dd");
        string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        string lastClaim = PlayerPrefs.GetString("daily.lastClaim", "");

        if (lastClaim == today) yield break; // already claimed today

        int streak = PlayerPrefs.GetInt("daily.streak", 0);
        streak = (lastClaim == yesterday) ? streak + 1 : 1;
        if (streak > 7) streak = 1; // safety reset

        bool isStreakBonus = streak == 7;
        int hintsGranted   = isStreakBonus ? 10 : 1;

        AddFreeHints(hintsGranted);
        PlayerPrefs.SetString("daily.lastClaim", today);
        PlayerPrefs.SetInt("daily.streak", isStreakBonus ? 0 : streak);
        PlayerPrefs.Save();

        uiManager?.ShowDailyRewardPopup(streak, hintsGranted, isStreakBonus);
    }

    private IEnumerator RequestATTThenInitAds()
    {
        yield return new WaitForSeconds(0.5f);
        ATTManager.RequestAuthorization(_ => monetizationManager?.InitializeAds());
    }

    private void OnThemeChanged()
    {
        Camera cam = Camera.main;
        if (cam != null && ThemeManager.Instance != null)
            cam.backgroundColor = ThemeManager.Instance.BgColor;
    }

    private void OnDestroy()
    {
        ThemeManager.OnThemeChanged -= OnThemeChanged;
    }

    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var camObj = new GameObject("MainCamera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        cam.orthographic = true;
        cam.backgroundColor = ThemeManager.Instance != null ? ThemeManager.Instance.BgColor : BgColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0, -0.5f, -10f);
        cam.allowHDR = false;
    }

    private void AdjustCameraToGrid()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (gridManager == null) return;

        float gridW = gridManager.BoardVisualWidth;
        float gridH = gridManager.BoardVisualHeight;

        Rect safe = Screen.safeArea;
        float screenH = Screen.height;
        float topInsetFrac = (screenH - safe.yMax) / screenH;
        float botInsetFrac = safe.yMin / screenH;

        // Triangle levels have many small cells — use tighter padding so the
        // camera can zoom in more, making each cell ~15-25% larger on screen.
        bool isTri = gridManager.IsTriangleMode;
        float paddingH      = isTri ? 0.5f : 1.2f;
        float paddingTop    = 2.0f + topInsetFrac * 4f;
        float paddingBottom = isTri ? (3.0f + botInsetFrac * 2f)
                                    : (4.5f + botInsetFrac * 2f);

        float neededWidth = gridW + paddingH * 2;
        float neededHeight = gridH + paddingTop + paddingBottom;

        float aspect = (float)Screen.width / Screen.height;

        float orthoH = neededHeight / 2f;
        float orthoW = neededWidth / (2f * aspect);
        cam.orthographicSize = Mathf.Max(orthoH, orthoW);

        float gridCenterY = gridManager.GridCenterY;
        float cameraY = gridCenterY - (paddingBottom - paddingTop) / 2f;
        cam.transform.position = new Vector3(0, cameraY, -10f);
    }

    public void LoadLevel(int index)
    {
        if (index < 0 || index >= LevelDatabase.TotalLevels)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("All levels completed!");
#endif
            return;
        }

        IsLevelComplete = false;
        currentLevelIndex = index;

        AudioManager.Instance?.OnChainReset();

        LevelData level = LevelDatabase.GetLevel(index);
        gridManager.Initialize(level);
        AdjustCameraToGrid();

        uiManager.SetLevelInfo(level.levelName, index, LevelDatabase.TotalLevels);
        uiManager.HideLevelComplete();
        uiManager.HideLevelSelect();

        LevelDatabase.PrefetchLevel(index + 1);

        if (index == 0)
            StartTutorial(true);
        else if (index == 1
#if UNITY_EDITOR
            )
#else
            && PlayerPrefs.GetInt("tutorial.v2.done", 0) == 0)
#endif
            StartTutorial(false);
        else if (index == SequenceTutorialLevelIndex
#if UNITY_EDITOR
            )
#else
            && PlayerPrefs.GetInt(SequenceTutorialDoneKey, 0) == 0)
#endif
            StartTutorial(false, true);
    }

    private void StartTutorial(bool isPreTutorial = false, bool isSequenceTutorial = false)
    {
        if (tutorialController != null) tutorialController.Cleanup();
        var obj = new GameObject("TutorialController");
        obj.transform.SetParent(transform);
        tutorialController = obj.AddComponent<TutorialController>();
        uiManager.SetTutorialMode(true);
        tutorialController.Run(gridManager, this, isPreTutorial, isSequenceTutorial);
    }

    // First level of the Sequence campaigns (level 901 for the player)
    public const int SequenceTutorialLevelIndex = 900;
    private const string SequenceTutorialDoneKey = "tutorial.sequence.done";

    public void OnSequenceTutorialComplete()
    {
        PlayerPrefs.SetInt(SequenceTutorialDoneKey, 1);
        PlayerPrefs.Save();
        SaveProgressTo(SequenceTutorialLevelIndex + 1);
        OnTutorialComplete();
    }

    public void OnTutorialComplete()
    {
        uiManager.SetTutorialMode(false);
    }

    public void OnMainTutorialComplete()
    {
        PlayerPrefs.SetInt("tutorial.v2.done", 1);
        PlayerPrefs.Save();
        SaveProgressTo(2);
        OnTutorialComplete();
    }

    public void SaveProgressTo(int toIndex)
    {
        int maxLevelIndex = LevelDatabase.TotalLevels - 1;
        toIndex = Mathf.Clamp(toIndex, 0, maxLevelIndex);
        if (toIndex > PlayerPrefs.GetInt(SavedLevelIndexKey, 0))
        {
            PlayerPrefs.SetInt(SavedLevelIndexKey, toIndex);
            PlayerPrefs.Save();
        }
    }

    public void OnLevelComplete()
    {
        if (IsLevelComplete)
            return;

        IsLevelComplete = true;
        AudioManager.Instance?.OnLevelComplete();
        HapticManager.Instance?.LevelComplete();

        SaveProgressForNextLevel();
        gameCenterManager?.ReportCampaignLevelCompleted(GetHighestUnlockedLevelIndex());
        TryRequestReview();
        uiManager.HideLevelComplete();
        NextLevel();
    }

    private void TryRequestReview()
    {
        // Show at level 29, then every 100 levels (129, 229, ...)
        int lvl = currentLevelIndex + 1;
        if (lvl < 29 || (lvl - 29) % 100 != 0)
            return;

        // Never show again if user already rated
        if (PlayerPrefs.GetInt("review.given", 0) == 1)
            return;

        uiManager?.ShowRatePopup(
            onRate: () =>
            {
                PlayerPrefs.SetInt("review.given", 1);
                PlayerPrefs.Save();
                StartCoroutine(RequestReviewAfterDelay(0.5f));
            },
            onDismiss: () => { }
        );
    }

    private IEnumerator RequestReviewAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
#if UNITY_IOS && !UNITY_EDITOR
        UnityEngine.iOS.Device.RequestStoreReview();
#endif
    }

    public void NextLevel()
    {
        int nextLevelIndex = currentLevelIndex + 1;

        if (IsLevelComplete && monetizationManager != null)
        {
            monetizationManager.ShowScheduledLevelGateAdIfNeeded(currentLevelIndex, () => TransitionToLevel(nextLevelIndex, true));
            return;
        }

        TransitionToLevel(nextLevelIndex, IsLevelComplete);
    }

    public void RetryLevel()
    {
        AudioManager.Instance?.OnRestart();
        TransitionToLevel(currentLevelIndex, false);
    }

    public void ToggleLevelSelectMenu()
    {
        if (uiManager == null)
            return;

        if (uiManager.IsLevelSelectVisible)
        {
            uiManager.HideLevelSelect();
            return;
        }

        uiManager.ShowLevelSelect(currentLevelIndex, GetLevelSelectMaxIndex(), LevelDatabase.TotalLevels);
    }

    public void SelectLevel(int index)
    {
        if (index < 0 || index > GetLevelSelectMaxIndex())
            return;

        TransitionToLevel(index, false);
    }

    // --- Free Hint Helpers ---
    // ── Referral System ──────────────────────────────────────────────────────────
    // Codes are 8 chars: 7 data chars + 1 checksum (base-36).
    // This makes ~97% of randomly guessed codes invalid structurally.

    private static readonly string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static char ReferralChecksum(string data)
    {
        int sum = 0;
        for (int i = 0; i < data.Length; i++)
            sum += (i + 1) * (Base36.IndexOf(data[i]) + 1);
        return Base36[sum % 36];
    }

    private static bool ValidateReferralCode(string code)
    {
        if (code.Length != 8) return false;
        string data = code.Substring(0, 7);
        char expected = ReferralChecksum(data);
        return code[7] == expected;
    }

    public string GetReferralCode()
    {
        string code = PlayerPrefs.GetString("referral.myCode", "");
        if (!string.IsNullOrEmpty(code) && ValidateReferralCode(code))
            return code;

        // Build 7-char data segment from device unique identifier
        string raw = SystemInfo.deviceUniqueIdentifier.Replace("-", "").ToUpper();
        var sb = new System.Text.StringBuilder();
        foreach (char c in raw)
            if (Base36.Contains(c)) sb.Append(c);
        string cleaned = sb.ToString();
        while (cleaned.Length < 7) cleaned += "0";
        string data = cleaned.Substring(0, 7);
        code = data + ReferralChecksum(data);
        PlayerPrefs.SetString("referral.myCode", code);
        PlayerPrefs.Save();
        return code;
    }

    public void ShareReferralCode()
    {
        string code = GetReferralCode();
        const string appStoreUrl = "https://apps.apple.com/app/id6763839953";
        string msg = $"Play Puzzle Muzzle with me! 🧩\nEnter code {code} to get 5 free hints!\nDownload: {appStoreUrl}";
        NativeShare.Share(msg);
    }

    public void ClaimReferralCode(string code, System.Action<bool, string> onResult)
    {
        code = code.Trim().ToUpper();
        if (!ValidateReferralCode(code))
        {
            onResult(false, "Invalid code. Check and try again."); return;
        }
        if (code == GetReferralCode())
        {
            onResult(false, "You can't use your own code!"); return;
        }
        if (PlayerPrefs.GetInt("referral.claimed", 0) == 1)
        {
            onResult(false, "You already claimed a referral code."); return;
        }
        PlayerPrefs.SetInt("referral.claimed", 1);
        PlayerPrefs.Save();
        AddFreeHints(5);
        onResult(true, "+5 Hints added! Thanks for connecting. 🎉");
    }

    private int GetFreeHints() => PlayerPrefs.GetInt("hints.free", 0);

    // No Ads owners have unlimited hints → badge shows ∞
    private void RefreshHintBadge()
    {
        if (uiManager == null) return;
        if (monetizationManager != null && monetizationManager.IsNoAdsPurchased)
            uiManager.UpdateHintBadge("∞");
        else
            uiManager.UpdateHintBadge(GetFreeHints().ToString());
    }

    private void AddFreeHints(int amount)
    {
        PlayerPrefs.SetInt("hints.free", GetFreeHints() + amount);
        PlayerPrefs.Save();
        RefreshHintBadge();
    }

    private void ConsumeFreeHint()
    {
        int current = GetFreeHints();
        if (current > 0)
        {
            PlayerPrefs.SetInt("hints.free", current - 1);
            PlayerPrefs.Save();
            RefreshHintBadge();
        }
    }

    public void UseHint()
    {
        if (IsLevelComplete) return;
        if (monetizationManager == null) return;

        // No Ads includes unlimited hints — never consumes the balance
        if (monetizationManager.IsNoAdsPurchased)
        {
            GrantHint();
            return;
        }

        if (GetFreeHints() > 0)
        {
            ConsumeFreeHint();
            GrantHint();
            return;
        }

        // No hints in balance — open hint store (Watch Ad grants hint immediately)
        OpenHintStore(grantOnWatch: true);
    }

    public void OpenHintStore(bool grantOnWatch = false)
    {
        Action onWatchAd = grantOnWatch
            ? (Action)(() => monetizationManager.ShowRewardedHintAdIfNeeded(GrantHint))
            : (Action)(() => monetizationManager.ShowRewardedHintAdIfNeeded(() => AddFreeHints(1)));

        uiManager?.ShowHintStore(
            onWatchAd:          onWatchAd,
            onBuyStarter:       () => PurchaseHintPack(MonetizationManager.HintStarterPackId),
            onBuyPack5:         () => PurchaseHintPack(MonetizationManager.HintPack5Id),
            onBuyPack50:        () => PurchaseHintPack(MonetizationManager.HintPack50Id),
            onBuyPack100:       () => PurchaseHintPack(MonetizationManager.HintPack100Id),
            onBuyNoAds:         () => PurchaseNoAds(),
            isNoAdsPurchased:   monetizationManager.IsNoAdsPurchased,
            noAdsPrice:         monetizationManager.NoAdsPrice,
            isStarterAvailable: monetizationManager.IsStarterPackAvailable,
            starterPrice:       monetizationManager.HintStarterPrice,
            price5:             monetizationManager.HintPack5Price,
            price50:            monetizationManager.HintPack50Price,
            price100:           monetizationManager.HintPack100Price,
            currentHints:       GetFreeHints()
        );
    }

    // ── Timed offer banner: every 3 min of active gameplay, suggest a random offer ──
    private const float OfferIntervalSeconds = 180f;
    private float offerTimer;
    private string lastOfferId;

    void Update()
    {
        if (monetizationManager == null || uiManager == null) return;
        if (monetizationManager.IsNoAdsPurchased) return;          // No Ads buyers never see promos
        if (!Application.isFocused) return;

        // A menu/popup opened over the banner — get it out of the way
        if (uiManager.IsOfferBannerVisible && uiManager.IsModalOpen) uiManager.HideOfferBanner();

        // Only count time the player is actually playing a level
        bool busy = IsTutorialRunning || isLevelTransitionRunning || IsLevelComplete
                    || uiManager.IsModalOpen || uiManager.IsOfferBannerVisible;
        if (busy) return;

        offerTimer += Time.unscaledDeltaTime;
        if (offerTimer < OfferIntervalSeconds) return;
#if !UNITY_EDITOR
        if (!monetizationManager.IsStoreReady) return;              // retry next frame once the store is up
#endif

        offerTimer = 0f;
        ShowRandomOffer();
    }

    private void ShowRandomOffer()
    {
        var mm = monetizationManager;
        var ids = new System.Collections.Generic.List<string>();
        if (mm.IsStarterPackAvailable) ids.Add(MonetizationManager.HintStarterPackId);
        ids.Add(MonetizationManager.HintPack5Id);
        ids.Add(MonetizationManager.HintPack50Id);
        ids.Add(MonetizationManager.HintPack100Id);
        ids.Add(MonetizationManager.NoAdsProductId);
        if (ids.Count > 1 && lastOfferId != null) ids.Remove(lastOfferId);   // don't repeat back-to-back

        string id = ids[UnityEngine.Random.Range(0, ids.Count)];
        lastOfferId = id;

        Color purple = new Color(0.36f, 0.21f, 0.68f, 1f);
        Color green  = new Color(0.24f, 0.60f, 0.30f, 1f);
        Color navy   = new Color(0.17f, 0.20f, 0.29f, 1f);
        Color gold   = new Color(0.96f, 0.68f, 0.10f, 1f);
        Color brown  = new Color(0.22f, 0.11f, 0.02f, 1f);
        Color bulb   = new Color(1f, 0.85f, 0.30f, 1f);

        if (id == MonetizationManager.HintStarterPackId)
            uiManager.ShowOfferBanner("icons/lightbulb_white", bulb, purple,
                "Welcome Deal · 25 Hints", "One-time offer", mm.HintStarterPrice,
                gold, brown, () => PurchaseHintPack(id));
        else if (id == MonetizationManager.HintPack5Id)
            uiManager.ShowOfferBanner("icons/lightbulb_white", bulb, green,
                "5 Hints", "Stuck? Get a quick boost", mm.HintPack5Price,
                Color.white, green, () => PurchaseHintPack(id));
        else if (id == MonetizationManager.HintPack50Id)
            uiManager.ShowOfferBanner("icons/lightbulb_white", bulb, green,
                "50 Hints · Popular", "Plenty of help for tough levels", mm.HintPack50Price,
                Color.white, green, () => PurchaseHintPack(id));
        else if (id == MonetizationManager.HintPack100Id)
            uiManager.ShowOfferBanner("icons/lightbulb_white", bulb, green,
                "100 Hints · Best Value", "Stock up for the hardest levels", mm.HintPack100Price,
                Color.white, green, () => PurchaseHintPack(id));
        else
            uiManager.ShowOfferBanner("icons/adblock_white", Color.white, navy,
                "No Ads + Unlimited Hints", "One-time purchase · forever", mm.NoAdsPrice,
                gold, brown, () => PurchaseNoAds());
    }

    public void PurchaseNoAds()
    {
        if (monetizationManager == null)
            return;

        if (!monetizationManager.IsStoreReady)
        {
            uiManager?.ShowStoreUnavailablePopup(monetizationManager.LastIapError);
            return;
        }

        monetizationManager.PurchaseNoAds();
    }

    public void PurchaseHintPack(string productId)
    {
        if (monetizationManager == null) return;

        if (!monetizationManager.IsStoreReady)
        {
            uiManager?.ShowStoreUnavailablePopup(monetizationManager.LastIapError);
            return;
        }

        monetizationManager.PurchaseHintPack(productId);
    }

    private void OnHintPackGranted(int amount)
    {
        AddFreeHints(amount);
    }

    public void RestoreNoAdsPurchases()
    {
        if (monetizationManager == null)
            return;

        if (!monetizationManager.IsStoreReady)
        {
            uiManager?.ShowStoreUnavailablePopup(monetizationManager.LastIapError);
            return;
        }

        monetizationManager.RestorePurchases((success, error) =>
        {
            if (!success)
                uiManager?.ShowRestoreResultPopup(false, error);
        });
    }

    private void RefreshMonetizationUI()
    {
        if (uiManager == null || monetizationManager == null)
            return;

        uiManager.SetNoAdsState(monetizationManager.IsNoAdsAvailable, monetizationManager.IsNoAdsPurchased, monetizationManager.NoAdsButtonLabel);
    }

    private void GrantHint()
    {
        if (gridManager == null) return;
        LevelData level = LevelDatabase.GetLevel(currentLevelIndex);
        if (gridManager.SolveHint(level.solutions))
        {
            AudioManager.Instance?.OnHintUsed();
            HapticManager.Instance?.CellCollected();
            if (gridManager.IsLevelComplete())
                OnLevelComplete();
        }
    }

    private int LoadSavedLevelIndex()
    {
        int maxLevelIndex = LevelDatabase.TotalLevels - 1;
        return Mathf.Clamp(PlayerPrefs.GetInt(SavedLevelIndexKey, 0), 0, maxLevelIndex);
    }

    private int GetHighestUnlockedLevelIndex()
    {
        int maxLevelIndex = LevelDatabase.TotalLevels - 1;
        // Use only the saved progress — never currentLevelIndex.
        // This ensures daily challenge level never inflates campaign unlock count.
        return Mathf.Clamp(LoadSavedLevelIndex(), 0, maxLevelIndex);
    }

    // Test builds with the UNLOCK_ALL_LEVELS define (Debug → Test Build: Unlock All Levels) unlock every level in the
    // level select for device testing. Release builds are unaffected; saved progress,
    // Game Center and iCloud still use the real progress.
    private int GetLevelSelectMaxIndex()
    {
#if UNLOCK_ALL_LEVELS
        return LevelDatabase.TotalLevels - 1;
#else
        return GetHighestUnlockedLevelIndex();
#endif
    }

    private void SaveProgressForNextLevel()
    {
        int maxLevelIndex = LevelDatabase.TotalLevels - 1;
        int savedLevelIndex = Mathf.Min(currentLevelIndex + 1, maxLevelIndex);

        if (savedLevelIndex <= PlayerPrefs.GetInt(SavedLevelIndexKey, 0))
            return;

        PlayerPrefs.SetInt(SavedLevelIndexKey, savedLevelIndex);
        PlayerPrefs.Save();
#if !UNLOCK_ALL_LEVELS
        iCloudSyncManager.SyncProgress(savedLevelIndex);   // don't push test-build progress to the player's iCloud
#endif
    }

    private void TransitionToLevel(int index, bool showCompletedPreview)
    {
        if (isLevelTransitionRunning)
            return;

        if (index < 0 || index >= LevelDatabase.TotalLevels)
        {
            LoadLevel(index);
            return;
        }

        StartCoroutine(PlayLevelTransition(index, showCompletedPreview));
    }

    private IEnumerator PlayLevelTransition(int targetLevelIndex, bool showCompletedPreview)
    {
        isLevelTransitionRunning = true;

        if (gridManager != null)
        {
            if (showCompletedPreview)
            {
                yield return StartCoroutine(gridManager.PlayTransitionOut(
                    CompletedLevelPreviewDuration,
                    CompletedLevelDisappearWaveStep,
                    CompletedLevelDisappearDuration));
            }
            else
            {
                yield return StartCoroutine(gridManager.PlayTransitionOut());
            }
        }

        LoadLevel(targetLevelIndex);
        isLevelTransitionRunning = false;
    }

}
