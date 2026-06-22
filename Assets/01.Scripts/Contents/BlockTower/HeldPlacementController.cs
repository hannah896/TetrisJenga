using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using JSAM;

public class HeldPlacementController : MonoBehaviour
{
    const float HeldOutlineThickness = 0.125f;

    BlockTower              _tower;
    HeldBlockController     _held;
    PlacementZoneController _placement;
    TowerPhysicsController  _physics;
    TowerCellVisualizer     _visualizer;
    TetrominoSelectionController _selection;
    TowerSceneBuilder       _sceneBuilder;
    BlockExtractionController _extraction;
    ScoreController         _score;
    BlockEffectController   _effects;

    readonly Color _validHoldColor   = new(0.55f, 0.85f, 0.6f,  0.5f);
    readonly Color _invalidHoldColor = new(1f,    0.25f, 0.25f, 0.6f);
    readonly Color _failFlashColor   = new(1f,    0.08f, 0.08f, 0.85f);

    readonly HashSet<Vector2Int> _lastPlacedCells = new();
    Vector2 _lastPlacementCenter;
    bool    _hasLastPlacementCenter;
    GameObject _heldOutlineRoot;
    string _heldOutlineSignature;

    [Header("Top Puyo Event")]
    [SerializeField, Min(1)] int topPuyoMinimumNumber = 2;
    [SerializeField, Min(2)] int topPuyoMatchCount = 5;
    [SerializeField, Min(0)] int topPuyoExtraScorePerBlock = 1;
    [SerializeField, Min(0f)] float topPuyoFlashDuration = 2f;
    [SerializeField, Min(0.05f)] float topPuyoFlashInterval = 0.15f;
    bool _isResolvingTopPuyo;

    public bool   HasLastPlacementCenter               => _hasLastPlacementCenter;
    public Vector2 LastPlacementCenter                 => _lastPlacementCenter;
    public Color ValidPreviewColor                     => _validHoldColor;
    public Color InvalidPreviewColor                   => _invalidHoldColor;
    public bool IsResolvingTopPuyo                     => _isResolvingTopPuyo;
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
        _extraction = GetComponent<BlockExtractionController>();
        _score = GetComponent<ScoreController>();
        _effects = GetComponent<BlockEffectController>();
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

        var heldTransform = _held.Root.transform;
        heldTransform.position = snappedWorldPos + _held.FailShakeOffset(isFailing);
        if (_tower.TowerRoot != null)
            heldTransform.rotation = _tower.TowerRoot.rotation;
        _held.SetPreviewColor(previewColor, _visualizer?.PreviewBlurAlpha ?? 0.10f);
        UpdateHeldOutlinePreview();
    }

    void UpdateHeldOutlinePreview()
    {
        if (_held.Root == null || _held.RelPos.Count == 0)
        {
            _heldOutlineRoot = null;
            _heldOutlineSignature = null;
            return;
        }

        string signature = string.Join(";", _held.RelPos);
        if (_heldOutlineRoot != null && _heldOutlineRoot.transform.parent == _held.Root.transform &&
            _heldOutlineSignature == signature)
            return;

        if (_heldOutlineRoot != null)
            Destroy(_heldOutlineRoot);

        _heldOutlineSignature = signature;
        _heldOutlineRoot = new GameObject("HeldOutlinePreview");
        _heldOutlineRoot.transform.SetParent(_held.Root.transform, false);
        _heldOutlineRoot.AddComponent<NoPostProcessingRenderer>();
        Util.SetNoPostLayer(_heldOutlineRoot);

        var occupied = new HashSet<Vector2Int>(_held.RelPos);
        foreach (var cell in _held.RelPos)
        {
            var localCenter = new Vector3(
                cell.x + 0.5f - _held.Center.x,
                cell.y + 0.5f - _held.Center.y,
                -0.01f);
            CreateHeldExposedEdges(cell, localCenter, occupied);
        }
    }

    void CreateHeldExposedEdges(Vector2Int cell, Vector3 localCenter, HashSet<Vector2Int> occupied)
    {
        float edgeOffset = 0.5f + HeldOutlineThickness * 0.5f;
        const float edgeLength = 1f;
        if (!occupied.Contains(cell + Vector2Int.left))
            CreateHeldOutlineEdge(cell, "Left", localCenter + Vector3.left * edgeOffset, HeldOutlineThickness, edgeLength);
        if (!occupied.Contains(cell + Vector2Int.right))
            CreateHeldOutlineEdge(cell, "Right", localCenter + Vector3.right * edgeOffset, HeldOutlineThickness, edgeLength);
        if (!occupied.Contains(cell + Vector2Int.down))
            CreateHeldOutlineEdge(cell, "Bottom", localCenter + Vector3.down * edgeOffset, edgeLength, HeldOutlineThickness);
        if (!occupied.Contains(cell + Vector2Int.up))
            CreateHeldOutlineEdge(cell, "Top", localCenter + Vector3.up * edgeOffset, edgeLength, HeldOutlineThickness);
    }

    void CreateHeldOutlineEdge(Vector2Int cell, string side, Vector3 localPosition, float width, float height)
    {
        var go = new GameObject($"HeldOutline_{cell.x}_{cell.y}_{side}");
        go.transform.SetParent(_heldOutlineRoot.transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = new Vector3(width, height, 1f);
        Util.SetNoPostLayer(go);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _visualizer?.CreateBoxSprite();
        sr.color = _visualizer?.SelectedOutlineColor ?? new Color(1f, 1f, 1f, 0.95f);
        sr.sortingOrder = 6;
    }

    public void UpdateHeldBaseFromMousePosition()
    {
        if (_tower.TowerRoot == null) return;
        var local = _tower.TowerRoot.InverseTransformPoint(Util.MouseWorldPos());
        _held.BaseCell = _placement.ClampHeldBase(new Vector2Int(
            Mathf.RoundToInt(local.x - _held.Center.x),
            Mathf.RoundToInt(local.y - _held.Center.y)));
    }

    public void TryPlaceHeldBlocks()
    {
        TryPlaceBlocks();
    }

    public void DropHeldToNearestSurfaceAndPlace()
    {
        if (!TryFindNearestDropBase(_held.BaseCell, out var dropBase))
        {
            _held.PlayPlacementFailFeedback();
            return;
        }
        var startBase = _held.BaseCell;
        _held.BaseCell = dropBase;
        _effects?.SpawnHardDropEffect(startBase, dropBase);
        TryPlaceBlocks();
    }

    public void BeginHeldGrowthAndAutoPlace()
    {
        if (_held.Root == null) return;
        StartCoroutine(GrowHeldAndAutoPlace(_held.Root));
    }

    IEnumerator GrowHeldAndAutoPlace(GameObject heldRoot)
    {
        float duration = _extraction != null ? _extraction.CurrentTitanEventDuration : 5f;
        float elapsed = 0f;
        heldRoot.transform.localScale = Vector3.one;
        var growingBlocks = new List<(Transform transform, Vector3 targetScale)>();
        foreach (Transform child in heldRoot.transform)
        {
            growingBlocks.Add((child, child.localScale));
            child.localScale = Vector3.zero;
        }

        while (heldRoot != null && _held.Root == heldRoot && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            foreach (var block in growingBlocks)
                if (block.transform != null)
                    block.transform.localScale = block.targetScale * t;
            yield return null;
        }

        if (heldRoot == null || _held.Root != heldRoot)
        {
            yield break;
        }

        foreach (var block in growingBlocks)
            if (block.transform != null)
                block.transform.localScale = block.targetScale;
        ResolveTitanPlacementTimeout();
    }

    void ResolveTitanPlacementTimeout()
    {
        if (CanPlaceHeldBlocks(_held.BaseCell, out _, out _))
        {
            TryPlaceBlocks();
            return;
        }

        if (TryFindNearestValidPlacementBase(_held.BaseCell, out var nearestBase))
        {
            _held.BaseCell = nearestBase;
            TryPlaceBlocks();
            return;
        }

        _held.PlayPlacementFailFeedback();
    }

    bool TryFindNearestValidPlacementBase(Vector2Int origin, out Vector2Int bestBase)
    {
        bestBase = origin;
        bool found = false;
        int bestDistance = int.MaxValue;

        int minRelX = int.MaxValue, minRelY = int.MaxValue;
        int maxRelX = int.MinValue, maxRelY = int.MinValue;
        foreach (var rel in _held.RelPos)
        {
            minRelX = Mathf.Min(minRelX, rel.x);
            minRelY = Mathf.Min(minRelY, rel.y);
            maxRelX = Mathf.Max(maxRelX, rel.x);
            maxRelY = Mathf.Max(maxRelY, rel.y);
        }

        int minX = _placement.placementMin.x - minRelX;
        int maxX = _placement.placementMax.x - maxRelX;
        int minY = _placement.PlacementFloorY - minRelY;
        int maxY = _placement.PlacementCeilingY - maxRelY;
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            var candidate = new Vector2Int(x, y);
            if (!CanPlaceHeldBlocks(candidate, out _, out _)) continue;
            int distance = Mathf.Abs(candidate.x - origin.x) + Mathf.Abs(candidate.y - origin.y);
            if (found && distance >= bestDistance) continue;
            found = true;
            bestDistance = distance;
            bestBase = candidate;
        }

        return found;
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
            heldView.go.transform.localRotation = Quaternion.identity;
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
        _heldOutlineRoot = null;
        _heldOutlineSignature = null;
        _held.RelPos.Clear();
        _held.Data.Clear();
        _held.SourceCells.Clear();
        _held.StartScore        = 0;
        _held.MatchesBonus      = false;
        _held.IsHolding         = false;
        _held.UsingKeyboardPlacement = false;
        _selection.ClearFocus();

        _effects?.SpawnExtractionEffects(_lastPlacedCells);
        AudioPlayback.PlaySound(_AudioLibrarySounds.Drop);
        var puyoMatches = FindTopPuyoMatches();
        if (puyoMatches.Count > 0)
        {
            StartCoroutine(FlashAndResolveTopPuyo(puyoMatches));
            return;
        }

        FinishPlacedTurn();
    }

    void FinishPlacedTurn()
    {
        _physics?.CheckForDetachment();
        _physics?.UpdateTowerPhysicsState();
        _tower.UpdateExtractionTowerRowsFromCells();
        _extraction?.CompleteTitanTurn();
        _tower.OnBlocksPlaced?.Invoke();
        if (!_selection.RestorePresetSelectionAfterPlacement(_extraction))
            _tower.FocusDefaultExtractionCell();
    }

    IEnumerator FlashAndResolveTopPuyo(List<List<Vector2Int>> matches)
    {
        _isResolvingTopPuyo = true;
        var originalColors = new Dictionary<SpriteRenderer, Color>();
        foreach (var group in matches)
        foreach (var cell in group)
        {
            if (!_tower.CellViews.TryGetValue(cell, out var view) || view.sr == null) continue;
            originalColors[view.sr] = view.sr.color;
        }

        float elapsed = 0f;
        while (elapsed < topPuyoFlashDuration)
        {
            elapsed += Time.deltaTime;
            bool showWhite = Mathf.FloorToInt(elapsed / Mathf.Max(0.05f, topPuyoFlashInterval)) % 2 == 0;
            foreach (var pair in originalColors)
            {
                if (pair.Key == null) continue;
                pair.Key.color = showWhite
                    ? new Color(1f, 1f, 1f, pair.Value.a)
                    : pair.Value;
            }
            yield return null;
        }

        foreach (var pair in originalColors)
            if (pair.Key != null)
                pair.Key.color = pair.Value;

        ApplyTopPuyoMatches(matches);
        _isResolvingTopPuyo = false;
        FinishPlacedTurn();
    }

    void ApplyTopPuyoMatches(List<List<Vector2Int>> matches)
    {

        var grid = _tower.Grid;
        var views = _tower.CellViews;
        foreach (var group in matches)
        {
            if (!grid.TryGetCell(group[0], out var matchedState)) continue;

            var scoreCenter = Vector3.zero;
            int scorePositions = 0;
            foreach (var cell in group)
            {
                if (!views.TryGetValue(cell, out var view) || view.go == null) continue;
                scoreCenter += view.go.transform.position;
                scorePositions++;
            }

            foreach (var cell in group)
            {
                if (views.TryGetValue(cell, out var view) && view.go != null)
                    Destroy(view.go);
                views.Remove(cell);
                grid.RemoveCell(cell);
                _lastPlacedCells.Remove(cell);
            }

            int bonus = matchedState.number + (group.Count - topPuyoMatchCount) * topPuyoExtraScorePerBlock;
            var worldPosition = scorePositions > 0
                ? scoreCenter / scorePositions
                : _tower.transform.position;
            _score?.AddScore(Mathf.Max(0, bonus), worldPosition);
        }

        CollapseTopBlocksDownward();
    }

    List<List<Vector2Int>> FindTopPuyoMatches()
    {
        var matches = new List<List<Vector2Int>>();
        var visited = new HashSet<Vector2Int>();
        var grid = _tower.Grid;

        foreach (var pair in grid.AllCells)
        {
            var start = pair.Key;
            var startState = pair.Value;
            if (visited.Contains(start) || !IsTopPuyoCell(startState)) continue;

            var group = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                group.Add(cell);
                foreach (var neighbor in Util.Neighbors(cell))
                {
                    if (visited.Contains(neighbor) || !grid.TryGetCell(neighbor, out var neighborState)) continue;
                    if (!IsTopPuyoCell(neighborState) || neighborState.number != startState.number) continue;
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            if (group.Count >= topPuyoMatchCount)
                matches.Add(group);
        }

        return matches;
    }

    bool IsTopPuyoCell(CellState state) =>
        state != null &&
        state.number >= topPuyoMinimumNumber &&
        !state.isOriginalTower &&
        state.kind == CellKind.Normal;

    void CollapseTopBlocksDownward()
    {
        _placement.Configure(_tower, _tower.TowerRoot,
            _sceneBuilder?.TowerStackDividerGO?.transform,
            _visualizer?.CreateBlockSprite());
        _placement.SyncPlacementZoneFromObject(updateVisuals: false);
        int floorY = _placement.PlacementFloorY;

        var grid = _tower.Grid;
        var views = _tower.CellViews;
        var snapshot = new List<(Vector2Int cell, CellState state)>();
        foreach (var pair in grid.AllCells)
            snapshot.Add((pair.Key, pair.Value));

        var columns = new HashSet<int>();
        foreach (var entry in snapshot)
            if (!entry.state.isOriginalTower)
                columns.Add(entry.cell.x);

        foreach (int column in columns)
        {
            var movable = new List<(Vector2Int cell, CellState state)>();
            var occupied = new HashSet<Vector2Int>();
            foreach (var entry in snapshot)
            {
                if (entry.cell.x != column) continue;
                if (entry.state.isOriginalTower) occupied.Add(entry.cell);
                else movable.Add(entry);
            }
            movable.Sort((a, b) => a.cell.y.CompareTo(b.cell.y));

            foreach (var entry in movable)
            {
                int targetY = entry.cell.y;
                while (targetY > floorY && !occupied.Contains(new Vector2Int(column, targetY - 1)))
                    targetY--;

                var target = new Vector2Int(column, targetY);
                occupied.Add(target);
                if (target == entry.cell) continue;

                grid.RemoveCell(entry.cell);
                grid.AddCell(target, entry.state);
                if (views.TryGetValue(entry.cell, out var view))
                {
                    views.Remove(entry.cell);
                    views[target] = view;
                    if (view.go != null)
                    {
                        view.go.transform.localPosition = new Vector3(target.x + 0.5f, target.y + 0.5f, 0f);
                        view.go.name = $"Cell_{target.x}_{target.y}";
                    }
                }

                if (_lastPlacedCells.Remove(entry.cell))
                    _lastPlacedCells.Add(target);
            }
        }
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
