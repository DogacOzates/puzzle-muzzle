using UnityEngine;

public class GameManager : MonoBehaviour
{
    public bool IsLevelComplete { get; private set; }

    private GridManager gridManager;
    private InputHandler inputHandler;
    private UIManager uiManager;
    private TutorialController tutorialController;
    private int currentLevelIndex = 0;

    public bool IsTutorialRunning => tutorialController != null && tutorialController.IsRunning;

    private static readonly Color BgColor = new Color(0.97f, 0.95f, 0.92f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInitialize()
    {
        if (FindFirstObjectByType<GameManager>() == null)
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

        var uiObj = new GameObject("UIManager");
        uiObj.transform.SetParent(transform);
        uiManager = uiObj.AddComponent<UIManager>();
        uiManager.Initialize();

        LoadLevel(currentLevelIndex);
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

        // iOS safe area: estimate top/bottom insets in world units
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

        float gridCenterY = gridManager.GridOrigin.y - (gridManager.GridHeight - 1) * gridManager.CellSpacing / 2f;
        float cameraY = gridCenterY - (paddingBottom - paddingTop) / 2f;
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

        LevelData level = LevelDatabase.Levels[index];
        gridManager.Initialize(level);
        AdjustCameraToGrid();

        uiManager.SetLevelInfo(level.levelName, index, LevelDatabase.Levels.Length);
        uiManager.HideLevelComplete();

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
        IsLevelComplete = true;
        uiManager.ShowLevelComplete();
    }

    public void NextLevel()
    {
        LoadLevel(currentLevelIndex + 1);
    }

    public void RetryLevel()
    {
        LoadLevel(currentLevelIndex);
    }

    public void UseHint()
    {
        if (IsLevelComplete) return;

        LevelData level = LevelDatabase.Levels[currentLevelIndex];
        if (gridManager.SolveHint(level.solutions))
        {
            if (gridManager.IsLevelComplete())
                OnLevelComplete();
        }
    }
}
