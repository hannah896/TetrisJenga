using System.Collections.Generic;
using UnityEngine;

public class TetrominoSelectionController : MonoBehaviour
{
    readonly List<Vector2Int> selected = new();

    public List<Vector2Int> Selected => selected;
    public bool HasFocusedCell { get; set; }
    public Vector2Int FocusedCell { get; set; }

    public bool IsPresetSelectionActive { get; set; }

public TetrominoPreset Preset { get; set; }
    public Vector2Int Anchor { get; set; }
    public int Rotation { get; set; }

    public void ResetState()
    {
        selected.Clear();
        HasFocusedCell = false;
        FocusedCell = Vector2Int.zero;
        IsPresetSelectionActive = false;
        Preset = default;
        Anchor = Vector2Int.zero;
        Rotation = 0;
    }

    public void SetFocus(BlockTower tower, Vector2Int cell)
    {
        var prev = FocusedCell;
        bool hadFocus = HasFocusedCell;
        FocusedCell = cell;
        HasFocusedCell = true;
        if (hadFocus && prev != cell)
            tower.SelectionApplyCellVisual(prev);
        tower.SelectionApplyCellVisual(cell);
    }

    public void ClearFocus()
    {
        HasFocusedCell = false;
    }

    public void EnsureFocusedCell(BlockTower tower)
    {
        if (HasFocusedCell && tower.SelectionHasCell(FocusedCell))
            return;

        tower.SelectionFocusDefaultExtractionCell();
    }

    public void MoveFocus(BlockTower tower, Vector2Int dir)
    {
        if (!HasFocusedCell)
        {
            tower.SelectionFocusDefaultExtractionCell();
            return;
        }

        var next = FocusedCell + dir;
        if (tower.SelectionHasCell(next) && tower.SelectionIsExtractableCell(next))
            SetFocus(tower, next);
    }

    public void ToggleFocusedSelection(BlockTower tower)
    {
        if (!HasFocusedCell)
            return;

        if (selected.Contains(FocusedCell))
        {
            tower.SelectionTryDeselect(FocusedCell);
        }
        else if (selected.Count < 4 && (selected.Count == 0 || IsAdjacentToSelected(FocusedCell)))
        {
            tower.SelectionSelectCell(FocusedCell);
        }
    }

    public void BeginPresetSelection(BlockTower tower, TetrominoPreset preset)
    {
        tower.SelectionClearSelectedCellsOnly();
        IsPresetSelectionActive = true;
        Preset = preset;
        Anchor = FocusedCell;
        Rotation = 0;
        ApplyPresetSelection(tower);
    }

    public void HandlePresetSelectionInput(
        BlockTower tower,
        bool hasMove,
        Vector2Int dir,
        bool hasConfirm,
        bool hasPreset,
        TetrominoPreset preset,
        bool hasTab,
        bool hasPresetRotate,
        bool hasPresetHalfTurn)
    {
        if (hasTab)
        {
            ExitPresetSelectionToFocus(tower);
            return;
        }

        if (hasPreset)
        {
            if (Preset == preset)
                TrySetPresetSelection(tower, Anchor, Preset, (Rotation + 1) % 4);
            else
                TrySetPresetSelection(tower, Anchor, preset, 0);
            return;
        }

        if (hasPresetHalfTurn)
        {
            TrySetPresetSelection(tower, Anchor, Preset, (Rotation + 2) % 4);
            return;
        }

        if (hasPresetRotate)
        {
            TrySetPresetSelection(tower, Anchor, Preset, (Rotation + 1) % 4);
            return;
        }

        if (hasMove)
            TrySetPresetSelection(tower, Anchor + dir, Preset, Rotation);

        if (hasConfirm)
            ConfirmPresetSelection(tower);
    }

    public bool TrySetPresetSelection(BlockTower tower, Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        if (!PresetOverlapsAnyExtractableCell(tower, anchor, preset, rotation))
        {
            tower.SelectionPlayPlacementFailFeedback();
            ApplyPresetSelection(tower);
            return false;
        }

        Anchor = anchor;
        Preset = preset;
        Rotation = ((rotation % 4) + 4) % 4;
        ApplyPresetSelection(tower);
        return true;
    }

    public void ExitPresetSelectionToFocus(BlockTower tower)
    {
        var anchor = Anchor;
        tower.SelectionClearSelectedCellsOnly();
        tower.ClearPresetOutlinePreview();
        IsPresetSelectionActive = false;
        if (tower.SelectionIsExtractableCell(anchor))
            SetFocus(tower, anchor);
    }

    public bool ApplyPresetSelection(BlockTower tower)
    {
        if (!IsPresetSelectionActive)
            return false;

        tower.SelectionClearSelectedCellsOnly();
        SetFocus(tower, Anchor);
        var presetCells = tower.GetPresetCells(Anchor, Preset, Rotation);
        tower.SelectionCreatePresetOutlinePreview(presetCells);
        if (CanApplyPresetSelection(tower, Anchor, Preset, Rotation))
        {
            foreach (var cell in presetCells)
                tower.SelectionSelectCell(cell);
        }

        return true;
    }

    public bool CanApplyPresetSelection(BlockTower tower, Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        foreach (var cell in tower.GetPresetCells(anchor, preset, rotation))
            if (!tower.SelectionIsExtractableCell(cell))
                return false;
        return true;
    }

    public bool PresetOverlapsAnyExtractableCell(BlockTower tower, Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        foreach (var cell in tower.GetPresetCells(anchor, preset, rotation))
            if (tower.SelectionIsExtractableCell(cell))
                return true;
        return false;
    }

    public void ConfirmPresetSelection(BlockTower tower)
    {
        if (selected.Count > 0)
        {
            IsPresetSelectionActive = false;
            tower.SelectionLiftBlocks();
        }
        else
        {
            tower.SelectionPlayPlacementFailFeedback();
        }
    }

    public void CancelPresetSelection()
    {
        IsPresetSelectionActive = false;
    }

    public bool IsAdjacentToSelected(Vector2Int cell)
    {
        foreach (var s in selected)
            if (Mathf.Abs(cell.x - s.x) + Mathf.Abs(cell.y - s.y) == 1)
                return true;
        return false;
    }
}
