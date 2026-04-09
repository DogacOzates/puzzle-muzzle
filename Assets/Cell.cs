using UnityEngine;
using System.Collections;

public enum CellState
{
    Empty,
    NumberTarget,
    Selecting,
    Completed
}

public class Cell : MonoBehaviour
{
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public int TargetNumber { get; private set; }
    public CellState State { get; private set; }
    public int SelectionOrder { get; set; }
    public int BlockId { get; set; } = -1;

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

    public void Initialize(int x, int y, int targetNumber, Sprite bgSprite)
    {
        GridX = x;
        GridY = y;
        TargetNumber = targetNumber;
        State = targetNumber > 0 ? CellState.NumberTarget : CellState.Empty;
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
    }

    public void SetState(CellState newState, int order = 0)
    {
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
        State = TargetNumber > 0 ? CellState.NumberTarget : CellState.Empty;
        SelectionOrder = 0;
        BlockId = -1;
        StopAllCoroutines();
        transform.localScale = baseScale;
        UpdateVisual();
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

    private void UpdateVisual()
    {
        switch (State)
        {
            case CellState.Empty:
                bgRenderer.color = EmptyColor;
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
        }
    }

    public bool IsAdjacentTo(Cell other)
    {
        return Mathf.Abs(GridX - other.GridX) + Mathf.Abs(GridY - other.GridY) == 1;
    }
}
