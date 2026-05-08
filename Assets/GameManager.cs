using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    private const string SavedLevelIndexKey = "progress.savedLevelIndex";

    private enum GameMode { Regular, Daily }

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
    private int hintPressedThisLevel;
    private GameMode currentGameMode = GameMode.Regular;
    private int preDailyLevelIndex;

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
        monetizationManager.NoAdsPriceChanged += RefreshMonetizationUI;
        RefreshMonetizationUI();

        // Show streak on launch (may be 0, which hides the label)
        uiManager.UpdateStreakDisplay(DailyChallengeManager.GetStreak());
        // Restore free hint badge if any hints were accumulated
        uiManager.UpdateHintBadge(GetFreeHints());

        currentLevelIndex = LoadSavedLevelIndex();
        LoadLevel(currentLevelIndex);
        StartCoroutine(PeriodicPromoBannerCoroutine());
        StartCoroutine(RequestATTThenInitAds());
    }

    // ATT must be shown after the app window is fully ready.
    // A short delay ensures iOS doesn't silently drop the dialog.
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

        float paddingH = 1.2f;
        float paddingTop = 2.0f + topInsetFrac * 4f;
        float paddingBottom = 4.5f + botInsetFrac * 2f;

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
        if (index < 0 || index >= LevelDatabase.Levels.Length)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("All levels completed!");
#endif
            return;
        }

        IsLevelComplete = false;
        currentLevelIndex = index;
        hintPressedThisLevel = 0;

        AudioManager.Instance?.OnChainReset();

        LevelData level = LevelDatabase.Levels[index];
        gridManager.Initialize(level);
        AdjustCameraToGrid();

        uiManager.SetLevelInfo(level.levelName, index, LevelDatabase.Levels.Length);
        uiManager.HideLevelComplete();
        uiManager.HideLevelSelect();

        if (index == 0)
            StartTutorial(true);
        else if (index == 1 && PlayerPrefs.GetInt("tutorial.done", 0) == 0)
            StartTutorial(false);
    }

    private void StartTutorial(bool isPreTutorial = false)
    {
        if (tutorialController != null) tutorialController.Cleanup();
        var obj = new GameObject("TutorialController");
        obj.transform.SetParent(transform);
        tutorialController = obj.AddComponent<TutorialController>();
        uiManager.SetTutorialMode(true);
        tutorialController.Run(gridManager, this, isPreTutorial);
    }

    public void OnTutorialComplete()
    {
        uiManager.SetTutorialMode(false);
    }

    public void OnMainTutorialComplete()
    {
        PlayerPrefs.SetInt("tutorial.done", 1);
        PlayerPrefs.Save();
        OnTutorialComplete();
    }

    public void SaveProgressTo(int toIndex)
    {
        int maxLevelIndex = LevelDatabase.Levels.Length - 1;
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

        if (currentGameMode == GameMode.Daily)
        {
            // Daily challenge: don't advance campaign progress.
            DailyChallengeManager.MarkTodayCompleted();
            int streak = DailyChallengeManager.GetStreak();
            gameCenterManager?.ReportDailyCompleted(streak);
            uiManager?.UpdateStreakDisplay(streak);
            // Award 1 free hint for completing today's daily challenge
            AddFreeHints(1);
            // Restore currentLevelIndex immediately so GetHighestUnlockedLevelIndex()
            // never sees the daily level index again (e.g. during transition animation).
            currentLevelIndex = preDailyLevelIndex;
            currentGameMode = GameMode.Regular;
            uiManager.HideLevelComplete();
            TransitionToLevel(preDailyLevelIndex, false);
            return;
        }

        // Regular campaign completion
        SaveProgressForNextLevel();
        gameCenterManager?.ReportCampaignLevelCompleted(GetHighestUnlockedLevelIndex());
        TryRequestReview();
        uiManager.HideLevelComplete();
        NextLevel();
    }

    public void PlayDailyChallenge()
    {
        preDailyLevelIndex = currentLevelIndex;
        currentGameMode = GameMode.Daily;
        uiManager?.HideLevelSelect();
        TransitionToLevel(DailyChallengeManager.GetDailyLevelIndex(), false);
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

        uiManager.ShowLevelSelect(currentLevelIndex, GetHighestUnlockedLevelIndex(), LevelDatabase.Levels.Length);
        uiManager.UpdateDailyChallengeCard();
    }

    public void SelectLevel(int index)
    {
        if (index < 0 || index > GetHighestUnlockedLevelIndex())
            return;

        currentGameMode = GameMode.Regular;
        TransitionToLevel(index, false);
    }

    // --- Free Hint Helpers ---
    private int GetFreeHints() => PlayerPrefs.GetInt("hints.free", 0);

    private void AddFreeHints(int amount)
    {
        PlayerPrefs.SetInt("hints.free", GetFreeHints() + amount);
        PlayerPrefs.Save();
        uiManager?.UpdateHintBadge(GetFreeHints());
    }

    private void ConsumeFreeHint()
    {
        int current = GetFreeHints();
        if (current > 0)
        {
            PlayerPrefs.SetInt("hints.free", current - 1);
            PlayerPrefs.Save();
            uiManager?.UpdateHintBadge(GetFreeHints());
        }
    }

    public void UseHint()
    {
        if (IsLevelComplete) return;
        if (monetizationManager == null)
            return;

        // Free hints take priority — no ad needed
        if (GetFreeHints() > 0)
        {
            ConsumeFreeHint();
            GrantHint();
            return;
        }

        hintPressedThisLevel++;

        if (hintPressedThisLevel >= 2 && !monetizationManager.IsNoAdsPurchased)
        {
            uiManager?.ShowHintPromoPopup(
                () => monetizationManager.ShowRewardedHintAdIfNeeded(GrantHint),
                () => PurchaseNoAds()
            );
            return;
        }

        monetizationManager.ShowRewardedHintAdIfNeeded(GrantHint);
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
        LevelData level = LevelDatabase.Levels[currentLevelIndex];
        if (gridManager.SolveHint(level.solutions))
        {
            if (monetizationManager != null && !monetizationManager.IsNoAdsPurchased &&
                Time.realtimeSinceStartup - lastPromoBannerTime > 180f)
            {
                uiManager?.ShowPromoTopBanner();
                lastPromoBannerTime = Time.realtimeSinceStartup;
            }
            if (gridManager.IsLevelComplete())
                OnLevelComplete();
        }
    }

    private float lastPromoBannerTime = -9999f;

    private IEnumerator PeriodicPromoBannerCoroutine()
    {
        yield return new WaitForSeconds(180f);
        while (true)
        {
            if (!IsLevelComplete && !isLevelTransitionRunning &&
                monetizationManager != null && !monetizationManager.IsNoAdsPurchased &&
                uiManager != null)
            {
                uiManager.ShowPromoTopBanner();
                lastPromoBannerTime = Time.realtimeSinceStartup;
            }
            yield return new WaitForSeconds(Random.Range(150f, 240f));
        }
    }

    private int LoadSavedLevelIndex()
    {
        int maxLevelIndex = LevelDatabase.Levels.Length - 1;
        return Mathf.Clamp(PlayerPrefs.GetInt(SavedLevelIndexKey, 0), 0, maxLevelIndex);
    }

    private int GetHighestUnlockedLevelIndex()
    {
        int maxLevelIndex = LevelDatabase.Levels.Length - 1;
        // Use only the saved progress — never currentLevelIndex.
        // This ensures daily challenge level never inflates campaign unlock count.
        return Mathf.Clamp(LoadSavedLevelIndex(), 0, maxLevelIndex);
    }

    private void SaveProgressForNextLevel()
    {
        int maxLevelIndex = LevelDatabase.Levels.Length - 1;
        int savedLevelIndex = Mathf.Min(currentLevelIndex + 1, maxLevelIndex);

        if (savedLevelIndex <= PlayerPrefs.GetInt(SavedLevelIndexKey, 0))
            return;

        PlayerPrefs.SetInt(SavedLevelIndexKey, savedLevelIndex);
        PlayerPrefs.Save();
        iCloudSyncManager.SyncProgress(savedLevelIndex);
    }

    private void TransitionToLevel(int index, bool showCompletedPreview)
    {
        if (isLevelTransitionRunning)
            return;

        if (index < 0 || index >= LevelDatabase.Levels.Length)
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
