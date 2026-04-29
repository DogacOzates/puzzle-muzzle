using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    private Text levelProgressText;
    private Text streakText;
    private GameObject levelCompletePanel;
    private Text completeText;
    private Button nextLevelButton;
    private Button retryButton;
    private Button restartButton;
    private Button hintButton;
    private Button noAdsButton;
    private Button levelSelectToggleButton;
    private GameObject noAdsPurchasePopup;
    private Image hintButtonIcon;
    private Image noAdsButtonIcon;
    private Image noAdsPingRing1;
    private Image noAdsPingRing2;
    private GameObject hintPromoPopup;
    private GameObject promoTopBanner;
    private Coroutine bannerCoroutine;
    private string noAdsPriceLabel = "$4.99";
    private GameObject levelSelectPanel;
    private ScrollRect levelSelectScrollRect;
    private Button[] levelSelectButtons;
    private Image[] levelSelectButtonImages;
    private Text[] levelSelectButtonLabels;
    private Image dailyChallengeCardImage;
    private Button dailyChallengeCardButton;
    private Text dailyChallengeCardMainText;
    private Text dailyChallengeCardSubText;
    private GameObject transitionOverlay;
    private Image transitionOverlayImage;
    private Text transitionOverlayText;
    private RectTransform transitionSquaresRoot;
    private Image[] transitionSquares;

    private Canvas canvas;
    private RectTransform safeAreaRect;
    private Font defaultFont;

    // Colors
    private static readonly Color BtnTeal = new Color(0.25f, 0.78f, 0.72f);
    private static readonly Color BtnCoral = new Color(0.91f, 0.40f, 0.35f);
    private static readonly Color BtnGreen = new Color(0.30f, 0.75f, 0.48f);
    private static readonly Color TextDark = new Color(0.18f, 0.18f, 0.22f);
    private static readonly Color TextMuted = new Color(0.52f, 0.50f, 0.48f);
    private static readonly Color CardWhite = new Color(1f, 1f, 1f, 0.985f);
    private static readonly Color SoftPanel = new Color(0.97f, 0.95f, 0.93f, 1f);
    private static readonly Color TransitionBg = new Color(0.97f, 0.95f, 0.92f);
    private static readonly Color TransitionSquareA = new Color(0.25f, 0.78f, 0.72f, 0.92f);
    private static readonly Color TransitionSquareB = new Color(0.30f, 0.75f, 0.48f, 0.90f);
    private static readonly Color TransitionSquareC = new Color(0.91f, 0.40f, 0.35f, 0.88f);

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
        CreateLevelSelectPanel();
        CreateLevelCompletePanel();
        CreateTransitionOverlay();
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
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
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

        levelSelectToggleButton = CreateInvisibleButton("LevelSelect", bar.transform, new Vector2(0, -70), new Vector2(520, 84));
        levelSelectToggleButton.onClick.AddListener(() => FindAnyObjectByType<GameManager>().ToggleLevelSelectMenu());

        // Settings gear button (right side of top bar)
        var settingsObj = new GameObject("SettingsBtn");
        settingsObj.transform.SetParent(bar.transform, false);
        var settingsRect = settingsObj.AddComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(1f, 1f);
        settingsRect.anchorMax = new Vector2(1f, 1f);
        settingsRect.pivot = new Vector2(1f, 0.5f);
        settingsRect.anchoredPosition = new Vector2(-20f, -70f);
        settingsRect.sizeDelta = new Vector2(90f, 90f);

        var settingsImg = settingsObj.AddComponent<Image>();
        settingsImg.sprite = SpriteGenerator.RoundedRect;
        settingsImg.color = new Color(0.95f, 0.93f, 0.88f, 1f);

        var settingsBtn = settingsObj.AddComponent<Button>();
        settingsBtn.targetGraphic = settingsImg;
        var sc = settingsBtn.colors;
        sc.highlightedColor = new Color(0.88f, 0.86f, 0.80f, 1f);
        sc.pressedColor    = new Color(0.78f, 0.76f, 0.70f, 1f);
        settingsBtn.colors = sc;
        settingsBtn.onClick.AddListener(ShowSettingsPopup);

        var settingsIconObj = new GameObject("Icon");
        settingsIconObj.transform.SetParent(settingsObj.transform, false);
        var siRect = settingsIconObj.AddComponent<RectTransform>();
        siRect.anchorMin = new Vector2(0.15f, 0.15f);
        siRect.anchorMax = new Vector2(0.85f, 0.85f);
        siRect.offsetMin = Vector2.zero;
        siRect.offsetMax = Vector2.zero;
        var siImg = settingsIconObj.AddComponent<Image>();
        var settingsSprite = Resources.Load<Sprite>("icons/settings");
        if (settingsSprite != null)
        {
            siImg.sprite = settingsSprite;
            siImg.color = TextDark;
        }
        else
        {
            // Fallback: emoji text if sprite not found
            Destroy(settingsIconObj);
            var fallback = new GameObject("Emoji");
            fallback.transform.SetParent(settingsObj.transform, false);
            var fbRect = fallback.AddComponent<RectTransform>();
            fbRect.anchorMin = Vector2.zero;
            fbRect.anchorMax = Vector2.one;
            fbRect.offsetMin = Vector2.zero;
            fbRect.offsetMax = Vector2.zero;
            var fbTxt = fallback.AddComponent<Text>();
            fbTxt.font = defaultFont;
            fbTxt.text = "⚙️";
            fbTxt.fontSize = 40;
            fbTxt.alignment = TextAnchor.MiddleCenter;
            fbTxt.color = TextDark;
        }
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
        hintButton.onClick.AddListener(() => FindAnyObjectByType<GameManager>().UseHint());

        // Restart button (right) with icon
        restartButton = CreateIconButton("Restart", bar.transform, new Vector2(-100, 80), 70, "icons/reload");
        var rRect = restartButton.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(1, 0);
        rRect.anchorMax = new Vector2(1, 0);
        restartButton.onClick.AddListener(() => FindAnyObjectByType<GameManager>().RetryLevel());

        noAdsButton = CreateIconButton("NoAds", bar.transform, new Vector2(0, 82), 106, "icons/adblock");
        var noAdsRect = noAdsButton.GetComponent<RectTransform>();
        noAdsRect.anchorMin = new Vector2(0.5f, 0f);
        noAdsRect.anchorMax = new Vector2(0.5f, 0f);
        noAdsRect.pivot = new Vector2(0.5f, 0.5f);
        noAdsButton.onClick.AddListener(ShowNoAdsPurchasePopup);

        // Two ping rings behind icon — expand outward on each glow pulse
        var ring1Obj = new GameObject("PingRing1");
        ring1Obj.transform.SetParent(noAdsButton.transform, false);
        ring1Obj.transform.SetSiblingIndex(0);
        var ring1Rect = ring1Obj.AddComponent<RectTransform>();
        ring1Rect.anchorMin = new Vector2(0.5f, 0.5f);
        ring1Rect.anchorMax = new Vector2(0.5f, 0.5f);
        ring1Rect.pivot = new Vector2(0.5f, 0.5f);
        ring1Rect.sizeDelta = new Vector2(106f, 106f);
        noAdsPingRing1 = ring1Obj.AddComponent<Image>();
        noAdsPingRing1.sprite = SpriteGenerator.Circle;
        noAdsPingRing1.color = new Color(1f, 0.82f, 0.08f, 0f);

        var ring2Obj = new GameObject("PingRing2");
        ring2Obj.transform.SetParent(noAdsButton.transform, false);
        ring2Obj.transform.SetSiblingIndex(1);
        var ring2Rect = ring2Obj.AddComponent<RectTransform>();
        ring2Rect.anchorMin = new Vector2(0.5f, 0.5f);
        ring2Rect.anchorMax = new Vector2(0.5f, 0.5f);
        ring2Rect.pivot = new Vector2(0.5f, 0.5f);
        ring2Rect.sizeDelta = new Vector2(106f, 106f);
        noAdsPingRing2 = ring2Obj.AddComponent<Image>();
        noAdsPingRing2.sprite = SpriteGenerator.Circle;
        noAdsPingRing2.color = new Color(1f, 0.82f, 0.08f, 0f);

        StartCoroutine(NoAdsGlowCoroutine());
    }

    private Sprite LoadIconSprite(string name)
    {
        return ResourceSpriteLoader.LoadSprite(name);
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
        c.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.55f);
        btn.colors = c;

        if (name == "Hint")
            hintButtonIcon = iconImg;
        if (name == "NoAds")
            noAdsButtonIcon = iconImg;

        return btn;
    }

    private Button CreateTopBarButton(string label, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var obj = new GameObject(label + "TopBarBtn");
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        var img = obj.AddComponent<Image>();
        img.color = color;

        var btn = obj.AddComponent<Button>();
        var c = btn.colors;
        c.highlightedColor = color * 0.9f;
        c.pressedColor = color * 0.78f;
        c.highlightedColor = new Color(c.highlightedColor.r, c.highlightedColor.g, c.highlightedColor.b, 1);
        c.pressedColor = new Color(c.pressedColor.r, c.pressedColor.g, c.pressedColor.b, 1);
        btn.colors = c;

        var txtObj = new GameObject("Text");
        txtObj.transform.SetParent(obj.transform, false);
        var textRect = txtObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var txt = txtObj.AddComponent<Text>();
        txt.font = defaultFont;
        txt.text = label;
        txt.fontSize = 30;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;

        return btn;
    }

    private Button CreateInvisibleButton(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var obj = new GameObject(name + "Btn");
        obj.transform.SetParent(parent, false);

        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1);
        rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        var img = obj.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f);

        var btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
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

    private void CreateLevelSelectPanel()
    {
        var panelObj = new GameObject("LevelSelectPanel");
        panelObj.transform.SetParent(canvas.transform, false);

        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var overlay = panelObj.AddComponent<Image>();
        overlay.color = new Color(0.16f, 0.15f, 0.18f, 0.42f);
        var overlayButton = panelObj.AddComponent<Button>();
        overlayButton.targetGraphic = overlay;
        overlayButton.onClick.AddListener(HideLevelSelect);

        var cardShadow = new GameObject("CardShadow");
        cardShadow.transform.SetParent(panelObj.transform, false);
        var shadowRect = cardShadow.AddComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.sizeDelta = new Vector2(900, 1296);
        shadowRect.anchoredPosition = new Vector2(0, -8);
        var shadowImage = cardShadow.AddComponent<Image>();
        shadowImage.sprite = SpriteGenerator.RoundedRect;
        shadowImage.color = new Color(0f, 0f, 0f, 0.12f);

        var card = new GameObject("Card");
        card.transform.SetParent(panelObj.transform, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(880, 1280);

        var cardImage = card.AddComponent<Image>();
        cardImage.sprite = SpriteGenerator.RoundedRect;
        cardImage.color = new Color(1f, 1f, 1f, 0.97f);

        var frame = new GameObject("LevelGridFrame");
        frame.transform.SetParent(card.transform, false);
        var frameRect = frame.AddComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0f, 0f);
        frameRect.anchorMax = new Vector2(1f, 1f);
        frameRect.offsetMin = new Vector2(52f, 52f);
        frameRect.offsetMax = new Vector2(-52f, -(52f + 176f)); // leave 176px (16 gap + 160 card) at top
        var frameImage = frame.AddComponent<Image>();
        frameImage.sprite = SpriteGenerator.RoundedRect;
        frameImage.color = new Color(0.97f, 0.96f, 0.95f, 0.95f);

        // Daily challenge card above the grid frame
        var dcCard = new GameObject("DailyCard");
        dcCard.transform.SetParent(card.transform, false);
        var dcRect = dcCard.AddComponent<RectTransform>();
        dcRect.anchorMin = new Vector2(0f, 1f);
        dcRect.anchorMax = new Vector2(1f, 1f);
        dcRect.pivot = new Vector2(0.5f, 1f);
        dcRect.offsetMin = new Vector2(52f, -(52f + 160f)); // 52 from sides, 160px tall
        dcRect.offsetMax = new Vector2(-52f, -52f);

        dailyChallengeCardImage = dcCard.AddComponent<Image>();
        dailyChallengeCardImage.sprite = SpriteGenerator.RoundedRect;
        dailyChallengeCardImage.color = new Color(0.25f, 0.78f, 0.72f, 0.9f);

        var dcBtn = dcCard.AddComponent<Button>();
        var dcColors = dcBtn.colors;
        dcColors.highlightedColor = new Color(0.8f, 0.97f, 0.96f, 1f);
        dcColors.pressedColor = new Color(0.6f, 0.9f, 0.88f, 1f);
        dcColors.disabledColor = new Color(0.7f, 0.82f, 0.80f, 0.7f);
        dcBtn.colors = dcColors;
        dcBtn.targetGraphic = dailyChallengeCardImage;
        dcBtn.onClick.AddListener(() => FindAnyObjectByType<GameManager>().PlayDailyChallenge());
        dailyChallengeCardButton = dcBtn;

        dailyChallengeCardMainText = MakeText("DailyMain", dcCard.transform, new Vector2(0, -44), 36, FontStyle.Bold, Color.white);
        dailyChallengeCardMainText.alignment = TextAnchor.MiddleCenter;
        dailyChallengeCardMainText.text = "📅 Daily Challenge";

        dailyChallengeCardSubText = MakeText("DailySub", dcCard.transform, new Vector2(0, -108), 28, FontStyle.Normal, new Color(1f, 1f, 1f, 0.88f));
        dailyChallengeCardSubText.alignment = TextAnchor.MiddleCenter;
        dailyChallengeCardSubText.text = "";

        var scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(frame.transform, false);
        var scrollRectTransform = scrollObj.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(28f, 28f);
        scrollRectTransform.offsetMax = new Vector2(-28f, -28f);

        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;
        levelSelectScrollRect = scrollRect;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        var viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        var viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.08f);
        var viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = true;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 1);
        contentRect.anchorMax = new Vector2(0.5f, 1);
        contentRect.pivot = new Vector2(0.5f, 1);

        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(150, 126);
        grid.spacing = new Vector2(14, 14);
        grid.padding = new RectOffset(14, 14, 14, 14);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        levelSelectButtons = new Button[LevelDatabase.TotalLevels];
        levelSelectButtonImages = new Image[LevelDatabase.TotalLevels];
        levelSelectButtonLabels = new Text[LevelDatabase.TotalLevels];

        for (int i = 0; i < LevelDatabase.TotalLevels; i++)
        {
            int levelIndex = i;
            var levelButtonObj = new GameObject($"Level{levelIndex + 1}Button");
            levelButtonObj.transform.SetParent(content.transform, false);

            var buttonRect = levelButtonObj.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(150, 126);

            var buttonShadowObj = new GameObject("Shadow");
            buttonShadowObj.transform.SetParent(levelButtonObj.transform, false);
            var buttonShadowRect = buttonShadowObj.AddComponent<RectTransform>();
            buttonShadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonShadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonShadowRect.pivot = new Vector2(0.5f, 0.5f);
            buttonShadowRect.sizeDelta = new Vector2(142, 118);
            buttonShadowRect.anchoredPosition = new Vector2(0, -3);
            var buttonShadowImage = buttonShadowObj.AddComponent<Image>();
            buttonShadowImage.sprite = SpriteGenerator.RoundedRect;
            buttonShadowImage.color = new Color(0f, 0f, 0f, 0.08f);

            var buttonImage = levelButtonObj.AddComponent<Image>();
            buttonImage.sprite = SpriteGenerator.RoundedRect;
            buttonImage.color = new Color(0.93f, 0.96f, 0.92f, 1f);

            var button = levelButtonObj.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.97f, 0.97f, 0.97f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.disabledColor = new Color(0.88f, 0.88f, 0.88f, 0.95f);
            button.colors = colors;
            button.onClick.AddListener(() => FindAnyObjectByType<GameManager>().SelectLevel(levelIndex));

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(levelButtonObj.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(124f, 50f);

            var label = labelObj.AddComponent<Text>();
            label.font = defaultFont;
            label.fontSize = 40;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = TextDark;
            label.text = (levelIndex + 1).ToString();

            levelSelectButtons[i] = button;
            levelSelectButtonImages[i] = buttonImage;
            levelSelectButtonLabels[i] = label;
        }

        levelSelectPanel = panelObj;
        levelSelectPanel.SetActive(false);
    }

    private void RefreshLevelSelectButtons(int currentLevelIndex, int highestUnlockedLevelIndex, int totalLevels)
    {
        for (int i = 0; i < levelSelectButtons.Length; i++)
        {
            bool exists = i < totalLevels;
            bool unlocked = exists && i <= highestUnlockedLevelIndex;
            bool isCurrent = i == currentLevelIndex;

            levelSelectButtons[i].gameObject.SetActive(exists);
            if (!exists)
                continue;

            levelSelectButtons[i].interactable = unlocked;
            levelSelectButtonLabels[i].text = (i + 1).ToString();
            levelSelectButtonLabels[i].color = unlocked ? TextDark : new Color(0.62f, 0.60f, 0.58f, 1f);

            if (isCurrent)
            {
                levelSelectButtonImages[i].color = new Color(0.74f, 0.90f, 0.86f, 1f);
                levelSelectButtonLabels[i].color = TextDark;
            }
            else if (unlocked)
            {
                levelSelectButtonImages[i].color = new Color(0.94f, 0.96f, 0.92f, 1f);
            }
            else
            {
                levelSelectButtonImages[i].color = new Color(0.92f, 0.90f, 0.89f, 1f);
            }
        }
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
        nextLevelButton.onClick.AddListener(() => FindAnyObjectByType<GameManager>().NextLevel());

        // Retry button
        retryButton = CreateCardButton("Retry", card.transform, new Vector2(0, -130), BtnCoral);
        retryButton.onClick.AddListener(() => FindAnyObjectByType<GameManager>().RetryLevel());

        levelCompletePanel = panelObj;
        levelCompletePanel.SetActive(false);
    }

    private void CreateTransitionOverlay()
    {
        transitionOverlay = new GameObject("LevelTransitionOverlay");
        transitionOverlay.transform.SetParent(canvas.transform, false);

        var overlayRect = transitionOverlay.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        transitionOverlayImage = transitionOverlay.AddComponent<Image>();
        transitionOverlayImage.color = new Color(TransitionBg.r, TransitionBg.g, TransitionBg.b, 0f);

        var squaresObj = new GameObject("Squares");
        squaresObj.transform.SetParent(transitionOverlay.transform, false);
        transitionSquaresRoot = squaresObj.AddComponent<RectTransform>();
        transitionSquaresRoot.anchorMin = Vector2.zero;
        transitionSquaresRoot.anchorMax = Vector2.one;
        transitionSquaresRoot.offsetMin = Vector2.zero;
        transitionSquaresRoot.offsetMax = Vector2.zero;

        const int squareColumns = 6;
        const int squareRows = 10;
        transitionSquares = new Image[squareColumns * squareRows];
        for (int i = 0; i < transitionSquares.Length; i++)
        {
            var squareObj = new GameObject($"Square{i}");
            squareObj.transform.SetParent(transitionSquaresRoot, false);
            var squareRect = squareObj.AddComponent<RectTransform>();
            squareRect.anchorMin = new Vector2(0.5f, 0.5f);
            squareRect.anchorMax = new Vector2(0.5f, 0.5f);
            squareRect.pivot = new Vector2(0.5f, 0.5f);

            var squareImage = squareObj.AddComponent<Image>();
            squareImage.color = GetTransitionSquareColor(i, 0f);
            squareRect.localScale = Vector3.zero;
            transitionSquares[i] = squareImage;
        }

        var textObj = new GameObject("LevelTransitionText");
        textObj.transform.SetParent(transitionOverlay.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(700, 100);
        textRect.anchoredPosition = new Vector2(0, 30);

        transitionOverlayText = textObj.AddComponent<Text>();
        transitionOverlayText.font = defaultFont;
        transitionOverlayText.fontSize = 54;
        transitionOverlayText.fontStyle = FontStyle.BoldAndItalic;
        transitionOverlayText.alignment = TextAnchor.MiddleCenter;
        transitionOverlayText.color = new Color(TextDark.r, TextDark.g, TextDark.b, 0f);
        transitionOverlayText.text = string.Empty;
        textObj.transform.SetAsLastSibling();

        transitionOverlay.SetActive(false);
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

    // --- Hint Promo Popup ---

    public void ShowHintPromoPopup(Action onWatchAd, Action onPurchase)
    {
        if (hintPromoPopup != null)
            Destroy(hintPromoPopup);

        hintPromoPopup = new GameObject("HintPromoPopup");
        hintPromoPopup.transform.SetParent(canvas.transform, false);
        hintPromoPopup.transform.SetAsLastSibling();

        var popupRect = hintPromoPopup.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;

        var overlay = hintPromoPopup.AddComponent<Image>();
        overlay.color = new Color(0.10f, 0.08f, 0.14f, 0.62f);
        var overlayBtn = hintPromoPopup.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        overlayBtn.onClick.AddListener(HideHintPromoPopup);

        // Shadow
        var shadowObj = new GameObject("Shadow");
        shadowObj.transform.SetParent(hintPromoPopup.transform, false);
        var shadowRect = shadowObj.AddComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.sizeDelta = new Vector2(726f, 506f);
        shadowRect.anchoredPosition = new Vector2(4f, -8f);
        var shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = SpriteGenerator.RoundedRect;
        shadowImg.color = new Color(0f, 0f, 0f, 0.18f);

        // Card
        var card = new GameObject("Card");
        card.transform.SetParent(hintPromoPopup.transform, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(700f, 480f);
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = SpriteGenerator.RoundedRect;
        cardImg.color = new Color(1f, 1f, 1f, 0.98f);

        // Title
        var title = MakeCardText("Title", card.transform, new Vector2(0, 155), 46, FontStyle.Bold, TextDark);
        title.text = "Want more hints?";

        // Subtitle
        var sub = MakeCardText("Subtitle", card.transform, new Vector2(0, 80), 30, FontStyle.Normal, TextMuted);
        sub.text = "Watch a short ad or remove ads forever";
        sub.GetComponent<RectTransform>().sizeDelta = new Vector2(560f, 60f);

        // Watch Ad button (teal)
        var watchBtn = CreateCardButton("Watch Ad", card.transform, new Vector2(0, -15), BtnTeal);
        watchBtn.onClick.AddListener(() =>
        {
            HideHintPromoPopup();
            onWatchAd?.Invoke();
        });

        // Remove Ads button (golden)
        var noAdsBuyBtn = CreateCardButton($"No Ads Forever  —  {noAdsPriceLabel}", card.transform, new Vector2(0, -110), new Color(0.92f, 0.68f, 0.08f));
        var noAdsBuyText = noAdsBuyBtn.GetComponentInChildren<Text>();
        if (noAdsBuyText != null) noAdsBuyText.fontSize = 28;
        noAdsBuyBtn.onClick.AddListener(() =>
        {
            HideHintPromoPopup();
            onPurchase?.Invoke();
        });
    }

    public void HideHintPromoPopup()
    {
        if (hintPromoPopup != null)
        {
            Destroy(hintPromoPopup);
            hintPromoPopup = null;
        }
    }

    // --- No Ads Purchase Popup ---

    private void ShowNoAdsPurchasePopup()
    {
        if (noAdsPurchasePopup != null)
            Destroy(noAdsPurchasePopup);

        bool hasPreviousPurchase = KeychainHelper.GetBool("noads.purchased");

        noAdsPurchasePopup = new GameObject("NoAdsPurchasePopup");
        noAdsPurchasePopup.transform.SetParent(canvas.transform, false);
        noAdsPurchasePopup.transform.SetAsLastSibling();

        var popupRect = noAdsPurchasePopup.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;

        var overlay = noAdsPurchasePopup.AddComponent<Image>();
        overlay.color = new Color(0.10f, 0.08f, 0.14f, 0.62f);
        var overlayBtn = noAdsPurchasePopup.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        overlayBtn.onClick.AddListener(() => { Destroy(noAdsPurchasePopup); noAdsPurchasePopup = null; });

        // Shadow
        var shadowObj = new GameObject("Shadow");
        shadowObj.transform.SetParent(noAdsPurchasePopup.transform, false);
        var shadowRect = shadowObj.AddComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.sizeDelta = new Vector2(726f, hasPreviousPurchase ? 360f : 330f);
        shadowRect.anchoredPosition = new Vector2(4f, -8f);
        var shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = SpriteGenerator.RoundedRect;
        shadowImg.color = new Color(0f, 0f, 0f, 0.18f);

        // Card
        var card = new GameObject("Card");
        card.transform.SetParent(noAdsPurchasePopup.transform, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(700f, hasPreviousPurchase ? 335f : 300f);
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = SpriteGenerator.RoundedRect;
        cardImg.color = new Color(1f, 1f, 1f, 0.98f);

        if (hasPreviousPurchase)
        {
            // Returning user who reinstalled — show restore only
            var title = MakeCardText("Title", card.transform, new Vector2(0, 110), 42, FontStyle.Bold, TextDark);
            title.text = "Welcome Back!";

            var sub = MakeCardText("Subtitle", card.transform, new Vector2(0, 45), 28, FontStyle.Normal, TextMuted);
            sub.text = $"You previously purchased\nRemove Ads for {noAdsPriceLabel}";
            sub.GetComponent<RectTransform>().sizeDelta = new Vector2(580f, 80f);
            sub.alignment = TextAnchor.MiddleCenter;
            sub.lineSpacing = 1.2f;

            var restoreBtn = CreateCardButton("Restore My Purchase", card.transform, new Vector2(0, -85), new Color(0.92f, 0.68f, 0.08f));
            var restoreText = restoreBtn.GetComponentInChildren<Text>();
            if (restoreText != null) restoreText.fontSize = 28;
            restoreBtn.onClick.AddListener(() =>
            {
                Destroy(noAdsPurchasePopup);
                noAdsPurchasePopup = null;
                FindAnyObjectByType<GameManager>().RestoreNoAdsPurchases();
            });
        }
        else
        {
            // New user — show buy only
            var title = MakeCardText("Title", card.transform, new Vector2(0, 85), 46, FontStyle.Bold, TextDark);
            title.text = "Remove Ads";

            var sub = MakeCardText("Subtitle", card.transform, new Vector2(0, 15), 30, FontStyle.Normal, TextMuted);
            sub.text = "Enjoy the full game, ad-free forever";
            sub.GetComponent<RectTransform>().sizeDelta = new Vector2(560f, 60f);

            var buyBtn = CreateCardButton($"Remove Ads — {noAdsPriceLabel}", card.transform, new Vector2(0, -80), new Color(0.92f, 0.68f, 0.08f));
            var buyText = buyBtn.GetComponentInChildren<Text>();
            if (buyText != null) buyText.fontSize = 28;
            buyBtn.onClick.AddListener(() =>
            {
                Destroy(noAdsPurchasePopup);
                noAdsPurchasePopup = null;
                FindAnyObjectByType<GameManager>().PurchaseNoAds();
            });
        }
    }

    // --- Store Unavailable Popup ---

    public void ShowStoreUnavailablePopup(string errorDetail = null)
    {
        var popup = new GameObject("StoreUnavailablePopup");
        popup.transform.SetParent(canvas.transform, false);
        popup.transform.SetAsLastSibling();

        var popupRect = popup.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;

        var overlay = popup.AddComponent<Image>();
        overlay.color = new Color(0.10f, 0.08f, 0.14f, 0.62f);
        var overlayBtn = popup.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        overlayBtn.onClick.AddListener(() => Destroy(popup));

        var cardObj = new GameObject("Card");
        cardObj.transform.SetParent(popup.transform, false);
        var cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(620f, string.IsNullOrEmpty(errorDetail) ? 260f : 320f);
        var cardImg = cardObj.AddComponent<Image>();
        cardImg.sprite = SpriteGenerator.RoundedRect;
        cardImg.color = new Color(0.13f, 0.10f, 0.20f, 1f);

        var msgObj = new GameObject("Message");
        msgObj.transform.SetParent(cardObj.transform, false);
        var msgRect = msgObj.AddComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0f, 0.35f);
        msgRect.anchorMax = new Vector2(1f, 1f);
        msgRect.offsetMin = new Vector2(30f, 0f);
        msgRect.offsetMax = new Vector2(-30f, -20f);
        var msgTxt = msgObj.AddComponent<Text>();
        msgTxt.font = defaultFont;
        string mainMsg = "Store is temporarily unavailable.\nPlease check your internet connection\nand try again.";
        msgTxt.text = string.IsNullOrEmpty(errorDetail) ? mainMsg : mainMsg + "\n\n<color=#ff9944>(" + errorDetail + ")</color>";
        msgTxt.fontSize = 28;
        msgTxt.alignment = TextAnchor.MiddleCenter;
        msgTxt.color = new Color(0.90f, 0.88f, 0.95f, 1f);
        msgTxt.supportRichText = true;

        var closeBtn = CreateCardButton("OK", cardObj.transform, new Vector2(0, -70), new Color(0.38f, 0.32f, 0.58f));
        closeBtn.onClick.AddListener(() => Destroy(popup));
    }

    public void ShowRestoreResultPopup(bool success, string errorMsg = null)
    {
        var popup = new GameObject("RestoreResultPopup");
        popup.transform.SetParent(canvas.transform, false);
        popup.transform.SetAsLastSibling();

        var popupRect = popup.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;

        var overlay = popup.AddComponent<Image>();
        overlay.color = new Color(0.10f, 0.08f, 0.14f, 0.62f);
        var overlayBtn = popup.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        overlayBtn.onClick.AddListener(() => Destroy(popup));

        var cardObj = new GameObject("Card");
        cardObj.transform.SetParent(popup.transform, false);
        var cardRect = cardObj.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(620f, 240f);
        var cardImg = cardObj.AddComponent<Image>();
        cardImg.sprite = SpriteGenerator.RoundedRect;
        cardImg.color = new Color(0.13f, 0.10f, 0.20f, 1f);

        var msgObj = new GameObject("Message");
        msgObj.transform.SetParent(cardObj.transform, false);
        var msgRect = msgObj.AddComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0f, 0.35f);
        msgRect.anchorMax = new Vector2(1f, 1f);
        msgRect.offsetMin = new Vector2(30f, 0f);
        msgRect.offsetMax = new Vector2(-30f, -20f);
        var msgTxt = msgObj.AddComponent<Text>();
        msgTxt.font = defaultFont;
        msgTxt.text = success
            ? "Purchases restored successfully!"
            : "No previous purchase found.\n" + (string.IsNullOrEmpty(errorMsg) ? "" : "(" + errorMsg + ")");
        msgTxt.fontSize = 30;
        msgTxt.alignment = TextAnchor.MiddleCenter;
        msgTxt.color = new Color(0.90f, 0.88f, 0.95f, 1f);

        var closeBtn = CreateCardButton("OK", cardObj.transform, new Vector2(0, -70), new Color(0.38f, 0.32f, 0.58f));
        closeBtn.onClick.AddListener(() => Destroy(popup));
    }

    // --- Promo Top Banner ---

    public void ShowPromoTopBanner()
    {
        if (promoTopBanner == null)
            promoTopBanner = CreatePromoTopBanner();

        if (promoTopBanner.activeSelf) return;

        if (bannerCoroutine != null)
            StopCoroutine(bannerCoroutine);
        bannerCoroutine = StartCoroutine(PromoTopBannerCoroutine());
    }

    private GameObject CreatePromoTopBanner()
    {
        // Calculate top safe area inset in canvas units so content clears the notch/Dynamic Island
        float canvasH = ((RectTransform)canvas.transform).rect.height;
        float safeTopFraction = Mathf.Max(0f, (Screen.height - Screen.safeArea.yMax) / (float)Screen.height);
        float topPad = canvasH * safeTopFraction;
        float contentH = 110f;
        float bannerH = contentH + topPad;

        var bannerObj = new GameObject("PromoTopBanner");
        bannerObj.transform.SetParent(canvas.transform, false);

        var bannerRect = bannerObj.AddComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0f, 1f);
        bannerRect.anchorMax = new Vector2(1f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.sizeDelta = new Vector2(0f, bannerH);
        bannerRect.anchoredPosition = new Vector2(0f, bannerH);

        var bgImg = bannerObj.AddComponent<Image>();
        bgImg.color = new Color(0.22f, 0.14f, 0.42f, 0.97f);

        var tapBtn = bannerObj.AddComponent<Button>();
        tapBtn.targetGraphic = bgImg;
        tapBtn.onClick.AddListener(() =>
        {
            if (bannerCoroutine != null) StopCoroutine(bannerCoroutine);
            promoTopBanner.SetActive(false);
            FindAnyObjectByType<GameManager>().PurchaseNoAds();
        });

        // Content row sits at the very bottom of the banner — always below the notch/island
        var rowObj = new GameObject("ContentRow");
        rowObj.transform.SetParent(bannerObj.transform, false);
        var rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 0f);
        rowRect.pivot = new Vector2(0.5f, 0f);
        rowRect.sizeDelta = new Vector2(0f, contentH);
        rowRect.anchoredPosition = Vector2.zero;

        // Star icon
        var starObj = new GameObject("Star");
        starObj.transform.SetParent(rowObj.transform, false);
        var starRect = starObj.AddComponent<RectTransform>();
        starRect.anchorMin = new Vector2(0f, 0f);
        starRect.anchorMax = new Vector2(0f, 1f);
        starRect.pivot = new Vector2(0f, 0.5f);
        starRect.anchoredPosition = new Vector2(16f, 0f);
        starRect.sizeDelta = new Vector2(70f, 0f);
        var starTxt = starObj.AddComponent<Text>();
        starTxt.font = defaultFont;
        starTxt.text = "★";
        starTxt.fontSize = 42;
        starTxt.alignment = TextAnchor.MiddleCenter;
        starTxt.color = new Color(1f, 0.85f, 0.2f);

        // Message text
        var msgObj = new GameObject("Message");
        msgObj.transform.SetParent(rowObj.transform, false);
        var msgRect = msgObj.AddComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0f, 0f);
        msgRect.anchorMax = new Vector2(0.80f, 1f);
        msgRect.offsetMin = new Vector2(96f, 0f);
        msgRect.offsetMax = new Vector2(0f, 0f);
        var msgTxt = msgObj.AddComponent<Text>();
        msgTxt.font = defaultFont;
        msgTxt.text = $"Tired of Ads?  Get Ad Free for {noAdsPriceLabel}";
        msgTxt.fontSize = 30;
        msgTxt.fontStyle = FontStyle.Bold;
        msgTxt.alignment = TextAnchor.MiddleLeft;
        msgTxt.color = Color.white;

        bannerObj.SetActive(false);
        return bannerObj;
    }

    private IEnumerator NoAdsGlowCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(25f, 35f));

            if (noAdsPingRing1 == null || noAdsButton == null || !noAdsButton.gameObject.activeSelf)
                continue;

            // Two staggered ping rings expand outward
            StartCoroutine(PingRingRoutine(noAdsPingRing1, 0f));
            StartCoroutine(PingRingRoutine(noAdsPingRing2, 0.22f));

            // Button scale bounce
            const float bounceDuration = 0.35f;
            float bt = 0f;
            while (bt < bounceDuration)
            {
                bt += Time.deltaTime;
                float s = 1f + 0.08f * Mathf.Sin((bt / bounceDuration) * Mathf.PI);
                noAdsButton.transform.localScale = Vector3.one * s;
                yield return null;
            }
            noAdsButton.transform.localScale = Vector3.one;
        }
    }

    private IEnumerator PingRingRoutine(Image ring, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (ring == null) yield break;

        const float duration = 0.65f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float ease = 1f - (1f - p) * (1f - p); // ease-out quad
            ring.rectTransform.localScale = Vector3.one * (1f + ease * 1.4f);
            ring.color = new Color(1f, 0.82f, 0.08f, (1f - p) * 0.55f);
            yield return null;
        }
        ring.color = new Color(1f, 0.82f, 0.08f, 0f);
        ring.rectTransform.localScale = Vector3.one;
    }

    private IEnumerator PromoTopBannerCoroutine()
    {
        if (promoTopBanner == null) yield break;

        var bannerRect = promoTopBanner.GetComponent<RectTransform>();
        float bannerH = bannerRect.sizeDelta.y;

        // Reset to off-screen before activating to prevent one-frame flash
        bannerRect.anchoredPosition = new Vector2(0f, bannerH);
        promoTopBanner.SetActive(true);
        promoTopBanner.transform.SetAsLastSibling();

        const float slideDuration = 0.38f;

        // Slide in from above
        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / slideDuration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            bannerRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(bannerH, 0f, eased));
            yield return null;
        }
        bannerRect.anchoredPosition = new Vector2(0f, 0f);

        yield return new WaitForSeconds(4.6f);

        if (promoTopBanner == null) yield break;

        // Slide out
        t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / slideDuration);
            float eased = p * p * p;
            bannerRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, bannerH, eased));
            yield return null;
        }
        if (promoTopBanner != null)
            promoTopBanner.SetActive(false);
    }

    // --- Public API ---

    public void SetLevelInfo(string name, int index, int total)
    {
        if (levelProgressText != null) levelProgressText.text = $"Level {index + 1} / {total}";
    }

    public void UpdateStreakDisplay(int streak)
    {
        // Streak is shown only inside the daily challenge card in level select.
        // Top-bar streak label is intentionally kept hidden.
    }

    public void UpdateDailyChallengeCard()
    {
        if (dailyChallengeCardImage == null) return;

        bool completed = DailyChallengeManager.IsTodayCompleted();
        int dailyIndex = DailyChallengeManager.GetDailyLevelIndex();
        string levelName = (dailyIndex >= 0 && dailyIndex < LevelDatabase.Levels.Length)
            ? LevelDatabase.Levels[dailyIndex].levelName
            : $"Level {dailyIndex + 1}";
        int streak = DailyChallengeManager.GetStreak();

        dailyChallengeCardImage.color = completed
            ? new Color(0.30f, 0.75f, 0.48f, 0.9f) // green = done
            : new Color(0.25f, 0.78f, 0.72f, 0.9f); // teal = available

        string streakSuffix = streak > 0 ? $"  🔥 {streak}" : "";
        dailyChallengeCardMainText.text = completed ? $"✓ Daily Complete!{streakSuffix}" : "📅 Daily Challenge";
        dailyChallengeCardSubText.text = completed ? "Come back tomorrow!" : levelName;

        if (dailyChallengeCardButton != null)
            dailyChallengeCardButton.interactable = !completed;
    }

    public void ShowLevelComplete()
    {
        levelCompletePanel.SetActive(true);
    }

    public void HideLevelComplete()
    {
        levelCompletePanel.SetActive(false);
    }

    private void ShowSettingsPopup()
    {
        var popup = new GameObject("SettingsPopup");
        popup.transform.SetParent(canvas.transform, false);
        popup.transform.SetAsLastSibling();

        var popupRect = popup.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;

        var overlay = popup.AddComponent<Image>();
        overlay.color = new Color(0.10f, 0.08f, 0.14f, 0.62f);
        var overlayBtn = popup.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        overlayBtn.onClick.AddListener(() => Destroy(popup));

        // Shadow
        var shadowObj = new GameObject("Shadow");
        shadowObj.transform.SetParent(popup.transform, false);
        var shadowRect = shadowObj.AddComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.sizeDelta = new Vector2(666f, 596f);
        shadowRect.anchoredPosition = new Vector2(4f, -8f);
        var shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = SpriteGenerator.RoundedRect;
        shadowImg.color = new Color(0f, 0f, 0f, 0.18f);

        // Card
        var card = new GameObject("Card");
        card.transform.SetParent(popup.transform, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(640f, 570f);
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = SpriteGenerator.RoundedRect;
        cardImg.color = new Color(1f, 1f, 1f, 0.98f);

        // Title
        var title = MakeCardText("Title", card.transform, new Vector2(0, 222), 44, FontStyle.Bold, TextDark);
        title.text = "⚙️  Settings";

        // ─── Sound toggle row ───────────────────────────────────────────────
        bool soundOn = !(AudioManager.Instance?.IsMuted ?? false);
        CreateToggleRow(card.transform, "🔊  Ses", soundOn, new Vector2(0, 130), (val) =>
        {
            AudioManager.Instance?.SetMuted(!val);
        });

        // ─── Haptic toggle row ──────────────────────────────────────────────
        bool hapticOn = HapticManager.Instance?.IsHapticsEnabled ?? true;
        CreateToggleRow(card.transform, "📳  Haptics", hapticOn, new Vector2(0, 30), (val) =>
        {
            if (HapticManager.Instance != null) HapticManager.Instance.IsHapticsEnabled = val;
        });

        // Divider
        var divObj = new GameObject("Divider");
        divObj.transform.SetParent(card.transform, false);
        var divRect = divObj.AddComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0.5f, 0.5f);
        divRect.anchorMax = new Vector2(0.5f, 0.5f);
        divRect.pivot = new Vector2(0.5f, 0.5f);
        divRect.anchoredPosition = new Vector2(0, -40f);
        divRect.sizeDelta = new Vector2(560f, 2f);
        var divImg = divObj.AddComponent<Image>();
        divImg.color = new Color(0.85f, 0.83f, 0.78f, 1f);

        // Achievements button
        var achBtn = CreateCardButton("🏆  Achievements", card.transform, new Vector2(0, -100f), BtnTeal);
        achBtn.onClick.AddListener(() =>
        {
            Destroy(popup);
            GameCenterManager.Instance?.ShowAchievements();
        });

        // Leaderboard button
        var lbBtn = CreateCardButton("📊  Leaderboard", card.transform, new Vector2(0, -195f), new Color(0.38f, 0.32f, 0.58f));
        lbBtn.onClick.AddListener(() =>
        {
            Destroy(popup);
            GameCenterManager.Instance?.ShowLeaderboard();
        });
    }

    // Creates a labeled toggle row (label on left, ON/OFF button on right)
    private void CreateToggleRow(Transform parent, string label, bool initialState, Vector2 position, System.Action<bool> onChange)
    {
        const float rowW = 560f, rowH = 74f;

        var row = new GameObject("ToggleRow_" + label);
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = position;
        rowRect.sizeDelta = new Vector2(rowW, rowH);

        // Row background
        var rowBg = row.AddComponent<Image>();
        rowBg.sprite = SpriteGenerator.RoundedRect;
        rowBg.color = new Color(0.96f, 0.94f, 0.90f, 1f);

        // Label
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(row.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(20f, 0f);
        labelRect.offsetMax = new Vector2(-110f, 0f);
        var labelTxt = labelObj.AddComponent<Text>();
        labelTxt.font = defaultFont;
        labelTxt.text = label;
        labelTxt.fontSize = 34;
        labelTxt.alignment = TextAnchor.MiddleLeft;
        labelTxt.color = TextDark;

        // Toggle button
        var togObj = new GameObject("Toggle");
        togObj.transform.SetParent(row.transform, false);
        var togRect = togObj.AddComponent<RectTransform>();
        togRect.anchorMin = new Vector2(1f, 0.5f);
        togRect.anchorMax = new Vector2(1f, 0.5f);
        togRect.pivot = new Vector2(1f, 0.5f);
        togRect.anchoredPosition = new Vector2(-12f, 0f);
        togRect.sizeDelta = new Vector2(86f, 50f);

        var togImg = togObj.AddComponent<Image>();
        togImg.sprite = SpriteGenerator.RoundedRect;
        togImg.color = initialState ? BtnTeal : new Color(0.72f, 0.70f, 0.65f);

        var togLabelObj = new GameObject("TogLabel");
        togLabelObj.transform.SetParent(togObj.transform, false);
        var tlRect = togLabelObj.AddComponent<RectTransform>();
        tlRect.anchorMin = Vector2.zero;
        tlRect.anchorMax = Vector2.one;
        tlRect.offsetMin = Vector2.zero;
        tlRect.offsetMax = Vector2.zero;
        var togTxt = togLabelObj.AddComponent<Text>();
        togTxt.font = defaultFont;
        togTxt.text = initialState ? "ON" : "OFF";
        togTxt.fontSize = 26;
        togTxt.fontStyle = FontStyle.Bold;
        togTxt.alignment = TextAnchor.MiddleCenter;
        togTxt.color = Color.white;

        bool state = initialState;
        var togBtn = togObj.AddComponent<Button>();
        togBtn.targetGraphic = togImg;
        togBtn.onClick.AddListener(() =>
        {
            state = !state;
            togImg.color = state ? BtnTeal : new Color(0.72f, 0.70f, 0.65f);
            togTxt.text = state ? "ON" : "OFF";
            onChange(state);
        });
    }

    private void ShowGameCenterMenu()
    {
        ShowSettingsPopup();
    }


    public bool IsLevelSelectVisible => levelSelectPanel != null && levelSelectPanel.activeSelf;

    public void ShowLevelSelect(int currentLevelIndex, int highestUnlockedLevelIndex, int totalLevels)
    {
        if (levelSelectPanel == null)
            return;

        RefreshLevelSelectButtons(currentLevelIndex, highestUnlockedLevelIndex, totalLevels);
        levelSelectPanel.SetActive(true);

        if (levelSelectScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            int totalRows = Mathf.CeilToInt(totalLevels / 4f);
            int currentRow = Mathf.Clamp(currentLevelIndex / 4, 0, Mathf.Max(0, totalRows - 1));
            float scrollPosition = totalRows <= 1 ? 1f : 1f - ((float)currentRow / (totalRows - 1));
            levelSelectScrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollPosition);
        }
    }

    public void HideLevelSelect()
    {
        if (levelSelectPanel != null)
            levelSelectPanel.SetActive(false);
    }

    public IEnumerator PlayLevelTransition(string title, Action onMidTransition)
    {
        if (transitionOverlay == null || transitionOverlayImage == null || transitionOverlayText == null || transitionSquaresRoot == null || transitionSquares == null)
        {
            onMidTransition?.Invoke();
            yield break;
        }

        transitionOverlayText.text = title;
        transitionOverlay.SetActive(true);
        transitionOverlay.transform.SetAsLastSibling();
        transitionOverlayText.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        LayoutTransitionSquares();

        RectTransform textRect = transitionOverlayText.rectTransform;
        Vector3 startScale = Vector3.one * 0.92f;
        Vector3 endScale = Vector3.one;
        textRect.localScale = startScale;

        const float coverDuration = 0.42f;
        const float revealDuration = 0.46f;
        const float holdDuration = 0.05f;
        const float squareDelay = 0.022f;
        const int squareColumns = 6;
        const int squareRows = 10;
        float maxDelay = (squareColumns + squareRows - 2) * squareDelay;

        float elapsed = 0f;
        while (elapsed < coverDuration + maxDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            float overlayProgress = Mathf.Clamp01(elapsed / (coverDuration + maxDelay * 0.5f));
            transitionOverlayImage.color = new Color(TransitionBg.r, TransitionBg.g, TransitionBg.b, Mathf.Lerp(0f, 0.34f, Mathf.SmoothStep(0f, 1f, overlayProgress)));
            transitionOverlayText.color = new Color(TextDark.r, TextDark.g, TextDark.b, Mathf.Lerp(0f, 1f, Mathf.SmoothStep(0f, 1f, overlayProgress)));
            textRect.localScale = Vector3.Lerp(startScale, endScale, EaseOutBack(Mathf.Clamp01(elapsed / (coverDuration + maxDelay * 0.35f))));

            for (int i = 0; i < transitionSquares.Length; i++)
            {
                int row = i / squareColumns;
                int col = i % squareColumns;
                float delay = (col + row) * squareDelay;
                float squareProgress = Mathf.Clamp01((elapsed - delay) / coverDuration);
                float eased = EaseOutBack(squareProgress);
                transitionSquares[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0f, 1.08f, eased);
                transitionSquares[i].color = GetTransitionSquareColor(i, Mathf.Lerp(0f, 1f, Mathf.SmoothStep(0f, 1f, squareProgress)));
            }
            yield return null;
        }

        yield return new WaitForSecondsRealtime(holdDuration);
        onMidTransition?.Invoke();
        yield return null;

        elapsed = 0f;
        while (elapsed < revealDuration + maxDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            float overlayProgress = Mathf.Clamp01(elapsed / (revealDuration + maxDelay * 0.7f));
            transitionOverlayImage.color = new Color(TransitionBg.r, TransitionBg.g, TransitionBg.b, Mathf.Lerp(0.34f, 0f, Mathf.SmoothStep(0f, 1f, overlayProgress)));
            transitionOverlayText.color = new Color(TextDark.r, TextDark.g, TextDark.b, Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, overlayProgress)));
            textRect.localScale = Vector3.Lerp(endScale, Vector3.one * 1.04f, Mathf.Clamp01(elapsed / (revealDuration + maxDelay * 0.4f)));

            for (int i = 0; i < transitionSquares.Length; i++)
            {
                int row = i / squareColumns;
                int col = i % squareColumns;
                float delay = ((squareColumns - 1 - col) + row) * squareDelay;
                float squareProgress = Mathf.Clamp01((elapsed - delay) / revealDuration);
                float eased = Mathf.SmoothStep(0f, 1f, squareProgress);
                transitionSquares[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.08f, 0f, eased);
                transitionSquares[i].color = GetTransitionSquareColor(i, Mathf.Lerp(1f, 0f, eased));
            }
            yield return null;
        }

        transitionOverlayImage.color = new Color(TransitionBg.r, TransitionBg.g, TransitionBg.b, 0f);
        transitionOverlayText.color = new Color(TextDark.r, TextDark.g, TextDark.b, 0f);
        textRect.localScale = Vector3.one;
        for (int i = 0; i < transitionSquares.Length; i++)
        {
            transitionSquares[i].rectTransform.localScale = Vector3.zero;
            transitionSquares[i].color = GetTransitionSquareColor(i, 0f);
        }
        transitionOverlay.SetActive(false);
    }

    private void LayoutTransitionSquares()
    {
        if (transitionSquaresRoot == null || transitionSquares == null)
            return;

        const int squareColumns = 6;
        const int squareRows = 10;
        Rect rect = transitionSquaresRoot.rect;
        float stepX = rect.width / squareColumns;
        float stepY = rect.height / squareRows;
        float squareSize = Mathf.Max(stepX, stepY) + 24f;

        for (int i = 0; i < transitionSquares.Length; i++)
        {
            int row = i / squareColumns;
            int col = i % squareColumns;
            RectTransform squareRect = transitionSquares[i].rectTransform;
            squareRect.sizeDelta = Vector2.one * squareSize;
            squareRect.anchoredPosition = new Vector2(
                -rect.width * 0.5f + stepX * (col + 0.5f),
                rect.height * 0.5f - stepY * (row + 0.5f));
        }
    }

    private static Color GetTransitionSquareColor(int index, float alpha)
    {
        Color baseColor;
        switch (index % 3)
        {
            case 0:
                baseColor = TransitionSquareA;
                break;
            case 1:
                baseColor = TransitionSquareB;
                break;
            default:
                baseColor = TransitionSquareC;
                break;
        }

        baseColor.a *= alpha;
        return baseColor;
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t = value - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    public void SetNoAdsState(bool isAvailable, bool isPurchased, string buttonLabel)
    {
        if (noAdsButton == null)
            return;

        // Extract price from "No Ads\n$4.99" label format
        if (!string.IsNullOrEmpty(buttonLabel))
        {
            var parts = buttonLabel.Split('\n');
            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                noAdsPriceLabel = parts[1];
        }

        noAdsButton.gameObject.SetActive(!isPurchased);
        noAdsButton.interactable = isAvailable && !isPurchased;
    }

    public void SetHintAvailable(bool isAvailable)
    {
        if (hintButton == null)
            return;

        hintButton.interactable = isAvailable;
        if (hintButtonIcon != null)
            hintButtonIcon.color = isAvailable ? Color.white : new Color(0.72f, 0.72f, 0.72f, 0.7f);
    }

    /// <summary>
    /// Disables all interactive buttons during tutorial so accidental taps don't trigger actions.
    /// </summary>
    public void SetTutorialMode(bool isTutorial)
    {
        if (hintButton != null)           hintButton.interactable           = !isTutorial;
        if (noAdsButton != null)          noAdsButton.interactable          = !isTutorial;
        if (restartButton != null)        restartButton.interactable        = !isTutorial;
        if (levelSelectToggleButton != null) levelSelectToggleButton.interactable = !isTutorial;
    }
}
