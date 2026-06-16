using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BombIceEffectController : MonoBehaviour
{
    [SerializeField] BlockTower _tower;

    [Header("Bomb")]
    [SerializeField] Sprite _bombObscureSprite;
    [SerializeField] Color  _bombObscureColor = new(0.45f, 0.45f, 0.45f, 0.92f);

    readonly HashSet<Vector2Int>               _bombConcealedCells = new();
    readonly Dictionary<Vector2Int, GameObject>       _bombObscureBlocks  = new();
    readonly Dictionary<Vector2Int, BombObscureKind>  _bombObscureKinds   = new();

    TowerGridModel                    Grid         => _tower.Grid;
    Dictionary<Vector2Int, CellView>  CellViews    => _tower.CellViews;
    Dictionary<Vector2Int, CellView>  IceCellViews => _tower.IceCellViews;
    Transform                         TowerRoot    => _tower.TowerRoot;
    BlockNumberSpriteSetAsset         SpriteSetAsset => _tower.NumberSpriteSetAsset;

    public Color BombObscureColor => _bombObscureColor;

    private void OnValidate()
    {
        _tower = GetComponent<BlockTower>();
    }

    void Awake()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
    }

    // ── 상태 초기화 ──────────────────────────────────────────────────────

    public void ClearBombState()
    {
        _bombConcealedCells.Clear();
        _bombObscureBlocks.Clear();
        _bombObscureKinds.Clear();
    }

    public bool IsConcealedByBomb(Vector2Int cell) => _bombConcealedCells.Contains(cell);

    // ── Bomb ──────────────────────────────────────────────────────────────

    public void TriggerBombAt(Vector2Int center)
    {
        foreach (var offset in BombBoxOffsets())
        {
            var cell = center + offset;
            _bombConcealedCells.Add(cell);
            _bombObscureKinds[cell] = GetBombObscureKind(offset);
            EnsureBombObscureBlock(cell);
            if (Grid.TryGetCell(cell, out var state))
            {
                state.concealedByBomb = true;
                if (CellViews.TryGetValue(cell, out var view))
                    _tower.UpdateCellDataVisuals(state, view);
                _tower.ApplyCellVisual(cell);
            }
        }
    }

    static IEnumerable<Vector2Int> BombBoxOffsets()
    {
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
                yield return new Vector2Int(x, y);
    }

    void EnsureBombObscureBlock(Vector2Int cell)
    {
        if (_bombObscureBlocks.ContainsKey(cell)) return;

        var go = new GameObject($"BombObscure_{cell.x}_{cell.y}");
        _tower.TrackGeneratedObject(go);
        go.transform.SetParent(_tower.transform, worldPositionStays: true);
        go.transform.position = _tower.LocalToWorld(new Vector3(cell.x + 0.5f, cell.y + 0.5f, -0.08f));
        go.transform.localScale = Vector3.one;

        var sr = go.AddComponent<SpriteRenderer>();
        var sprite = GetBombObscureSpriteFor(cell);
        sr.sprite = sprite;
        sr.color = HasBombObscureSprite(cell) ? Color.white : _bombObscureColor;
        _tower.FitRendererObjectToCell(sr);
        sr.sortingOrder = 30;
        _bombObscureBlocks[cell] = go;
    }

    public bool HasBombObscureSprite(Vector2Int cell)
    {
        _tower.EnsureNumberSpriteSetReady();
        return SpriteSetAsset != null &&
               _bombObscureKinds.TryGetValue(cell, out var kind) &&
               SpriteSetAsset.GetBombObscureSprite(kind) != null;
    }

    public Sprite GetBombObscureSpriteFor(Vector2Int cell)
    {
        _tower.EnsureNumberSpriteSetReady();
        if (SpriteSetAsset != null && _bombObscureKinds.TryGetValue(cell, out var kind))
        {
            var sprite = SpriteSetAsset.GetBombObscureSprite(kind);
            if (sprite != null) return sprite;
        }
        return GetFallbackObscureSprite();
    }

    public Sprite GetFallbackObscureSprite() =>
        _bombObscureSprite != null ? _bombObscureSprite : _tower.CreateBlockSprite();

    BombObscureKind GetBombObscureKind(Vector2Int offset)
    {
        if (offset == Vector2Int.zero) return BombObscureKind.Center;
        return offset.x == 0 || offset.y == 0 ? BombObscureKind.Edge : BombObscureKind.Corner;
    }

    // ── Ice ──────────────────────────────────────────────────────────────

    public bool ApplyIceColumnDamageToDetached(DetachedComponent detached)
    {
        if (detached == null || detached.root == null || detached.iceDamageApplied)
            return false;

        var children = new List<Transform>();
        foreach (Transform child in detached.root.transform)
            children.Add(child);

        bool changed = false;
        var damagedIce = new HashSet<Vector2Int>();
        foreach (var child in children)
        {
            if (child == null) continue;
            if (!_tower.TryWorldToGridCell(child.position, out var cell)) continue;
            if (!Grid.TryFindIceBelowInColumn(cell, out var iceCell)) continue;
            if (!damagedIce.Add(iceCell)) continue;
            if (!IceCellViews.TryGetValue(iceCell, out var iceView)) continue;
            if (ApplyIceDamageInternal(iceCell, iceView.go,
                    iceView.go != null ? iceView.go.GetComponent<BlockCell>() : null))
                changed = true;
        }

        if (!changed) return false;
        detached.iceDamageApplied = true;
        return false;
    }

    public void ApplyIceColumnLandingDamage(Vector2Int landedCell, CellState landedState)
    {
        if (landedState == null || landedState.kind == CellKind.Ice) return;

        if (Grid.TryFindIceBelowInColumn(landedCell, out var iceCell) &&
            IceCellViews.TryGetValue(iceCell, out var iceView))
            ApplyIceDamageInternal(iceCell, iceView.go,
                iceView.go != null ? iceView.go.GetComponent<BlockCell>() : null);
    }

    public bool ApplyIceContactDamage(BlockCell blockCell)
    {
        if (blockCell == null || blockCell.Kind == CellKind.Ice) return false;

        Vector2Int? runtimeCell = null;
        foreach (var pair in CellViews)
        {
            if (pair.Value.go == null || pair.Value.go != blockCell.gameObject) continue;
            runtimeCell = pair.Key;
            break;
        }

        if (runtimeCell.HasValue && Grid.TryGetCell(runtimeCell.Value, out var runtimeState))
        {
            runtimeState.number--;
            if (runtimeState.number <= 0)
            {
                Grid.RemoveCell(runtimeCell.Value);
                CellViews.Remove(runtimeCell.Value);
                Destroy(blockCell.gameObject);
            }
            else
            {
                if (CellViews.TryGetValue(runtimeCell.Value, out var view))
                    _tower.UpdateCellDataVisuals(runtimeState, view);
                _tower.ApplyCellVisual(runtimeCell.Value);
            }

            return true;
        }

        int number = Mathf.Max(1, Mathf.RoundToInt(blockCell.Weight)) - 1;
        if (number <= 0)
        {
            Destroy(blockCell.gameObject);
            return true;
        }

        UpdateLooseBlockNumberVisual(blockCell, number);
        return true;
    }

    public bool ApplyIceColumnContactDamage(Vector3 iceWorldPosition)
    {
        if (TowerRoot == null) return false;

        var local = TowerRoot.InverseTransformPoint(iceWorldPosition);
        int iceX = Mathf.RoundToInt(local.x - 0.5f);
        int iceY = Mathf.RoundToInt(local.y - 0.5f);

        Vector2Int? targetCell = null;
        int bestY = int.MaxValue;
        foreach (var pair in Grid.AllCells)
        {
            if (pair.Key.x != iceX || pair.Key.y <= iceY) continue;
            if (pair.Key.y < bestY)
            {
                bestY = pair.Key.y;
                targetCell = pair.Key;
            }
        }

        if (!targetCell.HasValue || !Grid.TryGetCell(targetCell.Value, out var state))
            return false;

        state.number--;
        if (state.number <= 0)
        {
            Grid.RemoveCell(targetCell.Value);
            if (CellViews.TryGetValue(targetCell.Value, out var view))
            {
                CellViews.Remove(targetCell.Value);
                if (view.go != null) Destroy(view.go);
            }
        }
        else
        {
            if (CellViews.TryGetValue(targetCell.Value, out var view))
                _tower.UpdateCellDataVisuals(state, view);
            _tower.ApplyCellVisual(targetCell.Value);
        }

        return true;
    }

    public bool ApplyIceDamage(BlockCell iceBlockCell, Vector3 iceWorldPosition)
    {
        if (iceBlockCell == null || iceBlockCell.Kind != CellKind.Ice)
            return ApplyIceDamageAtWorldPosition(iceWorldPosition);

        Vector2Int? iceCell = null;
        foreach (var pair in IceCellViews)
        {
            if (pair.Value.go == iceBlockCell.gameObject)
            {
                iceCell = pair.Key;
                break;
            }
        }

        return ApplyIceDamageInternal(iceCell, iceBlockCell.gameObject, iceBlockCell);
    }

    bool ApplyIceDamageAtWorldPosition(Vector3 iceWorldPosition)
    {
        if (TowerRoot == null) return false;

        var local = TowerRoot.InverseTransformPoint(iceWorldPosition);
        var cell = new Vector2Int(
            Mathf.RoundToInt(local.x - 0.5f),
            Mathf.RoundToInt(local.y - 0.5f));

        if (IceCellViews.TryGetValue(cell, out var view))
            return ApplyIceDamageInternal(cell, view.go,
                view.go != null ? view.go.GetComponent<BlockCell>() : null);

        return false;
    }

    bool ApplyIceDamageInternal(Vector2Int? iceCell, GameObject iceObject, BlockCell iceBlockCell)
    {
        if (iceObject == null || iceBlockCell == null) return false;

        int number = Mathf.Max(1, Mathf.RoundToInt(iceBlockCell.Weight)) - 1;
        if (number <= 0)
        {
            if (iceCell.HasValue)
            {
                Grid.RemoveIceCell(iceCell.Value);
                IceCellViews.Remove(iceCell.Value);
            }

            Destroy(iceObject);
            return true;
        }

        iceBlockCell.Weight = number;
        var label = iceObject.GetComponentInChildren<TextMeshPro>();
        if (label != null)
        {
            label.text = number.ToString();
            label.fontSize = 6f;
            label.gameObject.SetActive(true);
        }

        if (iceCell.HasValue && Grid.TryGetIceCell(iceCell.Value, out var state))
        {
            state.number = number;
            if (IceCellViews.TryGetValue(iceCell.Value, out var view))
                _tower.UpdateCellDataVisuals(state, view);
        }

        return true;
    }

    void UpdateLooseBlockNumberVisual(BlockCell blockCell, int number)
    {
        if (blockCell == null) return;

        blockCell.Weight = number;

        var label = blockCell.GetComponentInChildren<TextMeshPro>();
        if (label != null)
        {
            label.text = number.ToString();
            label.fontSize = 6f;
            label.gameObject.SetActive(true);
        }

        var sr = blockCell.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        _tower.EnsureNumberSpriteSetReady();
        var numberSprite = _tower.GetNumberSprite(number);
        var numberRenderer = _tower.EnsureStandaloneNumberSpriteRenderer(blockCell.transform);

        if (numberSprite != null)
        {
            sr.color = Color.clear;
            numberRenderer.sprite = numberSprite;
            numberRenderer.enabled = true;
            numberRenderer.color = Color.white;
            numberRenderer.sortingOrder = sr.sortingOrder + 1;
            _tower.FitNumberSpriteToCell(numberRenderer);
        }
        else
        {
            if (numberRenderer != null)
            {
                numberRenderer.enabled = false;
                numberRenderer.sprite = null;
            }

            sr.color = blockCell.IsOriginalTower
                ? Util.NumberColor(number)
                : Util.PlacedNumberColor(number);
        }
    }
}
