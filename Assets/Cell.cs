using UnityEngine;
using System.Collections;

public enum CellState
{
    Empty,
    NumberTarget,
    Selecting,
    Completed,
    Blocked
}

public class Cell : MonoBehaviour
{
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public int TargetNumber { get; private set; }
    public CellState State { get; private set; }
    public int SelectionOrder { get; set; }
    public int BlockId { get; set; } = -1;
    public bool IsBlocked => _isBlocked;

    private bool _isBlocked;
    private bool _isPentagonMode;
    private SpriteRenderer bgRenderer;
    private SpriteRenderer shadowRenderer;
    private SpriteRenderer numberRenderer;
    private Color blockColor;
    private Vector3 baseScale;

    // Modern palette
    private static readonly Color EmptyColor = new Color(0.85f, 0.82f, 0.78f);
    private static readonly Color TargetColor = new Color(0.90f, 0.30f, 0.25f);
    private static readonly Color SelectingColor = new Color(0.25f, 0.78f, 0.72f);
    private static readonly Color LastSelectedColor = new Color(0.16f, 0.88f, 0.52f);

    public void Initialize(int x, int y, int targetNumber, Sprite bgSprite, bool isBlocked = false, bool isPentagonMode = false)
    {
        GridX = x;
        GridY = y;
        TargetNumber = targetNumber;
        _isBlocked = isBlocked;
        _isPentagonMode = isPentagonMode;
        State = isBlocked ? CellState.Blocked : (targetNumber > 0 ? CellState.NumberTarget : CellState.Empty);
        baseScale = transform.localScale;

        // Drop shadow
        var shadowObj = new GameObject("Shadow");
        shadowObj.transform.SetParent(transform);
        shadowObj.transform.localPosition = new Vector3(0.04f, -0.06f, 0);
        shadowObj.transform.localScale = Vector3.one * 1.04f;
        shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = bgSprite;
        shadowRenderer.sortingOrder = 0;

        // Cell background
        bgRenderer = gameObject.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = bgSprite;
        bgRenderer.sortingOrder = 1;

        // Number
        var numObj = new GameObject("Number");
        numObj.transform.SetParent(transform);
        numObj.transform.localPosition = new Vector3(0, 0, -0.01f);
        numObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        numberRenderer = numObj.AddComponent<SpriteRenderer>();
        numberRenderer.sortingOrder = 15;
        numberRenderer.gameObject.SetActive(false);

        UpdateVisual();

        if (isBlocked)
            AddBlockedOverlay();
    }

    public void SetState(CellState newState, int order = 0)
    {
        if (_isBlocked) return;
        State = newState;
        SelectionOrder = order;
        UpdateVisual();

        if (newState == CellState.Selecting || newState == CellState.Completed)
            PunchScale();
    }

    public void SetCompletedColor(Color color)
    {
        blockColor = color;
        if (State == CellState.Completed)
        {
            bgRenderer.color = color;
            shadowRenderer.color = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.18f);
        }
    }

    public void SetAsLastSelected(bool isLast)
    {
        if (State == CellState.Selecting)
            bgRenderer.color = isLast ? LastSelectedColor : SelectingColor;
    }

    public void ResetToOriginal()
    {
        if (_isBlocked) return;
        State = TargetNumber > 0 ? CellState.NumberTarget : CellState.Empty;
        SelectionOrder = 0;
        BlockId = -1;
        StopAllCoroutines();
        transform.localScale = baseScale;
        ApplyVisualAlpha(1f);
        UpdateVisual();
    }

    public void PlaySpawn(float delay)
    {
        StopAllCoroutines();
        StartCoroutine(DoSpawn(delay));
    }

    public void PlayDisappear(float delay, float duration = 0.28f)
    {
        StopAllCoroutines();
        StartCoroutine(DoDisappear(delay, duration));
    }

    public void PlayCompletionPulse()
    {
        PunchScale();
    }

    private void PunchScale()
    {
        StopAllCoroutines();
        StartCoroutine(DoPunch());
    }

    private IEnumerator DoPunch()
    {
        float t = 0;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            float s = 1f + 0.1f * Mathf.Sin(t / 0.12f * Mathf.PI);
            transform.localScale = baseScale * s;
            yield return null;
        }
        transform.localScale = baseScale;
    }

    private IEnumerator DoSpawn(float delay)
    {
        transform.localScale = baseScale * 0.18f;
        ApplyVisualAlpha(0f);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        const float duration = 0.34f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            float scale = progress < 0.72f
                ? Mathf.Lerp(0.18f, 1.08f, EaseOutBack(progress / 0.72f))
                : Mathf.Lerp(1.08f, 1f, Mathf.SmoothStep(0f, 1f, (progress - 0.72f) / 0.28f));

            transform.localScale = baseScale * scale;
            ApplyVisualAlpha(Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        transform.localScale = baseScale;
        ApplyVisualAlpha(1f);
    }

    private IEnumerator DoDisappear(float delay, float duration)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / duration);
            float scale = progress < 0.2f
                ? Mathf.Lerp(1f, 1.08f, Mathf.SmoothStep(0f, 1f, progress / 0.2f))
                : Mathf.Lerp(1.08f, 0.12f, Mathf.SmoothStep(0f, 1f, (progress - 0.2f) / 0.8f));
            float fade = progress < 0.12f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (progress - 0.12f) / 0.88f);
            transform.localScale = baseScale * scale;
            ApplyVisualAlpha(fade);
            yield return null;
        }

        transform.localScale = baseScale * 0.12f;
        ApplyVisualAlpha(0f);
    }

    private void ApplyVisualAlpha(float alpha)
    {
        if (bgRenderer != null)
        {
            Color bgColor = bgRenderer.color;
            bgColor.a = alpha;
            bgRenderer.color = bgColor;
        }

        if (shadowRenderer != null)
        {
            Color shadowColor = shadowRenderer.color;
            shadowColor.a = GetShadowBaseAlpha() * alpha;
            shadowRenderer.color = shadowColor;
        }

        if (numberRenderer != null)
        {
            Color numberColor = numberRenderer.color;
            numberColor.a = GetNumberBaseAlpha() * alpha;
            numberRenderer.color = numberColor;
        }
    }

    private float GetShadowBaseAlpha()
    {
        switch (State)
        {
            case CellState.Empty:
                return 0.08f;
            case CellState.NumberTarget:
                return 0.2f;
            case CellState.Selecting:
                return 0.18f;
            case CellState.Completed:
                return 0.18f;
            case CellState.Blocked:
                return 0.04f;
            default:
                return 0f;
        }
    }

    private float GetNumberBaseAlpha()
    {
        switch (State)
        {
            case CellState.Empty:
                return 0f;
            case CellState.Completed:
                return 0.85f;
            case CellState.Blocked:
                return 0f;
            default:
                return 1f;
        }
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t = value - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    private void UpdateVisual()
    {
        Color emptyColor = ThemeManager.Instance != null ? ThemeManager.Instance.CellEmptyColor : EmptyColor;
        switch (State)
        {
            case CellState.Empty:
                bgRenderer.color = emptyColor;
                shadowRenderer.color = new Color(0, 0, 0, 0.08f);
                numberRenderer.gameObject.SetActive(false);
                break;

            case CellState.NumberTarget:
                bgRenderer.color = TargetColor;
                shadowRenderer.color = new Color(0.6f, 0.15f, 0.12f, 0.2f);
                numberRenderer.sprite = SpriteGenerator.GetNumberSprite(TargetNumber);
                numberRenderer.color = Color.white;
                numberRenderer.gameObject.SetActive(true);
                break;

            case CellState.Selecting:
                bgRenderer.color = SelectingColor;
                shadowRenderer.color = new Color(0.12f, 0.45f, 0.42f, 0.18f);
                numberRenderer.sprite = SpriteGenerator.GetNumberSprite(SelectionOrder);
                numberRenderer.color = Color.white;
                numberRenderer.gameObject.SetActive(true);
                break;

            case CellState.Completed:
                bgRenderer.color = blockColor;
                shadowRenderer.color = new Color(blockColor.r * 0.4f, blockColor.g * 0.4f, blockColor.b * 0.4f, 0.18f);
                numberRenderer.sprite = SpriteGenerator.GetNumberSprite(SelectionOrder);
                numberRenderer.color = new Color(1f, 1f, 1f, 0.85f);
                numberRenderer.gameObject.SetActive(true);
                break;

            case CellState.Blocked:
                bgRenderer.color = new Color(0.26f, 0.23f, 0.21f);
                shadowRenderer.color = new Color(0f, 0f, 0f, 0.04f);
                numberRenderer.gameObject.SetActive(false);
                break;
        }
    }

    private void AddBlockedOverlay()
    {
        Color crossColor = new Color(0.38f, 0.34f, 0.30f, 0.65f);
        for (int i = 0; i < 2; i++)
        {
            var crossObj = new GameObject("BlockedCross");
            crossObj.transform.SetParent(transform);
            crossObj.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            crossObj.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            crossObj.transform.localScale = new Vector3(0.12f, 0.72f, 1f);
            var crossR = crossObj.AddComponent<SpriteRenderer>();
            crossR.sprite = SpriteGenerator.RoundedRect;
            crossR.color = crossColor;
            crossR.sortingOrder = 5;
        }
    }

    public bool IsAdjacentTo(Cell other)
    {
        if (!_isPentagonMode)
            return Mathf.Abs(GridX - other.GridX) + Mathf.Abs(GridY - other.GridY) == 1;

        // Hex adjacency for offset grid (odd rows shifted +0.5 in X)
        int dx = other.GridX - GridX;
        int dy = other.GridY - GridY;
        if (dy == 0) return Mathf.Abs(dx) == 1;
        if (Mathf.Abs(dy) != 1) return false;
        // Even row: diagonal neighbors are at (x-1) and (x) in adjacent rows
        // Odd  row: diagonal neighbors are at (x)   and (x+1) in adjacent rows
        return GridY % 2 == 0 ? (dx == -1 || dx == 0) : (dx == 0 || dx == 1);
    }

    public void RefreshTheme()
    {
        if (State == CellState.Empty || State == CellState.NumberTarget)
            UpdateVisual();
    }
}
