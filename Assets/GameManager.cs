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
    private int currentLevelIndex = 0;
    private bool isLevelTransitionRunning;
    private int hintPressedThisLevel;

    public bool IsTutorialRunning => tutorialController != null && tutorialController.IsRunning;

    private static readonly Color BgColor = new Color(0.97f, 0.95f, 0.92f);
    private const float CompletedLevelPreviewDuration = 0.2f;
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
        SetupCamera();

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

        var uiObj = new GameObject("UIManager");
        uiObj.transform.SetParent(transform);
        uiManager = uiObj.AddComponent<UIManager>();
        uiManager.Initialize();
        monetizationManager.NoAdsStateChanged += RefreshMonetizationUI;
        monetizationManager.NoAdsPriceChanged += RefreshMonetizationUI;
        RefreshMonetizationUI();

        currentLevelIndex = LoadSavedLevelIndex();
        LoadLevel(currentLevelIndex);
        StartCoroutine(PeriodicPromoBannerCoroutine());
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
        cam.backgroundColor = BgColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0, -0.5f, -10f);
        cam.allowHDR = false;
    }

    private void AdjustCameraToGrid()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float gridW = (gridManager.GridWidth - 1) * gridManager.CellSpacing + gridManager.CellVisualSize;
        float gridH = (gridManager.GridHeight - 1) * gridManager.CellSpacing + gridManager.CellVisualSize;

        float aspect = (float)Screen.width / Screen.height;
        Rect safe = Screen.safeArea;

        // Safe area insets as fractions of total screen height (notch, Dynamic Island, home bar)
        float topInsetFrac = (Screen.height - safe.yMax) / (float)Screen.height;
        float botInsetFrac = safe.yMin / (float)Screen.height;

        // Derive canvas scale from CanvasScaler settings (Scale With Screen Size, ref=1080×1920, match=0.5)
        // This lets us convert UI bar heights (in canvas units) to screen-height fractions
        float scaleW = safe.width / 1080f;
        float scaleH = safe.height / 1920f;
        float canvasScale = Mathf.Lerp(scaleW, scaleH, 0.5f);

        // Top bar = 140 canvas units, bottom bar = 160 canvas units → as fractions of screen height
        float topBarFrac = (140f * canvasScale) / Screen.height;
        float botBarFrac = (160f * canvasScale) / Screen.height;

        // Available fraction of screen height after all UI and breathing-room margins
        const float marginFrac = 0.12f; // visual breathing room above and below the grid
        float uiFrac = topInsetFrac + botInsetFrac + topBarFrac + botBarFrac;
        float gameAreaFrac = Mathf.Max(1f - uiFrac - 2f * marginFrac, 0.35f);

        // Horizontal margin fraction on each side
        const float horzMarginFrac = 0.12f;

        // Camera orthoSize: take the more restrictive of height and width constraints
        float orthoFromH = gridH / (2f * gameAreaFrac);
        float orthoFromW = gridW / (2f * (1f - 2f * horzMarginFrac) * aspect);
        float orthoSize = Mathf.Max(orthoFromH, orthoFromW);

        cam.orthographicSize = orthoSize;

        // Position camera so the grid is vertically centred in the play area (between the two bars)
        float totalWorldH = orthoSize * 2f;
        float topWorldOccupied = totalWorldH * (topInsetFrac + topBarFrac + marginFrac);
        float botWorldOccupied = totalWorldH * (botInsetFrac + botBarFrac + marginFrac);

        float gridCenterY = gridManager.GridOrigin.y
            - (gridManager.GridHeight - 1) * gridManager.CellSpacing / 2f;
        float cameraY = gridCenterY - (botWorldOccupied - topWorldOccupied) / 2f;
        cam.transform.position = new Vector3(0, cameraY, -10f);
    }

    public void LoadLevel(int index)
    {
        if (index < 0 || index >= LevelDatabase.Levels.Length)
        {
            Debug.Log("All levels completed!");
            return;
        }

        IsLevelComplete = false;
        currentLevelIndex = index;
        hintPressedThisLevel = 0;

        LevelData level = LevelDatabase.Levels[index];
        gridManager.Initialize(level);
        AdjustCameraToGrid();

        uiManager.SetLevelInfo(level.levelName, index, LevelDatabase.Levels.Length);
        uiManager.HideLevelComplete();
        uiManager.HideLevelSelect();

        if (index == 0)
            StartTutorial();
    }

    private void StartTutorial()
    {
        if (tutorialController != null) tutorialController.Cleanup();
        var obj = new GameObject("TutorialController");
        obj.transform.SetParent(transform);
        tutorialController = obj.AddComponent<TutorialController>();
        tutorialController.Run(gridManager, this);
    }

    public void OnLevelComplete()
    {
        if (IsLevelComplete)
            return;

        IsLevelComplete = true;
        SaveProgressForNextLevel();
        uiManager.HideLevelComplete();
        NextLevel();
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
    }

    public void SelectLevel(int index)
    {
        if (index < 0 || index > GetHighestUnlockedLevelIndex())
            return;

        TransitionToLevel(index, false);
    }

    public void UseHint()
    {
        if (IsLevelComplete) return;
        if (monetizationManager == null)
            return;

        hintPressedThisLevel++;

        if (hintPressedThisLevel >= 2 && !monetizationManager.IsNoAdsPurchased)
        {
            uiManager.ShowHintPromoPopup(
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

        monetizationManager.PurchaseNoAds();
    }

    private void RefreshMonetizationUI()
    {
        if (uiManager == null || monetizationManager == null)
            return;

        uiManager.SetNoAdsState(monetizationManager.IsNoAdsAvailable, monetizationManager.IsNoAdsPurchased, monetizationManager.NoAdsButtonLabel);
    }

    private void GrantHint()
    {
        LevelData level = LevelDatabase.Levels[currentLevelIndex];
        if (gridManager.SolveHint(level.solutions))
        {
            if (!monetizationManager.IsNoAdsPurchased)
                uiManager.ShowPromoTopBanner();
            if (gridManager.IsLevelComplete())
                OnLevelComplete();
        }
    }

    private IEnumerator PeriodicPromoBannerCoroutine()
    {
        yield return new WaitForSeconds(60f);
        while (true)
        {
            if (!IsLevelComplete && !isLevelTransitionRunning &&
                monetizationManager != null && !monetizationManager.IsNoAdsPurchased &&
                uiManager != null)
            {
                uiManager.ShowPromoTopBanner();
            }
            yield return new WaitForSeconds(Random.Range(50f, 80f));
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
        return Mathf.Clamp(Mathf.Max(currentLevelIndex, LoadSavedLevelIndex()), 0, maxLevelIndex);
    }

    private void SaveProgressForNextLevel()
    {
        int maxLevelIndex = LevelDatabase.Levels.Length - 1;
        int savedLevelIndex = Mathf.Min(currentLevelIndex + 1, maxLevelIndex);

        if (savedLevelIndex <= PlayerPrefs.GetInt(SavedLevelIndexKey, 0))
            return;

        PlayerPrefs.SetInt(SavedLevelIndexKey, savedLevelIndex);
        PlayerPrefs.Save();
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
