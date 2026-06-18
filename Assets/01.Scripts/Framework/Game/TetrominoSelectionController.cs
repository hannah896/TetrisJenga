using System.Collections.Generic;
using UnityEngine;

public class TetrominoSelectionController : MonoBehaviour
{
    readonly List<Vector2Int> selected = new();

    public List<Vector2Int> Selected       => selected;
    public bool HasFocusedCell             { get; set; }
    public Vector2Int FocusedCell          { get; set; }
    public bool IsPresetSelectionActive    { get; set; }
    public TetrominoPreset Preset          { get; set; }
    public Vector2Int Anchor               { get; set; }
    public int Rotation                    { get; set; }

    public void ResetState()
    {
        selected.Clear();
        HasFocusedCell          = false;
        FocusedCell             = Vector2Int.zero;
        IsPresetSelectionActive = false;
        Preset                  = default;
        Anchor                  = Vector2Int.zero;
        Rotation                = 0;
    }

    public void SetFocus(BlockExtractionController extraction, Vector2Int cell)
    {
        var prev     = FocusedCell;
        bool hadFocus = HasFocusedCell;
        FocusedCell   = cell;
        HasFocusedCell = true;
        if (hadFocus && prev != cell)
            extraction.ApplyCellVisual(prev);
        extraction.ApplyCellVisual(cell);
    }

    public void ClearFocus() => HasFocusedCell = false;

    public void EnsureFocusedCell(BlockExtractionController extraction)
    {
        if (HasFocusedCell && extraction.HasCell(FocusedCell))
            return;

        extraction.FocusDefaultExtractionCell();
    }

    public void MoveFocus(BlockExtractionController extraction, Vector2Int dir)
    {
        if (!HasFocusedCell)
        {
            extraction.FocusDefaultExtractionCell();
            return;
        }

        var next = FocusedCell + dir;
        if (extraction.HasCell(next) && extraction.IsExtractableCell(next))
            SetFocus(extraction, next);
    }

    public void ToggleFocusedSelection(BlockExtractionController extraction)
    {
        if (!HasFocusedCell) return;

        if (selected.Contains(FocusedCell))
            extraction.TryDeselect(FocusedCell);
        else if (selected.Count < 4 && (selected.Count == 0 || IsAdjacentToSelected(FocusedCell)))
            extraction.SelectCell(FocusedCell);
    }

    public void BeginPresetSelection(BlockExtractionController extraction, TetrominoPreset preset)
    {
        extraction.ClearSelectedCellsOnly();
        IsPresetSelectionActive = true;
        Preset  = preset;
        Anchor  = FocusedCell;
        Rotation = 0;
        ApplyPresetSelection(extraction);
    }

    public void HandlePresetSelectionInput(
        BlockExtractionController extraction,
        bool hasMove,
        Vector2Int dir,
        bool hasConfirm,
        bool hasPreset,
        TetrominoPreset preset,
        bool hasTab,
        bool hasPresetRotate,
        bool hasPresetHalfTurn)
    {
        if (hasTab)                { ExitPresetSelectionToFocus(extraction); return; }
        if (hasPreset)
        {
            if (Preset == preset) TrySetPresetSelection(extraction, Anchor, Preset, (Rotation + 1) % 4);
            else                  TrySetPresetSelection(extraction, Anchor, preset, 0);
            return;
        }
        if (hasPresetHalfTurn)     { TrySetPresetSelection(extraction, Anchor, Preset, (Rotation + 2) % 4); return; }
        if (hasPresetRotate)       { TrySetPresetSelection(extraction, Anchor, Preset, (Rotation + 1) % 4); return; }
        if (hasMove)               TrySetPresetSelection(extraction, Anchor + dir, Preset, Rotation);
        if (hasConfirm)            ConfirmPresetSelection(extraction);
    }

    public bool TrySetPresetSelection(BlockExtractionController extraction, Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        if (!PresetOverlapsAnyExtractableCell(extraction, anchor, preset, rotation))
        {
            extraction.PlayPlacementFailFeedback();
            ApplyPresetSelection(extraction);
            return false;
        }

        Anchor   = anchor;
        Preset   = preset;
        Rotation = ((rotation % 4) + 4) % 4;
        ApplyPresetSelection(extraction);
        return true;
    }

    public void ExitPresetSelectionToFocus(BlockExtractionController extraction)
    {
        var anchor = Anchor;
        extraction.ClearSelectedCellsOnly();
        extraction.ClearPresetOutlinePreview();
        IsPresetSelectionActive = false;
        if (extraction.IsExtractableCell(anchor))
            SetFocus(extraction, anchor);
    }

    public bool ApplyPresetSelection(BlockExtractionController extraction)
    {
        if (!IsPresetSelectionActive) return false;

        extraction.ClearSelectedCellsOnly();
        SetFocus(extraction, Anchor);
        var presetCells = extraction.GetPresetCells(Anchor, Preset, Rotation);
        extraction.CreatePresetOutlinePreview(presetCells);
        if (CanApplyPresetSelection(extraction, Anchor, Preset, Rotation))
        {
            foreach (var cell in presetCells)
                extraction.SelectCell(cell);
        }
        return true;
    }

    public bool CanApplyPresetSelection(BlockExtractionController extraction, Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        foreach (var cell in extraction.GetPresetCells(anchor, preset, rotation))
            if (!extraction.IsExtractableCell(cell))
                return false;
        return true;
    }

    public bool PresetOverlapsAnyExtractableCell(BlockExtractionController extraction, Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        foreach (var cell in extraction.GetPresetCells(anchor, preset, rotation))
            if (extraction.IsExtractableCell(cell))
                return true;
        return false;
    }

    public void ConfirmPresetSelection(BlockExtractionController extraction)
    {
        if (selected.Count > 0)
        {
            IsPresetSelectionActive = false;
            extraction.LiftBlocks();
        }
        else
        {
            extraction.PlayPlacementFailFeedback();
        }
    }

    public void CancelPresetSelection() => IsPresetSelectionActive = false;

    public bool IsAdjacentToSelected(Vector2Int cell)
    {
        foreach (var s in selected)
            if (Mathf.Abs(cell.x - s.x) + Mathf.Abs(cell.y - s.y) == 1)
                return true;
        return false;
    }
}
