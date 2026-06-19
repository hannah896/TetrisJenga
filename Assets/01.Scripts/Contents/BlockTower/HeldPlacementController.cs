using System.Collections.Generic;
using UnityEngine;
using JSAM;

public class HeldPlacementController : MonoBehaviour
{
    BlockTower              _tower;
    HeldBlockController     _held;
    PlacementZoneController _placement;
    TowerPhysicsController  _physics;
    TowerCellVisualizer     _visualizer;
    TetrominoSelectionController _selection;
    TowerSceneBuilder       _sceneBuilder;

    readonly Color _validHoldColor   = new(0.55f, 0.85f, 0.6f,  0.5f);
    readonly Color _invalidHoldColor = new(1f,    0.25f, 0.25f, 0.6f);
    readonly Color _failFlashColor   = new(1f,    0.08f, 0.08f, 0.85f);

    readonly HashSet<Vector2Int> _lastPlacedCells = new();
    Vector2 _lastPlacementCenter;
    bool    _hasLastPlacementCenter;

    public bool   HasLastPlacementCenter               => _hasLastPlacementCenter;
    public Vector2 LastPlacementCenter                 => _lastPlacementCenter;
    public bool IsLastPlaced(Vector2Int cell) => _lastPlacedCells.Contains(cell);

    void Awake()
    {
        _tower      = GetComponent<BlockTower>();
        _held       = GetComponent<HeldBlockController>();
        _placement  = GetComponent<PlacementZoneController>();
        _physics    = GetComponent<TowerPhysicsController>();
        _visualizer = GetComponent<TowerCellVisualizer>();
        _selection  = GetComponent<TetrominoSelectionController>();
        _sceneBuilder = GetComponent<TowerSceneBuilder>();
    }

    public void ResetPlacementMemory()
    {
        _lastPlacedCells.Clear();
        _hasLastPlacementCenter = false;
    }

    public void InitKeyboardPlacement()
    {
        _held.BaseCell = _placement.ClampHeldBase(_held.BaseCell);
        _held.UsingKeyboardPlacement = true;
    }

    public void MoveHeldBase(Vector2Int dir)
    {
        _held.MoveHeldBase(dir, () => _tower.OnBlocksLifted?.Invoke());
        _held.BaseCell = _placement.ClampHeldBase(_held.BaseCell);
    }

    public void UpdateHeldPosition()
    {
        bool canPlace  = CanPlaceHeldBlocks(out _, out var snappedWorldPos);
        bool isFailing = Time.time < _held.FailEndTime;
        var previewColor = isFailing ? _failFlashColor
                         : canPlace  ? _validHoldColor
                                     : _invalidHoldColor;

        _held.Root.transform.position = snappedWorldPos + _held.FailShakeOffset(isFailing);
        _held.SetPreviewColor(previewColor, _visualizer?.PreviewBlurAlpha ?? 0.10f);
    }

    public void UpdateHeldBaseFromMousePosition()
    {
        if (_tower.TowerRoot == null) return;
        var local = _tower.TowerRoot.InverseTransformPoint(Util.MouseWorldPos());
        _held.BaseCell = _placement.ClampHeldBase(new Vector2Int(
            Mathf.RoundToInt(local.x - _held.Center.x),
            Mathf.RoundToInt(local.y - _held.Center.y)));
    }

    public void TryPlaceHeldBlocks() => TryPlaceBlocks();

    public void DropHeldToNearestSurfaceAndPlace()
    {
        if (!TryFindNearestDropBase(_held.BaseCell, out var dropBase))
        {
            _held.PlayPlacementFailFeedback();
            return;
        }
        _held.BaseCell = dropBase;
        TryPlaceBlocks();
    }

    public Vector2Int GetDefaultHeldBaseCell()
    {
        if (_hasLastPlacementCenter)
        {
            var lastBase = new Vector2Int(
                Mathf.RoundToInt(_lastPlacementCenter.x - _held.Center.x),
                Mathf.RoundToInt(_lastPlacementCenter.y - _held.Center.y));
            return _placement.ClampHeldBase(lastBase);
        }

        float cx = (_placement.placementMin.x + _placement.placementMax.x + 1f) * 0.5f;
        int   baseX = Mathf.RoundToInt(cx - _held.Center.x);
        return _placement.ClampHeldBase(new Vector2Int(baseX, _tower.Grid.HighestOccupiedRow() + 1));
    }

    void TryPlaceBlocks()
    {
        if (!CanPlaceHeldBlocks(out var targets, out _))
        {
            _held.PlayPlacementFailFeedback();
            return;
        }

        _lastPlacedCells.Clear();
        RememberLastPlacementCenter(targets);

        var grid       = _tower.Grid;
        var cellViews  = _tower.CellViews;
        var towerRoot  = _tower.TowerRoot;
        float bodyScale = _physics?.BlockBodyScale ?? 0.94f;

        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var (heldState, heldView) = _held.Data[i];

            _visualizer?.ClearPreviewBlur(heldView);
            if (grid.TryGetCell(target, out var existing))
            {
                existing.number = Mathf.Min(6, existing.number + 1);
                if (cellViews.TryGetValue(target, out var existingView))
                    _visualizer?.UpdateCellDataVisuals(existing, existingView);
                _visualizer?.ApplyCellVisual(target);
                _lastPlacedCells.Add(target);
                if (heldView.go != null) Destroy(heldView.go);
                continue;
            }

            heldView.go.transform.SetParent(towerRoot, false);
            heldView.go.transform.localPosition = new Vector3(target.x + 0.5f, target.y + 0.5f, 0f);
            heldView.go.transform.localScale    = Vector3.one * bodyScale;

            var box = heldView.go.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.size    = Vector3.one * (_physics?.LocalColliderSize() ?? 0.92f);
                box.enabled = true;
            }

            heldView.sr.sortingOrder = 0;
            if (heldView.label == null)
                heldView.label = _visualizer?.SpawnLabel(heldState.number, heldView.go.transform);

            // 분할선 위에 배치된 블록은 원본 타워로 취급하지 않는다.
            // 그렇지 않으면 ExtractionMaxRow가 올라가 placement zone이 계속 줄어든다.
            if (heldState.isOriginalTower && target.y >= _tower.ExtractionMaxRow + 1)
                heldState.isOriginalTower = false;

            var bc = heldView.go.GetComponent<BlockCell>();
            if (bc != null) { bc.Weight = heldState.number; bc.IsOriginalTower = heldState.isOriginalTower; }

            _visualizer?.UpdateCellDataVisuals(heldState, heldView);
            grid.AddCell(target, heldState);
            cellViews[target] = heldView;
            _lastPlacedCells.Add(target);
        }

        Destroy(_held.Root);
        _held.Root = null;
        _held.RelPos.Clear();
        _held.Data.Clear();
        _held.SourceCells.Clear();
        _held.StartScore        = 0;
        _held.MatchesBonus      = false;
        _held.IsHolding         = false;
        _held.UsingKeyboardPlacement = false;
        _selection.ClearFocus();

        AudioManager.PlaySound(_AudioLibrarySounds.Drop);
        _physics?.UpdateTowerPhysicsState();
        _tower.UpdateExtractionTowerRowsFromCells();
        _tower.OnBlocksPlaced?.Invoke();
        _tower.FocusDefaultExtractionCell();
    }

    bool TryFindNearestDropBase(Vector2Int startBase, out Vector2Int dropBase)
    {
        startBase = _placement.ClampHeldBase(startBase);
        int minBaseY = _placement.PlacementFloorY - _held.LowestRelativeY();
        for (int y = startBase.y; y >= minBaseY; y--)
        {
            if (CanPlaceHeldBlocks(new Vector2Int(startBase.x, y), out _, out _))
            {
                dropBase = new Vector2Int(startBase.x, y);
                return true;
            }
        }
        dropBase = startBase;
        return false;
    }

    bool CanPlaceHeldBlocks(out List<Vector2Int> targets, out Vector3 snappedWorldPos)
    {
        _physics?.RefreshDetachedComponents();
        return CanPlaceHeldBlocks(_held.BaseCell, out targets, out snappedWorldPos);
    }

    bool CanPlaceHeldBlocks(Vector2Int baseCell, out List<Vector2Int> targets, out Vector3 snappedWorldPos)
    {
        _placement.Configure(_tower, _tower.TowerRoot,
            _sceneBuilder?.TowerStackDividerGO?.transform,
            _visualizer?.CreateBlockSprite());
        _placement.SyncPlacementZoneFromObject(updateVisuals: false);

        var detachedCells = _physics?.CollectStableDetachedCells() ?? new HashSet<Vector2Int>();
        var grid = _tower.Grid;
        targets = new List<Vector2Int>(_held.RelPos.Count);
        snappedWorldPos = _tower.TowerRoot.TransformPoint(
            new Vector3(baseCell.x + _held.Center.x, baseCell.y + _held.Center.y, 0f));

        foreach (var rel in _held.RelPos)
        {
            var t = new Vector2Int(baseCell.x + rel.x, baseCell.y + rel.y);
            if (t.x < _placement.placementMin.x || t.x > _placement.placementMax.x ||
                t.y < _placement.PlacementFloorY  || t.y > _placement.PlacementCeilingY) return false;
            if (_placement.IsPlacementExcluded(t)) return false;
            if (detachedCells.Contains(t)) return false;
            if (grid.TryGetCell(t, out var existing))
            {
                if (existing.kind == CellKind.Ice) return false;
                if (existing.number >= 6) return false;
            }
            targets.Add(t);
        }

        bool adjacent = false;
        foreach (var t in targets)
        {
            if (grid.IsMergeableCell(t)) { adjacent = true; break; }
            foreach (var n in Util.Neighbors(t))
                if (grid.IsMergeableCell(n) || detachedCells.Contains(n)) { adjacent = true; break; }
            if (adjacent) break;
        }
        if (!adjacent && (grid.Count > 0 || detachedCells.Count > 0)) return false;

        foreach (var t in targets)
        {
            if (grid.IsMergeableCell(t) ||
                t.y == _placement.PlacementFloorY ||
                grid.IsMergeableCell(new Vector2Int(t.x, t.y - 1)) ||
                detachedCells.Contains(new Vector2Int(t.x, t.y - 1)))
                return true;
        }
        return false;
    }

    void RememberLastPlacementCenter(List<Vector2Int> targets)
    {
        if (targets == null || targets.Count == 0) { _hasLastPlacementCenter = false; return; }
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var t in targets)
        {
            minX = Mathf.Min(minX, t.x); minY = Mathf.Min(minY, t.y);
            maxX = Mathf.Max(maxX, t.x); maxY = Mathf.Max(maxY, t.y);
        }
        _lastPlacementCenter    = new Vector2((minX + maxX + 1f) * 0.5f, (minY + maxY + 1f) * 0.5f);
        _hasLastPlacementCenter = true;
    }
}
