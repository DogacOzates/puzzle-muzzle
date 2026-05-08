using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class TutorialController : MonoBehaviour
{
    private GridManager gridManager;
    private GameManager gameManager;
    private GameObject handObj;
    private GameObject hintTextObj;
    private Transform ringTransform;
    private Vector3 handBasePos;
    private bool firstMove = true;
    private bool pointingAtButton = false;
    private Vector3 buttonNudgeDir;

    // Guided-phase input
    private Cell waitingForCell;
    private bool cellTapped;

    public bool IsRunning { get; private set; }

    public void Run(GridManager grid, GameManager game, bool preMode = false)
    {
        gridManager = grid;
        gameManager = game;
        CreateHand();
        CreateHintText();
        IsRunning = true;
        StartCoroutine(preMode ? PlayPreTutorial() : PlayMainTutorial());
    }

    // Load pointer.png and create the hand object
    private void CreateHand()
    {
        handObj = new GameObject("TutorialHand");

        // Load pointer sprite from PNG
        Sprite pointerSprite = LoadPointerSprite();

        var handVisual = new GameObject("HandVisual");
        handVisual.transform.SetParent(handObj.transform);
        handVisual.transform.localPosition = Vector3.zero;
        var sr = handVisual.AddComponent<SpriteRenderer>();
        sr.sprite = pointerSprite;
        sr.sortingOrder = 102;

        // Scale so hand is ~0.75 world units wide
        float worldWidth = pointerSprite.texture.width / pointerSprite.pixelsPerUnit;
        float scale = 0.75f / worldWidth;
        handVisual.transform.localScale = Vector3.one * scale;

        // Tap ring at fingertip (handObj origin)
        var ring = new GameObject("Ring");
        ring.transform.SetParent(handObj.transform);
        ring.transform.localPosition = new Vector3(0, 0, -0.01f);
        ring.transform.localScale = Vector3.one * 0.35f;
        var ringSr = ring.AddComponent<SpriteRenderer>();
        ringSr.sprite = SpriteGenerator.RoundedRect;
        ringSr.color = new Color(0.25f, 0.78f, 0.72f, 0.4f);
        ringSr.sortingOrder = 103;
        ringTransform = ring.transform;

        handObj.SetActive(false);
    }

    private Sprite LoadPointerSprite()
    {
        Sprite pointerSprite = ResourceSpriteLoader.LoadSprite("icons/pointer", new Vector2(0.38f, 0.9f));
        if (pointerSprite == null)
            return SpriteGenerator.RoundedRect;

        return pointerSprite;
    }

    private void CreateHintText()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        hintTextObj = new GameObject("TutorialHint");
        hintTextObj.transform.SetParent(canvas.transform, false);

        var rect = hintTextObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0);
        rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 420);
        rect.sizeDelta = new Vector2(900, 180);

        var text = hintTextObj.AddComponent<UnityEngine.UI.Text>();
        text.text = "";  // set by each tutorial coroutine
        // Create font at high atlas resolution for crisp rendering on retina
        text.font = Font.CreateDynamicFontFromOSFont("Georgia", 72);
        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Font.CreateDynamicFontFromOSFont("Arial", 72);
        }
        text.fontSize = 38;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.35f, 0.33f, 0.30f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.fontStyle = FontStyle.Italic;

        var shadow = hintTextObj.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.1f);
        shadow.effectDistance = new Vector2(1, -1);
    }

    private void SetHintText(string msg)
    {
        if (hintTextObj == null) return;
        var txt = hintTextObj.GetComponent<UnityEngine.UI.Text>();
        if (txt != null) txt.text = msg;
    }

    void Update()
    {
        if (handObj == null || !handObj.activeSelf) return;

        // Ring pulse
        if (ringTransform != null)
        {
            float pulse = 1f + 0.1f * Mathf.Sin(Time.time * 5f);
            ringTransform.localScale = Vector3.one * 0.35f * pulse;
        }

        // Bob hand while waiting for player tap
        if (waitingForCell != null)
        {
            float bob = Mathf.Sin(Time.time * 3f) * 0.04f;
            handObj.transform.position = handBasePos + new Vector3(0, bob, 0);
        }

        // Nudge hand toward button when pointing at icons
        if (pointingAtButton)
        {
            float nudge = Mathf.Sin(Time.time * 3.5f) * 0.12f;
            handObj.transform.position = handBasePos + buttonNudgeDir * nudge;
        }

        // Detect player tap in guided phase
        if (waitingForCell == null) return;
        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Vector2 screenPos = pointer.position.ReadValue();
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z));
        Vector2Int gridPos = gridManager.WorldToGrid(worldPos);

        if (gridManager.IsValidGridPos(gridPos.x, gridPos.y))
        {
            Cell tapped = gridManager.GetCell(gridPos.x, gridPos.y);
            if (tapped == waitingForCell)
                cellTapped = true;
        }
    }

    // ===== Main Flow =====

    private IEnumerator PlayPreTutorial()
    {
        LevelData level = LevelDatabase.Levels[0];

        SetHintText("Tap each cell the hand points to!\nMake a path that ends on the number.");
        yield return new WaitForSeconds(0.8f);

        yield return StartCoroutine(GuidedPhase(level));

        handObj.SetActive(false);
        SetHintText("The number shows how many cells to connect!");
        yield return new WaitForSeconds(1.8f);

        if (hintTextObj != null) Destroy(hintTextObj);
        IsRunning = false;
        gameManager.OnTutorialComplete();
        gameManager.SaveProgressTo(1);
        yield return new WaitForSeconds(0.3f);
        gameManager.NextLevel();
    }

    private IEnumerator PlayMainTutorial()
    {
        LevelData level = LevelDatabase.Levels[1];

        SetHintText("Follow the hand!\nFill ALL cells to complete the level.");
        yield return new WaitForSeconds(0.6f);

        yield return StartCoroutine(GuidedPhase(level));

        handObj.SetActive(false);
        yield return new WaitForSeconds(0.4f);

        yield return StartCoroutine(ShowButtonExplanations());

        SetHintText("Have Fun!");
        yield return new WaitForSeconds(1.5f);

        if (hintTextObj != null) Destroy(hintTextObj);
        handObj.SetActive(false);
        IsRunning = false;
        gameManager.OnMainTutorialComplete();
        yield return new WaitForSeconds(0.3f);
        gameManager.NextLevel();
    }

    // Phase 2: hand guides, player taps each cell
    private IEnumerator GuidedPhase(LevelData level)
    {
        for (int p = 0; p < level.solutions.Length; p++)
        {
            var path = level.solutions[p];
            for (int i = 0; i < path.Length; i++)
            {
                Cell cell = gridManager.GetCell(path.GetX(i), path.GetY(i));
                Vector3 target = gridManager.GridToWorld(path.GetX(i), path.GetY(i));

                yield return StartCoroutine(MoveHand(target));

                // Wait for player to tap this exact cell
                yield return StartCoroutine(WaitForPlayerTap(cell));

                PerformTap(cell, i == 0);
                yield return new WaitForSeconds(0.15f);
            }
            yield return new WaitForSeconds(0.3f);
        }
    }

    private IEnumerator WaitForPlayerTap(Cell cell)
    {
        waitingForCell = cell;
        cellTapped = false;
        while (!cellTapped)
            yield return null;
        waitingForCell = null;
    }

    private void PerformTap(Cell cell, bool isFirstInPath)
    {
        if (isFirstInPath && !gridManager.HasActiveSelection)
        {
            if (cell.State == CellState.Empty)
                gridManager.TryStartFromEmpty(cell);
            else if (cell.State == CellState.NumberTarget && cell.TargetNumber == 1)
                gridManager.TryCompleteTarget1(cell);
        }
        else
        {
            gridManager.TryExtendSelection(cell);
        }
    }

    // ===== Button Explanations =====

    private IEnumerator ShowButtonExplanations()
    {
        var hintBtn = GameObject.Find("HintBtn");
        var menuBtn = GameObject.Find("LevelSelectBtn");
        var restartBtn = GameObject.Find("RestartBtn");
        var noAdsBtn = GameObject.Find("NoAdsBtn");

        var handVisual = handObj.transform.Find("HandVisual");
        Vector3 origScale = handVisual.localScale;
        Quaternion origRot = handVisual.localRotation;
        pointingAtButton = true;

        // 1. Hint button (bottom-left): hand from upper-right, finger points down-left
        if (hintBtn != null)
        {
            handVisual.localScale = new Vector3(origScale.x, -origScale.y, origScale.z);
            handVisual.localRotation = Quaternion.Euler(0, 0, -30f);
            Vector3 offset = new Vector3(0.4f, 0.55f, 0);
            buttonNudgeDir = -offset.normalized;

            SetHintText("Stuck on a level?\nTap this to get a hint!");
            Vector3 btnWorld = ScreenToWorld(hintBtn.GetComponent<RectTransform>());
            yield return StartCoroutine(MoveHand(btnWorld + offset));
            yield return StartCoroutine(WaitForAnyTap());
        }

        yield return new WaitForSeconds(0.3f);

        // 2. Restart button (bottom-right): hand from upper-left, finger points down-right
        if (restartBtn != null)
        {
            handVisual.localScale = new Vector3(-origScale.x, -origScale.y, origScale.z);
            handVisual.localRotation = Quaternion.Euler(0, 0, 30f);
            Vector3 offset = new Vector3(-0.4f, 0.55f, 0);
            buttonNudgeDir = -offset.normalized;

            SetHintText("Made a mistake?\nTap this to restart!");
            Vector3 btnWorld = ScreenToWorld(restartBtn.GetComponent<RectTransform>());
            yield return StartCoroutine(MoveHand(btnWorld + offset));
            yield return StartCoroutine(WaitForAnyTap());
        }

        yield return new WaitForSeconds(0.3f);

        // 3. No Ads button (bottom-center): hand from upper-right, finger points down-left
        if (noAdsBtn != null)
        {
            handVisual.localScale = new Vector3(origScale.x, -origScale.y, origScale.z);
            handVisual.localRotation = Quaternion.Euler(0, 0, -24f);
            Vector3 offset = new Vector3(0.42f, 0.58f, 0f);
            buttonNudgeDir = -offset.normalized;

            SetHintText("Want to remove ads forever?\nTap this to buy No Ads!");
            Vector3 btnWorld = ScreenToWorld(noAdsBtn.GetComponent<RectTransform>());
            yield return StartCoroutine(MoveHand(btnWorld + offset));
            yield return StartCoroutine(WaitForAnyTap());
        }

        yield return new WaitForSeconds(0.3f);

        // 4. Level text (top center, last): hand from lower-right, finger points up-left
        if (menuBtn != null)
        {
            handVisual.localScale = origScale;
            handVisual.localRotation = Quaternion.Euler(0, 0, 0f);
            Vector3 offset = new Vector3(0.48f, -0.52f, 0f);
            buttonNudgeDir = -offset.normalized;

            SetHintText("Want to replay an earlier level?\nTap the level text to open the level menu!");
            Vector3 btnWorld = ScreenToWorld(menuBtn.GetComponent<RectTransform>());
            yield return StartCoroutine(MoveHand(btnWorld + offset));
            yield return StartCoroutine(WaitForAnyTap());
        }

        // Restore original hand orientation
        pointingAtButton = false;
        handVisual.localScale = origScale;
        handVisual.localRotation = origRot;

        handObj.SetActive(false);
        yield return new WaitForSeconds(0.3f);
    }

    private Vector3 ScreenToWorld(RectTransform uiElement)
    {
        // For overlay canvas, RectTransform.position is screen-space
        Vector3 screenPos = uiElement.position;
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, Mathf.Abs(Camera.main.transform.position.z)));
        worldPos.z = 0;
        return worldPos;
    }

    private IEnumerator WaitForAnyTap()
    {
        yield return null;
        yield return null;
        while (true)
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
                yield break;
            yield return null;
        }
    }

    // ===== Animations =====

    private IEnumerator MoveHand(Vector3 worldTarget)
    {
        handObj.SetActive(true);
        Vector3 start;

        if (firstMove)
        {
            start = worldTarget + new Vector3(2f, -2f, 0);
            handObj.transform.position = start;
            firstMove = false;
        }
        else
        {
            start = handObj.transform.position;
        }

        float duration = 0.35f;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0, 1, t / duration);
            handObj.transform.position = Vector3.Lerp(start, worldTarget, p);
            yield return null;
        }
        handObj.transform.position = worldTarget;
        handBasePos = worldTarget;
    }

    public void Cleanup()
    {
        StopAllCoroutines();
        IsRunning = false;
        gameManager?.OnTutorialComplete();
        if (handObj != null) Destroy(handObj);
        if (hintTextObj != null) Destroy(hintTextObj);
    }
}
