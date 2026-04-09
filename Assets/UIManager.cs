using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    private Text levelProgressText;
    private GameObject levelCompletePanel;
    private Text completeText;
    private Button nextLevelButton;
    private Button retryButton;
    private Button restartButton;
    private Button hintButton;

    private Canvas canvas;
    private RectTransform safeAreaRect;
    private Font defaultFont;

    // Colors
    private static readonly Color BtnTeal = new Color(0.25f, 0.78f, 0.72f);
    private static readonly Color BtnCoral = new Color(0.91f, 0.40f, 0.35f);
    private static readonly Color BtnGreen = new Color(0.30f, 0.75f, 0.48f);
    private static readonly Color TextDark = new Color(0.18f, 0.18f, 0.22f);
    private static readonly Color TextMuted = new Color(0.52f, 0.50f, 0.48f);

    public void Initialize()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
            defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

        CreateCanvas();
        EnsureEventSystem();
        CreateSafeArea();
        CreateTopBar();
        CreateBottomBar();
        CreateLevelCompletePanel();
    }

    private void CreateCanvas()
    {
        var obj = new GameObject("UICanvas");
        obj.transform.SetParent(transform);
        canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = obj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        obj.AddComponent<GraphicRaycaster>();
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var obj = new GameObject("EventSystem");
        obj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        obj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private void CreateSafeArea()
    {
        var obj = new GameObject("SafeArea");
        obj.transform.SetParent(canvas.transform, false);
        safeAreaRect = obj.AddComponent<RectTransform>();
        safeAreaRect.anchorMin = Vector2.zero;
        safeAreaRect.anchorMax = Vector2.one;
        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        Rect safe = Screen.safeArea;
        Vector2 mn = safe.position;
        Vector2 mx = safe.position + safe.size;
        safeAreaRect.anchorMin = new Vector2(mn.x / Screen.width, mn.y / Screen.height);
        safeAreaRect.anchorMax = new Vector2(mx.x / Screen.width, mx.y / Screen.height);
        safeAreaRect.offsetMin = Vector2.zero;
        safeAreaRect.offsetMax = Vector2.zero;
    }

    private void CreateTopBar()
    {
        // Top bar container
        var bar = CreatePanel("TopBar", safeAreaRect, new Vector2(0, 0), new Vector2(0, -140));
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0, 1);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(0.5f, 1);

        // Level progress text (top center) — Georgia italic, matching tutorial style
        var georgiaFont = Font.CreateDynamicFontFromOSFont("Georgia", 72);
        levelProgressText = MakeText("Progress", bar.transform, new Vector2(0, -70), 42, FontStyle.Italic, TextDark);
        if (georgiaFont != null) levelProgressText.font = georgiaFont;
    }

    private void CreateBottomBar()
    {
        // Bottom bar container
        var bar = CreatePanel("BottomBar", safeAreaRect, new Vector2(0, 0), new Vector2(0, 0));
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0, 0);
        barRect.anchorMax = new Vector2(1, 0);
        barRect.pivot = new Vector2(0.5f, 0);
        barRect.sizeDelta = new Vector2(0, 160);

        // Hint button (left) with icon
        hintButton = CreateIconButton("Hint", bar.transform, new Vector2(100, 80), 88, "icons/lightbulb");
        var hRect = hintButton.GetComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0, 0);
        hRect.anchorMax = new Vector2(0, 0);
        hintButton.onClick.AddListener(() => FindFirstObjectByType<GameManager>().UseHint());

        // Restart button (right) with icon
        restartButton = CreateIconButton("Restart", bar.transform, new Vector2(-100, 80), 88, "icons/reload");
        var rRect = restartButton.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(1, 0);
        rRect.anchorMax = new Vector2(1, 0);
        restartButton.onClick.AddListener(() => FindFirstObjectByType<GameManager>().RetryLevel());
    }

    private Sprite LoadIconSprite(string name)
    {
        var tex = Resources.Load<Texture2D>(name);
        if (tex == null)
        {
            string path = Application.dataPath + "/" + name + ".png";
            if (System.IO.File.Exists(path))
            {
                tex = new Texture2D(2, 2);
                tex.LoadImage(System.IO.File.ReadAllBytes(path));
                tex.filterMode = FilterMode.Bilinear;
            }
        }
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
    }

    private Button CreateIconButton(string name, Transform parent, Vector2 pos, float size, string iconPath)
    {
        var obj = new GameObject(name + "Btn");
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(size, size);

        var img = obj.AddComponent<Image>();
        img.color = Color.clear; // transparent background

        var btn = obj.AddComponent<Button>();

        // Icon child
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(obj.transform, false);
        var iRect = iconObj.AddComponent<RectTransform>();
        iRect.anchorMin = Vector2.zero;
        iRect.anchorMax = Vector2.one;
        iRect.offsetMin = Vector2.zero;
        iRect.offsetMax = Vector2.zero;

        var iconImg = iconObj.AddComponent<Image>();
        var sprite = LoadIconSprite(iconPath);
        if (sprite != null)
        {
            iconImg.sprite = sprite;
            iconImg.preserveAspect = true;
        }
        else
        {
            iconImg.color = Color.gray;
        }

        // Make the icon the target graphic for press feedback
        btn.targetGraphic = iconImg;
        var c = btn.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1);
        c.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1);
        btn.colors = c;

        return btn;
    }

    private Text MakeText(string name, Transform parent, Vector2 pos, int size, FontStyle style, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1);
        rect.anchorMax = new Vector2(0.5f, 1);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(600, 60);

        var txt = obj.AddComponent<Text>();
        txt.font = defaultFont;
        txt.fontSize = size;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        // Subtle shadow for readability
        var shadow = obj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.12f);
        shadow.effectDistance = new Vector2(1, -1);

        return txt;
    }

    private GameObject CreatePanel(string name, RectTransform parent, Vector2 offsetMin, Vector2 offsetMax)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return obj;
    }

    private void CreateLevelCompletePanel()
    {
        // Full screen overlay
        var panelObj = new GameObject("LevelCompletePanel");
        panelObj.transform.SetParent(canvas.transform, false);

        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var overlay = panelObj.AddComponent<Image>();
        overlay.color = new Color(0, 0, 0, 0.45f);

        // Card
        var card = new GameObject("Card");
        card.transform.SetParent(panelObj.transform, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(700, 520);

        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(1f, 1f, 1f, 0.97f);

        // Shadow behind card
        var cardShadow = new GameObject("CardShadow");
        cardShadow.transform.SetParent(panelObj.transform, false);
        var csRect = cardShadow.AddComponent<RectTransform>();
        csRect.anchorMin = new Vector2(0.5f, 0.5f);
        csRect.anchorMax = new Vector2(0.5f, 0.5f);
        csRect.sizeDelta = new Vector2(720, 540);
        csRect.anchoredPosition = new Vector2(4, -6);

        var csImg = cardShadow.AddComponent<Image>();
        csImg.color = new Color(0, 0, 0, 0.15f);
        cardShadow.transform.SetAsFirstSibling();

        // "Well Done!" text
        completeText = MakeCardText("CompleteText", card.transform, new Vector2(0, 140), 58, FontStyle.Bold, TextDark);
        completeText.text = "Well Done!";

        // Next Level button
        nextLevelButton = CreateCardButton("Next Level", card.transform, new Vector2(0, -40), BtnGreen);
        nextLevelButton.onClick.AddListener(() => FindFirstObjectByType<GameManager>().NextLevel());

        // Retry button
        retryButton = CreateCardButton("Retry", card.transform, new Vector2(0, -130), BtnCoral);
        retryButton.onClick.AddListener(() => FindFirstObjectByType<GameManager>().RetryLevel());

        levelCompletePanel = panelObj;
        levelCompletePanel.SetActive(false);
    }

    private Text MakeCardText(string name, Transform parent, Vector2 pos, int size, FontStyle style, Color color)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(600, 70);

        var txt = obj.AddComponent<Text>();
        txt.font = defaultFont;
        txt.fontSize = size;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        return txt;
    }

    private Button CreateCardButton(string label, Transform parent, Vector2 pos, Color color)
    {
        var obj = new GameObject(label + "Btn");
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(440, 78);

        var img = obj.AddComponent<Image>();
        img.color = color;

        var btn = obj.AddComponent<Button>();
        var c = btn.colors;
        c.highlightedColor = color * 0.88f;
        c.pressedColor = color * 0.72f;
        c.highlightedColor = new Color(c.highlightedColor.r, c.highlightedColor.g, c.highlightedColor.b, 1);
        c.pressedColor = new Color(c.pressedColor.r, c.pressedColor.g, c.pressedColor.b, 1);
        btn.colors = c;

        var txtObj = new GameObject("Text");
        txtObj.transform.SetParent(obj.transform, false);
        var tRect = txtObj.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;

        var txt = txtObj.AddComponent<Text>();
        txt.font = defaultFont;
        txt.text = label;
        txt.fontSize = 34;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        return btn;
    }

    // --- Public API ---

    public void SetLevelInfo(string name, int index, int total)
    {
        if (levelProgressText != null) levelProgressText.text = $"Level {index + 1} / {total}";
    }

    public void ShowLevelComplete()
    {
        levelCompletePanel.SetActive(true);
    }

    public void HideLevelComplete()
    {
        levelCompletePanel.SetActive(false);
    }
}
