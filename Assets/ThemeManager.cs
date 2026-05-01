using UnityEngine;

public class ThemeManager : MonoBehaviour
{
    public static ThemeManager Instance { get; private set; }
    public static event System.Action OnThemeChanged;

    // Dark mode is session-only — always starts light on app launch
    private bool _isDarkMode = false;

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            _isDarkMode = value;
            OnThemeChanged?.Invoke();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Color palette ────────────────────────────────────────────────────────

    public Color BgColor         => IsDarkMode ? new Color(0.09f, 0.09f, 0.12f)           : new Color(0.97f, 0.95f, 0.92f);
    public Color GridBgColor     => IsDarkMode ? new Color(0.13f, 0.13f, 0.17f)           : new Color(0.99f, 0.98f, 0.96f);
    public Color CellEmptyColor  => IsDarkMode ? new Color(0.26f, 0.25f, 0.30f)           : new Color(0.85f, 0.82f, 0.78f);
    public Color TextPrimary     => IsDarkMode ? new Color(0.92f, 0.90f, 0.88f)           : new Color(0.18f, 0.18f, 0.22f);
    public Color TextMuted       => IsDarkMode ? new Color(0.60f, 0.58f, 0.56f)           : new Color(0.52f, 0.50f, 0.48f);
    public Color CardBg          => IsDarkMode ? new Color(0.18f, 0.17f, 0.22f, 0.98f)   : new Color(1f, 1f, 1f, 0.985f);
    public Color PanelBg         => IsDarkMode ? new Color(0.14f, 0.13f, 0.18f, 1f)      : new Color(0.97f, 0.95f, 0.93f, 1f);
    public Color LevelBtnCurrent => IsDarkMode ? new Color(0.20f, 0.55f, 0.55f, 1f)      : new Color(0.74f, 0.90f, 0.86f, 1f);
    public Color LevelBtnUnlocked=> IsDarkMode ? new Color(0.22f, 0.21f, 0.27f, 1f)      : new Color(0.94f, 0.96f, 0.92f, 1f);
    public Color LevelBtnLocked  => IsDarkMode ? new Color(0.15f, 0.14f, 0.19f, 1f)      : new Color(0.92f, 0.90f, 0.89f, 1f);
    public Color TransitionBg    => IsDarkMode ? new Color(0.09f, 0.09f, 0.12f)           : new Color(0.97f, 0.95f, 0.92f);
    public Color SettingsBtnBg   => IsDarkMode ? new Color(0.20f, 0.19f, 0.25f, 1f)      : new Color(0.95f, 0.93f, 0.88f, 1f);
    public Color ToggleRowBg     => IsDarkMode ? new Color(0.20f, 0.19f, 0.25f, 1f)      : new Color(0.96f, 0.94f, 0.90f, 1f);
    public Color LevelSelectFrame=> IsDarkMode ? new Color(0.12f, 0.12f, 0.16f, 0.95f)   : new Color(0.97f, 0.96f, 0.95f, 0.95f);
}
