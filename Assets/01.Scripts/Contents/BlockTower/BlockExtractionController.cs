using System.Collections.Generic;
using UnityEngine;
using JSAM;

public class BlockExtractionController : MonoBehaviour
{
    BlockTower              _tower;
    HeldBlockController     _held;
    TetrominoSelectionController _selection;
    HeldPlacementController _heldPlacement;
    TowerPhysicsController  _physics;
    TowerCellVisualizer     _visualizer;
    BombIceEffectController _bombIce;
    ScoreController         _score;
    TowerSceneBuilder       _sceneBuilder;

    readonly Color _failFlashColor = new(1f, 0.08f, 0.08f, 0.85f);

    int _extractionMinRow, _extractionMaxRow;
    int _extractionMinCol, _extractionMaxCol;
    Vector2 _lastExtractionCenter;
    GameObject _presetOutlineRoot;

    public int ExtractionMinRow => _extractionMinRow;
    public int ExtractionMaxRow => _extractionMaxRow;
    public int ExtractionMinCol => _extractionMinCol;
    public int ExtractionMaxCol => _extractionMaxCol;

    void Awake()
    {
        _tower         = GetComponent<BlockTower>();
        _held          = GetComponent<HeldBlockController>();
        _selection     = GetComponent<TetrominoSelectionController>();
        _heldPlacement = GetComponent<HeldPlacementController>();
        _physics       = GetComponent<TowerPhysicsController>();
        _visualizer    = GetComponent<TowerCellVisualizer>();
        _bombIce       = GetComponent<BombIceEffectController>();
        _score         = GetComponent<ScoreController>();
        _sceneBuilder  = GetComponent<TowerSceneBuilder>();
    }

    void Update()
    {
        if (!Application.isPlaying || _tower.IsGameOver) return;
        UpdatePresetOutlineFeedback();
    }

    // ── Grid Query (for TetrominoSelectionController) ────────────────

    public bool HasCell(Vector2Int cell)               => _tower.Grid.HasCell(cell);
    public bool IsExtractableCell(Vector2Int cell)     => _tower.Grid.IsExtractableCell(cell);
    public void ApplyCellVisual(Vector2Int cell)       => _visualizer?.ApplyCellVisual(cell);
    public void PlayPlacementFailFeedback()            => _held.PlayPlacementFailFeedback();
    public List<Vector2Int> GetPresetCells(Vector2Int anchor, TetrominoPreset preset, int rotation) =>
        TetrominoShapeUtil.GetCells(anchor, preset, rotation);

    // ── Selection ────────────────────────────────────────────────────

    public void HandleClick()
    {
        _physics?.RefreshDetachedComponents();

        var local = _tower.TowerRoot.InverseTransformPoint(Util.MouseWorldPos());
        var cell  = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));

        if (!IsExtractableCell(cell)) return;

        if (_selection.IsPresetSelectionActive)
        {
            ClearSelectedCellsOnly();
            ClearPresetOutlinePreview();
            _selection.CancelPresetSelection();
        }

        SetFocusCell(cell);

        if (_selection.Selected.Contains(cell))
        {
            TryDeselect(cell);
            return;
        }

        if (_selection.Selected.Count > 0 && !_selection.IsAdjacentToSelected(cell))
            ClearSelectedCellsOnly();

        if (_selection.Selected.Count < 4)
        {
            SelectCell(cell);
            if (_selection.Selected.Count == 4)
                LiftBlocks();
        }
    }

    public void SelectCell(Vector2Int cell)
    {
        var grid = _tower.Grid;
        if (!grid.TryGetCell(cell, out var state)) return;
        if (!state.isOriginalTower || state.kind == CellKind.Ice) return;
        if (_selection.Selected.Contains(cell)) return;
        _selection.Selected.Add(cell);
        _visualizer?.ApplyCellVisual(cell);
    }

    void DeselectCell(Vector2Int cell)
    {
        _selection.Selected.Remove(cell);
        _visualizer?.ApplyCellVisual(cell);
    }

    public void TryDeselect(Vector2Int cell)
    {
        var remaining = new List<Vector2Int>(_selection.Selected);
        remaining.Remove(cell);
        if (remaining.Count <= 1 || _tower.Grid.IsConnected(remaining))
            DeselectCell(cell);
    }

    public void ClearSelection()
    {
        _selection.IsPresetSelectionActive = false;
        ClearPresetOutlinePreview();
        foreach (var c in new List<Vector2Int>(_selection.Selected))
            DeselectCell(c);
    }

    public void ClearSelectedCellsOnly()
    {
        foreach (var c in new List<Vector2Int>(_selection.Selected))
            DeselectCell(c);
    }

    // ── Lift / Cancel ─────────────────────────────────────────────────

    public void LiftBlocks()
    {
        _selection.IsPresetSelectionActive = false;
        ClearPresetOutlinePreview();

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in _selection.Selected)
        {
            minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y);
            maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y);
        }

        var extractionScorePos = _tower.TowerRoot.TransformPoint(new Vector3(
            (minX + maxX + 1f) * 0.5f, (minY + maxY + 1f) * 0.5f, 0f));

        _held.Center = new Vector2((maxX - minX + 1) * 0.5f, (maxY - minY + 1) * 0.5f);
        _held.RelPos.Clear();
        _held.Data.Clear();
        _held.SourceCells.Clear();
        _held.StartScore = _score?.Score ?? 0;
        _lastExtractionCenter = new Vector2((minX + maxX + 1f) * 0.5f, (minY + maxY + 1f) * 0.5f);

        foreach (var c in _selection.Selected)
            _held.RelPos.Add(new Vector2Int(c.x - minX, c.y - minY));
        _held.MatchesBonus = TetrominoShapeUtil.ShapeMatchesPreset(
            _held.RelPos, _score?.BonusTargetPreset ?? TetrominoPreset.I);

        var grid      = _tower.Grid;
        var cellViews = _tower.CellViews;

        foreach (var pair in grid.AllCells)
            _held.SourceCells.Add(new HeldSourceCell
            {
                cell = pair.Key, number = pair.Value.number,
                isOriginalTower = pair.Value.isOriginalTower, kind = pair.Value.kind
            });

        var changedCells = new List<Vector2Int>();
        var bombCells    = new List<Vector2Int>();
        foreach (var cell in _selection.Selected)
        {
            if (!grid.TryGetCell(cell, out var state)) continue;
            if (!cellViews.TryGetValue(cell, out var view)) continue;
            changedCells.Add(cell);
            if (state.kind == CellKind.Bomb) bombCells.Add(cell);
            state.number--;
            if (state.number <= 0)
            {
                Destroy(view.go);
                grid.RemoveCell(cell);
                cellViews.Remove(cell);
            }
            else
            {
                var bc = view.go.GetComponent<BlockCell>();
                if (bc != null) { bc.Weight = state.number; bc.IsOriginalTower = state.isOriginalTower; bc.Kind = state.kind; }
                _visualizer?.UpdateCellDataVisuals(state, view);
            }
        }

        _selection.Selected.Clear();
        _selection.HasFocusedCell = false;
        foreach (var cell in changedCells) _visualizer?.ApplyCellVisual(cell);
        foreach (var bomb  in bombCells)   _bombIce?.TriggerBombAt(bomb);

        _held.Root = new GameObject("HeldBlocks");
        _tower.TrackGeneratedObject(_held.Root);
        _held.Root.transform.SetParent(_tower.transform);

        var pc        = _visualizer?.PlacedBlockColor ?? new Color(0.55f, 0.58f, 0.60f, 1f);
        var heldColor = new Color(pc.r, pc.g, pc.b, 0.6f);
        float bodyScale = _physics?.BlockBodyScale ?? 0.94f;

        for (int i = 0; i < _held.RelPos.Count; i++)
        {
            var rel = _held.RelPos[i];
            var go = new GameObject($"Held_{i}");
            _tower.TrackGeneratedObject(go);
            go.transform.SetParent(_held.Root.transform, false);
            go.transform.localPosition = new Vector3(rel.x + 0.5f - _held.Center.x, rel.y + 0.5f - _held.Center.y, 0f);
            go.transform.localScale    = Vector3.one * bodyScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _visualizer?.CreateBlockSprite();
            sr.color  = heldColor;
            sr.sortingOrder = 20;
            var blurRenderers = _visualizer?.CreatePreviewBlur(go.transform) ?? new List<SpriteRenderer>();

            var box = go.AddComponent<BoxCollider>();
            box.size           = Vector3.one * (_physics?.LocalColliderSize() ?? 0.92f);
            box.sharedMaterial = _physics?.CreateFrictionMaterial();
            box.enabled        = false;

            var bc = go.AddComponent<BlockCell>();
            bc.Weight = 1; bc.IsOriginalTower = false;

            _held.Data.Add((
                new CellState { number = 1, isOriginalTower = false },
                new CellView  { go = go, sr = sr, previewBlurRenderers = blurRenderers }));
        }

        _physics?.CheckForDetachment();
        _physics?.UpdateTowerPhysicsState();

        AudioManager.PlaySound(_AudioLibrarySounds.Hold);
        _held.IsHolding = true;
        if (_heldPlacement != null)
            _held.BaseCell = _heldPlacement.GetDefaultHeldBaseCell();
        _held.UsingKeyboardPlacement = true;
        _tower.OnBlocksLifted?.Invoke();
        _score?.AddScore(_held.MatchesBonus ? 2 : 1, extractionScorePos);
        _score?.RollBonusTarget();
    }

    void RestoreHeldSourceCells()
    {
        var grid         = _tower.Grid;
        var cellViews    = _tower.CellViews;
        var iceCellViews = _tower.IceCellViews;

        foreach (var pair in cellViews)
            if (pair.Value.go != null) _tower.DestroyTracked(pair.Value.go);

        grid.Clear();
        cellViews.Clear();
        iceCellViews.Clear();
        _physics?.ClearDetachedBlocks();

        foreach (var source in _held.SourceCells)
        {
            var (state, view) = SpawnCell(source.cell, source.number, source.isOriginalTower);
            state.kind = source.kind;
            state.concealedByBomb = _bombIce?.IsConcealedByBomb(source.cell) ?? false;
            _visualizer?.UpdateCellDataVisuals(state, view);
            grid.AddCell(source.cell, state);
            cellViews[source.cell] = view;
            _visualizer?.ApplyCellVisual(source.cell);
        }

        _score?.SetScoreTo(_held.StartScore);
        _heldPlacement?.ResetPlacementMemory();
        _selection.ClearFocus();
        UpdateExtractionTowerRowsFromCells();
    }

    public void CancelHold()
    {
        if (!_held.IsHolding) return;
        if (_held.Root != null) { Destroy(_held.Root); _held.Root = null; }
        RestoreHeldSourceCells();
        _held.RelPos.Clear();
        _held.Data.Clear();
        _held.SourceCells.Clear();
        _held.StartScore           = 0;
        _held.MatchesBonus         = false;
        _held.IsHolding            = false;
        _held.UsingKeyboardPlacement = false;
        _physics?.UpdateTowerPhysicsState();
        _tower.OnHoldCancelled?.Invoke();
        FocusDefaultExtractionCell();
        _score?.AddScore(-2, _tower.transform.position);
    }

    // ── Preset Outline ────────────────────────────────────────────────

    public void CreatePresetOutlinePreview(List<Vector2Int> cells)
    {
        ClearPresetOutlinePreview();
        _presetOutlineRoot = new GameObject("PresetOutlinePreview");
        _tower.TrackGeneratedObject(_presetOutlineRoot);
        _presetOutlineRoot.transform.SetParent(_tower.TowerRoot, false);
        Util.SetNoPostLayer(_presetOutlineRoot);

        foreach (var cell in cells)
        {
            var go = new GameObject($"PresetOutline_{cell.x}_{cell.y}");
            _tower.TrackGeneratedObject(go);
            go.transform.SetParent(_presetOutlineRoot.transform, false);
            Util.SetNoPostLayer(go);
            go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0.04f);
            go.transform.localScale    = Vector3.one * (_visualizer?.SelectedOutlineScale ?? 1.10f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _visualizer?.CreateOutlineSprite();
            sr.color  = _visualizer?.SelectedOutlineColor ?? new Color(1f, 1f, 1f, 0.95f);
            sr.sortingOrder = 5;
        }
    }

    void UpdatePresetOutlineFeedback()
    {
        if (_presetOutlineRoot == null) return;
        bool isFailing = Time.time < _held.FailEndTime;
        _presetOutlineRoot.transform.localPosition = _held.FailShakeOffset(isFailing);
        var color = isFailing ? _failFlashColor : (_visualizer?.SelectedOutlineColor ?? new Color(1f, 1f, 1f, 0.95f));
        foreach (var sr in _presetOutlineRoot.GetComponentsInChildren<SpriteRenderer>())
            sr.color = color;
    }

    public void ClearPresetOutlinePreview()
    {
        if (_presetOutlineRoot != null) { _tower.DestroyTracked(_presetOutlineRoot); _presetOutlineRoot = null; }
    }

    // ── Extraction Zone ───────────────────────────────────────────────

    public void UpdateExtractionTowerRowsFromCells()
    {
        var grid = _tower.Grid;
        bool hasOriginal = false;
        foreach (var pair in grid.AllCells)
            if (pair.Value.isOriginalTower) { hasOriginal = true; break; }

        if (!hasOriginal)
        {
            _extractionMinCol = 0; _extractionMaxCol = _tower.columns - 1;
            _extractionMinRow = 0; _extractionMaxRow = _tower.rows - 1;
            _sceneBuilder?.UpdateTowerStackDivider();
            return;
        }

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var pair in grid.AllCells)
        {
            var cell = pair.Key;
            if (!grid.IsExtractableCell(cell)) continue;
            minX = Mathf.Min(minX, cell.x); maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y); maxY = Mathf.Max(maxY, cell.y);
        }
        _extractionMinCol = minX == int.MaxValue ? 0               : minX;
        _extractionMaxCol = maxX == int.MinValue ? _tower.columns-1 : maxX;
        _extractionMinRow = minY == int.MaxValue ? 0               : minY;
        _extractionMaxRow = maxY == int.MinValue ? -1              : maxY;
        _sceneBuilder?.UpdateTowerStackDivider();
    }

    bool IsInExtractionTowerRows(Vector2Int cell) =>
        cell.x >= _extractionMinCol && cell.x <= _extractionMaxCol &&
        cell.y >= _extractionMinRow && cell.y <= _extractionMaxRow;

    // ── Focus ─────────────────────────────────────────────────────────

    void SetFocusCell(Vector2Int cell) => _selection.SetFocus(this, cell);

    public void FocusDefaultExtractionCell()
    {
        _selection.ClearFocus();
        if (TryFindFocusNearLastExtraction(ignoreLastPlaced: true,  out var best) ||
            TryFindFocusNearLastExtraction(ignoreLastPlaced: false, out best))
        { SetFocusCell(best); return; }

        if (TryFindDefaultFocusCell(ignoreLastPlaced: true,  out var cell) ||
            TryFindDefaultFocusCell(ignoreLastPlaced: false, out cell))
            SetFocusCell(cell);
    }

    bool TryFindFocusNearLastExtraction(bool ignoreLastPlaced, out Vector2Int best)
    {
        best = default;
        bool found = false;
        float bestDist = float.MaxValue;
        foreach (var pair in _tower.Grid.AllCells)
        {
            var cell = pair.Key;
            if (ignoreLastPlaced && _heldPlacement.IsLastPlaced(cell)) continue;
            if (!_tower.Grid.IsExtractableCell(cell)) continue;
            if (!IsInExtractionTowerRows(cell)) continue;
            float dist = Vector2.SqrMagnitude(new Vector2(cell.x + 0.5f, cell.y + 0.5f) - _lastExtractionCenter);
            if (!found || dist < bestDist || (Mathf.Approximately(dist, bestDist) && cell.y > best.y))
            { best = cell; bestDist = dist; found = true; }
        }
        return found;
    }

    bool TryFindDefaultFocusCell(bool ignoreLastPlaced, out Vector2Int cell)
    {
        cell = default;
        foreach (var pair in _tower.Grid.AllCells)
        {
            var c = pair.Key;
            if (ignoreLastPlaced && _heldPlacement.IsLastPlaced(c)) continue;
            if (!_tower.Grid.IsExtractableCell(c)) continue;
            if (!IsInExtractionTowerRows(c)) continue;
            cell = c; return true;
        }
        return false;
    }

    // ── SpawnCell ──────────────────────────────────────────────────────

    (CellState state, CellView view) SpawnCell(Vector2Int cell, int number, bool isOriginalTower)
    {
        var go = new GameObject($"Cell_{cell.x}_{cell.y}");
        _tower.TrackGeneratedObject(go);
        go.transform.SetParent(_tower.TowerRoot, false);
        go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        go.transform.localScale    = Vector3.one * (_physics?.BlockBodyScale ?? 0.94f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _visualizer?.CreateBlockSprite();
        sr.sortingOrder = 0;

        var box = go.AddComponent<BoxCollider>();
        box.size           = Vector3.one * (_physics?.LocalColliderSize() ?? 0.92f);
        box.sharedMaterial = _physics?.CreateFrictionMaterial();

        var bc = go.AddComponent<BlockCell>();
        bc.Weight = number; bc.IsOriginalTower = isOriginalTower; bc.Kind = CellKind.Normal;

        var outline = _visualizer?.SpawnCellOutline(go.transform);
        var label   = _visualizer?.SpawnLabel(number, go.transform);
        var state   = new CellState { number = number, isOriginalTower = isOriginalTower, kind = CellKind.Normal };
        var view    = new CellView  { go = go, sr = sr, outline = outline, label = label };

        _visualizer?.UpdateCellDataVisuals(state, view);
        return (state, view);
    }
}
