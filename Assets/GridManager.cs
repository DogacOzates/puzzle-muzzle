using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public float CellSpacing { get; private set; } = 1.0f;
    public float CellVisualSize { get; private set; } = 0.9f;
    public float RowSpacing { get; private set; } = 1.0f;
    public int GridWidth { get; private set; }
    public int GridHeight { get; private set; }
    public Vector2 GridOrigin { get; private set; }
    // Center Y of the cell-centers bounding box (always GridOrigin.y - gridWorldHeight/2)
    public float GridCenterY { get; private set; }

    private bool isPentagonMode;
    private bool isHexagonMode;
    private bool isThreeGenMode;
    public bool IsTriangleMode => isThreeGenMode;
    // CellVisualSize = 2/√3 ≈ 1.1547: hexagon apothem = r*cos(30°) = 0.5*0.866; 2*apothem*CVS = 1.0 → no gaps
    private const float HexCellVisualSize = 1.1547f;
    // Pentagon in column-offset hex grid (r=126px, PPU=256):
    //   X scale = HexCellVisualSize so horizontal contacts behave like hexagons.
    //   Y scale = 256/(126*1.5) ≈ 1.354 so flat-bottom exactly touches down-neighbor's top vertex
    //   (k_y = r*scaleY/256 = 2/3; flat-bottom at -k_y/2 = -1/3; neighbor top at -1+k_y = -1/3 ✓).
    private const float PentagonCellVisualSizeY = 256f / (126f * 1.5f); // ≈ 1.354
    // Triangle: circumradius R=120 in 256px texture; touching distance = 1/√3 world units (CellSpacing=0.5)
    // CellVisualSize = 256/(120*√3) ≈ 1.232 makes cells touch edge-to-edge exactly.
    // 1.16 ≈ 94% of that → ~0.036 world unit gap between adjacent cell edges (≈ 5 px on screen).
    private const float TriangleCVS = 1.16f;

    public float BoardVisualWidth => (isHexagonMode || isPentagonMode)
        ? (GridWidth - 1) * CellSpacing + CellVisualSize
        : (GridWidth - 1) * CellSpacing + CellVisualSize;
    public float BoardVisualHeight => (isHexagonMode || isPentagonMode)
        ? (GridHeight - 0.5f) * RowSpacing + (isPentagonMode ? PentagonCellVisualSizeY : CellVisualSize)
        : (isThreeGenMode
            ? GridHeight * RowSpacing + CellVisualSize * 0.5f
            : (GridHeight - 1) * RowSpacing + CellVisualSize);

    private Cell[,] cells;
    private GameObject gridContainer;
    public bool HasGrid => cells != null;

    // Active selection chain
    public List<Cell> ActiveChain { get; private set; } = new List<Cell>();
    public bool HasActiveSelection => ActiveChain.Count > 0;

    // Completed blocks
    private List<List<Cell>> completedBlocks = new List<List<Cell>>();
    private int nextBlockId = 0;

    private static readonly Color GridBgColor = new Color(0.99f, 0.98f, 0.96f);
    private SpriteRenderer gridBgRenderer;

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
        isPentagonMode = level.cellShape == CellShape.Pentagon;
        isThreeGenMode = level.cellShape == CellShape.ThreeGen;
        isHexagonMode  = level.cellShape == CellShape.Hexagon;
        CellVisualSize = isThreeGenMode ? TriangleCVS
                       : (isPentagonMode || isHexagonMode) ? HexCellVisualSize : 0.9f;
        if (isHexagonMode || isPentagonMode)
        {
            // Column-offset hex grid: ColSpacing=√3/2, RowSpacing=1.0 (both 5gen and 6gen)
            CellSpacing = Mathf.Sqrt(3f) / 2f;
            RowSpacing = 1.0f;
        }
        else if (isThreeGenMode)
        {
            // Triangle grid: CellSpacing=0.5 (half side), RowSpacing=√3/2 (triangle height)
            CellSpacing = 0.5f;
            RowSpacing = Mathf.Sqrt(3f) / 2f;
        }
        else
        {
            CellSpacing = 1.0f;
            RowSpacing = CellSpacing;
        }
        cells = new Cell[GridWidth, GridHeight];

        gridContainer = new GameObject("Grid");
        gridContainer.transform.SetParent(transform);

        CalculateGridOrigin();
        CreateGridBackground();
        CreateCells(level);
        AnimateCellEntrance();
    }

    private void CalculateGridOrigin()
    {
        float gridWorldWidth, gridWorldHeight;
        if (isHexagonMode || isPentagonMode)
        {
            // Column-offset: odd columns shift down 0.5*RowSpacing (both 5gen and 6gen)
            gridWorldWidth = (GridWidth - 1) * CellSpacing;
            gridWorldHeight = (GridHeight - 1) * RowSpacing;
            if (GridWidth >= 2) gridWorldHeight += 0.5f * RowSpacing;
        }
        else if (isThreeGenMode)
        {
            // Triangle: cols span (GridWidth-1)*0.5 in X; full row height = GridHeight*RowSpacing
            gridWorldWidth = (GridWidth - 1) * CellSpacing;
            gridWorldHeight = GridHeight * RowSpacing;
        }
        else
        {
            gridWorldWidth = (GridWidth - 1) * CellSpacing;
            gridWorldHeight = (GridHeight - 1) * RowSpacing;
        }

        GridOrigin = new Vector2(
            -gridWorldWidth / 2f,
            gridWorldHeight / 2f - 0.5f
        );
        GridCenterY = GridOrigin.y - gridWorldHeight / 2f;
    }

    private void CreateGridBackground()
    {
        float bgWidth, bgHeight;
        if (isHexagonMode || isPentagonMode)
        {
            bgWidth = (GridWidth - 1) * CellSpacing + CellVisualSize;
            bgHeight = (GridHeight - 0.5f) * RowSpacing + (isPentagonMode ? PentagonCellVisualSizeY : CellVisualSize);
        }
        else if (isThreeGenMode)
        {
            bgWidth  = (GridWidth - 1) * CellSpacing + CellVisualSize;
            bgHeight = GridHeight * RowSpacing + CellVisualSize * 0.5f;
        }
        else
        {
            bgWidth = (GridWidth - 1) * CellSpacing + CellVisualSize;
            bgHeight = (GridHeight - 1) * RowSpacing + CellVisualSize;
        }
        float padding = 0.35f;

        Vector3 bgCenter = new Vector3(0f, GridCenterY, 0.1f);
        Vector3 bgScale = new Vector3(bgWidth + padding * 2, bgHeight + padding * 2, 1f);

        // Soft shadow behind grid (larger offset, more visible)
        var shadowObj = new GameObject("GridShadow");
        shadowObj.transform.SetParent(gridContainer.transform);
        var shadowR = shadowObj.AddComponent<SpriteRenderer>();
        shadowR.sprite = SpriteGenerator.RoundedRect;
        shadowR.color = new Color(0f, 0f, 0f, 0.18f);
        shadowR.sortingOrder = -2;
        shadowR.sharedMaterial = SpriteGenerator.UnlitMaterial;
        shadowObj.transform.position = bgCenter + new Vector3(0.08f, -0.14f, 0.01f);
        shadowObj.transform.localScale = bgScale * 1.03f;

        // Main grid background — clean white card
        var bgObj = new GameObject("GridBackground");
        bgObj.transform.SetParent(gridContainer.transform);
        var renderer = bgObj.AddComponent<SpriteRenderer>();
        renderer.sprite = SpriteGenerator.RoundedRect;
        renderer.color = ThemeManager.Instance != null ? ThemeManager.Instance.GridBgColor : GridBgColor;
        renderer.sortingOrder = -1;
        renderer.sharedMaterial = SpriteGenerator.UnlitMaterial;
        bgObj.transform.position = bgCenter;
        bgObj.transform.localScale = bgScale;
        gridBgRenderer = renderer;
    }

    private void CreateCells(LevelData level)
    {
        Sprite cellSprite = isThreeGenMode ? SpriteGenerator.Triangle
            : (isHexagonMode ? SpriteGenerator.FlatHexagon
            : (isPentagonMode ? SpriteGenerator.Pentagon : SpriteGenerator.RoundedRect));

        // Build a fast lookup set for blocked positions
        var blockedSet = new System.Collections.Generic.HashSet<Vector2Int>();
        if (level.blockedCells != null)
        {
            foreach (var bc in level.blockedCells)
                blockedSet.Add(new Vector2Int(bc.x, bc.y));
        }

        for (int y = 0; y < GridHeight; y++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                bool isBlocked = blockedSet.Contains(new Vector2Int(x, y));

                int number = 0;
                if (!isBlocked)
                {
                    foreach (var nc in level.numberCells)
                    {
                        if (nc.x == x && nc.y == y)
                        {
                            number = nc.value;
                            break;
                        }
                    }
                }

                Vector3 worldPos = GridToWorld(x, y);
                var cellObj = new GameObject($"Cell_{x}_{y}");
                cellObj.transform.SetParent(gridContainer.transform);
                cellObj.transform.position = worldPos;
                cellObj.transform.localScale = new Vector3(CellVisualSize, isPentagonMode ? PentagonCellVisualSizeY : CellVisualSize, 1f);

                var cell = cellObj.AddComponent<Cell>();
                cell.Initialize(x, y, number, cellSprite, isBlocked, isPentagonMode, isHexagonMode, isThreeGenMode);
                cells[x, y] = cell;
            }
        }
    }

    private void AnimateCellEntrance()
    {
        float centerX = (GridWidth - 1) * 0.5f;
        float centerY = (GridHeight - 1) * 0.5f;

        for (int y = 0; y < GridHeight; y++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                float delay = (Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY)) * 0.04f;
                cells[x, y].PlaySpawn(delay);
            }
        }
    }

    public IEnumerator PlayTransitionOut(float previewDelay = 0f, float waveStepDelay = 0.032f, float disappearDuration = 0.28f)
    {
        if (cells == null)
            yield break;

        if (previewDelay > 0f)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    if (cells[x, y] != null && cells[x, y].State == CellState.Completed)
                        cells[x, y].PlayCompletionPulse();
                }
            }

            yield return new WaitForSeconds(previewDelay);
        }

        float centerX = (GridWidth - 1) * 0.5f;
        float centerY = (GridHeight - 1) * 0.5f;
        float maxDelay = 0f;

        for (int y = 0; y < GridHeight; y++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                if (cells[x, y] == null)
                    continue;

                float delay = (Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY)) * waveStepDelay;
                maxDelay = Mathf.Max(maxDelay, delay);
                cells[x, y].PlayDisappear(delay, disappearDuration);
            }
        }

        yield return new WaitForSeconds(maxDelay + disappearDuration + 0.02f);
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

    public bool IsSecondToLastInChain(Cell cell)
    {
        return ActiveChain.Count >= 2 && ActiveChain[ActiveChain.Count - 2] == cell;
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

    // Resets all player-made moves without destroying the grid structure.
    public void ResetPlayerMoves()
    {
        if (HasActiveSelection) CancelSelection();
        foreach (var block in completedBlocks)
            foreach (var c in block)
                c.ResetToOriginal();
        completedBlocks.Clear();
        nextBlockId = 0;
    }

    // Returns true if every unsolved solution path still has all its cells available.
    // Solution paths never share cells, so this is an exact solvability check.
    private bool IsSolvable(SolutionPath[] solutions)
    {
        if (solutions == null) return false;
        foreach (var path in solutions)
        {
            int targetX = path.GetX(path.Length - 1);
            int targetY = path.GetY(path.Length - 1);
            Cell targetCell = GetCell(targetX, targetY);
            if (targetCell == null || targetCell.State != CellState.NumberTarget) continue; // already solved

            for (int i = 0; i < path.Length - 1; i++)
            {
                Cell c = GetCell(path.GetX(i), path.GetY(i));
                if (c == null || c.State != CellState.Empty) return false; // cell blocked
            }
        }
        return true;
    }

    // Auto-solve one unsolved path. Returns true if a hint was applied.
    public bool SolveHint(SolutionPath[] solutions)
    {
        if (solutions == null) return false;

        CancelSelection();

        if (!IsSolvable(solutions))
            ResetPlayerMoves();

        foreach (var path in solutions)
        {
            int targetX = path.GetX(path.Length - 1);
            int targetY = path.GetY(path.Length - 1);
            Cell targetCell = GetCell(targetX, targetY);

            if (targetCell == null || targetCell.State != CellState.NumberTarget) continue;

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

            int blockId = nextBlockId++;
            Color blockColor = BlockPalette[blockId % BlockPalette.Length];
            var block = new List<Cell>();

            for (int i = 0; i < path.Length; i++)
            {
                Cell c = GetCell(path.GetX(i), path.GetY(i));
                if (c == null) continue;
                int selOrder = i == path.Length - 1 ? path.Length : i + 1;
                c.SetState(CellState.Completed, selOrder);
                c.SetCompletedColor(blockColor);
                c.BlockId = blockId;
                block.Add(c);
            }
            completedBlocks.Add(block);
            return true;
        }

        return false;
    }

    // Triangle hint: Voronoi-partition all empty cells among segments, apply the largest
    // segment's path first (best value per hint press). Recomputed fresh each press.
    private bool SolveTriangleHint(SolutionPath[] solutions)
    {
        var voronoiPaths = ComputeTriangleVoronoi(solutions);
        if (voronoiPaths == null || voronoiPaths.Count == 0) return false;
        // Pick the largest multi-cell path (best value per hint press).
        List<Vector2Int> best = null;
        foreach (var p in voronoiPaths)
        {
            if (p.Count > 1 && (best == null || p.Count > best.Count))
                best = p;
        }
        if (best != null)
        {
            ApplyTriangleHintPath(best);
            return true;
        }
        ApplyTriangleHintPath(voronoiPaths[0]);
        return true;
    }

    private void ApplyTriangleHintPath(List<Vector2Int> dp)
    {
        int blockId = nextBlockId++;
        Color blockColor = BlockPalette[blockId % BlockPalette.Length];
        var block = new List<Cell>();
        for (int i = 0; i < dp.Count; i++)
        {
            Cell c = GetCell(dp[i].x, dp[i].y);
            if (c == null) continue;
            int selOrder = i == dp.Count - 1 ? dp.Count : i + 1;
            c.SetState(CellState.Completed, selOrder);
            c.SetCompletedColor(blockColor);
            c.BlockId = blockId;
            block.Add(c);
        }
        completedBlocks.Add(block);
    }

    // Sequential partition: Voronoi gives zone sizes for constraint ordering; paths are found
    // through ALL available cells (not zone-restricted) so topology never blocks DFS.
    // After each path is found, its cells are reserved so later zones don't overlap.
    private List<List<Vector2Int>> ComputeTriangleVoronoi(SolutionPath[] solutions)
    {
        // Shared visited map: completed/blocked cells start as unavailable.
        var globalVisited = new bool[GridHeight, GridWidth];
        for (int y = 0; y < GridHeight; y++)
            for (int x = 0; x < GridWidth; x++)
            {
                var c = GetCell(x, y);
                if (c == null || c.State == CellState.Completed || c.State == CellState.Blocked)
                    globalVisited[y, x] = true;
            }

        // Collect valid targets with stored segment lengths.
        var targetPos = new List<Vector2Int>();
        var targetLen = new List<int>();
        foreach (var sol in solutions)
        {
            int tX = sol.GetX(sol.Length - 1), tY = sol.GetY(sol.Length - 1);
            var tc = GetCell(tX, tY);
            if (tc != null && tc.State == CellState.NumberTarget)
            {
                targetPos.Add(new Vector2Int(tX, tY));
                targetLen.Add(sol.Length);
            }
        }
        if (targetPos.Count == 0) return null;

        // Multi-source Voronoi BFS to measure zone sizes (used for constraint ordering only).
        var zoneDist = new int[GridHeight, GridWidth];
        var zoneOwner = new int[GridHeight, GridWidth];
        for (int y = 0; y < GridHeight; y++)
            for (int x = 0; x < GridWidth; x++) { zoneOwner[y, x] = -1; zoneDist[y, x] = int.MaxValue; }

        var queue = new Queue<Vector2Int>();
        for (int ti = 0; ti < targetPos.Count; ti++)
        {
            var t = targetPos[ti];
            if (globalVisited[t.y, t.x]) continue;
            zoneOwner[t.y, t.x] = ti;
            zoneDist[t.y, t.x] = 0;
            queue.Enqueue(t);
        }
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            int cOwner = zoneOwner[cur.y, cur.x];
            int cDist = zoneDist[cur.y, cur.x];
            void VisitNeighbor(int nx, int ny)
            {
                if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight) return;
                if (globalVisited[ny, nx] || zoneDist[ny, nx] != int.MaxValue) return;
                zoneOwner[ny, nx] = cOwner;
                zoneDist[ny, nx] = cDist + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
            VisitNeighbor(cur.x - 1, cur.y);
            VisitNeighbor(cur.x + 1, cur.y);
            bool isUp = (cur.x + cur.y) % 2 == 0;
            VisitNeighbor(cur.x, cur.y + (isUp ? 1 : -1));
        }

        var zoneSizes = new int[targetPos.Count];
        for (int y = 0; y < GridHeight; y++)
            for (int x = 0; x < GridWidth; x++)
            {
                int o = zoneOwner[y, x];
                if (o >= 0) zoneSizes[o]++;
            }

        // Sort most constrained (smallest zone) first to prevent stranding small segments.
        var order = new List<int>();
        for (int ti = 0; ti < targetPos.Count; ti++) order.Add(ti);
        order.Sort((a, b) => zoneSizes[a] - zoneSizes[b]);

        // Find path for each target through ALL currently available cells.
        // Reserve found cells so subsequent targets cannot overlap.
        var result = new List<List<Vector2Int>>();
        foreach (int ti in order)
        {
            var p = FindPathForTarget(targetPos[ti], targetLen[ti], globalVisited);
            if (p != null)
            {
                foreach (var cell in p) globalVisited[cell.y, cell.x] = true;
                result.Add(p);
            }
            else
            {
                result.Add(new List<Vector2Int> { targetPos[ti] });
            }
        }
        return result;
    }

    // BFS from target through all currently available cells; DFS finds a path of `storedLen` cells.
    private List<Vector2Int> FindPathForTarget(Vector2Int target, int storedLen, bool[,] globalVisited)
    {
        if (globalVisited[target.y, target.x]) return null;

        // BFS from target through all available cells for hop-distances.
        var dist = new int[GridHeight, GridWidth];
        for (int y = 0; y < GridHeight; y++) for (int x = 0; x < GridWidth; x++) dist[y, x] = int.MaxValue;
        dist[target.y, target.x] = 0;
        var bfsQ = new Queue<Vector2Int>();
        bfsQ.Enqueue(target);
        int reachable = 1;
        while (bfsQ.Count > 0)
        {
            var cur = bfsQ.Dequeue();
            void BFSTry(int nx, int ny)
            {
                if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight) return;
                if (globalVisited[ny, nx] || dist[ny, nx] != int.MaxValue) return;
                dist[ny, nx] = dist[cur.y, cur.x] + 1;
                reachable++;
                bfsQ.Enqueue(new Vector2Int(nx, ny));
            }
            BFSTry(cur.x - 1, cur.y);
            BFSTry(cur.x + 1, cur.y);
            bool isUp = (cur.x + cur.y) % 2 == 0;
            BFSTry(cur.x, cur.y + (isUp ? 1 : -1));
        }

        int n = Mathf.Min(storedLen, reachable);
        if (n <= 1) return new List<Vector2Int> { target };

        // Candidate starts: cells reachable from target, not the target itself,
        // sorted by closeness to ideal distance n-1 (maximises DFS convergence speed).
        int ideal = n - 1;
        var starts = new List<Vector2Int>();
        for (int y = 0; y < GridHeight; y++)
            for (int x = 0; x < GridWidth; x++)
            {
                int d = dist[y, x];
                if (d > 0 && d < n) starts.Add(new Vector2Int(x, y));
            }
        starts.Sort((a, b) => System.Math.Abs(dist[a.y, a.x] - ideal)
                             - System.Math.Abs(dist[b.y, b.x] - ideal));

        // DFS from each candidate start through all available cells.
        var visited = new bool[GridHeight, GridWidth];
        var path = new List<Vector2Int>(n);
        foreach (var start in starts)
        {
            // Copy current availability state (globalVisited) into visited for this attempt.
            for (int y = 0; y < GridHeight; y++)
                for (int x = 0; x < GridWidth; x++)
                    visited[y, x] = globalVisited[y, x];
            path.Clear();
            path.Add(start);
            visited[start.y, start.x] = true;
            if (TriangleDFS(path, visited, dist, target, n))
                return path;
        }
        return new List<Vector2Int> { target };
    }

    // DFS with BFS-distance pruning. No zone restriction — searches all available cells.
    private bool TriangleDFS(List<Vector2Int> path, bool[,] visited, int[,] dist,
        Vector2Int target, int total)
    {
        if (path.Count == total)
            return path[path.Count - 1] == target;

        var cur = path[path.Count - 1];
        int remaining = total - path.Count;

        var candidates = new List<Vector2Int>(3);
        void TryAdd(int nx, int ny)
        {
            if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight) return;
            if (visited[ny, nx]) return;
            int vDist = dist[ny, nx];
            if (vDist == int.MaxValue || vDist > remaining - 1) return; // BFS-distance prune
            var v = new Vector2Int(nx, ny);
            if (v == target && remaining > 1) return; // reserve target for last step
            candidates.Add(v);
        }
        TryAdd(cur.x - 1, cur.y);
        TryAdd(cur.x + 1, cur.y);
        bool isUpCur = (cur.x + cur.y) % 2 == 0;
        TryAdd(cur.x, cur.y + (isUpCur ? 1 : -1));

        // Farthest-from-target first: path winds away before returning to target.
        candidates.Sort((a, b) => dist[b.y, b.x] - dist[a.y, a.x]);

        foreach (var next in candidates)
        {
            visited[next.y, next.x] = true;
            path.Add(next);
            if (TriangleDFS(path, visited, dist, target, total))
                return true;
            path.RemoveAt(path.Count - 1);
            visited[next.y, next.x] = false;
        }
        return false;
    }

    public Vector3 GridToWorld(int x, int y)
    {
        if (isThreeGenMode)
        {
            // ▲ (isUp) centroid is 2/3 row-height from top (apex touches strip top);
            // ▽ (isDown) centroid is 1/3 row-height from top (base at strip top).
            bool isUp = (x + y) % 2 == 0;
            float yIntra = isUp ? 2f * RowSpacing / 3f : RowSpacing / 3f;
            return new Vector3(
                GridOrigin.x + x * CellSpacing,
                GridOrigin.y - (y * RowSpacing + yIntra),
                0f
            );
        }
        if (isHexagonMode || isPentagonMode)
        {
            // Column-offset: odd columns shift down 0.5*RowSpacing (both 5gen and 6gen)
            float colOffset = (x % 2 == 1) ? RowSpacing * 0.5f : 0f;
            return new Vector3(
                GridOrigin.x + x * CellSpacing,
                GridOrigin.y - y * RowSpacing - colOffset,
                0f
            );
        }
        return new Vector3(
            GridOrigin.x + x * CellSpacing,
            GridOrigin.y - y * RowSpacing,
            0f
        );
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        if (isThreeGenMode)
        {
            int approxX = Mathf.RoundToInt((worldPos.x - GridOrigin.x) / CellSpacing);
            int approxY = Mathf.RoundToInt((GridOrigin.y - worldPos.y) / RowSpacing);
            // Reject taps that are clearly outside the grid boundary
            if (approxX < 0 || approxX >= GridWidth || approxY < 0 || approxY >= GridHeight)
                return new Vector2Int(-1, -1);
            float bestDist2 = float.MaxValue;
            Vector2Int bestCell = Vector2Int.zero;
            for (int cy = Mathf.Max(0, approxY - 1); cy <= Mathf.Min(GridHeight - 1, approxY + 1); cy++)
                for (int cx = Mathf.Max(0, approxX - 2); cx <= Mathf.Min(GridWidth - 1, approxX + 2); cx++)
                {
                    Vector3 cw = GridToWorld(cx, cy);
                    float dx = worldPos.x - cw.x, dy = worldPos.y - cw.y;
                    float dist2 = dx * dx + dy * dy;
                    if (dist2 < bestDist2) { bestDist2 = dist2; bestCell = new Vector2Int(cx, cy); }
                }
            return bestCell;
        }

        if (!isPentagonMode && !isHexagonMode)
        {
            int x = Mathf.RoundToInt((worldPos.x - GridOrigin.x) / CellSpacing);
            int y = Mathf.RoundToInt((GridOrigin.y - worldPos.y) / CellSpacing);
            // Out-of-bounds coords are already rejected by IsValidGridPos; return as-is
            return new Vector2Int(x, y);
        }

        if (isHexagonMode || isPentagonMode)
        {
            // Column-offset: find nearest cell by brute-force over nearby columns (both 5gen and 6gen)
            int approxX = Mathf.RoundToInt((worldPos.x - GridOrigin.x) / CellSpacing);
            // Reject taps that are clearly outside the grid boundary
            if (approxX < 0 || approxX >= GridWidth)
                return new Vector2Int(-1, -1);
            float colOffset0 = (approxX % 2 == 1) ? RowSpacing * 0.5f : 0f;
            int approxY0 = Mathf.RoundToInt((GridOrigin.y - worldPos.y - colOffset0) / RowSpacing);
            if (approxY0 < 0 || approxY0 >= GridHeight)
                return new Vector2Int(-1, -1);

            float bestDist2 = float.MaxValue;
            Vector2Int bestCell = Vector2Int.zero;

            for (int cx = Mathf.Max(0, approxX - 1); cx <= Mathf.Min(GridWidth - 1, approxX + 1); cx++)
            {
                float colOffset = (cx % 2 == 1) ? RowSpacing * 0.5f : 0f;
                int approxY = Mathf.RoundToInt((GridOrigin.y - worldPos.y - colOffset) / RowSpacing);
                for (int cy = Mathf.Max(0, approxY - 1); cy <= Mathf.Min(GridHeight - 1, approxY + 1); cy++)
                {
                    Vector3 cellWorld = GridToWorld(cx, cy);
                    float dx = worldPos.x - cellWorld.x, dy = worldPos.y - cellWorld.y;
                    float dist2 = dx * dx + dy * dy;
                    if (dist2 < bestDist2) { bestDist2 = dist2; bestCell = new Vector2Int(cx, cy); }
                }
            }
            return bestCell;
        }

        return Vector2Int.zero;
    }

    public bool IsValidGridPos(int x, int y)
    {
        return cells != null && x >= 0 && x < GridWidth && y >= 0 && y < GridHeight;
    }

    public Cell GetCell(int x, int y)
    {
        if (cells == null || !IsValidGridPos(x, y)) return null;
        return cells[x, y];
    }

    public bool IsLevelComplete()
    {
        for (int y = 0; y < GridHeight; y++)
            for (int x = 0; x < GridWidth; x++)
                if (!cells[x, y].IsBlocked && cells[x, y].State != CellState.Completed)
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

        GridWidth = 0;
        GridHeight = 0;
        cells = null;
    }

    void OnEnable()  { ThemeManager.OnThemeChanged += OnThemeChanged; }
    void OnDisable() { ThemeManager.OnThemeChanged -= OnThemeChanged; }

    private void OnThemeChanged()
    {
        if (gridBgRenderer != null && ThemeManager.Instance != null)
            gridBgRenderer.color = ThemeManager.Instance.GridBgColor;

        if (cells == null) return;
        foreach (var cell in cells)
            cell?.RefreshTheme();
    }
}
