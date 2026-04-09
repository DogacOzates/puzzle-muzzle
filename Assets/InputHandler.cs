using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    private GridManager gridManager;
    private GameManager gameManager;

    public void Initialize(GridManager grid, GameManager game)
    {
        gridManager = grid;
        gameManager = game;
    }

    void Update()
    {
        if (gameManager.IsLevelComplete) return;
        if (gameManager.IsTutorialRunning) return;

        var pointer = Pointer.current;
        if (pointer == null) return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        if (pointer.press.wasPressedThisFrame)
            OnTap();
    }

    private Vector3 GetWorldPos()
    {
        var pointer = Pointer.current;
        if (pointer == null) return Vector3.zero;

        Vector2 screenPos = pointer.position.ReadValue();
        Vector3 pos = new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z);
        return Camera.main.ScreenToWorldPoint(pos);
    }

    private void OnTap()
    {
        Vector3 worldPos = GetWorldPos();
        Vector2Int gridPos = gridManager.WorldToGrid(worldPos);

        if (!gridManager.IsValidGridPos(gridPos.x, gridPos.y)) return;

        Cell cell = gridManager.GetCell(gridPos.x, gridPos.y);
        if (cell == null) return;

        if (gridManager.HasActiveSelection)
            HandleSelectionTap(cell);
        else
            HandleIdleTap(cell);
    }

    private void HandleIdleTap(Cell cell)
    {
        switch (cell.State)
        {
            case CellState.Empty:
                // Start a new chain from this empty cell
                gridManager.TryStartFromEmpty(cell);
                break;

            case CellState.NumberTarget:
                // If target number is 1, auto-complete immediately
                if (cell.TargetNumber == 1)
                {
                    gridManager.TryCompleteTarget1(cell);
                    if (gridManager.IsLevelComplete())
                        gameManager.OnLevelComplete();
                }
                break;

            case CellState.Completed:
                // Remove a completed block
                gridManager.TryRemoveBlock(cell);
                break;
        }
    }

    private void HandleSelectionTap(Cell cell)
    {
        // 1. Tap last selected cell → undo one step
        if (cell.State == CellState.Selecting && gridManager.IsLastInChain(cell))
        {
            gridManager.UndoLastStep();
            return;
        }

        // 2. Tap adjacent empty or matching target → extend chain
        if (cell.State == CellState.Empty || cell.State == CellState.NumberTarget)
        {
            if (gridManager.TryExtendSelection(cell))
            {
                if (gridManager.IsLevelComplete())
                    gameManager.OnLevelComplete();
                return;
            }

            // Not adjacent or target didn't match count
            // If it's an empty cell, cancel current and start fresh there
            if (cell.State == CellState.Empty)
            {
                gridManager.CancelSelection();
                gridManager.TryStartFromEmpty(cell);
                return;
            }
        }

        // 3. Tap completed block → cancel selection, remove block
        if (cell.State == CellState.Completed)
        {
            gridManager.CancelSelection();
            gridManager.TryRemoveBlock(cell);
            return;
        }
    }
}
