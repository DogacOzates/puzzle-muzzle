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
    private GameObject ratePopup;
    private GameObject hintFreeBadgeObj;
    private Text hintFreeBadgeText;
    private Image hintButtonIcon;
    private Image noAdsButtonIcon;
    private Image restartButtonIcon;
    private Image noAdsPingRing1;
    private Image noAdsPingRing2;
    // (basePath, Image) pairs for icons that need dark/light sprite swap
    private System.Collections.Generic.List<(string basePath, Image img)> themedIcons
        = new System.Collections.Generic.List<(string, Image)>();
    private GameObject hintPromoPopup;
    private GameObject promoTopBanner;
    private Coroutine bannerCoroutine;
    private string noAdsPriceLabel = "$4.99";
    private GameObject levelSelectPanel;
    private ScrollRect levelSelectScrollRect;
    private Button[] levelSelectButtons;
    private Image[] levelSelectButtonImages;
    private Text[] levelSelectButtonLabels;
    private int _lsCurrentIdx;
    private int _lsHighestUnlocked = -1;
    private int _lsTotalLevels;
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

    // Theme-reactive UI refs
    private Image levelSelectCardBg;
    private Image levelSelectFrameBg;
    private Image settingsButtonBg;
    private Image settingsIconImg;
    private Text levelProgressTextRef;  // alias — same as levelProgressText

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

        ThemeManager.OnThemeChanged += OnThemeChanged;
    }

    private void OnDestroy()
    {
        ThemeManager.OnThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged()
    {
        var tm = ThemeManager.Instance;
        if (tm == null) return;

        // Settings button bg (transparent) + themed icon sprite swap
        if (settingsButtonBg != null)
            settingsButtonBg.color = Color.clear;

        // Swap all tracked icon sprites to dark/light variant
        bool dark = tm.IsDarkMode;
        foreach (var (basePath, img) in themedIcons)
        {
            if (img == null) continue;
            string path = dark ? basePath + "_white" : basePath;
            var s = LoadIconSprite(path) ?? LoadIconSprite(basePath);
            if (s != null) img.sprite = s;
        }

        // Level progress text
        if (levelProgressText != null)
            levelProgressText.color = tm.TextPrimary;

        // Level select card & frame
        if (levelSelectCardBg != null)
            levelSelectCardBg.color = tm.CardBg;
        if (levelSelectFrameBg != null)
            levelSelectFrameBg.color = tm.LevelSelectFrame;

        // Level select buttons — re-apply section-specific colors using stored state
        if (levelSelectButtonImages != null && _lsTotalLevels > 0)
            RefreshLevelSelectButtons(_lsCurrentIdx, _lsHighestUnlocked, _lsTotalLevels);

        // Transition overlay
        if (transitionOverlayImage != null)
            transitionOverlayImage.color = tm.TransitionBg;
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
        settingsRect.pivot = new Vector2(0.5f, 0.5f);
        settingsRect.anchoredPosition = new Vector2(-100f, -70f);
        settingsRect.sizeDelta = new Vector2(90f, 90f);

        var settingsImg = settingsObj.AddComponent<Image>();
        settingsImg.sprite = SpriteGenerator.RoundedRect;
        settingsImg.color = Color.clear;
        settingsButtonBg = settingsImg;

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
        bool isDark = ThemeManager.Instance?.IsDarkMode ?? false;
        string settingsPath = isDark ? "icons/settings_white" : "icons/settings";
        var settingsSprite = Resources.Load<Sprite>(settingsPath)
                          ?? Resources.Load<Sprite>("icons/settings");
        if (settingsSprite != null)
        {
            siImg.sprite = settingsSprite;
            settingsIconImg = siImg;
            themedIcons.Add(("icons/settings", siImg));
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
            fbTxt.color = isDark ? Color.white : TextDark;
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

        // Free hint badge (top-right corner of hint button)
        hintFreeBadgeObj = new GameObject("FreeBadge");
        hintFreeBadgeObj.transform.SetParent(hintButton.transform, false);
        var badgeRect = hintFreeBadgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(1f, 1f);
        badgeRect.anchoredPosition = new Vector2(10f, 10f);
        badgeRect.sizeDelta = new Vector2(34f, 34f);
        var badgeImg = hintFreeBadgeObj.AddComponent<Image>();
        badgeImg.sprite = SpriteGenerator.Circle;
        badgeImg.color = new Color(0.95f, 0.35f, 0.25f);
        hintFreeBadgeText = MakeText("Count", hintFreeBadgeObj.transform, Vector2.zero, 20, FontStyle.Bold, Color.white);
        hintFreeBadgeText.alignment = TextAnchor.MiddleCenter;
        hintFreeBadgeObj.SetActive(false);

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
        bool darkNow = ThemeManager.Instance?.IsDarkMode ?? false;
        string spritePath = darkNow ? iconPath + "_white" : iconPath;
        var sprite = LoadIconSprite(spritePath) ?? LoadIconSprite(iconPath);
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
        if (name == "Restart")
            restartButtonIcon = iconImg;
        if (name == "NoAds")
            noAdsButtonIcon = iconImg;

        themedIcons.Add((iconPath, iconImg));

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

        // Card shadow
        var cardShadow = new GameObject("CardShadow");
        cardShadow.transform.SetParent(panelObj.transform, false);
        var shadowRect = cardShadow.AddComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.sizeDelta = new Vector2(900, 1316);
        shadowRect.anchoredPosition = new Vector2(0, -8);
        var shadowImage = cardShadow.AddComponent<Image>();
        shadowImage.sprite = SpriteGenerator.RoundedRect;
        shadowImage.color = new Color(0f, 0f, 0f, 0.12f);

        // Main card (880 × 1300)
        var card = new GameObject("Card");
        card.transform.SetParent(panelObj.transform, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(880, 1300);
        var cardImage = card.AddComponent<Image>();
        cardImage.sprite = SpriteGenerator.RoundedRect;
        cardImage.color = ThemeManager.Instance != null ? ThemeManager.Instance.CardBg : new Color(1f, 1f, 1f, 0.97f);
        levelSelectCardBg = cardImage;

        // "LEVELS" title
        var titleObj = new GameObject("LevelsTitle");
        titleObj.transform.SetParent(card.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(52f, -84f);
        titleRect.offsetMax = new Vector2(-52f, -16f);
        var titleText = titleObj.AddComponent<Text>();
        titleText.font = defaultFont;
        titleText.fontSize = 44;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = TextDark;
        titleText.text = "LEVELS";

        // Daily challenge card
        var dcCard = new GameObject("DailyCard");
        dcCard.transform.SetParent(card.transform, false);
        var dcRect = dcCard.AddComponent<RectTransform>();
        dcRect.anchorMin = new Vector2(0f, 1f);
        dcRect.anchorMax = new Vector2(1f, 1f);
        dcRect.pivot = new Vector2(0.5f, 1f);
        dcRect.offsetMax = new Vector2(-52f, -(16f + 68f));       // 16 top pad + 68 title
        dcRect.offsetMin = new Vector2(52f, -(16f + 68f + 160f)); // 160px tall
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

        // Scrollable level grid frame (below title + daily card)
        // Top offset: 16 pad + 68 title + 160 daily card + 12 gap = 256
        var frame = new GameObject("LevelGridFrame");
        frame.transform.SetParent(card.transform, false);
        var frameRect = frame.AddComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0f, 0f);
        frameRect.anchorMax = new Vector2(1f, 1f);
        frameRect.offsetMin = new Vector2(40f, 40f);
        frameRect.offsetMax = new Vector2(-40f, -(16f + 68f + 160f + 12f));
        var frameImage = frame.AddComponent<Image>();
        frameImage.sprite = SpriteGenerator.RoundedRect;
        frameImage.color = ThemeManager.Instance != null ? ThemeManager.Instance.LevelSelectFrame : new Color(0.97f, 0.96f, 0.95f, 0.95f);
        levelSelectFrameBg = frameImage;

        var scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(frame.transform, false);
        var scrollRectTransform = scrollObj.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(8f, 8f);
        scrollRectTransform.offsetMax = new Vector2(-8f, -8f);
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
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        var viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = true;

        // Content: VerticalLayoutGroup containing three level sections
        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.padding = new RectOffset(6, 6, 8, 24);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        levelSelectButtons = new Button[LevelDatabase.TotalLevels];
        levelSelectButtonImages = new Image[LevelDatabase.TotalLevels];
        levelSelectButtonLabels = new Text[LevelDatabase.TotalLevels];

        // Three visually distinct sections
        CreateLevelSection(content.transform, "■  Square",   "1 – 300",
            new Color(0.92f, 0.68f, 0.12f, 1f), SpriteGenerator.RoundedRect,   0,   299);
        CreateLevelSection(content.transform, "⬟  Pentagon", "301 – 600",
            new Color(0.55f, 0.30f, 0.86f, 1f), SpriteGenerator.Pentagon,      300, 599);
        CreateLevelSection(content.transform, "⬡  Hexagon",  "601 – 900",
            new Color(0.20f, 0.72f, 0.67f, 1f), SpriteGenerator.FlatHexagon,   600, 899);
        CreateLevelSection(content.transform, "△  3gen",     "901 – 1200",
            new Color(0.88f, 0.22f, 0.52f, 1f), SpriteGenerator.Triangle,     900, 1199);

        levelSelectPanel = panelObj;
        levelSelectPanel.SetActive(false);
    }

    private void CreateLevelSection(Transform parent, string label, string rangeStr,
        Color accentColor, Sprite btnSprite, int startIdx, int endIdx)
    {
        // Section container with vertical layout
        var block = new GameObject($"Section_{startIdx}");
        block.transform.SetParent(parent, false);
        var blockVlg = block.AddComponent<VerticalLayoutGroup>();
        blockVlg.spacing = 0f;
        blockVlg.padding = new RectOffset(0, 0, 0, 4);
        blockVlg.childForceExpandWidth = true;
        blockVlg.childForceExpandHeight = false;
        blockVlg.childControlWidth = true;
        blockVlg.childControlHeight = true;
        var blockFitter = block.AddComponent<ContentSizeFitter>();
        blockFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        blockFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Section header bar
        var header = new GameObject("Header");
        header.transform.SetParent(block.transform, false);
        var headerImg = header.AddComponent<Image>();
        headerImg.sprite = SpriteGenerator.RoundedRect;
        headerImg.color = accentColor;
        var headerLE = header.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 70f;
        headerLE.minHeight = 70f;
        headerLE.flexibleWidth = 1f;
        var headerHlg = header.AddComponent<HorizontalLayoutGroup>();
        headerHlg.padding = new RectOffset(24, 24, 0, 0);
        headerHlg.spacing = 0f;
        headerHlg.childAlignment = TextAnchor.MiddleLeft;
        headerHlg.childForceExpandWidth = false;
        headerHlg.childForceExpandHeight = true;
        headerHlg.childControlWidth = false;
        headerHlg.childControlHeight = true;

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(header.transform, false);
        var labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 460f;
        labelLE.flexibleWidth = 1f;
        var labelTxt = labelObj.AddComponent<Text>();
        labelTxt.font = defaultFont;
        labelTxt.fontSize = 34;
        labelTxt.fontStyle = FontStyle.Bold;
        labelTxt.color = Color.white;
        labelTxt.text = label;
        labelTxt.alignment = TextAnchor.MiddleLeft;

        var rangeObj = new GameObject("Range");
        rangeObj.transform.SetParent(header.transform, false);
        var rangeLE = rangeObj.AddComponent<LayoutElement>();
        rangeLE.preferredWidth = 150f;
        var rangeTxt = rangeObj.AddComponent<Text>();
        rangeTxt.font = defaultFont;
        rangeTxt.fontSize = 26;
        rangeTxt.fontStyle = FontStyle.Normal;
        rangeTxt.color = new Color(1f, 1f, 1f, 0.78f);
        rangeTxt.text = rangeStr;
        rangeTxt.alignment = TextAnchor.MiddleRight;

        // Grid of level buttons (5 columns)
        var gridObj = new GameObject("Grid");
        gridObj.transform.SetParent(block.transform, false);
        var gridLE = gridObj.AddComponent<LayoutElement>();
        gridLE.flexibleWidth = 1f;
        var grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(116f, 116f);
        grid.spacing = new Vector2(10f, 10f);
        grid.padding = new RectOffset(10, 10, 12, 12);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        var gridFitter = gridObj.AddComponent<ContentSizeFitter>();
        gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        gridFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        for (int i = startIdx; i <= endIdx; i++)
        {
            int levelIndex = i;
            var btnObj = new GameObject($"L{levelIndex + 1}");
            btnObj.transform.SetParent(gridObj.transform, false);
            btnObj.AddComponent<RectTransform>().sizeDelta = new Vector2(116f, 116f);

            var shadowObj = new GameObject("S");
            shadowObj.transform.SetParent(btnObj.transform, false);
            var shadowObjRect = shadowObj.AddComponent<RectTransform>();
            shadowObjRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowObjRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowObjRect.pivot = new Vector2(0.5f, 0.5f);
            shadowObjRect.sizeDelta = new Vector2(110f, 110f);
            shadowObjRect.anchoredPosition = new Vector2(0f, -2f);
            var shadowImg = shadowObj.AddComponent<Image>();
            shadowImg.sprite = btnSprite;
            shadowImg.color = new Color(0f, 0f, 0f, 0.07f);

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.sprite = btnSprite;
            btnImg.color = new Color(0.93f, 0.96f, 0.92f, 1f);

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.97f, 0.97f, 0.97f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.disabledColor = new Color(0.88f, 0.88f, 0.88f, 0.95f);
            btn.colors = colors;
            btn.onClick.AddListener(() => FindAnyObjectByType<GameManager>().SelectLevel(levelIndex));

            var lblObj = new GameObject("N");
            lblObj.transform.SetParent(btnObj.transform, false);
            var lblRect = lblObj.AddComponent<RectTransform>();
            lblRect.anchorMin = new Vector2(0.5f, 0.5f);
            lblRect.anchorMax = new Vector2(0.5f, 0.5f);
            lblRect.pivot = new Vector2(0.5f, 0.5f);
            lblRect.anchoredPosition = Vector2.zero;
            lblRect.sizeDelta = new Vector2(100f, 50f);
            var lbl = lblObj.AddComponent<Text>();
            lbl.font = defaultFont;
            lbl.fontSize = 32;
            lbl.fontStyle = FontStyle.Bold;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = TextDark;
            lbl.text = (levelIndex + 1).ToString();

            levelSelectButtons[i] = btn;
            levelSelectButtonImages[i] = btnImg;
            levelSelectButtonLabels[i] = lbl;
        }
    }

    private void RefreshLevelSelectButtons(int currentLevelIndex, int highestUnlockedLevelIndex, int totalLevels)
    {
        // Square section (0-299): amber palette
        Color sqU = new Color(1.00f, 0.92f, 0.72f, 1f);
        Color sqC = new Color(0.92f, 0.68f, 0.12f, 1f);
        Color sqL = new Color(0.92f, 0.90f, 0.87f, 1f);
        // Pentagon section (300-599): purple palette
        Color pgU = new Color(0.90f, 0.82f, 0.98f, 1f);
        Color pgC = new Color(0.55f, 0.30f, 0.86f, 1f);
        Color pgL = new Color(0.90f, 0.87f, 0.93f, 1f);
        // Hexagon section (600-899): teal palette
        Color hxU = new Color(0.78f, 0.96f, 0.92f, 1f);
        Color hxC = new Color(0.20f, 0.72f, 0.67f, 1f);
        Color hxL = new Color(0.86f, 0.90f, 0.89f, 1f);
        // Triangle / 3gen section (900-1199): rose palette
        Color cgU = new Color(0.98f, 0.80f, 0.88f, 1f);
        Color cgC = new Color(0.88f, 0.22f, 0.52f, 1f);
        Color cgL = new Color(0.92f, 0.87f, 0.89f, 1f);

        for (int i = 0; i < levelSelectButtons.Length; i++)
        {
            bool exists = i < totalLevels;
            bool unlocked = exists && i <= highestUnlockedLevelIndex;
            bool isCurrent = i == currentLevelIndex;

            levelSelectButtons[i].gameObject.SetActive(exists);
            if (!exists) continue;

            levelSelectButtons[i].interactable = unlocked;
            levelSelectButtonLabels[i].text = (i + 1).ToString();

            Color colU, colC, colL;
            if (i < 300)      { colU = sqU; colC = sqC; colL = sqL; }
            else if (i < 600) { colU = pgU; colC = pgC; colL = pgL; }
            else if (i < 900) { colU = hxU; colC = hxC; colL = hxL; }
            else              { colU = cgU; colC = cgC; colL = cgL; }

            if (isCurrent)
            {
                levelSelectButtonImages[i].color = colC;
                levelSelectButtonLabels[i].color = Color.white;
            }
            else if (unlocked)
            {
                levelSelectButtonImages[i].color = colU;
                levelSelectButtonLabels[i].color = TextDark;
            }
            else
            {
                levelSelectButtonImages[i].color = colL;
                levelSelectButtonLabels[i].color = TextMuted;
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

    // --- Free Hint Badge ---

    public void UpdateHintBadge(int count)
    {
        if (hintFreeBadgeObj == null) return;
        hintFreeBadgeObj.SetActive(count > 0);
        if (hintFreeBadgeText != null)
            hintFreeBadgeText.text = count.ToString();
    }

    // --- Rate App Popup ---

    public void ShowRatePopup(System.Action onRate, System.Action onDismiss)
    {
        if (ratePopup != null) return;

        ratePopup = new GameObject("RatePopup");
        ratePopup.transform.SetParent(canvas.transform, false);
        ratePopup.transform.SetAsLastSibling();

        var popupRect = ratePopup.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;

        var overlay = ratePopup.AddComponent<Image>();
        overlay.color = new Color(0.10f, 0.08f, 0.14f, 0.62f);
        var overlayBtn = ratePopup.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        overlayBtn.onClick.AddListener(() => { HideRatePopup(); onDismiss?.Invoke(); });

        // Shadow
        var shadowObj = new GameObject("Shadow");
        shadowObj.transform.SetParent(ratePopup.transform, false);
        var shadowRect = shadowObj.AddComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
        shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.sizeDelta = new Vector2(636f, 436f);
        shadowRect.anchoredPosition = new Vector2(4f, -8f);
        var shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = SpriteGenerator.RoundedRect;
        shadowImg.color = new Color(0f, 0f, 0f, 0.18f);

        // Card
        var card = new GameObject("Card");
        card.transform.SetParent(ratePopup.transform, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(610f, 410f);
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = SpriteGenerator.RoundedRect;
        cardImg.color = new Color(1f, 1f, 1f, 0.98f);

        // Emoji
        var emoji = MakeCardText("Emoji", card.transform, new Vector2(0, 148), 52, FontStyle.Normal, TextDark);
        emoji.text = "⭐⭐⭐⭐⭐";
        emoji.fontSize = 44;

        // Title
        var title = MakeCardText("Title", card.transform, new Vector2(0, 80), 42, FontStyle.Bold, TextDark);
        title.text = "Enjoying Puzzle Muzzle?";
        title.GetComponent<RectTransform>().sizeDelta = new Vector2(530f, 60f);

        // Subtitle
        var sub = MakeCardText("Subtitle", card.transform, new Vector2(0, 20), 28, FontStyle.Normal, TextMuted);
        sub.text = "A quick rating helps us a lot!";
        sub.GetComponent<RectTransform>().sizeDelta = new Vector2(530f, 50f);

        // Rate button
        var rateBtn = CreateCardButton("⭐  Rate Now", card.transform, new Vector2(0, -80), BtnTeal);
        rateBtn.onClick.AddListener(() => { HideRatePopup(); onRate?.Invoke(); });

        // Dismiss button
        var dismissBtn = CreateCardButton("Not Now", card.transform, new Vector2(0, -170), new Color(0.88f, 0.86f, 0.84f));
        var dismissText = dismissBtn.GetComponentInChildren<Text>();
        if (dismissText != null) { dismissText.color = TextMuted; dismissText.fontSize = 26; }
        dismissBtn.onClick.AddListener(() => { HideRatePopup(); onDismiss?.Invoke(); });
    }

    public void HideRatePopup()
    {
        if (ratePopup != null)
        {
            Destroy(ratePopup);
            ratePopup = null;
        }
    }



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
        dailyChallengeCardSubText.text = completed
            ? "Great job! Come back tomorrow 🎉"
            : "A new puzzle every day • Complete to earn 1 hint 💡";

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
        var tm = ThemeManager.Instance;
        Color cardColor   = tm != null ? tm.CardBg    : new Color(1f, 1f, 1f, 0.98f);
        Color textColor   = tm != null ? tm.TextPrimary : TextDark;
        Color divColor    = tm != null ? (tm.IsDarkMode ? new Color(0.30f, 0.29f, 0.35f) : new Color(0.85f, 0.83f, 0.78f)) : new Color(0.85f, 0.83f, 0.78f);

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
        shadowRect.sizeDelta = new Vector2(666f, 696f);
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
        cardRect.sizeDelta = new Vector2(640f, 670f);
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = SpriteGenerator.RoundedRect;
        cardImg.color = cardColor;

        // Title
        var title = MakeCardText("Title", card.transform, new Vector2(0, 272), 44, FontStyle.Bold, textColor);
        title.text = "⚙️  Settings";

        // ─── Sound toggle ────────────────────────────────────────────────────
        bool soundOn = !(AudioManager.Instance?.IsMuted ?? false);
        CreateToggleRow(card.transform, "🔊  Sound", soundOn, new Vector2(0, 180), (val) =>
        {
            AudioManager.Instance?.SetMuted(!val);
        });

        // ─── Haptic toggle ───────────────────────────────────────────────────
        bool hapticOn = HapticManager.Instance?.IsHapticsEnabled ?? true;
        CreateToggleRow(card.transform, "📳  Haptics", hapticOn, new Vector2(0, 80), (val) =>
        {
            if (HapticManager.Instance != null) HapticManager.Instance.IsHapticsEnabled = val;
        });

        // ─── Dark Mode toggle ────────────────────────────────────────────────
        bool darkOn = tm?.IsDarkMode ?? false;
        CreateToggleRow(card.transform, "🌙  Dark Mode", darkOn, new Vector2(0, -20), (val) =>
        {
            if (ThemeManager.Instance != null) ThemeManager.Instance.IsDarkMode = val;
            // Update card color live
            cardImg.color = ThemeManager.Instance != null ? ThemeManager.Instance.CardBg : cardColor;
        });

        // Divider
        var divObj = new GameObject("Divider");
        divObj.transform.SetParent(card.transform, false);
        var divRect = divObj.AddComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0.5f, 0.5f);
        divRect.anchorMax = new Vector2(0.5f, 0.5f);
        divRect.pivot = new Vector2(0.5f, 0.5f);
        divRect.anchoredPosition = new Vector2(0, -90f);
        divRect.sizeDelta = new Vector2(560f, 2f);
        divObj.AddComponent<Image>().color = divColor;

        // Achievements button
        var achBtn = CreateCardButton("🏆  Achievements", card.transform, new Vector2(0, -155f), BtnTeal);
        achBtn.onClick.AddListener(() =>
        {
            Destroy(popup);
            GameCenterManager.Instance?.ShowAchievements();
        });

        // Leaderboard button
        var lbBtn = CreateCardButton("📊  Leaderboard", card.transform, new Vector2(0, -250f), new Color(0.38f, 0.32f, 0.58f));
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
        rowBg.color = ThemeManager.Instance != null ? ThemeManager.Instance.ToggleRowBg : new Color(0.96f, 0.94f, 0.90f, 1f);

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
        labelTxt.color = ThemeManager.Instance != null ? ThemeManager.Instance.TextPrimary : TextDark;

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

        _lsCurrentIdx = currentLevelIndex;
        _lsHighestUnlocked = highestUnlockedLevelIndex;
        _lsTotalLevels = totalLevels;

        RefreshLevelSelectButtons(currentLevelIndex, highestUnlockedLevelIndex, totalLevels);
        levelSelectPanel.SetActive(true);
        StartCoroutine(ScrollToCurrentLevelButton(currentLevelIndex));
    }

    private IEnumerator ScrollToCurrentLevelButton(int idx)
    {
        yield return null; // wait one frame for layout to settle
        Canvas.ForceUpdateCanvases();
        if (levelSelectScrollRect == null || idx >= levelSelectButtons.Length || levelSelectButtons[idx] == null)
            yield break;
        var content = levelSelectScrollRect.content;
        var vp = levelSelectScrollRect.viewport;
        float contentH = content.rect.height;
        float vpH = vp.rect.height;
        float maxScroll = contentH - vpH;
        if (maxScroll <= 0f) yield break;
        Vector2 btnLocal = content.InverseTransformPoint(levelSelectButtons[idx].transform.position);
        float btnTopFromContentTop = -btnLocal.y;
        float targetFromTop = Mathf.Clamp(btnTopFromContentTop - vpH * 0.35f, 0f, maxScroll);
        levelSelectScrollRect.verticalNormalizedPosition = 1f - targetFromTop / maxScroll;
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
