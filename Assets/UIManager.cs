using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SocialPlatforms;
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
    private Button cartButton;
    private Button levelSelectToggleButton;
    private GameObject noAdsPurchasePopup;
    private GameObject ratePopup;
    private GameObject hintFreeBadgeObj;
    private Text hintFreeBadgeText;
    private Image hintButtonIcon;
    private Image restartButtonIcon;
    private Image cartIconImage;
    // (basePath, Image) pairs for icons that need dark/light sprite swap
    private System.Collections.Generic.List<(string basePath, Image img)> themedIcons
        = new System.Collections.Generic.List<(string, Image)>();
    private GameObject hintPromoPopup;
    private string noAdsPriceLabel = "$19.99";

    private GameObject levelSelectPanel;
    private ScrollRect levelSelectScrollRect;
    private Button[] levelSelectButtons;
    private Image[] levelSelectButtonImages;
    private Text[] levelSelectButtonLabels;
    private int _lsCurrentIdx;
    private int _lsHighestUnlocked = -1;
    private int _lsTotalLevels;
    private GameObject transitionOverlay;
    private Image transitionOverlayImage;
    private Text transitionOverlayText;
    private RectTransform transitionSquaresRoot;
    private Image[] transitionSquares;
    private int _lsActiveGroup;
    private Image[] _lsGroupTabBgs = new Image[6];
    private Button[] _lsGroupTabButtons = new Button[6];
    private Image[] _lsGroupTabIcons = new Image[6];
    private Image[] _lsGroupTabIconChips = new Image[6];
    private static Sprite _cartSprite;
    private Text[] _lsGroupTabRanges = new Text[6];
    private Text _lsHeaderTitle;
    private Image _lsProgressBarFill;
    private Text _lsProgressCountText;
    private Text[] _lsLockLabels;
    private Transform[] _lsGroupGridRoots = new Transform[6];

    // Lazy-load data for group grids (built on first tab switch, not all at startup)
    private Transform _lsContentParent;
    private readonly Sprite[]  _lsGroupSpritesLazy = new Sprite[6];
    private readonly int[]     _lsGroupStart        = new int[6];
    private readonly int[]     _lsGroupEnd          = new int[6];

    private Canvas canvas;
    private RectTransform safeAreaRect;
    private Font defaultFont;

    // Theme-reactive UI refs
    private Image levelSelectCardBg;
    private Image levelSelectFrameBg;
    private Image settingsButtonBg;
    private Image settingsIconImg;
    private Image leaderboardButtonBg;
    private Text levelProgressTextRef;  // alias — same as levelProgressText
    // Level select panel theme refs
    private Image _lsPanelBg;
    private Text  _lsBackArrowText;

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

    private static readonly Color[] GrpAccent = {
        new Color(0.92f, 0.68f, 0.12f),
        new Color(0.20f, 0.72f, 0.67f),
        new Color(0.88f, 0.22f, 0.52f),
        new Color(0.30f, 0.52f, 0.90f),
        new Color(0.28f, 0.68f, 0.40f),
        new Color(0.62f, 0.36f, 0.86f),
    };
    private static readonly string[] GrpName = { "Square", "Hexagon", "Triangle", "Seq Square", "Seq Hexagon", "Seq Triangle" };
    private static readonly string[] GrpRange = { "1-300", "301-600", "601-900", "901-1200", "1201-1500", "1501-1800" };
    private static readonly Func<Sprite>[] GrpSprite = {
        () => SpriteGenerator.RoundedRect,
        () => SpriteGenerator.FlatHexagon,
        () => SpriteGenerator.Triangle,
        () => SpriteGenerator.RoundedRect,
        () => SpriteGenerator.FlatHexagon,
        () => SpriteGenerator.Triangle,
    };

    private const int GroupCount = 6;
    private static readonly int[] GrpStart = { 0, 300, 600, 900, 1200, 1500 };
    private static readonly int[] GrpEnd   = { 299, 599, 899, 1199, 1499, 1799 };

    private static int LsGroupOf(int levelIndex)
        => Mathf.Clamp(levelIndex / 300, 0, GroupCount - 1);

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
        CreateTransitionOverlay();

        ThemeManager.OnThemeChanged += OnThemeChanged;

        // Apply theme immediately so startup state matches saved preference
        OnThemeChanged();
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

        // Level select buttons — re-apply section-specific colors using stored state
        if (levelSelectButtonImages != null && _lsTotalLevels > 0)
            RefreshLevelSelectButtons(_lsCurrentIdx, _lsHighestUnlocked, _lsTotalLevels);

        // Level select panel background + texts
        Color lsBg   = dark ? new Color(0.09f, 0.09f, 0.12f, 1f) : new Color(0.931f, 0.914f, 0.894f, 1f);
        Color lsTxt  = dark ? new Color(0.92f, 0.90f, 0.88f) : TextDark;
        Color lsMuted = dark ? new Color(0.60f, 0.58f, 0.56f) : TextMuted;
        if (_lsPanelBg      != null) _lsPanelBg.color      = lsBg;
        if (_lsBackArrowText != null) _lsBackArrowText.color = lsTxt;
        if (_lsHeaderTitle  != null) _lsHeaderTitle.color   = lsTxt;

        // Cart icon: teal in light mode, white in dark mode
        if (cartIconImage != null)
            cartIconImage.color = dark ? Color.white : new Color(0.18f, 0.55f, 0.62f, 1f);

        // Hint badge text color
        if (hintFreeBadgeText != null)
            hintFreeBadgeText.color = dark ? Color.white : Color.black;

        // Camera background
        var cam = Camera.main;
        if (cam != null)
            cam.backgroundColor = tm.BgColor;

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
        float aspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
        bool useRaisedTopBarLayout = aspect >= 0.65f;
        float topBarElementY = useRaisedTopBarLayout ? -42f : -70f;

        // Level progress text (top center) — Georgia italic, matching tutorial style
        var georgiaFont = Font.CreateDynamicFontFromOSFont("Georgia", 72);
        levelProgressText = MakeText("Progress", bar.transform, new Vector2(0, topBarElementY), 42, FontStyle.Italic, TextDark);
        if (georgiaFont != null) levelProgressText.font = georgiaFont;

        levelSelectToggleButton = CreateInvisibleButton("LevelSelect", bar.transform, new Vector2(0, topBarElementY), new Vector2(520, 84));
        levelSelectToggleButton.onClick.AddListener(() => FindAnyObjectByType<GameManager>().ToggleLevelSelectMenu());

        // Leaderboard button (top-left)
        float lbY = topBarElementY;
        var lbTopBtn = CreateIconButton("Leaderboard", bar.transform, new Vector2(100f, lbY), 68f, "icons/top-three");
        lbTopBtn.onClick.AddListener(() => {
            if (GameCenterManager.Instance != null)
                GameCenterManager.Instance.ShowLeaderboard();
            else
            {
#if UNITY_IOS && !UNITY_EDITOR
                Social.ShowLeaderboardUI();
#endif
            }
        });
        leaderboardButtonBg = lbTopBtn.GetComponent<Image>();

        bool isDark = ThemeManager.Instance?.IsDarkMode ?? false;
        var settingsObj = new GameObject("SettingsBtn");
        settingsObj.transform.SetParent(bar.transform, false);
        var settingsRect = settingsObj.AddComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(1f, 1f);
        settingsRect.anchorMax = new Vector2(1f, 1f);
        settingsRect.pivot = new Vector2(0.5f, 0.5f);
        settingsRect.anchoredPosition = new Vector2(-100f, topBarElementY);
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

        // Shop/Cart button (bottom-left) — opens Hint Store
        var cartBtn = CreateIconButton("Shop", bar.transform, new Vector2(100, 80), 72f, "icons/cart");
        var cartRect = cartBtn.GetComponent<RectTransform>();
        cartRect.anchorMin = new Vector2(0, 0);
        cartRect.anchorMax = new Vector2(0, 0);
        cartBtn.onClick.AddListener(() => FindAnyObjectByType<GameManager>()?.OpenHintStore(grantOnWatch: false));
        var cartIconImg = cartBtn.transform.Find("Icon")?.GetComponent<Image>();
        if (cartIconImg != null)
        {
            bool darkNow = ThemeManager.Instance?.IsDarkMode ?? false;
            cartIconImg.color = darkNow ? Color.white : new Color(0.18f, 0.55f, 0.62f, 1f);
        }
        cartIconImage = cartIconImg;
        cartButton = cartBtn;
        StartCoroutine(CartGlowCoroutine());

        // Hint button (center) with icon
        hintButton = CreateIconButton("Hint", bar.transform, new Vector2(0, 82), 106, "icons/lightbulb");
        var hRect = hintButton.GetComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0.5f, 0f);
        hRect.anchorMax = new Vector2(0.5f, 0f);
        hRect.pivot = new Vector2(0.5f, 0.5f);
        hintButton.onClick.AddListener(() => FindAnyObjectByType<GameManager>().UseHint());

        // Hint count badge (bottom-right corner of hint button) — always visible
        hintFreeBadgeObj = new GameObject("FreeBadge");
        hintFreeBadgeObj.transform.SetParent(hintButton.transform, false);
        var badgeRect = hintFreeBadgeObj.AddComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 0f);
        badgeRect.anchorMax = new Vector2(1f, 0f);
        badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.anchoredPosition = new Vector2(-2f, 2f); // 2px inset from bottom-right
        badgeRect.sizeDelta = new Vector2(40f, 40f);
        var badgeImg = hintFreeBadgeObj.AddComponent<Image>();
        badgeImg.color = Color.clear; // no background
        // Text fills the circle exactly
        var badgeTxtGo = new GameObject("CountTxt");
        badgeTxtGo.transform.SetParent(hintFreeBadgeObj.transform, false);
        var btRect = badgeTxtGo.AddComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.offsetMin = Vector2.zero;
        btRect.offsetMax = Vector2.zero;
        hintFreeBadgeText = badgeTxtGo.AddComponent<Text>();
        hintFreeBadgeText.font = defaultFont;
        hintFreeBadgeText.fontSize = 33;
        hintFreeBadgeText.fontStyle = FontStyle.Bold;
        hintFreeBadgeText.alignment = TextAnchor.MiddleCenter;
        hintFreeBadgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
        hintFreeBadgeText.verticalOverflow = VerticalWrapMode.Overflow;
        hintFreeBadgeText.color = (ThemeManager.Instance?.IsDarkMode ?? false) ? Color.white : Color.black;
        hintFreeBadgeText.text = "0";
        hintFreeBadgeObj.SetActive(true);

        // Restart button (right) with icon
        restartButton = CreateIconButton("Restart", bar.transform, new Vector2(-100, 80), 70, "icons/reload");
        var rRect = restartButton.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(1, 0);
        rRect.anchorMax = new Vector2(1, 0);
        restartButton.onClick.AddListener(() => FindAnyObjectByType<GameManager>().RetryLevel());
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
        // Full-screen, parented to safeAreaRect so notch/home-indicator are respected
        var panelObj = new GameObject("LevelSelectPanel");
        panelObj.transform.SetParent(safeAreaRect, false);

        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(0, 160f);  // expose bottom bar (160px)
        panelRect.offsetMax = Vector2.zero;

        var panelBg = panelObj.AddComponent<Image>();
        bool lsDark = ThemeManager.Instance?.IsDarkMode ?? false;
        panelBg.color = lsDark ? new Color(0.09f, 0.09f, 0.12f, 1f) : new Color(0.931f, 0.914f, 0.894f, 1f);
        _lsPanelBg = panelBg;

        // ── Header (fixed, 120px tall) ──────────────────────────────────────────────
        var header = new GameObject("Header");
        header.transform.SetParent(panelObj.transform, false);
        var hRect = header.AddComponent<RectTransform>();
        hRect.anchorMin = new Vector2(0f, 1f);
        hRect.anchorMax = new Vector2(1f, 1f);
        hRect.pivot     = new Vector2(0.5f, 1f);
        hRect.sizeDelta = new Vector2(0f, 120f);
        hRect.anchoredPosition = Vector2.zero;

        // Back button
        var backObj = new GameObject("BackBtn");
        backObj.transform.SetParent(header.transform, false);
        var backRect = backObj.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0.5f);
        backRect.anchorMax = new Vector2(0f, 0.5f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.anchoredPosition = new Vector2(20f, 0f);
        backRect.sizeDelta = new Vector2(80f, 80f);
        var backImg = backObj.AddComponent<Image>();
        backImg.color = Color.clear;
        var backBtn = backObj.AddComponent<Button>();
        backBtn.targetGraphic = backImg;
        backBtn.onClick.AddListener(HideLevelSelect);
        var backArrow = new GameObject("Arrow");
        backArrow.transform.SetParent(backObj.transform, false);
        var baRect = backArrow.AddComponent<RectTransform>();
        baRect.anchorMin = Vector2.zero; baRect.anchorMax = Vector2.one;
        baRect.offsetMin = Vector2.zero; baRect.offsetMax = Vector2.zero;
        var baTxt = backArrow.AddComponent<Text>();
        baTxt.font = defaultFont; baTxt.text = "‹"; baTxt.fontSize = 60;
        baTxt.fontStyle = FontStyle.Bold; baTxt.alignment = TextAnchor.MiddleCenter;
        baTxt.color = lsDark ? new Color(0.92f, 0.90f, 0.88f) : TextDark;
        _lsBackArrowText = baTxt;

        // Title "Level X / Y"
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(header.transform, false);
        var tRect = titleObj.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.12f, 0f); tRect.anchorMax = new Vector2(0.88f, 1f);
        tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;
        _lsHeaderTitle = titleObj.AddComponent<Text>();
        _lsHeaderTitle.font = defaultFont;
        _lsHeaderTitle.fontSize = 42; _lsHeaderTitle.fontStyle = FontStyle.Bold;
        _lsHeaderTitle.alignment = TextAnchor.MiddleCenter;
        _lsHeaderTitle.color = lsDark ? new Color(0.92f, 0.90f, 0.88f) : TextDark;
        _lsHeaderTitle.text = "Level 1 / 900";

        // Settings gear (right)
        var gearObj = new GameObject("GearBtn");
        gearObj.transform.SetParent(header.transform, false);
        var gRect = gearObj.AddComponent<RectTransform>();
        gRect.anchorMin = new Vector2(1f, 0.5f); gRect.anchorMax = new Vector2(1f, 0.5f);
        gRect.pivot = new Vector2(1f, 0.5f);
        gRect.anchoredPosition = new Vector2(-20f, 0f); gRect.sizeDelta = new Vector2(80f, 80f);
        var gImg = gearObj.AddComponent<Image>(); gImg.color = Color.clear;
        var gBtn = gearObj.AddComponent<Button>(); gBtn.targetGraphic = gImg;
        gBtn.onClick.AddListener(ShowSettingsPopup);
        var gearTxt = new GameObject("Icon");
        gearTxt.transform.SetParent(gearObj.transform, false);
        var gtRect = gearTxt.AddComponent<RectTransform>();
        gtRect.anchorMin = Vector2.zero; gtRect.anchorMax = Vector2.one;
        gtRect.offsetMin = Vector2.zero; gtRect.offsetMax = Vector2.zero;
        var gtTxt = gearTxt.AddComponent<Text>();
        gtTxt.font = defaultFont; gtTxt.text = "⚙"; gtTxt.fontSize = 44;
        gtTxt.alignment = TextAnchor.MiddleCenter;
        gtTxt.color = new Color(0.80f, 0.64f, 0.20f, 1f);

        // ── ScrollView (fills below header) ─────────────────────────────────────────
        var scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(panelObj.transform, false);
        var scrollRT = scrollObj.AddComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = Vector2.zero; scrollRT.offsetMax = new Vector2(0f, -130f);
        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 40f;
        levelSelectScrollRect = scrollRect;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        var vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero; vpRect.offsetMax = Vector2.zero;
        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.01f);
        var vpMask = viewport.AddComponent<Mask>();
        vpMask.showMaskGraphic = true;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cRect = content.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0f, 1f); cRect.anchorMax = new Vector2(1f, 1f);
        cRect.pivot = new Vector2(0.5f, 1f);
        cRect.offsetMin = Vector2.zero; cRect.offsetMax = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0f;
        vlg.padding = new RectOffset(0, 0, 12, 48);
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        var cFitter = content.AddComponent<ContentSizeFitter>();
        cFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        cFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        scrollRect.viewport = vpRect;
        scrollRect.content = cRect;

        // ── Group Tabs ──────────────────────────────────────────────────────────────
        LsBuildGroupTabsRow(content.transform);
        LsBuildProgressRow(content.transform);

        // ── Level Button Grids ──────────────────────────────────────────────────────
        levelSelectButtons      = new Button[LevelDatabase.TotalLevels];
        levelSelectButtonImages = new Image[LevelDatabase.TotalLevels];
        levelSelectButtonLabels = new Text[LevelDatabase.TotalLevels];
        _lsLockLabels           = new Text[LevelDatabase.TotalLevels];

        // Store lazy-load params — only group 0 is built now; others built on first tab switch.
        _lsContentParent = content.transform;
        for (int g = 0; g < GroupCount; g++)
        {
            _lsGroupSpritesLazy[g] = GrpSprite[g]();
            _lsGroupStart[g] = GrpStart[g];
            _lsGroupEnd[g]   = GrpEnd[g];
        }

        LsBuildGroupGrid(_lsContentParent, 0, _lsGroupSpritesLazy[0], _lsGroupStart[0], _lsGroupEnd[0]);

        levelSelectPanel = panelObj;
        levelSelectPanel.SetActive(false);
    }

    private Text LsMakeVlgText(string name, Transform parent, int size, FontStyle style, Color color, string text)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = size + 12f; le.minHeight = size + 4f;
        var t = obj.AddComponent<Text>();
        t.font = defaultFont; t.fontSize = size; t.fontStyle = style;
        t.alignment = TextAnchor.MiddleLeft; t.color = color; t.text = text;
        return t;
    }

    private void LsBuildGroupTabsRow(Transform parent)
    {
        // Two rows of three tabs: classic campaigns on top, sequence campaigns below.
        var container = new GameObject("GroupTabs");
        container.transform.SetParent(parent, false);
        var contLE = container.AddComponent<LayoutElement>();
        contLE.preferredHeight = 216f; contLE.minHeight = 216f; contLE.flexibleWidth = 1f;
        var contVlg = container.AddComponent<VerticalLayoutGroup>();
        contVlg.spacing = 0f;
        contVlg.childForceExpandWidth = true; contVlg.childForceExpandHeight = false;
        contVlg.childControlWidth = true; contVlg.childControlHeight = false;

        var rows = new Transform[2];
        for (int r = 0; r < 2; r++)
        {
            var row = new GameObject($"Row{r}");
            row.transform.SetParent(container.transform, false);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 108f; rowLE.minHeight = 108f; rowLE.flexibleWidth = 1f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(20, 20, r == 0 ? 14 : 4, r == 0 ? 4 : 10);
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true; hlg.childControlHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            rows[r] = row.transform;
        }

        bool tabsDark = ThemeManager.Instance?.IsDarkMode ?? false;
        Color tabIdleBg = tabsDark ? new Color(0.18f, 0.17f, 0.22f, 1f) : Color.white;

        for (int g = 0; g < GroupCount; g++)
        {
            int grp = g;
            var tab = new GameObject($"Tab{g}");
            tab.transform.SetParent(rows[g / 3], false);
            var tabRT = tab.AddComponent<RectTransform>();
            tabRT.sizeDelta = new Vector2(0f, 90f);
            var tabLE = tab.AddComponent<LayoutElement>();
            tabLE.flexibleWidth = 1f; tabLE.preferredHeight = 90f; tabLE.minHeight = 90f;
            var tabImg = tab.AddComponent<Image>();
            tabImg.sprite = SpriteGenerator.RoundedRect;
            tabImg.color = (g == 0) ? GrpAccent[0] : tabIdleBg;
            _lsGroupTabBgs[g] = tabImg;
            var tabBtn = tab.AddComponent<Button>();
            tabBtn.targetGraphic = tabImg;
            _lsGroupTabButtons[g] = tabBtn;
            var tc = tabBtn.colors;
            tc.highlightedColor = new Color(0.97f, 0.97f, 0.97f, 1f);
            tc.pressedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
            tabBtn.colors = tc;
            tabBtn.onClick.AddListener(() => LsSwitchGroupTab(grp));

            int shapeIdx = g % 3;   // 0 square, 1 hexagon, 2 triangle
            float iconSize = (shapeIdx == 2) ? 40f : (shapeIdx == 1 ? 36f : 32f);

            // Icon inside a soft accent-tinted chip
            var chipObj = new GameObject("Chip");
            chipObj.transform.SetParent(tab.transform, false);
            var chipRT = chipObj.AddComponent<RectTransform>();
            chipRT.anchorMin = new Vector2(0.5f, 1f); chipRT.anchorMax = new Vector2(0.5f, 1f);
            chipRT.pivot = new Vector2(0.5f, 1f);
            chipRT.anchoredPosition = new Vector2(0f, -8f);
            chipRT.sizeDelta = new Vector2(48f, 48f);
            var chipImg = chipObj.AddComponent<Image>();
            chipImg.sprite = SpriteGenerator.RoundedRect;
            chipImg.color = (g == 0)
                ? new Color(1f, 1f, 1f, 0.22f)
                : new Color(GrpAccent[g].r, GrpAccent[g].g, GrpAccent[g].b, tabsDark ? 0.26f : 0.14f);
            _lsGroupTabIconChips[g] = chipImg;

            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(chipObj.transform, false);
            var iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 0.5f); iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = new Vector2(iconSize, iconSize);
            _lsGroupTabIcons[g] = iconObj.AddComponent<Image>();
            _lsGroupTabIcons[g].sprite = GrpSprite[g]();
            _lsGroupTabIcons[g].preserveAspect = true;
            _lsGroupTabIcons[g].color = (g == 0) ? Color.white : GrpAccent[g];

            var rangeObj = new GameObject("Range");
            rangeObj.transform.SetParent(tab.transform, false);
            var rangeRT = rangeObj.AddComponent<RectTransform>();
            rangeRT.anchorMin = new Vector2(0f, 0f); rangeRT.anchorMax = new Vector2(1f, 0f);
            rangeRT.pivot = new Vector2(0.5f, 0f);
            rangeRT.anchoredPosition = new Vector2(0f, 6f);
            rangeRT.sizeDelta = new Vector2(0f, 24f);
            _lsGroupTabRanges[g] = rangeObj.AddComponent<Text>();
            _lsGroupTabRanges[g].font = defaultFont; _lsGroupTabRanges[g].text = GrpRange[g];
            _lsGroupTabRanges[g].fontSize = 19; _lsGroupTabRanges[g].fontStyle = FontStyle.Bold;
            _lsGroupTabRanges[g].alignment = TextAnchor.MiddleCenter;
            _lsGroupTabRanges[g].color = (g == 0)
                ? new Color(1f, 1f, 1f, 0.92f)
                : (tabsDark ? new Color(0.60f, 0.58f, 0.56f) : TextMuted);
        }
    }

    private void LsBuildProgressRow(Transform parent)
    {
        bool dark = ThemeManager.Instance?.IsDarkMode ?? false;

        var rowObj = new GameObject("ProgressRow");
        rowObj.transform.SetParent(parent, false);
        var le = rowObj.AddComponent<LayoutElement>();
        le.preferredHeight = 40f; le.minHeight = 40f; le.flexibleWidth = 1f;

        var track = new GameObject("Track");
        track.transform.SetParent(rowObj.transform, false);
        var trackRT = track.AddComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0f, 0.5f); trackRT.anchorMax = new Vector2(1f, 0.5f);
        trackRT.pivot = new Vector2(0.5f, 0.5f);
        trackRT.offsetMin = new Vector2(24f, -6f); trackRT.offsetMax = new Vector2(-122f, 6f);
        var trackImg = track.AddComponent<Image>();
        trackImg.sprite = SpriteGenerator.RoundedRectSliced;
        trackImg.type = Image.Type.Sliced;
        trackImg.color = dark ? new Color(0.22f, 0.21f, 0.27f, 1f) : new Color(0.88f, 0.86f, 0.83f, 1f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(track.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.pivot = new Vector2(0f, 0.5f);
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
        _lsProgressBarFill = fill.AddComponent<Image>();
        _lsProgressBarFill.sprite = SpriteGenerator.RoundedRectSliced;
        _lsProgressBarFill.type = Image.Type.Sliced;
        _lsProgressBarFill.color = GrpAccent[0];

        var countObj = new GameObject("Count");
        countObj.transform.SetParent(rowObj.transform, false);
        var countRT = countObj.AddComponent<RectTransform>();
        countRT.anchorMin = new Vector2(1f, 0f); countRT.anchorMax = new Vector2(1f, 1f);
        countRT.pivot = new Vector2(1f, 0.5f);
        countRT.anchoredPosition = new Vector2(-24f, 0f);
        countRT.sizeDelta = new Vector2(92f, 0f);
        _lsProgressCountText = countObj.AddComponent<Text>();
        _lsProgressCountText.font = defaultFont; _lsProgressCountText.fontSize = 22;
        _lsProgressCountText.fontStyle = FontStyle.Bold;
        _lsProgressCountText.alignment = TextAnchor.MiddleRight;
        _lsProgressCountText.color = dark ? new Color(0.60f, 0.58f, 0.56f) : TextMuted;
        _lsProgressCountText.text = "0/300";
    }

    private static Sprite LoadCartSprite()
    {
        if (_cartSprite != null) return _cartSprite;
        var tex = Resources.Load<Texture2D>("icons/cart");
        if (tex == null) return null;
        _cartSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        return _cartSprite;
    }

    private void LsBuildGroupGrid(Transform parent, int groupIndex, Sprite btnSprite, int startIdx, int endIdx)
    {
        const float BtnSize = 140f;

        var wrapper = new GameObject($"Group{groupIndex}");
        wrapper.transform.SetParent(parent, false);
        var wLE = wrapper.AddComponent<LayoutElement>();
        wLE.flexibleWidth = 1f;
        var wVlg = wrapper.AddComponent<VerticalLayoutGroup>();
        wVlg.spacing = 12f;
        wVlg.padding = new RectOffset(0, 0, 8, 12);
        wVlg.childForceExpandWidth = true; wVlg.childForceExpandHeight = false;
        wVlg.childControlWidth = true; wVlg.childControlHeight = true;
        var wFitter = wrapper.AddComponent<ContentSizeFitter>();
        wFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        wFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        _lsGroupGridRoots[groupIndex] = wrapper.transform;
        wrapper.SetActive(groupIndex == 0);

        int count   = endIdx - startIdx + 1;
        const int Cols = 4;

        for (int row = 0; row * Cols < count; row++)
        {
            var rowObj = new GameObject($"R{row}");
            rowObj.transform.SetParent(wrapper.transform, false);
            var rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = BtnSize; rowLE.minHeight = BtnSize; rowLE.flexibleWidth = 1f;
            var rowHlg = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowHlg.spacing = 18f;
            rowHlg.padding = new RectOffset(0, 0, 0, 0);
            rowHlg.childForceExpandWidth = false; rowHlg.childForceExpandHeight = false;
            rowHlg.childControlWidth = true; rowHlg.childControlHeight = false;
            rowHlg.childAlignment = TextAnchor.MiddleCenter;

            // Leading flexible spacer
            var lsp = new GameObject("LS"); lsp.transform.SetParent(rowObj.transform, false);
            var lspRT = lsp.AddComponent<RectTransform>(); lspRT.sizeDelta = Vector2.zero;
            var lspLE = lsp.AddComponent<LayoutElement>(); lspLE.flexibleWidth = 1f;

            for (int col = 0; col < Cols; col++)
            {
                int idx = startIdx + row * Cols + col;
                if (idx > endIdx) break;

                // Button cell
                int levelIndex = idx;
                var cell = new GameObject($"L{idx + 1}");
                cell.transform.SetParent(rowObj.transform, false);
                var cellRT = cell.AddComponent<RectTransform>();
                cellRT.sizeDelta = new Vector2(BtnSize, BtnSize);
                var cellLE = cell.AddComponent<LayoutElement>();
                cellLE.preferredWidth = BtnSize; cellLE.minWidth = BtnSize;

                // Shadow
                var sh = new GameObject("S"); sh.transform.SetParent(cell.transform, false);
                var shRect = sh.AddComponent<RectTransform>();
                shRect.anchorMin = new Vector2(0.5f, 0.5f); shRect.anchorMax = new Vector2(0.5f, 0.5f);
                shRect.pivot = new Vector2(0.5f, 0.5f);
                shRect.sizeDelta = new Vector2(BtnSize - 8f, BtnSize - 8f);
                shRect.anchoredPosition = new Vector2(0f, -3f);
                var shImg = sh.AddComponent<Image>();
                shImg.sprite = btnSprite; shImg.color = new Color(0f, 0f, 0f, 0.06f);

                // Button image
                var btnImg = cell.AddComponent<Image>();
                btnImg.sprite = btnSprite;
                btnImg.color = new Color(0.93f, 0.91f, 0.88f, 1f);

                var btn = cell.AddComponent<Button>();
                var bc = btn.colors;
                bc.highlightedColor = new Color(0.97f, 0.97f, 0.97f);
                bc.pressedColor     = new Color(0.86f, 0.86f, 0.86f);
                bc.disabledColor    = new Color(0.88f, 0.88f, 0.88f, 0.85f);
                btn.colors = bc;
                btn.onClick.AddListener(() => FindAnyObjectByType<GameManager>().SelectLevel(levelIndex));

                // Number label
                bool isTriGroup = (groupIndex % 3 == 2);
                var lblObj = new GameObject("N"); lblObj.transform.SetParent(cell.transform, false);
                var lblRect = lblObj.AddComponent<RectTransform>();
                lblRect.anchorMin = new Vector2(0.5f, 0.5f); lblRect.anchorMax = new Vector2(0.5f, 0.5f);
                lblRect.pivot = new Vector2(0.5f, 0.5f);
                lblRect.anchoredPosition = new Vector2(0f, isTriGroup ? 0f : 10f);
                lblRect.sizeDelta = new Vector2(isTriGroup ? 78f : BtnSize - 12f, 40f);
                var lbl = lblObj.AddComponent<Text>();
                lbl.font = defaultFont; lbl.fontStyle = FontStyle.Bold;
                lbl.alignment = TextAnchor.MiddleCenter; lbl.color = TextDark;
                lbl.resizeTextForBestFit = true;
                lbl.resizeTextMinSize = isTriGroup ? 14 : 18;
                lbl.resizeTextMaxSize = isTriGroup ? 24 : 30;
                lbl.fontSize = lbl.resizeTextMaxSize;
                lbl.text = (idx + 1).ToString();

                // Lock label (hidden by default)
                var lockObj = new GameObject("Lock"); lockObj.transform.SetParent(cell.transform, false);
                var lockRect = lockObj.AddComponent<RectTransform>();
                lockRect.anchorMin = new Vector2(0.5f, 0.5f); lockRect.anchorMax = new Vector2(0.5f, 0.5f);
                lockRect.pivot = new Vector2(0.5f, 0.5f);
                lockRect.anchoredPosition = new Vector2(0f, -22f);
                lockRect.sizeDelta = new Vector2(40f, 30f);
                var lockTxt = lockObj.AddComponent<Text>();
                lockTxt.font = defaultFont; lockTxt.text = "🔒"; lockTxt.fontSize = 20;
                lockTxt.alignment = TextAnchor.MiddleCenter; lockTxt.color = TextMuted;
                lockObj.SetActive(false);
                _lsLockLabels[idx] = lockTxt;

                levelSelectButtons[idx]      = btn;
                levelSelectButtonImages[idx] = btnImg;
                levelSelectButtonLabels[idx] = lbl;
            }

            // Trailing flexible spacer
            var rsp = new GameObject("RS"); rsp.transform.SetParent(rowObj.transform, false);
            var rspRT = rsp.AddComponent<RectTransform>(); rspRT.sizeDelta = Vector2.zero;
            var rspLE = rsp.AddComponent<LayoutElement>(); rspLE.flexibleWidth = 1f;
        }
    }

    private void LsSwitchGroupTab(int g)
    {
        _lsActiveGroup = g;

        // Lazy-build this group's grid the first time its tab is selected
        if (_lsGroupGridRoots[g] == null && _lsContentParent != null)
        {
            LsBuildGroupGrid(_lsContentParent, g, _lsGroupSpritesLazy[g], _lsGroupStart[g], _lsGroupEnd[g]);
            if (_lsTotalLevels > 0)
                RefreshLevelSelectButtons(_lsCurrentIdx, _lsHighestUnlocked, _lsTotalLevels);
        }
        bool tabsDark = ThemeManager.Instance?.IsDarkMode ?? false;
        Color tabIdleBg = tabsDark ? new Color(0.18f, 0.17f, 0.22f, 1f) : Color.white;
        for (int i = 0; i < GroupCount; i++)
        {
            bool active = (i == g);
            if (_lsGroupTabBgs[i] != null)
                _lsGroupTabBgs[i].color = active ? GrpAccent[i] : tabIdleBg;
            if (_lsGroupTabIconChips[i] != null)
                _lsGroupTabIconChips[i].color = active
                    ? new Color(1f, 1f, 1f, 0.22f)
                    : new Color(GrpAccent[i].r, GrpAccent[i].g, GrpAccent[i].b, tabsDark ? 0.26f : 0.14f);
            if (_lsGroupTabIcons[i] != null)
                _lsGroupTabIcons[i].color = active ? Color.white : GrpAccent[i];
            if (_lsGroupTabRanges[i] != null)
                _lsGroupTabRanges[i].color = active
                    ? new Color(1f, 1f, 1f, 0.92f)
                    : (tabsDark ? new Color(0.60f, 0.58f, 0.56f) : TextMuted);
            if (_lsGroupGridRoots[i] != null) _lsGroupGridRoots[i].gameObject.SetActive(active);
        }
        LsRefreshProgressBar(g);
        // Scroll to top when switching tabs
        if (levelSelectScrollRect != null)
            levelSelectScrollRect.verticalNormalizedPosition = 1f;
    }

    private void LsRefreshProgressBar(int g)
    {
        int groupStart    = GrpStart[g];
        int groupSize     = GrpEnd[g] - GrpStart[g] + 1;
        int completed     = Mathf.Clamp(_lsHighestUnlocked - groupStart, 0, groupSize);
        float fillFraction = Mathf.Clamp01((float)completed / groupSize);

        if (_lsProgressCountText != null)
            _lsProgressCountText.text = $"{completed}/{groupSize}";
        if (_lsProgressBarFill != null)
        {
            var fillRect = _lsProgressBarFill.GetComponent<RectTransform>();
            fillRect.anchorMax = new Vector2(fillFraction, 1f);
            _lsProgressBarFill.color = GrpAccent[g];
        }
    }

    private void RefreshLevelSelectButtons(int currentLevelIndex, int highestUnlockedLevelIndex, int totalLevels)
    {
        var tm = ThemeManager.Instance;
        bool dark = tm?.IsDarkMode ?? false;

        // Per-group palette: unlocked, current, locked
        Color darkU = new Color(0.22f, 0.21f, 0.27f, 1f);
        Color darkL = new Color(0.15f, 0.14f, 0.19f, 1f);
        Color lightL = new Color(0.90f, 0.88f, 0.85f, 1f);
        Color[] colU = dark
            ? new Color[] { darkU, darkU, darkU, darkU, darkU, darkU }
            : new Color[] {
                new Color(1.00f, 0.93f, 0.76f, 1f),  // square
                new Color(0.80f, 0.96f, 0.93f, 1f),  // hexagon
                new Color(0.98f, 0.82f, 0.90f, 1f),  // triangle
                new Color(0.80f, 0.88f, 0.99f, 1f),  // seq square
                new Color(0.84f, 0.95f, 0.82f, 1f),  // seq hexagon
                new Color(0.90f, 0.84f, 0.98f, 1f),  // seq triangle
            };
        Color[] colC = GrpAccent;   // current level uses its group's accent color
        Color[] colL = dark
            ? new Color[] { darkL, darkL, darkL, darkL, darkL, darkL }
            : new Color[] { lightL, lightL, lightL, lightL, lightL, lightL };
        Color txtPrimary = tm?.TextPrimary ?? TextDark;
        Color txtMuted   = tm?.TextMuted   ?? TextMuted;

        for (int i = 0; i < levelSelectButtons.Length; i++)
        {
            if (levelSelectButtons[i] == null) continue;
            bool exists    = i < totalLevels;
            bool unlocked  = exists && i <= highestUnlockedLevelIndex;
            bool isCurrent = i == currentLevelIndex;

            levelSelectButtons[i].gameObject.SetActive(exists);
            if (!exists) continue;

            levelSelectButtons[i].interactable = unlocked;
            levelSelectButtonLabels[i].text = (i + 1).ToString();

            int grp = LsGroupOf(i);

            if (isCurrent)
            {
                levelSelectButtonImages[i].color = colC[grp];
                levelSelectButtonLabels[i].color = Color.white;
            }
            else if (unlocked)
            {
                levelSelectButtonImages[i].color = colU[grp];
                levelSelectButtonLabels[i].color = txtPrimary;
            }
            else
            {
                levelSelectButtonImages[i].color = colL[grp];
                levelSelectButtonLabels[i].color = txtMuted;
            }

            // Lock icon
            if (_lsLockLabels != null && _lsLockLabels[i] != null)
                _lsLockLabels[i].gameObject.SetActive(!unlocked);
        }

        // Refresh progress bar for active tab
        LsRefreshProgressBar(_lsActiveGroup);
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

    // ─────────────────────────────────────────────────────────────────────────────
    // --- Hint Store Popup ---
    // ─────────────────────────────────────────────────────────────────────────────

    public void ShowHintStore(
        Action onWatchAd, Action onBuyStarter, Action onBuyPack5, Action onBuyPack50, Action onBuyPack100,
        Action onBuyNoAds, bool isNoAdsPurchased, string noAdsPrice,
        bool isStarterAvailable, string starterPrice,
        string price5, string price50, string price100, int currentHints)
    {
        if (hintPromoPopup != null) Destroy(hintPromoPopup);

        hintPromoPopup = new GameObject("HintStorePopup");
        hintPromoPopup.transform.SetParent(canvas.transform, false);
        hintPromoPopup.transform.SetAsLastSibling();

        var popupRect = hintPromoPopup.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero; popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero; popupRect.offsetMax = Vector2.zero;

        var overlay = hintPromoPopup.AddComponent<Image>();
        overlay.color = new Color(0.05f, 0.06f, 0.10f, 0.78f);
        var overlayBtn = hintPromoPopup.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(HideHintPromoPopup);

        var tm = ThemeManager.Instance;
        bool dark = tm?.IsDarkMode ?? false;
        Color accent   = new Color(0.13f, 0.59f, 0.63f);
        Color green    = new Color(0.28f, 0.68f, 0.32f);
        Color gold     = new Color(0.96f, 0.68f, 0.10f);
        Color coral    = new Color(0.93f, 0.35f, 0.30f);
        Color cardCol  = dark ? new Color(0.15f, 0.145f, 0.19f, 1f) : new Color(0.972f, 0.965f, 0.955f, 1f);
        Color tileCol  = dark ? new Color(0.22f, 0.21f, 0.27f, 1f) : Color.white;
        Color textCol  = tm != null ? tm.TextPrimary : TextDark;
        Color mutedCol = dark ? new Color(0.62f, 0.60f, 0.66f) : new Color(0.55f, 0.53f, 0.58f);

        // Portrait card: 2 columns × 3 rows of offer tiles
        float canvasW = ((RectTransform)canvas.transform).rect.width;
        float cardW = canvasW > 0f ? Mathf.Min(700f, canvasW - 60f) : 680f;
        const float pad = 28f;

        // Drop-shadow (resized at the end)
        var shadowObj = new GameObject("Shadow");
        shadowObj.transform.SetParent(hintPromoPopup.transform, false);
        var shadowRect = shadowObj.AddComponent<RectTransform>();
        shadowRect.anchorMin = new Vector2(0.5f, 0.5f); shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.anchoredPosition = new Vector2(5f, -10f);
        var shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = SpriteGenerator.RoundedRectSliced;
        shadowImg.type = Image.Type.Sliced;
        shadowImg.color = new Color(0f, 0f, 0f, 0.24f);

        // Main card (height set at the end once content is measured)
        var card = new GameObject("Card");
        card.transform.SetParent(hintPromoPopup.transform, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f); cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(cardW, 900f);
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = SpriteGenerator.RoundedRectSliced;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = cardCol;
        var cardBlock = card.AddComponent<Button>();
        cardBlock.targetGraphic = cardImg;
        cardBlock.transition = Selectable.Transition.None;

        // No close button — tapping the dimmed background closes the store
        float y = -24f;
        float rowW = cardW - pad * 2f;
        Sprite bulbSprite = LoadIconSprite("icons/lightbulb") ?? LoadIconSprite("icons/lightbulb_white");
        Sprite adSprite = LoadIconSprite("icons/adblock") ?? LoadIconSprite("icons/adblock_white");

        // ── Offer tiles (2-column grid) ───────────────────────────────────────────
        var packSection = new GameObject("PackSection");
        packSection.transform.SetParent(card.transform, false);
        var psRT = packSection.AddComponent<RectTransform>();
        psRT.anchorMin = psRT.anchorMax = new Vector2(0f, 1f);
        psRT.pivot = new Vector2(0f, 1f);

        const int cols = 2;
        const float packGap = 20f;      // horizontal gap
        const float rowGap = 32f;       // vertical gap (leaves room for the ribbons)
        const float packH = 290f;
        int packCount = (isStarterAvailable ? 1 : 0) + 3 + 1 + (isNoAdsPurchased ? 0 : 1);
        int rows = (packCount + cols - 1) / cols;
        float packW = (rowW - packGap * (cols - 1)) / cols;
        float gridH = rows * packH + (rows - 1) * rowGap;
        y -= 22f;   // headroom for the first row's ribbons
        psRT.anchoredPosition = new Vector2(pad, y);
        psRT.sizeDelta = new Vector2(rowW, gridH);

        void BuildPackCard(int slot, string title, string subtitle, string priceStr,
                           Sprite iconSpr, Color iconTint, float iconSize,
                           string ribbon, Color ribbonCol, bool highlight,
                           Color btnCol, Action onTap,
                           Color? tileBg = null, Color? titleCol = null, Color? subCol = null,
                           bool featured = false, string iconGlyph = "💡")
        {
            var holder = new GameObject("Pack_" + slot);
            holder.transform.SetParent(packSection.transform, false);
            var hRT = holder.AddComponent<RectTransform>();
            hRT.anchorMin = hRT.anchorMax = new Vector2(0f, 1f);
            hRT.pivot = new Vector2(0.5f, 0.5f);   // centered so the featured pulse scales in place
            int col = slot % cols, row = slot / cols;
            // A lone tile on the last row is centered
            float xOff = (slot == packCount - 1 && packCount % cols == 1) ? (packW + packGap) * 0.5f : 0f;
            hRT.anchoredPosition = new Vector2(col * (packW + packGap) + packW * 0.5f + xOff,
                                               -(row * (packH + rowGap)) - packH * 0.5f);
            hRT.sizeDelta = new Vector2(packW, packH);

            // Featured: soft pulsing glow behind the border
            Image glowImg = null;
            if (featured)
            {
                var glow = new GameObject("Glow");
                glow.transform.SetParent(holder.transform, false);
                var gRT = glow.AddComponent<RectTransform>();
                gRT.anchorMin = Vector2.zero; gRT.anchorMax = Vector2.one;
                gRT.offsetMin = new Vector2(-8f, -8f); gRT.offsetMax = new Vector2(8f, 8f);
                glowImg = glow.AddComponent<Image>();
                glowImg.sprite = SpriteGenerator.RoundedRectSliced;
                glowImg.type = Image.Type.Sliced;
                glowImg.color = new Color(ribbonCol.r, ribbonCol.g, ribbonCol.b, 0.3f);
                glowImg.raycastTarget = false;
            }

            if (highlight)
            {
                var border = new GameObject("Border");
                border.transform.SetParent(holder.transform, false);
                var bRT = border.AddComponent<RectTransform>();
                bRT.anchorMin = Vector2.zero; bRT.anchorMax = Vector2.one;
                bRT.offsetMin = new Vector2(-4f, -4f); bRT.offsetMax = new Vector2(4f, 4f);
                var bImg = border.AddComponent<Image>();
                bImg.sprite = SpriteGenerator.RoundedRectSliced;
                bImg.type = Image.Type.Sliced;
                bImg.color = ribbonCol;
            }

            var tile = new GameObject("Tile");
            tile.transform.SetParent(holder.transform, false);
            var tRT = tile.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;
            var tImg = tile.AddComponent<Image>();
            tImg.sprite = SpriteGenerator.RoundedRectSliced;
            tImg.type = Image.Type.Sliced;
            tImg.color = tileBg ?? tileCol;
            var tBtn = tile.AddComponent<Button>();
            tBtn.targetGraphic = tImg;
            if (onTap != null)
                tBtn.onClick.AddListener(() => { HideHintPromoPopup(); onTap(); });
            else
                tBtn.transition = Selectable.Transition.None;

            // Icon
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(tile.transform, false);
            var iRT = iconGo.AddComponent<RectTransform>();
            iRT.anchorMin = iRT.anchorMax = new Vector2(0.5f, 1f);
            iRT.pivot = new Vector2(0.5f, 1f);
            iRT.anchoredPosition = new Vector2(0f, -26f);
            iRT.sizeDelta = new Vector2(iconSize, iconSize);
            if (iconSpr != null)
            {
                var iImg = iconGo.AddComponent<Image>();
                iImg.sprite = iconSpr; iImg.preserveAspect = true;
                iImg.color = iconTint;
            }
            else
            {
                var iT = iconGo.AddComponent<Text>();
                iT.font = defaultFont; iT.text = iconGlyph; iT.fontSize = (int)(iconSize * 0.8f);
                iT.color = iconTint;
                iT.alignment = TextAnchor.MiddleCenter;
                iT.horizontalOverflow = HorizontalWrapMode.Overflow;
                iT.verticalOverflow = VerticalWrapMode.Overflow;
            }

            // Title (e.g. ×50 or No Ads)
            var cntGo = new GameObject("Title");
            cntGo.transform.SetParent(tile.transform, false);
            var cRT = cntGo.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(1f, 1f);
            cRT.pivot = new Vector2(0.5f, 1f);
            cRT.anchoredPosition = new Vector2(0f, -118f);
            cRT.sizeDelta = new Vector2(-16f, 52f);
            var cT = cntGo.AddComponent<Text>();
            cT.font = defaultFont; cT.text = title;
            cT.fontSize = 42; cT.fontStyle = FontStyle.Bold;
            cT.resizeTextForBestFit = true;
            cT.resizeTextMinSize = 24; cT.resizeTextMaxSize = 42;
            cT.color = titleCol ?? textCol; cT.alignment = TextAnchor.MiddleCenter;

            var subGo = new GameObject("Subtitle");
            subGo.transform.SetParent(tile.transform, false);
            var hlRT = subGo.AddComponent<RectTransform>();
            hlRT.anchorMin = new Vector2(0f, 1f); hlRT.anchorMax = new Vector2(1f, 1f);
            hlRT.pivot = new Vector2(0.5f, 1f);
            hlRT.anchoredPosition = new Vector2(0f, -172f);
            hlRT.sizeDelta = new Vector2(0f, 30f);
            var hlT = subGo.AddComponent<Text>();
            hlT.font = defaultFont; hlT.text = subtitle;
            hlT.fontSize = 23; hlT.color = subCol ?? mutedCol; hlT.alignment = TextAnchor.MiddleCenter;

            // Price button
            var priceBtnGo = new GameObject("PriceBtn");
            priceBtnGo.transform.SetParent(tile.transform, false);
            var pbRT = priceBtnGo.AddComponent<RectTransform>();
            pbRT.anchorMin = new Vector2(0f, 0f); pbRT.anchorMax = new Vector2(1f, 0f);
            pbRT.pivot = new Vector2(0.5f, 0f);
            pbRT.offsetMin = new Vector2(16f, 18f); pbRT.offsetMax = new Vector2(-16f, 18f + 64f);
            var pbImg = priceBtnGo.AddComponent<Image>();
            pbImg.sprite = SpriteGenerator.RoundedRectSliced;
            pbImg.type = Image.Type.Sliced;
            pbImg.color = btnCol;
            var pbBtn = priceBtnGo.AddComponent<Button>();
            pbBtn.targetGraphic = pbImg;
            if (onTap != null)
                pbBtn.onClick.AddListener(() => { HideHintPromoPopup(); onTap(); });
            else
                pbBtn.transition = Selectable.Transition.None;
            var pbTxtObj = new GameObject("Txt");
            pbTxtObj.transform.SetParent(priceBtnGo.transform, false);
            var pbtRT = pbTxtObj.AddComponent<RectTransform>();
            pbtRT.anchorMin = Vector2.zero; pbtRT.anchorMax = Vector2.one;
            pbtRT.offsetMin = Vector2.zero; pbtRT.offsetMax = Vector2.zero;
            var pbT = pbTxtObj.AddComponent<Text>();
            pbT.font = defaultFont; pbT.text = priceStr;
            pbT.fontSize = 28; pbT.fontStyle = FontStyle.Bold;
            pbT.color = Color.white; pbT.alignment = TextAnchor.MiddleCenter;
            pbT.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Ribbon tag centered on the card's top edge
            if (!string.IsNullOrEmpty(ribbon))
            {
                var rb = new GameObject("Ribbon");
                rb.transform.SetParent(holder.transform, false);
                var rbRT = rb.AddComponent<RectTransform>();
                rbRT.anchorMin = rbRT.anchorMax = new Vector2(0.5f, 1f);
                rbRT.pivot = new Vector2(0.5f, 0.5f);
                rbRT.anchoredPosition = new Vector2(0f, 2f);
                rbRT.sizeDelta = new Vector2(Mathf.Min(ribbon.Length * 12f + 32f, packW - 8f), 36f);
                var rbImg = rb.AddComponent<Image>();
                rbImg.sprite = SpriteGenerator.RoundedRectSliced;
                rbImg.type = Image.Type.Sliced;
                rbImg.color = ribbonCol;
                var rbTxtObj = new GameObject("Txt");
                rbTxtObj.transform.SetParent(rb.transform, false);
                var rbtRT = rbTxtObj.AddComponent<RectTransform>();
                rbtRT.anchorMin = Vector2.zero; rbtRT.anchorMax = Vector2.one;
                rbtRT.offsetMin = Vector2.zero; rbtRT.offsetMax = Vector2.zero;
                var rbT = rbTxtObj.AddComponent<Text>();
                rbT.font = defaultFont; rbT.text = ribbon;
                rbT.fontSize = 18; rbT.fontStyle = FontStyle.Bold;
                rbT.color = Color.white; rbT.alignment = TextAnchor.MiddleCenter;
                rbT.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            if (featured)
                StartCoroutine(FeaturedPackPulse(hRT, glowImg));
        }

        Color bulbGold = new Color(1f, 0.80f, 0.20f, 1f);
        Color starterPurple = new Color(0.36f, 0.21f, 0.68f, 1f);
        int nextSlot = 0;
        if (isStarterAvailable)
        {
            // Featured one-time Welcome Deal: purple tile, gold border + pulsing glow
            BuildPackCard(nextSlot++, "×25", "Hints", starterPrice, bulbSprite, bulbGold, 76f,
                "WELCOME DEAL", gold, true, gold, onBuyStarter,
                starterPurple, Color.white, new Color(1f, 1f, 1f, 0.8f), true);
        }
        BuildPackCard(nextSlot++, "×5",   "Hints", price5,   bulbSprite, bulbGold, 58f, "CHEAPEST",   accent,      true,  green, onBuyPack5);
        BuildPackCard(nextSlot++, "×50",  "Hints", price50,  bulbSprite, bulbGold, 68f, "POPULAR",    coral,       true,  green, onBuyPack50);
        BuildPackCard(nextSlot++, "×100", "Hints", price100, bulbSprite, bulbGold, 78f, "BEST VALUE", gold,        true,  green, onBuyPack100);
        Sprite adWhite = LoadIconSprite("icons/adblock_white") ?? adSprite;
        Color noAdsNavy = new Color(0.17f, 0.20f, 0.29f, 1f);
        if (isNoAdsPurchased)
            BuildPackCard(nextSlot++, "No Ads", "Owned", "✓", adWhite, Color.white, 70f,
                null, Color.clear, false, new Color(0.45f, 0.58f, 0.50f, 1f), null,
                noAdsNavy, Color.white, new Color(1f, 1f, 1f, 0.75f));
        else
            BuildPackCard(nextSlot++, "No Ads", "+ Unlimited Hints", noAdsPrice, adWhite, Color.white, 70f,
                "FOREVER", gold, true, gold, onBuyNoAds,
                noAdsNavy, Color.white, new Color(1f, 1f, 1f, 0.75f));

        // Free hint via rewarded ad — same tile style as the packs
        if (!isNoAdsPurchased)
            BuildPackCard(nextSlot++, "+1 Hint", "Watch a short ad", "Watch Ad",
                LoadIconSprite("icons/ad_white"), Color.white, 72f,
                "FREE", accent, false, new Color(0.17f, 0.47f, 0.22f, 1f), onWatchAd,
                green, Color.white, new Color(1f, 1f, 1f, 0.85f));

        y -= gridH + 4f;

        // Resize card to fit content (content is top-anchored; grow downward)
        float used = Mathf.Abs(y) + pad;
        cardRect.sizeDelta = new Vector2(cardW, used);
        shadowRect.sizeDelta = new Vector2(cardW + 26f, used + 26f);
    }

    private System.Collections.IEnumerator FeaturedPackPulse(RectTransform target, Image glow)
    {
        float t = 0f;
        while (target != null)
        {
            t += Time.unscaledDeltaTime;
            float s = (Mathf.Sin(t * 3.2f) + 1f) * 0.5f;   // 0..1
            target.localScale = Vector3.one * (1f + 0.035f * s);
            if (glow != null)
            {
                var c = glow.color; c.a = 0.15f + 0.35f * s; glow.color = c;
            }
            yield return null;
        }
    }

    public void HideHintPromoPopup()
    {
        if (hintPromoPopup != null)
        {
            Destroy(hintPromoPopup);
            hintPromoPopup = null;
        }
    }

    private System.Collections.IEnumerator CartGlowCoroutine()
    {
        var tealColor    = new Color(0.18f, 0.55f, 0.62f, 1f);
        var brightColor  = new Color(0.55f, 0.92f, 1.00f, 1f);
        while (true)
        {
            yield return new WaitForSeconds(60f);
            if (cartIconImage == null) yield break;
            bool dark = ThemeManager.Instance != null && ThemeManager.Instance.IsDarkMode;
            var normalColor = dark ? Color.white : tealColor;
            // Pulse 3 times
            for (int i = 0; i < 3; i++)
            {
                float t = 0f;
                while (t < 0.35f)
                {
                    t += Time.deltaTime;
                    cartIconImage.color = Color.Lerp(normalColor, brightColor, t / 0.35f);
                    yield return null;
                }
                t = 0f;
                while (t < 0.35f)
                {
                    t += Time.deltaTime;
                    cartIconImage.color = Color.Lerp(brightColor, normalColor, t / 0.35f);
                    yield return null;
                }
            }
            cartIconImage.color = normalColor;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────

    // --- Free Hint Badge ---

    public void UpdateHintBadge(int count) => UpdateHintBadge(count.ToString());

    public void UpdateHintBadge(string label)
    {
        if (hintFreeBadgeObj == null) return;
        hintFreeBadgeObj.SetActive(true);
        if (hintFreeBadgeText != null)
            hintFreeBadgeText.text = label;
    }


    // --- Daily Reward Popup ---

    private bool dailyRewardCollecting;

    public void ShowDailyRewardPopup(int streak, int hintsGranted, bool isStreakBonus)
    {
        dailyRewardCollecting = false;
        var tm = ThemeManager.Instance;
        bool dark = tm?.IsDarkMode ?? false;
        Color green    = new Color(0.28f, 0.68f, 0.32f);
        Color teal     = new Color(0.13f, 0.59f, 0.63f);
        Color gold     = new Color(0.96f, 0.68f, 0.10f);
        Color coral    = new Color(0.93f, 0.35f, 0.30f);
        Color purple   = new Color(0.36f, 0.21f, 0.68f);
        Color brown    = new Color(0.22f, 0.11f, 0.02f);
        Color cardCol  = dark ? new Color(0.15f, 0.145f, 0.19f, 1f) : new Color(0.972f, 0.965f, 0.955f, 1f);
        Color tileCol  = dark ? new Color(0.22f, 0.21f, 0.27f, 1f) : Color.white;
        Color textCol  = tm != null ? tm.TextPrimary : TextDark;
        Color mutedCol = dark ? new Color(0.62f, 0.60f, 0.66f) : new Color(0.55f, 0.53f, 0.58f);

        // Dark overlay (absorbs touches)
        var overlay = new GameObject("DailyRewardOverlay");
        overlay.transform.SetParent(safeAreaRect, false);
        overlay.transform.SetAsLastSibling();
        var oRect = overlay.AddComponent<RectTransform>();
        oRect.anchorMin = new Vector2(0f, 0f); oRect.anchorMax = Vector2.one;
        oRect.offsetMin = new Vector2(-400f, -400f); oRect.offsetMax = new Vector2(400f, 400f); // bleed past the safe area
        var oImg = overlay.AddComponent<Image>();
        oImg.color = new Color(0.05f, 0.06f, 0.10f, 0.78f);
        overlay.AddComponent<Button>().transition = Selectable.Transition.None;

        float canvasW = ((RectTransform)canvas.transform).rect.width;
        float cardW = canvasW > 0f ? Mathf.Min(760f, canvasW - 60f) : 740f;
        const float pad = 40f;
        float rowW = cardW - pad * 2f;

        // Shadow + card (height set at the end)
        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(overlay.transform, false);
        var shRT = shadow.AddComponent<RectTransform>();
        shRT.anchorMin = shRT.anchorMax = new Vector2(0.5f, 0.5f);
        shRT.anchoredPosition = new Vector2(5f, -10f);
        var shImg = shadow.AddComponent<Image>();
        shImg.sprite = SpriteGenerator.RoundedRectSliced; shImg.type = Image.Type.Sliced;
        shImg.color = new Color(0f, 0f, 0f, 0.24f);

        var card = new GameObject("DailyRewardCard");
        card.transform.SetParent(overlay.transform, false);
        var cRect = card.AddComponent<RectTransform>();
        cRect.anchorMin = cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.pivot = new Vector2(0.5f, 0.5f);
        var cImg = card.AddComponent<Image>();
        cImg.sprite = SpriteGenerator.RoundedRectSliced; cImg.type = Image.Type.Sliced;
        cImg.color = cardCol;

        RectTransform MakeRect(string n, Transform parent, float x, float yTop, float w, float h)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(x, yTop - h * 0.5f);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }
        Image MakeImg(RectTransform r, Sprite spr, Color c, bool sliced = true)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = spr; img.color = c; img.raycastTarget = false;
            if (sliced) img.type = Image.Type.Sliced;
            return img;
        }
        Text MakeTxt(RectTransform r, string s, int size, FontStyle st, Color c)
        {
            var t = r.gameObject.AddComponent<Text>();
            t.font = defaultFont; t.text = s; t.fontSize = size; t.fontStyle = st;
            t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        Text FillTxt(Transform parent, string s, int size, FontStyle st, Color c)
        {
            var go = new GameObject("Txt");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return MakeTxt(r, s, size, st, c);
        }

        float y = -36f;

        // Title
        MakeTxt(MakeRect("Title", card.transform, 0f, y, rowW, 60f),
            isStreakBonus ? "7-Day Streak Bonus!" : "Daily Reward", 48, FontStyle.Bold, textCol);
        y -= 60f + 16f;

        // Streak badge (highlighted, pulses)
        string streakLabel = streak == 1 ? "DAY 1 · START YOUR STREAK" : $"{streak}-DAY STREAK";
        float badgeW = Mathf.Min(rowW, streakLabel.Length * 19f + 90f);
        var badge = MakeRect("StreakBadge", card.transform, 0f, y, badgeW, 60f);
        MakeImg(badge, SpriteGenerator.RoundedRectSliced, isStreakBonus ? purple : coral);
        var flame = new GameObject("Flame"); flame.transform.SetParent(badge, false);
        var fRT = flame.AddComponent<RectTransform>();
        fRT.anchorMin = fRT.anchorMax = new Vector2(0f, 0.5f);
        fRT.anchoredPosition = new Vector2(34f, 0f); fRT.sizeDelta = new Vector2(40f, 40f);
        MakeTxt(fRT, "★", 34, FontStyle.Bold, new Color(1f, 0.88f, 0.35f));
        var bTxt = FillTxt(badge, streakLabel, 28, FontStyle.Bold, Color.white);
        ((RectTransform)bTxt.transform).offsetMin = new Vector2(40f, 0f);
        y -= 60f + 26f;

        // Gift with a soft glow
        const float giftSize = 210f;
        var glow = MakeRect("GiftGlow", card.transform, 0f, y - 10f, giftSize + 70f, giftSize + 70f);
        var glowImg = MakeImg(glow, SpriteGenerator.Circle, new Color(gold.r, gold.g, gold.b, 0.25f), false);
        var gift = MakeRect("Gift", card.transform, 0f, y + 25f, giftSize, giftSize);
        gift.anchoredPosition = glow.anchoredPosition;
        var giftSpr = LoadIconSprite("gift");
        if (giftSpr != null) { var gImg = MakeImg(gift, giftSpr, Color.white, false); gImg.preserveAspect = true; }
        else MakeTxt(gift, "🎁", 150, FontStyle.Normal, Color.white);
        y -= giftSize + 70f + 6f;

        // Amount
        var amt = MakeRect("Amount", card.transform, 0f, y, rowW, 84f);
        MakeTxt(amt, hintsGranted == 1 ? "+1 Hint" : $"+{hintsGranted} Hints", 70, FontStyle.Bold,
            isStreakBonus ? purple : green);
        y -= 84f + 26f;

        // 7-day streak track
        const float dayGap = 12f;
        const float dayH = 112f;
        float dayW = (rowW - dayGap * 6f) / 7f;
        RectTransform currentDay = null;
        Image currentGlow = null;
        for (int d = 1; d <= 7; d++)
        {
            bool completed = d < streak || (isStreakBonus && d <= 7);
            bool current   = d == streak && !isStreakBonus;
            float x = -rowW * 0.5f + dayW * 0.5f + (d - 1) * (dayW + dayGap);

            var holder = MakeRect($"Day{d}", card.transform, x, y, dayW, dayH);
            Color bg, fg;
            if (completed)      { bg = teal;  fg = Color.white; }
            else if (current)   { bg = gold;  fg = brown; }
            else if (d == 7)    { bg = purple; fg = Color.white; }
            else                { bg = tileCol; fg = mutedCol; }

            if (current)
            {
                var g = new GameObject("Glow"); g.transform.SetParent(holder, false);
                var gRT = g.AddComponent<RectTransform>();
                gRT.anchorMin = Vector2.zero; gRT.anchorMax = Vector2.one;
                gRT.offsetMin = new Vector2(-7f, -7f); gRT.offsetMax = new Vector2(7f, 7f);
                currentGlow = MakeImg(gRT, SpriteGenerator.RoundedRectSliced, new Color(gold.r, gold.g, gold.b, 0.35f));
                currentDay = holder;
            }
            var tile = new GameObject("Tile"); tile.transform.SetParent(holder, false);
            var tRT = tile.AddComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
            tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;
            MakeImg(tRT, SpriteGenerator.RoundedRectSliced, bg);
            if (!completed && !current && d != 7)
            {
                // subtle outline for future days
                var ol = tile.GetComponent<Image>();
                ol.color = dark ? tileCol : new Color(0.93f, 0.92f, 0.90f, 1f);
            }

            var dayLbl = MakeRect("Day", tRT, 0f, -10f, dayW, 26f);
            MakeTxt(dayLbl, $"Day {d}", 19, FontStyle.Bold, new Color(fg.r, fg.g, fg.b, 0.85f));
            var val = MakeRect("Val", tRT, 0f, -44f, dayW, 50f);
            MakeTxt(val, completed ? "✓" : (d == 7 ? "+10" : "+1"),
                completed ? 38 : 30, FontStyle.Bold, fg);
        }
        y -= dayH + 24f;

        // Sub label
        MakeTxt(MakeRect("Sub", card.transform, 0f, y, rowW, 34f),
            isStreakBonus ? "Amazing! You played 7 days in a row!"
                          : (streak >= 6 ? "Come back tomorrow for the +10 bonus!"
                                         : $"Come back tomorrow for day {streak + 1}!"),
            26, FontStyle.Normal, mutedCol);
        y -= 34f + 28f;

        // Collect button
        var btnRT = MakeRect("CollectBtn", card.transform, 0f, y, Mathf.Min(460f, rowW), 88f);
        var btnImg = btnRT.gameObject.AddComponent<Image>();
        btnImg.sprite = SpriteGenerator.RoundedRectSliced; btnImg.type = Image.Type.Sliced;
        btnImg.color = isStreakBonus ? purple : green;
        var btn = btnRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        FillTxt(btnRT, "Collect", 38, FontStyle.Bold, Color.white);
        y -= 88f;

        float cardH = Mathf.Abs(y) + pad;
        cRect.sizeDelta = new Vector2(cardW, cardH);
        shRT.sizeDelta = new Vector2(cardW + 26f, cardH + 26f);

        var confettiColors = new[] { gold, coral, teal, green, purple, new Color(0.98f, 0.55f, 0.75f) };
        bool collected = false;
        btn.onClick.AddListener(() =>
        {
            if (collected) return;
            collected = true;
            btn.interactable = false;
            StartCoroutine(DailyRewardCollect(overlay, card.transform, gift, amt, confettiColors));
        });

        StartCoroutine(DailyRewardIdle(overlay, card.transform, gift, glow, glowImg, badge, currentDay, currentGlow));
    }

    private IEnumerator DailyRewardIdle(GameObject overlay, Transform card, RectTransform gift, RectTransform glow,
                                        Image glowImg, RectTransform badge, RectTransform currentDay, Image currentGlow)
    {
        // Pop-in
        float t = 0f;
        while (t < 0.35f && card != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.35f);
            float e = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
            card.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, e);
            yield return null;
        }
        if (card != null) card.localScale = Vector3.one;

        t = 0f;
        while (overlay != null && gift != null)
        {
            t += Time.unscaledDeltaTime;
            float s = (Mathf.Sin(t * 3.2f) + 1f) * 0.5f;
            if (!dailyRewardCollecting)
            {
                gift.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 5f) * 6f * (0.6f + 0.4f * Mathf.Sin(t * 1.3f)));
                gift.localScale = Vector3.one * (1f + 0.04f * s);
            }
            if (glow != null) glow.localScale = Vector3.one * (0.92f + 0.12f * s);
            if (glowImg != null) { var c = glowImg.color; c.a = 0.15f + 0.2f * s; glowImg.color = c; }
            if (badge != null) badge.localScale = Vector3.one * (1f + 0.05f * s);
            if (currentDay != null) currentDay.localScale = Vector3.one * (1f + 0.06f * s);
            if (currentGlow != null) { var c = currentGlow.color; c.a = 0.2f + 0.4f * s; currentGlow.color = c; }
            yield return null;
        }
    }

    private IEnumerator DailyRewardCollect(GameObject overlay, Transform card, RectTransform gift,
                                           RectTransform amount, Color[] colors)
    {
        if (overlay == null) yield break;
        dailyRewardCollecting = true;   // idle animation stops touching the gift
        HapticManager.Instance?.LevelComplete();
        AudioManager.Instance?.OnLevelComplete();

        // Confetti burst from the gift
        const int count = 90;
        var pieces = new RectTransform[count];
        var vel = new Vector2[count];
        var spin = new float[count];
        var imgs = new Image[count];
        Vector2 origin = gift != null ? gift.anchoredPosition : Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Confetti");
            go.transform.SetParent(card, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = origin;
            bool round = UnityEngine.Random.value < 0.3f;
            float w = UnityEngine.Random.Range(12f, 20f);
            r.sizeDelta = round ? new Vector2(w, w) : new Vector2(w, w * UnityEngine.Random.Range(1.6f, 2.4f));
            r.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            var img = go.AddComponent<Image>();
            img.sprite = round ? SpriteGenerator.Circle : SpriteGenerator.RoundedRect;
            img.color = colors[UnityEngine.Random.Range(0, colors.Length)];
            img.raycastTarget = false;
            float ang = UnityEngine.Random.Range(20f, 160f) * Mathf.Deg2Rad;
            float spd = UnityEngine.Random.Range(700f, 1500f);
            vel[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
            spin[i] = UnityEngine.Random.Range(-720f, 720f);
            pieces[i] = r; imgs[i] = img;
        }

        const float dur = 1.7f;
        float t = 0f;
        while (t < dur)
        {
            if (overlay == null) yield break;
            float dt = Time.unscaledDeltaTime;
            t += dt;

            // Gift: quick squash-pop then shrink away
            if (gift != null)
            {
                float g = Mathf.Clamp01(t / 0.45f);
                float sc = g < 0.35f ? Mathf.Lerp(1f, 1.35f, g / 0.35f) : Mathf.Lerp(1.35f, 0f, (g - 0.35f) / 0.65f);
                gift.localScale = Vector3.one * sc;
                gift.localRotation = Quaternion.Euler(0f, 0f, g * 25f);
            }
            // Amount: bounce
            if (amount != null)
            {
                float a = Mathf.Clamp01(t / 0.5f);
                amount.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(a * Mathf.PI));
            }
            // Confetti physics
            float fade = Mathf.Clamp01((dur - t) / 0.5f);
            for (int i = 0; i < count; i++)
            {
                if (pieces[i] == null) continue;
                vel[i] += new Vector2(-vel[i].x * 1.2f * dt, -2200f * dt);
                pieces[i].anchoredPosition += vel[i] * dt;
                pieces[i].localRotation = Quaternion.Euler(0f, 0f, pieces[i].localEulerAngles.z + spin[i] * dt);
                var c = imgs[i].color; c.a = fade; imgs[i].color = c;
            }
            yield return null;
        }
        if (overlay != null) Destroy(overlay);
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
        shadowRect.sizeDelta = new Vector2(636f, 466f);
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
        cardRect.sizeDelta = new Vector2(610f, 440f);
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = SpriteGenerator.RoundedRect;
        cardImg.color = new Color(1f, 1f, 1f, 0.98f);

        // Emoji
        var emoji = MakeCardText("Emoji", card.transform, new Vector2(0, 158), 52, FontStyle.Normal, TextDark);
        emoji.text = "⭐⭐⭐⭐⭐";
        emoji.fontSize = 44;

        // Title
        var title = MakeCardText("Title", card.transform, new Vector2(0, 88), 42, FontStyle.Bold, TextDark);
        title.text = "Enjoying Puzzle Muzzle?";
        title.GetComponent<RectTransform>().sizeDelta = new Vector2(530f, 60f);

        // Subtitle
        var sub = MakeCardText("Subtitle", card.transform, new Vector2(0, 22), 28, FontStyle.Normal, TextMuted);
        sub.text = "A quick rating helps us a lot!";
        sub.GetComponent<RectTransform>().sizeDelta = new Vector2(530f, 50f);

        // Rate button
        var rateBtn = CreateCardButton("⭐  Rate Now", card.transform, new Vector2(0, -65), BtnTeal);
        rateBtn.onClick.AddListener(() => { HideRatePopup(); onRate?.Invoke(); });

        // Dismiss button
        var dismissBtn = CreateCardButton("Not Now", card.transform, new Vector2(0, -157), new Color(0.88f, 0.86f, 0.84f));
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
            sub.text = "Ad-free forever + unlimited hints";
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

    private void ShowSettingsPopup()
    {
        var tm = ThemeManager.Instance;
        bool dark = tm?.IsDarkMode ?? false;
        Color accent    = new Color(0.16f, 0.62f, 0.66f);
        Color cardColor = tm != null ? tm.CardBg : new Color(1f, 1f, 1f, 0.99f);
        Color textColor = tm != null ? tm.TextPrimary : TextDark;
        Color groupBg   = dark ? new Color(0.24f, 0.23f, 0.29f) : new Color(0.96f, 0.955f, 0.975f);
        Color sectionCol= dark ? new Color(0.56f, 0.54f, 0.62f) : new Color(0.55f, 0.53f, 0.58f);
        Color divCol    = dark ? new Color(0.31f, 0.29f, 0.37f) : new Color(0.90f, 0.89f, 0.92f);

        const float cardW = 600f, cardH = 800f;
        const float pad = 32f;
        float leftX = -cardW * 0.5f + pad;     // left content edge (card-center coords)
        float groupW = cardW - pad * 2f;

        var popup = new GameObject("SettingsPopup");
        popup.transform.SetParent(canvas.transform, false);
        popup.transform.SetAsLastSibling();

        var popupRect = popup.AddComponent<RectTransform>();
        popupRect.anchorMin = Vector2.zero;
        popupRect.anchorMax = Vector2.one;
        popupRect.offsetMin = Vector2.zero;
        popupRect.offsetMax = Vector2.zero;

        var overlay = popup.AddComponent<Image>();
        overlay.color = new Color(0.08f, 0.07f, 0.11f, 0.66f);
        var overlayBtn = popup.AddComponent<Button>();
        overlayBtn.targetGraphic = overlay;
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(() => Destroy(popup));

        // Soft drop shadow
        var shadowObj = new GameObject("Shadow");
        shadowObj.transform.SetParent(popup.transform, false);
        var shadowRect = shadowObj.AddComponent<RectTransform>();
        shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
        shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.sizeDelta = new Vector2(cardW + 30f, cardH + 30f);
        shadowRect.anchoredPosition = new Vector2(0f, -10f);
        var shadowImg = shadowObj.AddComponent<Image>();
        shadowImg.sprite = SpriteGenerator.RoundedRectSliced;
        shadowImg.type = Image.Type.Sliced;
        shadowImg.color = new Color(0f, 0f, 0f, 0.22f);

        // Card
        var card = new GameObject("Card");
        card.transform.SetParent(popup.transform, false);
        var cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(cardW, cardH);
        var cardImg = card.AddComponent<Image>();
        cardImg.sprite = SpriteGenerator.RoundedRectSliced;
        cardImg.type = Image.Type.Sliced;
        cardImg.color = cardColor;
        var cardBlock = card.AddComponent<Button>();   // swallow taps on the card
        cardBlock.targetGraphic = cardImg;
        cardBlock.transition = Selectable.Transition.None;

        float top = cardH * 0.5f;

        // ── Header: left-aligned title + close ──────────────────────────────────
        var title = MakeLeftText(card.transform, new Vector2(leftX + 6f, top - 54f), new Vector2(420f, 50f),
            "Settings", 38, FontStyle.Bold, textColor);
        title.alignment = TextAnchor.MiddleLeft;
        MakeCloseButton(card.transform, Vector2.zero, cardW, cardH, accent, () => Destroy(popup));

        // ── Section: GENERAL ────────────────────────────────────────────────────
        MakeSectionLabel(card.transform, top - 132f, leftX + 6f, groupW, "GENERAL", sectionCol);
        var g1 = MakeListGroup(card.transform, 134f, groupW, 216f, groupBg);

        bool soundOn = !(AudioManager.Instance?.IsMuted ?? false);
        AddListToggleRow(g1.transform, 72f, groupW, "🔊  Sound", soundOn, textColor, dark, (val) =>
        {
            AudioManager.Instance?.SetMuted(!val);
        });
        AddListDivider(g1.transform, 36f, groupW - 52f, divCol);

        bool hapticOn = HapticManager.Instance?.IsHapticsEnabled ?? true;
        AddListToggleRow(g1.transform, 0f, groupW, "📳  Haptics", hapticOn, textColor, dark, (val) =>
        {
            if (HapticManager.Instance != null) HapticManager.Instance.IsHapticsEnabled = val;
        });
        AddListDivider(g1.transform, -36f, groupW - 52f, divCol);

        AddListToggleRow(g1.transform, -72f, groupW, "🌙  Dark Mode", dark, textColor, dark, (val) =>
        {
            if (ThemeManager.Instance != null) ThemeManager.Instance.IsDarkMode = val;
            // Rebuild so every element re-reads the new palette.
            Destroy(popup);
            ShowSettingsPopup();
        });

        // ── Section: MORE ───────────────────────────────────────────────────────
        MakeSectionLabel(card.transform, -8f, leftX + 6f, groupW, "MORE", sectionCol);
        var g2 = MakeListGroup(card.transform, -108f, groupW, 156f, groupBg);
        AddListNavRow(g2.transform, 39f, groupW, "🤝  Invite Friends", textColor, sectionCol, () =>
        {
            Destroy(popup);
            var gm = FindAnyObjectByType<GameManager>();
            if (gm == null) return;
            ShowInvitePopup(
                gm.GetReferralCode(),
                (code) => gm.ClaimReferralCode(code, (ok, msg) => ShowToast(msg))
            );
        });
        AddListDivider(g2.transform, 0f, groupW - 52f, divCol);
        AddListNavRow(g2.transform, -39f, groupW, "Restore Purchases", textColor, sectionCol, () =>
        {
            Destroy(popup);
            FindAnyObjectByType<GameManager>()?.RestoreNoAdsPurchases();
        });

        // ── Section: DAILY STREAK ───────────────────────────────────────────────
        int streak = PlayerPrefs.GetInt("daily.streak", 0);
        if (streak == 0) streak = 1; // show at least day 1
        string lastClaim = PlayerPrefs.GetString("daily.lastClaim", "");
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        bool claimedToday = lastClaim == today;

        MakeSectionLabel(card.transform, -218f, leftX + 6f, groupW,
            claimedToday ? $"DAILY STREAK · DAY {streak}/7 ✓" : $"DAILY STREAK · DAY {streak}/7", sectionCol);
        var g3 = MakeListGroup(card.transform, -300f, groupW, 120f, groupBg);

        float dotSpacing = 70f;
        float dotsStartX = -(dotSpacing * 3);
        for (int d = 1; d <= 7; d++)
        {
            var dot = new GameObject($"D{d}"); dot.transform.SetParent(g3.transform, false);
            var dR = dot.AddComponent<RectTransform>();
            dR.anchorMin = dR.anchorMax = new Vector2(0.5f, 0.5f);
            dR.pivot = new Vector2(0.5f, 0.5f);
            dR.anchoredPosition = new Vector2(dotsStartX + (d - 1) * dotSpacing, 0f);
            dR.sizeDelta = new Vector2(48f, 48f);
            var dI = dot.AddComponent<Image>(); dI.sprite = SpriteGenerator.Circle;
            bool done    = claimedToday ? d <= streak : d < streak;
            bool current = !claimedToday && d == streak;
            dI.color = done    ? new Color(0.16f, 0.70f, 0.56f) :
                       current ? new Color(1.00f, 0.78f, 0.12f) :
                                 (dark ? new Color(0.32f, 0.30f, 0.38f) : new Color(0.86f, 0.84f, 0.90f));
            var dTGo = new GameObject("N"); dTGo.transform.SetParent(dot.transform, false);
            var dTR = dTGo.AddComponent<RectTransform>();
            dTR.anchorMin = Vector2.zero; dTR.anchorMax = Vector2.one;
            dTR.offsetMin = Vector2.zero; dTR.offsetMax = Vector2.zero;
            var dT = dTGo.AddComponent<Text>();
            dT.font = defaultFont; dT.text = d == 7 ? "★" : d.ToString();
            dT.fontSize = d == 7 ? 24 : 21; dT.fontStyle = FontStyle.Bold;
            dT.color = (done || current) ? Color.white
                       : (dark ? new Color(0.52f, 0.48f, 0.58f) : new Color(0.60f, 0.56f, 0.66f));
            dT.alignment = TextAnchor.MiddleCenter;
            dT.horizontalOverflow = HorizontalWrapMode.Overflow;
            dT.verticalOverflow   = VerticalWrapMode.Overflow;
        }
    }

    // ── Shared popup chrome ────────────────────────────────────────────────────
    // Circular "✕" close button anchored to the card's top-right corner.
    private Button MakeCloseButton(Transform card, Vector2 _ignored, float cardW, float cardH, Color accent, System.Action onClose)
    {
        var go = new GameObject("CloseBtn");
        go.transform.SetParent(card, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-22f, -22f);
        r.sizeDelta = new Vector2(52f, 52f);
        var img = go.AddComponent<Image>();
        img.sprite = SpriteGenerator.Circle;
        img.color = new Color(accent.r, accent.g, accent.b, 0.16f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClose?.Invoke());

        var x = new GameObject("X"); x.transform.SetParent(go.transform, false);
        var xr = x.AddComponent<RectTransform>();
        xr.anchorMin = Vector2.zero; xr.anchorMax = Vector2.one;
        xr.offsetMin = Vector2.zero; xr.offsetMax = Vector2.zero;
        var xt = x.AddComponent<Text>();
        xt.font = defaultFont; xt.text = "✕";
        xt.fontSize = 30; xt.fontStyle = FontStyle.Bold;
        xt.color = accent; xt.alignment = TextAnchor.MiddleCenter;
        return btn;
    }

    // ── Candy / game-theme building blocks ──────────────────────────────────────
    // The game board is made of chunky, softly-shadowed rounded tiles. These helpers
    // give popups the same tactile look: constant-radius corners (9-slice) + a soft
    // drop shadow + a subtle press-down tint.

    private static readonly Color CandyBlue   = new Color(0.39f, 0.52f, 0.88f);
    private static readonly Color CandyCoral  = new Color(0.91f, 0.57f, 0.39f);
    private static readonly Color CandyGreen  = new Color(0.42f, 0.71f, 0.44f);
    private static readonly Color CandyPurple = new Color(0.64f, 0.40f, 0.71f);
    private static readonly Color CandyTeal   = new Color(0.20f, 0.72f, 0.68f);
    private static readonly Color CandyCream  = new Color(0.975f, 0.965f, 0.94f);
    private static readonly Color CandyInk    = new Color(0.22f, 0.22f, 0.28f);

    // Apply the 9-sliced rounded sprite (constant chunky corners) to an Image.
    private Image Sliced(Image img, Color c)
    {
        img.sprite = SpriteGenerator.RoundedRectSliced;
        img.type = Image.Type.Sliced;
        img.color = c;
        return img;
    }

    // A rounded, softly-shadowed "candy" button matching the game tiles.
    // Self-contained: a container holds a shadow + coloured face + label, so toggling
    // the returned Button's GameObject (SetActive) or .interactable affects the whole
    // thing. targetGraphic is the face, so it tints on press.
    private Button CreateCandyButton(string label, Transform parent, Vector2 pos, Vector2 size,
                                     Color color, int fontSize = 30, Color? textColor = null)
    {
        var go = new GameObject(label + "Btn");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        // Shadow (child 1 → behind the face), shifted down for a soft drop
        var sh = new GameObject("Shadow");
        sh.transform.SetParent(go.transform, false);
        var shRT = sh.AddComponent<RectTransform>();
        shRT.anchorMin = new Vector2(0f, 0f); shRT.anchorMax = new Vector2(1f, 1f);
        shRT.offsetMin = new Vector2(0f, -7f); shRT.offsetMax = new Vector2(0f, -7f);
        var shImg = sh.AddComponent<Image>();
        shImg.sprite = SpriteGenerator.RoundedRectSliced;
        shImg.type = Image.Type.Sliced;
        shImg.color = new Color(0f, 0f, 0f, 0.14f);
        shImg.raycastTarget = false;

        // Face (child 2 → on top)
        var face = new GameObject("Face");
        face.transform.SetParent(go.transform, false);
        var faceRT = face.AddComponent<RectTransform>();
        faceRT.anchorMin = Vector2.zero; faceRT.anchorMax = Vector2.one;
        faceRT.offsetMin = Vector2.zero; faceRT.offsetMax = Vector2.zero;
        var img = face.AddComponent<Image>();
        img.sprite = SpriteGenerator.RoundedRectSliced;
        img.type = Image.Type.Sliced;
        img.color = color;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
        cb.pressedColor     = new Color(0.86f, 0.86f, 0.86f);
        cb.selectedColor    = Color.white;
        cb.fadeDuration     = 0.08f;
        btn.colors = cb;

        var txtObj = new GameObject("Text");
        txtObj.transform.SetParent(face.transform, false);
        var tRect = txtObj.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;
        var txt = txtObj.AddComponent<Text>();
        txt.font = defaultFont; txt.text = label;
        txt.fontSize = fontSize; txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = textColor ?? Color.white;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        return btn;
    }

    // ── List / sheet style helpers ─────────────────────────────────────────────
    // Left-aligned text block (pivot on the left edge).
    private Text MakeLeftText(Transform parent, Vector2 pos, Vector2 size, string text, int fontSize, FontStyle style, Color col)
    {
        var go = new GameObject("Txt"); go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.font = defaultFont; t.text = text; t.fontSize = fontSize; t.fontStyle = style;
        t.color = col; t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // Small uppercase section label.
    private void MakeSectionLabel(Transform parent, float y, float leftX, float width, string text, Color col)
    {
        var go = new GameObject("Section"); go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = new Vector2(leftX, y); r.sizeDelta = new Vector2(width, 26f);
        var t = go.AddComponent<Text>();
        t.font = defaultFont; t.text = text; t.fontSize = 20; t.fontStyle = FontStyle.Bold;
        t.color = col; t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    // Rounded container that backs a group of list rows.
    private GameObject MakeListGroup(Transform parent, float centerY, float width, float height, Color color)
    {
        var go = new GameObject("Group"); go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, centerY); r.sizeDelta = new Vector2(width, height);
        var img = go.AddComponent<Image>();
        img.sprite = SpriteGenerator.RoundedRectSliced; img.type = Image.Type.Sliced; img.color = color;
        return go;
    }

    // Hairline divider between rows (inset from the left to align under labels).
    private void AddListDivider(Transform group, float localY, float width, Color col)
    {
        var go = new GameObject("Div"); go.transform.SetParent(group, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(20f, localY); r.sizeDelta = new Vector2(width, 2f);
        go.AddComponent<Image>().color = col;
    }

    // A list row: label on the left, iOS-style sliding switch on the right. No row background.
    private void AddListToggleRow(Transform group, float localY, float rowW, string label, bool initialState,
                                  Color labelCol, bool dark, System.Action<bool> onChange)
    {
        const float rowH = 72f;
        Color onCol  = CandyTeal;
        Color offCol = dark ? new Color(0.40f, 0.38f, 0.46f) : new Color(0.80f, 0.78f, 0.74f);

        var row = new GameObject("Row_" + label); row.transform.SetParent(group, false);
        var rr = row.AddComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f); rr.pivot = new Vector2(0.5f, 0.5f);
        rr.anchoredPosition = new Vector2(0f, localY); rr.sizeDelta = new Vector2(rowW, rowH);
        var rbg = row.AddComponent<Image>(); rbg.color = new Color(0f, 0f, 0f, 0f); // transparent hit area

        var lo = new GameObject("L"); lo.transform.SetParent(row.transform, false);
        var lr = lo.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 1f);
        lr.offsetMin = new Vector2(26f, 0f); lr.offsetMax = new Vector2(-120f, 0f);
        var lt = lo.AddComponent<Text>();
        lt.font = defaultFont; lt.text = label; lt.fontSize = 31;
        lt.alignment = TextAnchor.MiddleLeft; lt.color = labelCol;

        var track = new GameObject("Track"); track.transform.SetParent(row.transform, false);
        var tr = track.AddComponent<RectTransform>();
        tr.anchorMin = tr.anchorMax = new Vector2(1f, 0.5f); tr.pivot = new Vector2(1f, 0.5f);
        tr.anchoredPosition = new Vector2(-22f, 0f); tr.sizeDelta = new Vector2(90f, 50f);
        var ti = track.AddComponent<Image>(); ti.sprite = SpriteGenerator.RoundedRectSliced; ti.type = Image.Type.Sliced;
        ti.color = initialState ? onCol : offCol;

        var knob = new GameObject("Knob"); knob.transform.SetParent(track.transform, false);
        var kr = knob.AddComponent<RectTransform>();
        kr.anchorMin = kr.anchorMax = new Vector2(0f, 0.5f); kr.pivot = new Vector2(0.5f, 0.5f);
        kr.sizeDelta = new Vector2(40f, 40f);
        const float knobOn = 64f, knobOff = 26f;
        kr.anchoredPosition = new Vector2(initialState ? knobOn : knobOff, 0f);
        var ki = knob.AddComponent<Image>(); ki.sprite = SpriteGenerator.Circle; ki.color = Color.white;

        bool state = initialState;
        System.Action toggle = () =>
        {
            state = !state;
            ti.color = state ? onCol : offCol;
            kr.anchoredPosition = new Vector2(state ? knobOn : knobOff, 0f);
            onChange(state);
        };
        var tb = track.AddComponent<Button>(); tb.targetGraphic = ti; tb.onClick.AddListener(() => toggle());
        var rb = row.AddComponent<Button>(); rb.targetGraphic = rbg; rb.transition = Selectable.Transition.None;
        rb.onClick.AddListener(() => toggle());
    }

    // A tappable list row with a trailing chevron (navigation affordance).
    private void AddListNavRow(Transform group, float localY, float rowW, string label,
                               Color labelCol, Color chevCol, System.Action onClick)
    {
        const float rowH = 78f;
        var row = new GameObject("Nav_" + label); row.transform.SetParent(group, false);
        var rr = row.AddComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f); rr.pivot = new Vector2(0.5f, 0.5f);
        rr.anchoredPosition = new Vector2(0f, localY); rr.sizeDelta = new Vector2(rowW, rowH);
        var rbg = row.AddComponent<Image>(); rbg.color = new Color(0f, 0f, 0f, 0f);

        var lo = new GameObject("L"); lo.transform.SetParent(row.transform, false);
        var lr = lo.AddComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 1f);
        lr.offsetMin = new Vector2(26f, 0f); lr.offsetMax = new Vector2(-58f, 0f);
        var lt = lo.AddComponent<Text>();
        lt.font = defaultFont; lt.text = label; lt.fontSize = 31;
        lt.alignment = TextAnchor.MiddleLeft; lt.color = labelCol;
        lt.horizontalOverflow = HorizontalWrapMode.Overflow;

        var ch = new GameObject("Chev"); ch.transform.SetParent(row.transform, false);
        var cr = ch.AddComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = new Vector2(1f, 0.5f); cr.pivot = new Vector2(1f, 0.5f);
        cr.anchoredPosition = new Vector2(-24f, 0f); cr.sizeDelta = new Vector2(40f, 44f);
        var ct = ch.AddComponent<Text>();
        ct.font = defaultFont; ct.text = "›"; ct.fontSize = 42; ct.fontStyle = FontStyle.Bold;
        ct.color = chevCol; ct.alignment = TextAnchor.MiddleRight;
        ct.horizontalOverflow = HorizontalWrapMode.Overflow; ct.verticalOverflow = VerticalWrapMode.Overflow;

        var rb = row.AddComponent<Button>(); rb.targetGraphic = rbg; rb.transition = Selectable.Transition.None;
        rb.onClick.AddListener(() => onClick?.Invoke());
    }

    private void ShowGameCenterMenu()
    {
        ShowSettingsPopup();
    }


    public bool IsLevelSelectVisible => levelSelectPanel != null && levelSelectPanel.activeSelf;

    public void ShowLevelSelect(int currentLevelIndex, int highestUnlockedLevelIndex, int totalLevels)
    {
        if (levelSelectPanel == null)
            CreateLevelSelectPanel();

        _lsCurrentIdx       = currentLevelIndex;
        _lsHighestUnlocked  = highestUnlockedLevelIndex;
        _lsTotalLevels      = totalLevels;

        if (_lsHeaderTitle != null)
            _lsHeaderTitle.text = $"Level {currentLevelIndex + 1} / {totalLevels}";

        int targetGroup = LsGroupOf(currentLevelIndex);
        LsSwitchGroupTab(targetGroup);

        RefreshLevelSelectButtons(currentLevelIndex, highestUnlockedLevelIndex, totalLevels);
        levelSelectPanel.SetActive(true);
        StartCoroutine(ScrollToCurrentLevelButton(currentLevelIndex));
    }

    // ── Timed offer banner (slides down from the top) ─────────────────────────
    private GameObject offerBanner;

    public bool IsOfferBannerVisible => offerBanner != null;

    /// <summary>True when any full-screen popup/panel/overlay is on screen (gameplay is not the focus).</summary>
    public bool IsModalOpen
    {
        get
        {
            if (IsLevelSelectVisible) return true;
            if (canvas == null) return false;
            foreach (Transform child in canvas.transform)
                if (IsModalChild(child)) return true;
            if (safeAreaRect != null)
                foreach (Transform child in safeAreaRect)
                    if (IsModalChild(child)) return true;
            return false;
        }
    }

    private static bool IsModalChild(Transform child)
    {
        if (!child.gameObject.activeSelf) return false;
        string n = child.name;
        return n.EndsWith("Popup") || n.EndsWith("Panel") || n.EndsWith("Overlay");
    }

    public void ShowOfferBanner(string iconPath, Color iconTint, Color bg,
                                string title, string sub, string priceText,
                                Color pillCol, Color pillTextCol, Action onBuy)
    {
        HideOfferBanner();

        const float bannerH = 132f;
        const float margin = 24f;
        Transform parent = safeAreaRect != null ? (Transform)safeAreaRect : canvas.transform;

        offerBanner = new GameObject("OfferBanner");
        offerBanner.transform.SetParent(parent, false);
        offerBanner.transform.SetAsLastSibling();
        var rt = offerBanner.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(margin, 0f); rt.offsetMax = new Vector2(-margin, 0f);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, bannerH);
        rt.anchoredPosition = new Vector2(0f, bannerH + 40f);   // start off-screen

        // Shadow
        var sh = new GameObject("Shadow");
        sh.transform.SetParent(offerBanner.transform, false);
        var shRT = sh.AddComponent<RectTransform>();
        shRT.anchorMin = Vector2.zero; shRT.anchorMax = Vector2.one;
        shRT.offsetMin = new Vector2(-4f, -12f); shRT.offsetMax = new Vector2(4f, 0f);
        var shImg = sh.AddComponent<Image>();
        shImg.sprite = SpriteGenerator.RoundedRectSliced; shImg.type = Image.Type.Sliced;
        shImg.color = new Color(0f, 0f, 0f, 0.25f); shImg.raycastTarget = false;

        // Body (tap anywhere = buy)
        var body = new GameObject("Body");
        body.transform.SetParent(offerBanner.transform, false);
        var bRT = body.AddComponent<RectTransform>();
        bRT.anchorMin = Vector2.zero; bRT.anchorMax = Vector2.one;
        bRT.offsetMin = Vector2.zero; bRT.offsetMax = Vector2.zero;
        var bImg = body.AddComponent<Image>();
        bImg.sprite = SpriteGenerator.RoundedRectSliced; bImg.type = Image.Type.Sliced;
        bImg.color = bg;
        var bBtn = body.AddComponent<Button>();
        bBtn.targetGraphic = bImg;
        bBtn.onClick.AddListener(() => { HideOfferBanner(); onBuy?.Invoke(); });

        var shine = new GameObject("Shine");
        shine.transform.SetParent(body.transform, false);
        var snRT = shine.AddComponent<RectTransform>();
        snRT.anchorMin = new Vector2(0f, 0.55f); snRT.anchorMax = Vector2.one;
        snRT.offsetMin = new Vector2(6f, 0f); snRT.offsetMax = new Vector2(-6f, -6f);
        var snImg = shine.AddComponent<Image>();
        snImg.sprite = SpriteGenerator.RoundedRectSliced; snImg.type = Image.Type.Sliced;
        snImg.color = new Color(1f, 1f, 1f, 0.07f); snImg.raycastTarget = false;

        // Icon
        var icon = new GameObject("Icon");
        icon.transform.SetParent(body.transform, false);
        var iRT = icon.AddComponent<RectTransform>();
        iRT.anchorMin = iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(24f, 0f);
        iRT.sizeDelta = new Vector2(64f, 64f);
        var iSpr = LoadIconSprite(iconPath);
        if (iSpr != null)
        {
            var iImg = icon.AddComponent<Image>();
            iImg.sprite = iSpr; iImg.preserveAspect = true; iImg.color = iconTint;
            iImg.raycastTarget = false;
        }

        // Title + subtitle
        Text MakeLabel(string n, string txt, int size, FontStyle style, Color col, float yOff, float h)
        {
            var go = new GameObject(n);
            go.transform.SetParent(body.transform, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 0.5f); r.anchorMax = new Vector2(1f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.offsetMin = new Vector2(108f, yOff - h * 0.5f); r.offsetMax = new Vector2(-196f, yOff + h * 0.5f);
            var t = go.AddComponent<Text>();
            t.font = defaultFont; t.text = txt; t.fontSize = size; t.fontStyle = style;
            t.color = col; t.alignment = TextAnchor.MiddleLeft;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = size - 8; t.resizeTextMaxSize = size;
            t.raycastTarget = false;
            return t;
        }
        MakeLabel("Title", title, 32, FontStyle.Bold, Color.white, 17f, 40f);
        MakeLabel("Sub", sub, 22, FontStyle.Normal, new Color(1f, 1f, 1f, 0.8f), -20f, 30f);

        // Price pill
        var pill = new GameObject("Pill");
        pill.transform.SetParent(body.transform, false);
        var pRT = pill.AddComponent<RectTransform>();
        pRT.anchorMin = pRT.anchorMax = new Vector2(1f, 0.5f);
        pRT.pivot = new Vector2(1f, 0.5f);
        pRT.anchoredPosition = new Vector2(-22f, 0f);
        pRT.sizeDelta = new Vector2(150f, 62f);
        var pImg = pill.AddComponent<Image>();
        pImg.sprite = SpriteGenerator.RoundedRectSliced; pImg.type = Image.Type.Sliced;
        pImg.color = pillCol; pImg.raycastTarget = false;
        var pTxt = new GameObject("Txt");
        pTxt.transform.SetParent(pill.transform, false);
        var ptRT = pTxt.AddComponent<RectTransform>();
        ptRT.anchorMin = Vector2.zero; ptRT.anchorMax = Vector2.one;
        ptRT.offsetMin = Vector2.zero; ptRT.offsetMax = Vector2.zero;
        var pT = pTxt.AddComponent<Text>();
        pT.font = defaultFont; pT.text = priceText; pT.fontSize = 28; pT.fontStyle = FontStyle.Bold;
        pT.color = pillTextCol; pT.alignment = TextAnchor.MiddleCenter;
        pT.horizontalOverflow = HorizontalWrapMode.Overflow; pT.raycastTarget = false;

        var banner = offerBanner;
        StartCoroutine(OfferBannerRoutine(banner));
    }

    public void HideOfferBanner()
    {
        if (offerBanner != null) Destroy(offerBanner);
        offerBanner = null;
    }

    private IEnumerator OfferBannerRoutine(GameObject banner)
    {
        yield return OfferBannerSlide(banner, true);
        float t = 0f;
        while (banner != null && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
        if (banner != null) yield return OfferBannerSlide(banner, false);
    }

    private IEnumerator OfferBannerSlide(GameObject banner, bool slideIn)
    {
        if (banner == null) yield break;
        var rt = (RectTransform)banner.transform;
        float hidden = rt.sizeDelta.y + 40f;
        float shown = -16f;
        float from = slideIn ? hidden : rt.anchoredPosition.y;
        float to = slideIn ? shown : hidden;
        float dur = slideIn ? 0.4f : 0.3f;
        float t = 0f;
        while (t < dur)
        {
            if (banner == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            // ease-out-back when entering, ease-in when leaving
            float e = slideIn ? 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f) : k * k;
            rt.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(from, to, e));
            yield return null;
        }
        if (banner == null) yield break;
        rt.anchoredPosition = new Vector2(0f, to);
        if (!slideIn)
        {
            if (offerBanner == banner) offerBanner = null;
            Destroy(banner);
        }
    }

    // ── Toast notification ────────────────────────────────────────────────────
    public void ShowToast(string message)
    {
        StartCoroutine(ToastCoroutine(message));
    }

    private IEnumerator ToastCoroutine(string message)
    {
        var go = new GameObject("Toast");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 220f);
        rect.sizeDelta = new Vector2(500f, 68f);
        var bg = go.AddComponent<Image>();
        bg.sprite = SpriteGenerator.RoundedRect;
        bg.color = new Color(0.10f, 0.10f, 0.12f, 0.92f);
        var txt = new GameObject("Txt"); txt.transform.SetParent(go.transform, false);
        var tr = txt.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var t = txt.AddComponent<Text>();
        t.font = defaultFont; t.text = message;
        t.fontSize = 28; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        yield return new WaitForSeconds(2.2f);
        if (go != null) Destroy(go);
    }

    // ── Invite Friends Popup ──────────────────────────────────────────────────
    public void ShowInvitePopup(string myCode, System.Action<string> onClaim)
    {
        var tm = ThemeManager.Instance;
        bool dark = tm?.IsDarkMode ?? false;

        // ── Overlay ──────────────────────────────────────────────────────────────
        var overlay = new GameObject("InviteOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetAsLastSibling();
        var oRect = overlay.AddComponent<RectTransform>();
        oRect.anchorMin = Vector2.zero; oRect.anchorMax = Vector2.one;
        oRect.offsetMin = Vector2.zero; oRect.offsetMax = Vector2.zero;
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.60f);
        overlay.AddComponent<Button>().onClick.AddListener(() => Destroy(overlay));

        // ── Card ─────────────────────────────────────────────────────────────────
        const float cardW = 560f, cardH = 520f;
        const float pad = 30f;
        float leftX = -cardW * 0.5f + pad;
        float groupW = cardW - pad * 2f;

        Color accent     = new Color(0.16f, 0.62f, 0.66f);
        Color cardCol    = dark ? new Color(0.18f, 0.17f, 0.22f, 1f) : new Color(1f, 1f, 1f, 1f);
        Color groupBg    = dark ? new Color(0.24f, 0.23f, 0.29f) : new Color(0.96f, 0.955f, 0.975f);
        Color sectionCol = dark ? new Color(0.56f, 0.54f, 0.62f) : new Color(0.55f, 0.53f, 0.58f);
        Color mutedCol   = dark ? new Color(0.58f, 0.56f, 0.64f) : new Color(0.58f, 0.56f, 0.60f);
        Color primaryCol = dark ? new Color(0.92f, 0.90f, 0.88f) : new Color(0.18f, 0.18f, 0.22f);
        Color codeCol    = new Color(0.12f, 0.66f, 0.58f);

        // Soft drop shadow
        var shadowGo = new GameObject("Shadow"); shadowGo.transform.SetParent(overlay.transform, false);
        var shR = shadowGo.AddComponent<RectTransform>();
        shR.anchorMin = shR.anchorMax = new Vector2(0.5f, 0.5f); shR.pivot = new Vector2(0.5f, 0.5f);
        shR.sizeDelta = new Vector2(cardW + 30f, cardH + 30f); shR.anchoredPosition = new Vector2(0f, -10f);
        var shImg = shadowGo.AddComponent<Image>(); shImg.sprite = SpriteGenerator.RoundedRectSliced; shImg.type = Image.Type.Sliced;
        shImg.color = new Color(0f, 0f, 0f, 0.22f);

        var card = new GameObject("Card");
        card.transform.SetParent(overlay.transform, false);
        var cRect = card.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0.5f); cRect.anchorMax = new Vector2(0.5f, 0.5f);
        cRect.pivot = new Vector2(0.5f, 0.5f);
        cRect.anchoredPosition = Vector2.zero;
        cRect.sizeDelta = new Vector2(cardW, cardH);
        var cImg = card.AddComponent<Image>();
        cImg.sprite = SpriteGenerator.RoundedRectSliced;
        cImg.type = Image.Type.Sliced;
        cImg.color = cardCol;
        var cBlock = card.AddComponent<Button>(); cBlock.targetGraphic = cImg;
        cBlock.transition = Selectable.Transition.None;
        cBlock.onClick.AddListener(() => {});

        float top = cardH * 0.5f;

        // ── Header: left title + close ──────────────────────────────────────────
        MakeLeftText(card.transform, new Vector2(leftX + 6f, top - 50f), new Vector2(420f, 46f),
            "Invite Friends", 34, FontStyle.Bold, primaryCol);
        MakeCloseButton(card.transform, Vector2.zero, cardW, cardH, accent, () => Destroy(overlay));

        // ── Section: YOUR INVITE CODE ───────────────────────────────────────────
        MakeSectionLabel(card.transform, top - 122f, leftX + 6f, groupW, "YOUR INVITE CODE", sectionCol);
        var g1 = MakeListGroup(card.transform, 60f, groupW, 108f, groupBg);

        var codeTxtGo = new GameObject("CodeTxt"); codeTxtGo.transform.SetParent(g1.transform, false);
        var ctR = codeTxtGo.AddComponent<RectTransform>();
        ctR.anchorMin = ctR.anchorMax = new Vector2(0.5f, 0.5f); ctR.pivot = new Vector2(0.5f, 0.5f);
        ctR.anchoredPosition = new Vector2(0f, 14f); ctR.sizeDelta = new Vector2(groupW - 20f, 56f);
        var ctT = codeTxtGo.AddComponent<Text>();
        ctT.font = defaultFont; ctT.text = myCode;
        ctT.fontSize = 46; ctT.fontStyle = FontStyle.Bold;
        ctT.color = codeCol; ctT.alignment = TextAnchor.MiddleCenter;
        ctT.horizontalOverflow = HorizontalWrapMode.Overflow;
        ctT.verticalOverflow = VerticalWrapMode.Overflow;

        var capGo = new GameObject("Caption"); capGo.transform.SetParent(g1.transform, false);
        var capR = capGo.AddComponent<RectTransform>();
        capR.anchorMin = capR.anchorMax = new Vector2(0.5f, 0.5f); capR.pivot = new Vector2(0.5f, 0.5f);
        capR.anchoredPosition = new Vector2(0f, -30f); capR.sizeDelta = new Vector2(groupW - 20f, 28f);
        var capT = capGo.AddComponent<Text>();
        capT.font = defaultFont; capT.text = "Share it — friends get +5 hints, and so do you";
        capT.fontSize = 20; capT.color = mutedCol; capT.alignment = TextAnchor.MiddleCenter;
        capT.horizontalOverflow = HorizontalWrapMode.Overflow;

        // ── Section: REDEEM A CODE ──────────────────────────────────────────────
        MakeSectionLabel(card.transform, -28f, leftX + 6f, groupW, "REDEEM A FRIEND'S CODE", sectionCol);

        var inputGo = new GameObject("CodeInput"); inputGo.transform.SetParent(card.transform, false);
        var inR = inputGo.AddComponent<RectTransform>();
        inR.anchorMin = inR.anchorMax = new Vector2(0.5f, 0.5f); inR.pivot = new Vector2(0.5f, 0.5f);
        inR.anchoredPosition = new Vector2(0f, -89f);
        inR.sizeDelta = new Vector2(groupW, 74f);
        var inBg = inputGo.AddComponent<Image>(); inBg.sprite = SpriteGenerator.RoundedRectSliced; inBg.type = Image.Type.Sliced;
        inBg.color = groupBg;
        var inputField = inputGo.AddComponent<InputField>();
        inputField.targetGraphic = inBg;

        var ph = new GameObject("PH"); ph.transform.SetParent(inputGo.transform, false);
        var phR = ph.AddComponent<RectTransform>();
        phR.anchorMin = Vector2.zero; phR.anchorMax = Vector2.one;
        phR.offsetMin = new Vector2(22, 0); phR.offsetMax = new Vector2(-22, 0);
        var phT = ph.AddComponent<Text>();
        phT.font = defaultFont; phT.text = "Paste or type a code";
        phT.fontSize = 26; phT.fontStyle = FontStyle.Italic;
        phT.color = mutedCol; phT.alignment = TextAnchor.MiddleCenter;
        phT.horizontalOverflow = HorizontalWrapMode.Overflow;

        var inTxtGo = new GameObject("IT"); inTxtGo.transform.SetParent(inputGo.transform, false);
        var inTxtR = inTxtGo.AddComponent<RectTransform>();
        inTxtR.anchorMin = Vector2.zero; inTxtR.anchorMax = Vector2.one;
        inTxtR.offsetMin = new Vector2(22, 0); inTxtR.offsetMax = new Vector2(-22, 0);
        var inTxt = inTxtGo.AddComponent<Text>();
        inTxt.font = defaultFont; inTxt.fontSize = 32; inTxt.fontStyle = FontStyle.Bold;
        inTxt.color = primaryCol; inTxt.alignment = TextAnchor.MiddleCenter;
        inTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        inputField.textComponent = inTxt;
        inputField.placeholder = phT;
        inputField.characterLimit = 8;
        inputField.characterValidation = InputField.CharacterValidation.Alphanumeric;

        // Claim button (gold candy)
        var claimBtn = CreateCandyButton("🎁  Get +5 Hints", card.transform, new Vector2(0f, -178f),
            new Vector2(groupW, 70f), new Color(0.95f, 0.68f, 0.12f), 29);
        claimBtn.onClick.AddListener(() =>
        {
            if (!string.IsNullOrWhiteSpace(inputField.text))
            {
                onClaim?.Invoke(inputField.text);
                Destroy(overlay);
            }
        });

        // "Tap outside to close" hint
        var tipGo = new GameObject("CloseTip"); tipGo.transform.SetParent(card.transform, false);
        var tipR = tipGo.AddComponent<RectTransform>();
        tipR.anchorMin = tipR.anchorMax = new Vector2(0.5f, 0.5f); tipR.pivot = new Vector2(0.5f, 0.5f);
        tipR.anchoredPosition = new Vector2(0f, -234f); tipR.sizeDelta = new Vector2(groupW, 28f);
        var tipT = tipGo.AddComponent<Text>();
        tipT.font = defaultFont; tipT.text = "Tap outside to close";
        tipT.fontSize = 20; tipT.color = mutedCol; tipT.alignment = TextAnchor.MiddleCenter;
        tipT.horizontalOverflow = HorizontalWrapMode.Overflow;
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
        // Extract price from "No Ads\n$9.99" label format
        if (!string.IsNullOrEmpty(buttonLabel))
        {
            var parts = buttonLabel.Split('\n');
            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                noAdsPriceLabel = parts[1];
        }
    }

    public void SetHintAvailable(bool isAvailable)
    {
        if (hintButton == null)
            return;

        bool enabled = isAvailable;
        hintButton.interactable = enabled;
        if (hintButtonIcon != null)
            hintButtonIcon.color = enabled ? Color.white : new Color(0.72f, 0.72f, 0.72f, 0.7f);
    }

    /// <summary>
    /// Disables all interactive buttons during tutorial so accidental taps don't trigger actions.
    /// </summary>
    public void SetTutorialMode(bool isTutorial)
    {
        if (hintButton != null)           hintButton.interactable           = !isTutorial;
        if (restartButton != null)        restartButton.interactable        = !isTutorial;
        if (cartButton != null)           cartButton.interactable           = !isTutorial;
        if (levelSelectToggleButton != null) levelSelectToggleButton.interactable = !isTutorial;
    }

    public void UpdateLevelProgress(int currentIndex, int total)
    {
        if (levelProgressText != null)
            levelProgressText.text = $"Level {currentIndex + 1} / {total}";
    }

    public void SetHintCount(int count)
    {
        UpdateHintBadge(count);
    }

    public void SetNoAdsPurchased(bool purchased) { }
}
