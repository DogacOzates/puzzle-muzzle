using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public float CellSpacing { get; private set; } = 1.0f;
    public float CellVisualSize { get; private set; } = 0.9f;
    public int GridWidth { get; private set; }
    public int GridHeight { get; private set; }
    public Vector2 GridOrigin { get; private set; }

    private Cell[,] cells;
    private GameObject gridContainer;

    // Active selection chain
    public List<Cell> ActiveChain { get; private set; } = new List<Cell>();
    public bool HasActiveSelection => ActiveChain.Count > 0;

    // Completed blocks
    private List<List<Cell>> completedBlocks = new List<List<Cell>>();
    private int nextBlockId = 0;

    private static readonly Color GridBgColor = new Color(0.99f, 0.98f, 0.96f);

    // Block color palette - distinct vibrant colors for completed paths
    private static readonly Color[] BlockPalette = new Color[]
    {
        new Color(0.36f, 0.55f, 0.94f), // Blue
        new Color(1.00f, 0.54f, 0.40f), // Coral
        new Color(0.35f, 0.72f, 0.40f), // Green
        new Color(0.67f, 0.33f, 0.76f), // Purple
        new Color(0.98f, 0.76f, 0.18f), // Gold
        new Color(0.25f, 0.78f, 0.85f), // Cyan
        new Color(0.91f, 0.40f, 0.56f), // Pink
        new Color(0.58f, 0.46f, 0.38f), // Mocha
        new Color(0.18f, 0.65f, 0.58f), // Teal
        new Color(0.95f, 0.55f, 0.22f), // Orange
    };

    public void Initialize(LevelData level)
    {
        ClearAll();
        GridWidth = level.gridWidth;
        GridHeight = level.gridHeight;
        cells = new Cell[GridWidth, GridHeight];

        gridContainer = new GameObject("Grid");
        gridContainer.transform.SetParent(transform);

        CalculateGridOrigin();
        CreateGridBackground();
        CreateCells(level);
    }

    private void CalculateGridOrigin()
    {
        float gridWorldWidth = (GridWidth - 1) * CellSpacing;
        float gridWorldHeight = (GridHeight - 1) * CellSpacing;

        GridOrigin = new Vector2(
            -gridWorldWidth / 2f,
            gridWorldHeight / 2f - 0.5f
        );
    }

    private void CreateGridBackground()
    {
        float gridWorldWidth = (GridWidth - 1) * CellSpacing + CellVisualSize;
        float gridWorldHeight = (GridHeight - 1) * CellSpacing + CellVisualSize;
        float padding = 0.35f;

        Vector3 bgCenter = new Vector3(
            GridOrigin.x + (GridWidth - 1) * CellSpacing / 2f,
            GridOrigin.y - (GridHeight - 1) * CellSpacing / 2f,
            0.1f
        );
        Vector3 bgScale = new Vector3(gridWorldWidth + padding * 2, gridWorldHeight + padding * 2, 1f);

        // Soft shadow behind grid (larger offset, more visible)
        var shadowObj = new GameObject("GridShadow");
        shadowObj.transform.SetParent(gridContainer.transform);
        var shadowR = shadowObj.AddComponent<SpriteRenderer>();
        shadowR.sprite = SpriteGenerator.RoundedRect;
        shadowR.color = new Color(0f, 0f, 0f, 0.18f);
        shadowR.sortingOrder = -2;
        shadowObj.transform.position = bgCenter + new Vector3(0.08f, -0.14f, 0.01f);
        shadowObj.transform.localScale = bgScale * 1.03f;

        // Main grid background — clean white card
        var bgObj = new GameObject("GridBackground");
        bgObj.transform.SetParent(gridContainer.transform);
        var renderer = bgObj.AddComponent<SpriteRenderer>();
        renderer.sprite = SpriteGenerator.RoundedRect;
        renderer.color = GridBgColor;
        renderer.sortingOrder = -1;
        bgObj.transform.position = bgCenter;
        bgObj.transform.localScale = bgScale;
    }

    private void CreateCells(LevelData level)
    {
        Sprite cellSprite = SpriteGenerator.RoundedRect;

        for (int y = 0; y < GridHeight; y++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                int number = 0;
                foreach (var nc in level.numberCells)
                {
                    if (nc.x == x && nc.y == y)
                    {
                        number = nc.value;
                        break;
                    }
                }

                Vector3 worldPos = GridToWorld(x, y);
                var cellObj = new GameObject($"Cell_{x}_{y}");
                cellObj.transform.SetParent(gridContainer.transform);
                cellObj.transform.position = worldPos;
                cellObj.transform.localScale = new Vector3(CellVisualSize, CellVisualSize, 1f);

                var cell = cellObj.AddComponent<Cell>();
                cell.Initialize(x, y, number, cellSprite);
                cells[x, y] = cell;
            }
        }
    }

    // --- Selection Chain ---

    // Start a chain from an empty cell (becomes "1")
    public bool TryStartFromEmpty(Cell cell)
    {
        if (cell == null || cell.State != CellState.Empty) return false;
        if (HasActiveSelection) return false;

        ActiveChain.Clear();
        ActiveChain.Add(cell);
        cell.SetState(CellState.Selecting, 1);
        cell.SetAsLastSelected(true);
        return true;
    }

    // Start and auto-complete on a NumberTarget cell with value 1
    public bool TryCompleteTarget1(Cell cell)
    {
        if (cell == null || cell.State != CellState.NumberTarget) return false;
        if (cell.TargetNumber != 1) return false;
        if (HasActiveSelection) return false;

        ActiveChain.Clear();
        ActiveChain.Add(cell);
        cell.SetState(CellState.Selecting, 1);
        CompleteSelection();
        return true;
    }

    // Extend the chain to an adjacent cell
    public bool TryExtendSelection(Cell cell)
    {
        if (!HasActiveSelection) return false;
        if (cell == null) return false;

        Cell lastCell = ActiveChain[ActiveChain.Count - 1];
        if (!cell.IsAdjacentTo(lastCell)) return false;

        int nextOrder = ActiveChain.Count + 1;

        if (cell.State == CellState.Empty)
        {
            // Extend to an empty cell
            lastCell.SetAsLastSelected(false);
            ActiveChain.Add(cell);
            cell.SetState(CellState.Selecting, nextOrder);
            cell.SetAsLastSelected(true);
            return true;
        }
        else if (cell.State == CellState.NumberTarget)
        {
            // Can only step onto a target if chain length will match its number
            if (nextOrder == cell.TargetNumber)
            {
                lastCell.SetAsLastSelected(false);
                ActiveChain.Add(cell);
                cell.SetState(CellState.Selecting, nextOrder);
                CompleteSelection();
                return true;
            }
            // Count doesn't match - reject
            return false;
        }

        return false;
    }

    public bool IsLastInChain(Cell cell)
    {
        return ActiveChain.Count > 0 && ActiveChain[ActiveChain.Count - 1] == cell;
    }

    public bool UndoLastStep()
    {
        if (!HasActiveSelection) return false;

        if (ActiveChain.Count <= 1)
        {
            CancelSelection();
            return true;
        }

        Cell lastCell = ActiveChain[ActiveChain.Count - 1];
        ActiveChain.RemoveAt(ActiveChain.Count - 1);
        lastCell.ResetToOriginal();

        // Mark new last
        Cell newLast = ActiveChain[ActiveChain.Count - 1];
        newLast.SetAsLastSelected(true);
        return true;
    }

    public void CancelSelection()
    {
        if (!HasActiveSelection) return;

        foreach (var cell in ActiveChain)
        {
            cell.ResetToOriginal();
        }
        ActiveChain.Clear();
    }

    private void CompleteSelection()
    {
        int blockId = nextBlockId++;
        Color blockColor = BlockPalette[blockId % BlockPalette.Length];

        foreach (var cell in ActiveChain)
        {
            cell.SetState(CellState.Completed, cell.SelectionOrder);
            cell.SetCompletedColor(blockColor);
            cell.BlockId = blockId;
        }

        completedBlocks.Add(new List<Cell>(ActiveChain));
        ActiveChain = new List<Cell>();
    }

    public bool TryRemoveBlock(Cell cell)
    {
        if (cell == null || cell.State != CellState.Completed) return false;

        int blockId = cell.BlockId;
        List<Cell> block = null;
        for (int i = 0; i < completedBlocks.Count; i++)
        {
            if (completedBlocks[i].Count > 0 && completedBlocks[i][0].BlockId == blockId)
            {
                block = completedBlocks[i];
                completedBlocks.RemoveAt(i);
                break;
            }
        }
        if (block == null) return false;

        foreach (var c in block)
        {
            c.ResetToOriginal();
        }
        return true;
    }

    // --- Hint System ---

    // Auto-solve one unsolved target. Returns true if a hint was applied.
    public bool SolveHint(SolutionPath[] solutions)
    {
        if (solutions == null) return false;

        CancelSelection();

        foreach (var path in solutions)
        {
            // Last cell in path is the target
            int targetX = path.GetX(path.Length - 1);
            int targetY = path.GetY(path.Length - 1);
            Cell targetCell = GetCell(targetX, targetY);

            if (targetCell == null || targetCell.State != CellState.NumberTarget) continue;

            // Check if all cells in the path are available
            bool pathClear = true;
            for (int i = 0; i < path.Length; i++)
            {
                Cell c = GetCell(path.GetX(i), path.GetY(i));
                if (c == null) { pathClear = false; break; }

                if (i < path.Length - 1)
                {
                    if (c.State != CellState.Empty) { pathClear = false; break; }
                }
                else
                {
                    if (c.State != CellState.NumberTarget) { pathClear = false; break; }
                }
            }

            if (!pathClear) continue;

            // Auto-complete this path
            int blockId = nextBlockId++;
            Color blockColor = BlockPalette[blockId % BlockPalette.Length];
            var block = new List<Cell>();
            for (int i = 0; i < path.Length; i++)
            {
                Cell c = GetCell(path.GetX(i), path.GetY(i));
                c.SetState(CellState.Completed, i + 1);
                c.SetCompletedColor(blockColor);
                c.BlockId = blockId;
                block.Add(c);
            }
            completedBlocks.Add(block);
            return true;
        }

        return false;
    }

    // --- Grid Utilities ---

    public Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(
            GridOrigin.x + x * CellSpacing,
            GridOrigin.y - y * CellSpacing,
            0f
        );
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt((worldPos.x - GridOrigin.x) / CellSpacing);
        int y = Mathf.RoundToInt((GridOrigin.y - worldPos.y) / CellSpacing);
        return new Vector2Int(x, y);
    }

    public bool IsValidGridPos(int x, int y)
    {
        return x >= 0 && x < GridWidth && y >= 0 && y < GridHeight;
    }

    public Cell GetCell(int x, int y)
    {
        if (!IsValidGridPos(x, y)) return null;
        return cells[x, y];
    }

    public bool IsLevelComplete()
    {
        for (int y = 0; y < GridHeight; y++)
            for (int x = 0; x < GridWidth; x++)
                if (cells[x, y].State != CellState.Completed)
                    return false;
        return true;
    }

    public void ClearAll()
    {
        if (HasActiveSelection) CancelSelection();

        completedBlocks.Clear();
        nextBlockId = 0;

        if (gridContainer != null)
            Destroy(gridContainer);

        cells = null;
    }
}
