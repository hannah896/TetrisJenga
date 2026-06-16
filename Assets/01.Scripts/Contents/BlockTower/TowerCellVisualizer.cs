using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TowerCellVisualizer : MonoBehaviour
{
    [SerializeField] BlockTower _tower;

    [SerializeField] Color placedBlockColor = new(0.55f, 0.58f, 0.60f, 1f);
    [SerializeField] BlockNumberSpriteSetAsset numberSpriteSetAsset;
    [SerializeField, HideInInspector] BlockNumberSpriteSet numberSpriteSet;

    [Header("Focus Feedback")]
    [SerializeField] Color focusedOutlineColor  = new(1f, 0.95f, 0.05f, 1f);
    [SerializeField] Color selectedOutlineColor = new(1f, 1f,    1f,    0.95f);
    [SerializeField, Range(1f, 1.4f)] float focusedOutlineScale  = 1.18f;
    [SerializeField, Range(1f, 1.3f)] float selectedOutlineScale = 1.10f;

    [Header("Placement Preview")]
    [SerializeField] bool  previewBlurEnabled = true;
    [SerializeField, Range(0f, 0.5f)] float previewBlurRadius = 0.12f;
    [SerializeField, Range(0f, 1f)]   float previewBlurAlpha  = 0.10f;
    [SerializeField, Range(1, 8)]     int   previewBlurCopies  = 8;

    Sprite _blockSprite;
    Sprite _outlineSprite;
    readonly Color _focusedCellColor = new(0f, 1f, 1f, 1f);

    TowerGridModel                   Grid         => _tower.Grid;
    Dictionary<Vector2Int, CellView> CellViews    => _tower.CellViews;
    Dictionary<Vector2Int, CellView> IceCellViews => _tower.IceCellViews;
    BombIceEffectController          BombIce      => _tower.BombIceController;

    public Color PlacedBlockColor     => placedBlockColor;
    public Color SelectedOutlineColor => selectedOutlineColor;
    public float SelectedOutlineScale => selectedOutlineScale;
    public float PreviewBlurAlpha     => previewBlurAlpha;
    public BlockNumberSpriteSetAsset NumberSpriteSetAsset => numberSpriteSetAsset;

    private void OnValidate()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
    }

    void Awake()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
    }

    // ── 셀 비주얼 ─────────────────────────────────────────────────────────

    public void ApplyCellVisual(Vector2Int cell)
    {
        if (!Grid.TryGetCell(cell, out var state)) return;
        if (!CellViews.TryGetValue(cell, out var view)) return;

        bool isSelected           = _tower.IsSelectedCell(cell);
        bool isFocused            = !_tower.IsPresetSelectionActive && _tower.HasFocusedCell && _tower.FocusedCell == cell;
        bool showSelectedOutline  = isSelected && !_tower.IsPresetSelectionActive;

        if (state.concealedByBomb)
        {
            if (view.sr != null)
            {
                bool hasCustom = BombIce != null && BombIce.HasBombObscureSprite(cell);
                var sprite = BombIce?.GetBombObscureSpriteFor(cell);
                if (hasCustom && sprite != null)
                {
                    ApplyFittedOverlaySprite(view, sprite, view.sr.sortingOrder + 1);
                    view.sr.color = Color.clear;
                }
                else
                {
                    DisableNumberSpriteRenderer(view);
                    view.sr.sprite = sprite ?? CreateBlockSprite();
                    view.sr.color  = BombIce?.BombObscureColor ?? new Color(0.45f, 0.45f, 0.45f, 0.92f);
                }
            }

            ApplyCellOutline(view, false, false);
            return;
        }

        var color = state.isOriginalTower ? Util.NumberColor(state.number) : Util.PlacedNumberColor(state.number);
        if (state.kind == CellKind.Bomb)
            color = Color.black;
        else if (state.kind == CellKind.Ice)
            color = Util.IceBlockColor();
        if (isSelected) color = Color.Lerp(color, Color.white, 0.45f);
        if (isFocused)  color = Color.Lerp(color, _focusedCellColor, 0.8f);

        bool hasNumberSprite = view.numberSpriteRenderer != null &&
                               view.numberSpriteRenderer.enabled &&
                               view.numberSpriteRenderer.sprite != null;
        if (hasNumberSprite)
        {
            view.sr.color = Color.clear;
            view.numberSpriteRenderer.color = Color.white;
        }
        else if (TryGetSpecialBlockSprite(state.kind, out var specialSprite))
        {
            ApplyFittedOverlaySprite(view, specialSprite, view.sr.sortingOrder + 1);
            view.sr.color = Color.Lerp(Color.clear, color, isSelected || isFocused ? 0.35f : 0f);
        }
        else
        {
            if (view.numberSpriteRenderer != null)
            {
                view.numberSpriteRenderer.enabled = false;
                view.numberSpriteRenderer.sprite  = null;
            }

            view.sr.color = color;
        }

        ApplyCellOutline(view, isFocused, showSelectedOutline);
    }

    void ApplyCellOutline(CellView view, bool isFocused, bool isSelected)
    {
        if (view.outline == null) return;
        view.outline.enabled = isFocused || isSelected;
        view.outline.color   = isFocused ? focusedOutlineColor : selectedOutlineColor;
        view.outline.transform.localScale = Vector3.one * (isFocused ? focusedOutlineScale : selectedOutlineScale);
        view.outline.sortingOrder = isFocused ? 4 : 3;
    }

    public void UpdateCellDataVisuals(CellState state, CellView view)
    {
        EnsureNumberSpriteSet();

        if (view.label == null && view.go != null)
            view.label = SpawnLabel(state.number, view.go.transform);

        var blockCell = view.go.GetComponent<BlockCell>();
        if (blockCell != null)
        {
            blockCell.Weight          = state.number;
            blockCell.IsOriginalTower = state.isOriginalTower;
            blockCell.Kind            = state.kind;
        }

        if (view.label != null)
        {
            view.label.text = state.kind == CellKind.Bomb ? "X" : state.number.ToString();
            view.label.fontSize = 6f;
            view.label.gameObject.SetActive(!state.concealedByBomb);
        }

        if (view.sr == null) return;

        var numberSprite = GetNumberSprite(state.number);
        if (state.concealedByBomb)
        {
            DisableNumberSpriteRenderer(view);
            var cell          = FindCellForView(view);
            bool hasCustom    = cell.HasValue && (BombIce?.HasBombObscureSprite(cell.Value) ?? false);
            var sprite        = cell.HasValue ? BombIce?.GetBombObscureSpriteFor(cell.Value) : BombIce?.GetFallbackObscureSprite();
            if (hasCustom && sprite != null)
            {
                ApplyFittedOverlaySprite(view, sprite, view.sr.sortingOrder + 1);
                view.sr.color = Color.clear;
            }
            else
            {
                DisableNumberSpriteRenderer(view);
                view.sr.sprite = sprite ?? CreateBlockSprite();
                view.sr.color  = BombIce?.BombObscureColor ?? new Color(0.45f, 0.45f, 0.45f, 0.92f);
            }

            view.sr.drawMode = SpriteDrawMode.Simple;
        }
        else if (state.kind == CellKind.Bomb)
        {
            var sprite = GetBombSprite();
            if (sprite != null)
            {
                ApplyFittedOverlaySprite(view, sprite, view.sr.sortingOrder + 1);
                view.sr.color = Color.clear;
            }
            else
            {
                DisableNumberSpriteRenderer(view);
                view.sr.sprite = CreateBlockSprite();
                view.sr.color  = Color.black;
            }

            view.sr.drawMode = SpriteDrawMode.Simple;
        }
        else if (state.kind == CellKind.Ice)
        {
            var sprite = GetIceSprite();
            if (sprite != null)
            {
                ApplyFittedOverlaySprite(view, sprite, view.sr.sortingOrder + 1);
                view.sr.color = Color.clear;
            }
            else
            {
                DisableNumberSpriteRenderer(view);
                view.sr.sprite = CreateBlockSprite();
                view.sr.color  = Util.IceBlockColor();
            }

            view.sr.drawMode = SpriteDrawMode.Simple;
        }
        else if (state.isOriginalTower && numberSprite != null)
        {
            view.sr.sprite   = CreateBlockSprite();
            view.sr.color    = Color.clear;
            view.sr.drawMode = SpriteDrawMode.Simple;
            var numberRenderer = EnsureNumberSpriteRenderer(view);
            numberRenderer.sprite       = numberSprite;
            numberRenderer.enabled      = true;
            numberRenderer.color        = Color.white;
            numberRenderer.sortingOrder = view.sr.sortingOrder + 1;
            FitNumberSpriteToCell(numberRenderer);
        }
        else
        {
            if (view.numberSpriteRenderer != null)
            {
                view.numberSpriteRenderer.enabled = false;
                view.numberSpriteRenderer.sprite  = null;
            }

            view.sr.sprite   = CreateBlockSprite();
            view.sr.color    = state.isOriginalTower
                ? Util.NumberColor(state.number)
                : Util.PlacedNumberColor(state.number);
            view.sr.drawMode = SpriteDrawMode.Simple;
        }
    }

    // ── 스프라이트 / 렌더러 헬퍼 ─────────────────────────────────────────

    SpriteRenderer EnsureNumberSpriteRenderer(CellView view)
    {
        view.numberSpriteRenderer = EnsureStandaloneNumberSpriteRenderer(view.go.transform);
        return view.numberSpriteRenderer;
    }

    void ApplyFittedOverlaySprite(CellView view, Sprite sprite, int sortingOrder)
    {
        if (view == null || view.sr == null || sprite == null) return;

        view.sr.sprite   = CreateBlockSprite();
        view.sr.drawMode = SpriteDrawMode.Simple;
        var overlay = EnsureNumberSpriteRenderer(view);
        overlay.sprite       = sprite;
        overlay.enabled      = true;
        overlay.color        = Color.white;
        overlay.sortingOrder = sortingOrder;
        FitNumberSpriteToCell(overlay);
    }

    void DisableNumberSpriteRenderer(CellView view)
    {
        if (view?.numberSpriteRenderer == null) return;
        view.numberSpriteRenderer.enabled = false;
        view.numberSpriteRenderer.sprite  = null;
    }

    public SpriteRenderer EnsureStandaloneNumberSpriteRenderer(Transform parent)
    {
        var existing = parent.Find("NumberSpriteImage");
        if (existing != null && existing.TryGetComponent<SpriteRenderer>(out var existingRenderer))
            return existingRenderer;

        var old = parent.Find("NumberSprite");
        if (old != null && old.TryGetComponent<SpriteRenderer>(out var oldRenderer))
        {
            oldRenderer.enabled = false;
            oldRenderer.sprite  = null;
        }

        var go = new GameObject("NumberSpriteImage");
        _tower.TrackGeneratedObject(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        return go.AddComponent<SpriteRenderer>();
    }

    public void FitNumberSpriteToCell(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return;

        renderer.drawMode = SpriteDrawMode.Simple;
        var size   = renderer.sprite.bounds.size;
        float scaleX = size.x > 0.0001f ? 1f / size.x : 1f;
        float scaleY = size.y > 0.0001f ? 1f / size.y : 1f;
        renderer.transform.localScale    = new Vector3(scaleX, scaleY, 1f);
        renderer.transform.localPosition = Vector3.zero;
    }

    public void FitRendererObjectToCell(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return;

        renderer.drawMode = SpriteDrawMode.Simple;
        var size   = renderer.sprite.bounds.size;
        float scaleX = size.x > 0.0001f ? 1f / size.x : 1f;
        float scaleY = size.y > 0.0001f ? 1f / size.y : 1f;
        renderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    void EnsureNumberSpriteSet()
    {
#if UNITY_EDITOR
        if (numberSpriteSetAsset == null)
            numberSpriteSetAsset = FindDefaultNumberSpriteSetAsset();
#endif
        if (numberSpriteSet == null)
            numberSpriteSet = GetComponent<BlockNumberSpriteSet>();

        SyncBlockWeightGuideImages();
    }

    public void EnsureNumberSpriteSetReady() => EnsureNumberSpriteSet();

    public Sprite GetNumberSprite(int number)
    {
        EnsureNumberSpriteSet();
        return GetNumberSpriteRaw(number);
    }

    Sprite GetNumberSpriteRaw(int number)
    {
        if (numberSpriteSetAsset != null)
        {
            var sprite = numberSpriteSetAsset.GetSprite(number);
            if (sprite != null) return sprite;
        }

        return numberSpriteSet != null ? numberSpriteSet.GetSprite(number) : null;
    }

    Sprite GetBombSprite()
    {
        EnsureNumberSpriteSet();
        return numberSpriteSetAsset != null ? numberSpriteSetAsset.BombSprite : null;
    }

    public Sprite GetIceSprite()
    {
        EnsureNumberSpriteSet();
        return numberSpriteSetAsset != null ? numberSpriteSetAsset.IceSprite : null;
    }

    bool TryGetSpecialBlockSprite(CellKind kind, out Sprite sprite)
    {
        sprite = kind switch
        {
            CellKind.Bomb => GetBombSprite(),
            CellKind.Ice  => GetIceSprite(),
            _             => null
        };

        return sprite != null;
    }

    Vector2Int? FindCellForView(CellView view)
    {
        if (view == null) return null;

        foreach (var pair in CellViews)
            if (ReferenceEquals(pair.Value, view)) return pair.Key;

        foreach (var pair in IceCellViews)
            if (ReferenceEquals(pair.Value, view)) return pair.Key;

        return null;
    }

    void SyncBlockWeightGuideImages() { }

#if UNITY_EDITOR
    BlockNumberSpriteSetAsset FindDefaultNumberSpriteSetAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:BlockNumberSpriteSetAsset");
        foreach (var guid in guids)
        {
            var path  = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<BlockNumberSpriteSetAsset>(path);
            if (asset != null) return asset;
        }

        return null;
    }
#endif

    // ── 스프라이트 생성 ───────────────────────────────────────────────────

    public Sprite CreateBlockSprite()
    {
        if (_blockSprite != null) return _blockSprite;

#if UNITY_EDITOR
        _blockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Contents/box.png");
        if (_blockSprite != null) return _blockSprite;
#endif

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.hideFlags   = HideFlags.DontSave;
        tex.filterMode  = FilterMode.Point;

        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % size, y = i / size;
            bool border = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
            pixels[i] = border ? new Color(0f, 0f, 0f, 0.3f) : Color.white;
        }

        tex.SetPixels(pixels);
        tex.Apply();

        _blockSprite           = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _blockSprite.hideFlags = HideFlags.DontSave;
        return _blockSprite;
    }

    public Sprite CreateOutlineSprite()
    {
        if (_outlineSprite != null) return _outlineSprite;

        const int size      = 32;
        const int thickness = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.hideFlags  = HideFlags.DontSave;
        tex.filterMode = FilterMode.Point;

        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % size, y = i / size;
            bool border = x < thickness || y < thickness || x >= size - thickness || y >= size - thickness;
            pixels[i] = border ? Color.white : Color.clear;
        }

        tex.SetPixels(pixels);
        tex.Apply();

        _outlineSprite           = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _outlineSprite.hideFlags = HideFlags.DontSave;
        return _outlineSprite;
    }

    // ── 셀 자식 오브젝트 스폰 ────────────────────────────────────────────

    public SpriteRenderer SpawnCellOutline(Transform parent)
    {
        var go = new GameObject("FocusOutline");
        _tower.TrackGeneratedObject(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        go.transform.localScale    = Vector3.one * selectedOutlineScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = CreateOutlineSprite();
        sr.color        = selectedOutlineColor;
        sr.sortingOrder = 3;
        sr.enabled      = false;
        return sr;
    }

    public TextMeshPro SpawnLabel(int number, Transform parent)
    {
        var go = new GameObject("Label");
        _tower.TrackGeneratedObject(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var tmp  = go.AddComponent<TextMeshPro>();
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.one;

        tmp.text             = number.ToString();
        tmp.fontSize         = 6f;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = Color.white;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Overflow;

        var r = go.GetComponent<Renderer>();
        if (r != null) r.sortingOrder = 2;

        return tmp;
    }

    // ── 프리뷰 블러 ──────────────────────────────────────────────────────

    public List<SpriteRenderer> CreatePreviewBlur(Transform parent)
    {
        var renderers = new List<SpriteRenderer>();
        if (!previewBlurEnabled) return renderers;

        int copies = Mathf.Max(1, previewBlurCopies);
        for (int i = 0; i < copies; i++)
        {
            float angle = Mathf.PI * 2f * i / copies;
            var go = new GameObject($"PreviewBlur_{i}");
            _tower.TrackGeneratedObject(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * previewBlurRadius,
                Mathf.Sin(angle) * previewBlurRadius, 0f);
            go.transform.localScale = Vector3.one * (1f + previewBlurRadius);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = CreateBlockSprite();
            sr.color        = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = -1;
            sr.enabled      = false;
            renderers.Add(sr);
        }

        return renderers;
    }

    public void ClearPreviewBlur(CellView view)
    {
        if (view.previewBlurRenderers == null) return;

        foreach (var blur in view.previewBlurRenderers)
            if (blur != null)
                Destroy(blur.gameObject);

        view.previewBlurRenderers = null;
    }
}
