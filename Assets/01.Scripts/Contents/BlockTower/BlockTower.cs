using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using JSAM;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif



[RequireComponent(typeof(PlacementZoneController))]
[RequireComponent(typeof(PostProcessingExclusionCamera))]
[RequireComponent(typeof(HeldBlockController))]
[RequireComponent(typeof(TetrominoSelectionController))]
[RequireComponent(typeof(InputHandler))]
[RequireComponent(typeof(CameraController))]
[RequireComponent(typeof(BombIceEffectController))]
[RequireComponent(typeof(TowerCellVisualizer))]
[RequireComponent(typeof(TowerPhysicsController))]
[RequireComponent(typeof(TowerSceneBuilder))]
[RequireComponent(typeof(HeldPlacementController))]
[RequireComponent(typeof(GameUIController))]
[RequireComponent(typeof(ScoreController))]
[ExecuteAlways]
public class BlockTower : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    #region Fields — Inspector

    [Header("Grid")]
    public int columns = 4;
    public int rows = 10;

    [Header("Controllers")]
    [SerializeField] private PlacementZoneController placementController;
    [SerializeField] private HeldBlockController heldController;
    [SerializeField] private TetrominoSelectionController selectionController;
    [SerializeField] BonusTetrominoSpriteSet bonusTetrominoSpriteSet;

    [Header("Special Blocks")]
    [SerializeField] TowerPhysicsController _physicsController;
    [SerializeField] BombIceEffectController _bombIceController;
    [SerializeField] TowerCellVisualizer _visualizer;
    [SerializeField] TowerSceneBuilder _sceneBuilder;
    [SerializeField] HeldPlacementController _heldPlacementController;

    [Header("Initial Tower Numbers")]
    [SerializeField] int initialTowerNumberTotal = 160;
    [SerializeField] bool randomizeInitialTowerNumbersOnPlay = true;

    [Header("Scene References (optional)")]
    [SerializeField] Transform towerRootTransform;
    [SerializeField] Transform floorTransform;
    [SerializeField] ScoreController _scoreController;

    #endregion

    #region Fields — Runtime State

    Transform _towerRoot;
    GameObject _generatedFloor;
    GameObject _generatedScoreLabel;
    GameObject _bonusPreviewRoot;
    GameObject _presetOutlineRoot;
    readonly HashSet<GameObject> _generatedObjects = new();
    bool _initialTowerNumbersRandomizedThisPlay;

    readonly TowerGridModel _grid = new();
    readonly Dictionary<Vector2Int, CellView> _cellViews    = new();
    readonly Dictionary<Vector2Int, CellView> _iceCellViews = new();

    Vector2 _lastExtractionCenter;

    readonly Color _failFlashColor = new(1f, 0.08f, 0.08f, 0.85f);

    float _floorY;
    int _extractionMinRow;
    int _extractionMaxRow;
    int _extractionMinCol;
    int _extractionMaxCol;

    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Events

    public UnityAction OnHudRebind;

    public event UnityAction OnTowerReady;
    public event UnityAction OnTowerReset;
    public UnityAction OnBlocksLifted;
    public UnityAction OnBlocksPlaced;
    public event UnityAction OnHoldCancelled;

    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Properties — Public

    public bool IsGameOver               => _scoreController?.IsGameOver ?? false;
    public bool IsHolding                => Held.IsHolding;
    public bool HasFocusedCell           => Selection.HasFocusedCell;
    public bool IsPresetSelectionActive  => Selection.IsPresetSelectionActive;
    public bool HasLastPlacementCenter   => _heldPlacementController != null && _heldPlacementController.HasLastPlacementCenter;
    public Vector2 LastPlacementCenter   => _heldPlacementController?.LastPlacementCenter ?? Vector2.zero;
    public int  ExtractionMinRow         => _extractionMinRow;
    public int  ExtractionMaxRow         => _extractionMaxRow;
    public float     FloorY              => _floorY;
    public Transform TowerRoot           => _towerRoot;
    public Transform FloorTransformRef   => floorTransform;
    public Vector2Int FocusedCell        => Selection.FocusedCell;
    public ScoreController  Score        => _scoreController;
    public GameObject GeneratedFloor     => _generatedFloor;
    public BonusTetrominoSpriteSet BonusTetrominoSpriteSet => bonusTetrominoSpriteSet;

    #endregion
    
    #region Properties — Internal API (for sub-controllers)

    public TowerGridModel                   Grid             => _grid;
    public Dictionary<Vector2Int, CellView> CellViews        => _cellViews;
    public Dictionary<Vector2Int, CellView> IceCellViews     => _iceCellViews;
    public BlockNumberSpriteSetAsset NumberSpriteSetAsset    => _visualizer?.NumberSpriteSetAsset;

    public void   TrackGeneratedObject(GameObject go) => MarkGeneratedObject(go);
    public void   DestroyTracked(GameObject go)       => DestroyLocal(go);
    public bool   IsTrackedObject(GameObject go)      => IsGeneratedObject(go);
    public Vector3 LocalToWorld(Vector3 local)        => _towerRoot != null
        ? _towerRoot.TransformPoint(local)
        : new Vector3(-columns * 0.5f, -rows * 0.5f, 0f) + local;

    #endregion

    #region Properties — TowerSceneBuilder API

    public void SetFloorY(float y)                    => _floorY = y;
    public void SetGeneratedFloor(GameObject floor)   => _generatedFloor = floor;

    #endregion

    #region Properties — Private Shorthands

    float blockBodyScale => _physicsController?.BlockBodyScale ?? 0.94f;
    Vector2Int placementMin => placementController.placementMin;
    Vector2Int placementMax => placementController.placementMax;
    HeldBlockController Held => heldController;
    TetrominoSelectionController Selection => selectionController;

    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Lifecycle
#if UNITY_EDITOR
    void OnValidate()
    {
        if (_physicsController == null)
            _physicsController = GetComponent<TowerPhysicsController>();
        if (_visualizer == null)
            _visualizer = GetComponent<TowerCellVisualizer>();
        if (_bombIceController == null)
            _bombIceController = GetComponent<BombIceEffectController>();
        if (_sceneBuilder == null)
            _sceneBuilder = GetComponent<TowerSceneBuilder>();
        if (placementController == null)
            placementController = GetComponent<PlacementZoneController>();
        if (heldController == null)
            heldController = GetComponent<HeldBlockController>();
        if (selectionController == null)
            selectionController = GetComponent<TetrominoSelectionController>();
        if (_scoreController == null)
            _scoreController = GetComponent<ScoreController>();
        if (_heldPlacementController == null)
            _heldPlacementController = GetComponent<HeldPlacementController>();

        if (Application.isPlaying)
            return;

        GetComponent<CameraController>()?.EnsureSecondaryViewObjects();
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!this || Application.isPlaying || !gameObject.scene.IsValid())
                return;

            EnsureEditorSceneObjectsVisible();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        };
    }
#endif
    
    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            EnsureEditorSceneObjectsVisible();
            return;
        }

        _initialTowerNumbersRandomizedThisPlay = false;
        if (_grid.Count > 0 || TryRestoreRuntimeStateFromScene())
        {
            SetupRuntimeSceneObjectsAfterTowerRestore();
            return;
        }

        RebuildEmptyTowerOnly();
    }
    
    void Update()
    {
        if (!Application.isPlaying)
        {
            placementController.UpdateEditorPlacementZonePreview();
            return;
        }

        if (IsGameOver) return;
        SyncPlacementZoneFromObject();
        UpdatePresetOutlineFeedback();
    }

    #endregion

    #region Tower Build & Restore

    public void Restart() => Rebuild();

    void Rebuild()
    {
        if (Application.isPlaying)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        RebuildEmptyTowerOnly();
    }

    void RebuildEmptyTowerOnly()
    {
        if (TryRestoreRuntimeStateFromScene())
        {
            placementController.EnsurePlacementZoneObjectVisible();
            SyncPlacementZoneFromObject();
            CreateFloor();
            CreateBoundaries();

            if (Application.isPlaying)
            {
                CreateScoreLabel();
                CreateGameOverScreen();
                OnTowerReady?.Invoke();
            }

            return;
        }

        if (Held.Root != null)
        {
            DestroyLocal(Held.Root);
            Held.Root = null;
        }

        Held.RelPos.Clear();
        Held.Data.Clear();
        Held.SourceCells.Clear();
        ClearGenerated();
        _grid.Clear();
        _cellViews.Clear();
        _iceCellViews.Clear();
        Selection.Selected.Clear();
        _heldPlacementController?.ResetPlacementMemory();
        Held.IsHolding = false;
        placementController.SetFrozen(false);
        Selection.HasFocusedCell = false;
        Selection.IsPresetSelectionActive = false;
        Selection.Anchor = Vector2Int.zero;
        Selection.Rotation = 0;
        Held.UsingKeyboardPlacement = false;
        _extractionMinCol = 0;
        _extractionMaxCol = columns - 1;
        _extractionMinRow = 0;
        _extractionMaxRow = rows - 1;
        Held.StartScore = 0;
        Held.MatchesBonus = false;
        OnTowerReset?.Invoke();
        HideResultScreens();
        BuildTower();
    }

    void SetupRuntimeSceneObjectsAfterTowerRestore()
    {
        RandomizeInitialTowerNumbersIfNeeded();
        foreach (var pair in _grid.AllCells)
            if (_cellViews.TryGetValue(pair.Key, out var view))
                _visualizer?.UpdateCellDataVisuals(pair.Value, view);
        RenameTowerCellsSequentially();
        UpdateExtractionTowerRowsFromCells();
        _physicsController?.UpdateTowerPhysicsState();
        placementController.EnsurePlacementZoneObjectVisible();
        SyncPlacementZoneFromObject();
        CreateFloor();
        CreateBoundaries();
        CreateScoreLabel();
        CreateGameOverScreen();
        OnTowerReady?.Invoke();
    }

    bool TryRestoreRuntimeStateFromScene()
    {
        var root = towerRootTransform != null ? towerRootTransform : transform.Find("TowerRoot");
        if (root == null) return false;

        var restoredStates = new Dictionary<Vector2Int, CellState>();
        var restoredViews  = new Dictionary<Vector2Int, CellView>();
        var restoredIceStates = new Dictionary<Vector2Int, CellState>();
        var restoredIceViews  = new Dictionary<Vector2Int, CellView>();
        var restoredOccupiedCells = new HashSet<Vector2Int>();

        var sceneCells = CollectSceneCellTransforms(root);
        foreach (var child in sceneCells)
        {
            var blockCell = child.GetComponent<BlockCell>();
            var sr = child.GetComponent<SpriteRenderer>();
            if (blockCell == null || sr == null) continue;

            var local = root.InverseTransformPoint(child.position);
            var cell = new Vector2Int(
                Mathf.RoundToInt(local.x - 0.5f),
                Mathf.RoundToInt(local.y - 0.5f));
            if (!restoredOccupiedCells.Add(cell))
            {
                DestroyDuplicateSceneCell(child);
                continue;
            }

            bool isIce = blockCell.Kind == CellKind.Ice;
            if (isIce)
            {
                SnapIceCellTransform(child, root, cell);
                if (restoredIceStates.ContainsKey(cell))
                    continue;
            }
            else
            {
                if (restoredStates.ContainsKey(cell))
                    continue;

                if (Application.isPlaying && child.parent != root)
                    AttachSceneCellToTowerRoot(child, root);
            }

            var outline = isIce ? null : child.Find("FocusOutline")?.GetComponent<SpriteRenderer>();
            if (!isIce && outline == null)
                outline = _visualizer?.SpawnCellOutline(child);
            if (outline != null)
                outline.enabled = false;

            var label = child.GetComponentInChildren<TextMeshPro>();
            if (label == null)
                label = _visualizer?.SpawnLabel(Mathf.Max(1, Mathf.RoundToInt(blockCell.Weight)), child);
            var box = child.GetComponent<BoxCollider>();
            if (box == null)
                box = child.gameObject.AddComponent<BoxCollider>();
            box.size = Vector3.one * (_physicsController?.LocalColliderSize() ?? 0.92f);
            box.sharedMaterial = _physicsController?.CreateFrictionMaterial();
            box.enabled = Application.isPlaying;

            if (Application.isPlaying && isIce)
                MakeIceCellStatic(child);

            var state = new CellState
            {
                number          = Mathf.Max(1, Mathf.RoundToInt(blockCell.Weight)),
                isOriginalTower = blockCell.IsOriginalTower,
                kind            = blockCell.Kind,
                concealedByBomb = _bombIceController?.IsConcealedByBomb(cell) ?? false
            };
            var view = new CellView
            {
                go                   = child.gameObject,
                sr                   = sr,
                numberSpriteRenderer = child.Find("NumberSprite")?.GetComponent<SpriteRenderer>(),
                outline              = outline,
                label                = label
            };

            if (isIce)
            {
                restoredIceStates[cell] = state;
                restoredIceViews[cell]  = view;
            }
            else
            {
                restoredStates[cell] = state;
                restoredViews[cell]  = view;
            }
        }

        if (restoredStates.Count == 0 && restoredIceStates.Count == 0)
            return false;

        _towerRoot = root;
        _physicsController?.ConfigureTowerRigidbody();

        _grid.Clear();
        _cellViews.Clear();
        _iceCellViews.Clear();
        foreach (var pair in restoredStates)
        {
            _grid.AddCell(pair.Key, pair.Value);
            _cellViews[pair.Key] = restoredViews[pair.Key];
        }
        foreach (var pair in restoredIceStates)
        {
            _grid.AddIceCell(pair.Key, pair.Value);
            _iceCellViews[pair.Key] = restoredIceViews[pair.Key];
        }

        RandomizeInitialTowerNumbersIfNeeded();

        foreach (var pair in _grid.AllCells)
            if (_cellViews.TryGetValue(pair.Key, out var view))
                _visualizer?.UpdateCellDataVisuals(pair.Value, view);

        foreach (var pair in _grid.AllIceCells)
            if (_iceCellViews.TryGetValue(pair.Key, out var view))
                _visualizer?.UpdateCellDataVisuals(pair.Value, view);

        RenameTowerCellsSequentially();

        Selection.Selected.Clear();
        _heldPlacementController?.ResetPlacementMemory();
        _physicsController?.ClearDetachedBlocks();
        _bombIceController?.ClearBombState();
        Held.RelPos.Clear();
        Held.Data.Clear();
        Held.SourceCells.Clear();
        Held.IsHolding = false;
        placementController.SetFrozen(false);
        Selection.HasFocusedCell = false;
        Selection.IsPresetSelectionActive = false;
        Held.UsingKeyboardPlacement = false;
        Held.StartScore = 0;
        Held.MatchesBonus = false;
        OnTowerReset?.Invoke();
        HideResultScreens();

        if (_generatedFloor == null)
        {
            var f = Util.FindSceneObjectByName(transform, gameObject.scene, "Floor");
            if (f) _generatedFloor = f.gameObject;
        }

        if (_generatedScoreLabel == null)
        {
            var f = transform.Find("ScoreLabel");
            if (f) _generatedScoreLabel = f.gameObject;
        }

        if (_bonusPreviewRoot == null)
        {
            var f = transform.Find("BonusPreview");
            if (f) _bonusPreviewRoot = f.gameObject;
        }

        UpdateExtractionTowerRowsFromCells();
        _scoreController?.RollBonusTarget();
        _physicsController?.UpdateTowerPhysicsState();
        return true;
    }

    void BuildTower()
    {
        if (TryRestoreRuntimeStateFromScene())
        {
            placementController.EnsurePlacementZoneObjectVisible();
            SyncPlacementZoneFromObject();
            CreateFloor();
            CreateBoundaries();

            if (!Application.isPlaying) return;

            CreateScoreLabel();
            CreateGameOverScreen();
            OnTowerReady?.Invoke();
            return;
        }

        GameObject towerRootGO;
        bool createdTowerRoot = false;
        if (towerRootTransform != null)
        {
            towerRootGO = towerRootTransform.gameObject;
        }
        else
        {
            var existingRoot = transform.Find("TowerRoot");
            if (existingRoot != null)
            {
                towerRootGO = existingRoot.gameObject;
            }
            else
            {
                towerRootGO = new GameObject("TowerRoot");
                MarkGeneratedObject(towerRootGO);
                towerRootGO.transform.SetParent(transform);
                createdTowerRoot = true;
            }
        }

        if (createdTowerRoot)
            towerRootGO.transform.position = new Vector3(-columns * 0.5f, -rows * 0.5f, 0f);
        _towerRoot = towerRootGO.transform;

        if (Application.isPlaying)
            _physicsController?.ConfigureTowerRigidbody();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var cell = new Vector2Int(col, row);
                var (state, view) = SpawnCell(cell, 2, isOriginalTower: true);
                _grid.AddCell(cell, state);
                _cellViews[cell] = view;
            }
        }

        RandomizeInitialTowerNumbersIfNeeded();
        foreach (var pair in _grid.AllCells)
            if (_cellViews.TryGetValue(pair.Key, out var view))
                _visualizer?.UpdateCellDataVisuals(pair.Value, view);
        RenameTowerCellsSequentially();

        if (Application.isPlaying)
            _physicsController?.UpdateTowerPhysicsState();

        UpdateExtractionTowerRowsFromCells();
        placementController.EnsurePlacementZoneObjectVisible();
        SyncPlacementZoneFromObject();
        CreateFloor();
        CreateBoundaries();

        if (!Application.isPlaying) return;

        CreateScoreLabel();
        _scoreController?.RollBonusTarget();
        CreateGameOverScreen();
        OnTowerReady?.Invoke();
    }

    (CellState state, CellView view) SpawnCell(Vector2Int cell, int number, bool isOriginalTower)
    {
        var go = new GameObject($"Cell_{cell.x}_{cell.y}");
        MarkGeneratedObject(go);
        go.transform.SetParent(_towerRoot, false);
        go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        go.transform.localScale = Vector3.one * blockBodyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _visualizer?.CreateBlockSprite();
        sr.sortingOrder = 0;

        var box = go.AddComponent<BoxCollider>();
        box.size = Vector3.one * (_physicsController?.LocalColliderSize() ?? 0.92f);
        box.sharedMaterial = _physicsController?.CreateFrictionMaterial();

        var bc = go.AddComponent<BlockCell>();
        bc.Weight = number;
        bc.IsOriginalTower = isOriginalTower;
        bc.Kind = CellKind.Normal;

        var outline = _visualizer?.SpawnCellOutline(go.transform);
        var label   = _visualizer?.SpawnLabel(number, go.transform);

        var state = new CellState { number = number, isOriginalTower = isOriginalTower, kind = CellKind.Normal };
        var view  = new CellView  { go = go, sr = sr, outline = outline, label = label };

        _visualizer?.UpdateCellDataVisuals(state, view);
        return (state, view);
    }

    void RandomizeInitialTowerNumbersIfNeeded()
    {
        if (!Application.isPlaying || !randomizeInitialTowerNumbersOnPlay || _initialTowerNumbersRandomizedThisPlay ||
            _grid.Count == 0)
            return;

        var states = new List<CellState>();
        foreach (var pair in _grid.AllCells)
            if (pair.Value.kind == CellKind.Normal)
                states.Add(pair.Value);

        int count = states.Count;
        if (count == 0)
            return;

        int minTotal = count * 2;
        int maxTotal = count * 6;
        int targetTotal = Mathf.Clamp(initialTowerNumberTotal, minTotal, maxTotal);

        var numbers = new int[count];
        for (int i = 0; i < count; i++)
            numbers[i] = 2;

        int remaining = targetTotal - minTotal;
        while (remaining > 0)
        {
            var available = new List<int>();
            for (int i = 0; i < count; i++)
                if (numbers[i] < 6)
                    available.Add(i);

            if (available.Count == 0)
                break;

            int index = available[Random.Range(0, available.Count)];
            numbers[index]++;
            remaining--;
        }

        for (int i = 0; i < count; i++)
            states[i].number = numbers[i];

        _initialTowerNumbersRandomizedThisPlay = true;
    }

    void RenameTowerCellsSequentially()
    {
        var orderedCells = new List<KeyValuePair<Vector2Int, CellState>>(_grid.AllCells);
        orderedCells.Sort((a, b) =>
        {
            int yCompare = a.Key.y.CompareTo(b.Key.y);
            return yCompare != 0 ? yCompare : a.Key.x.CompareTo(b.Key.x);
        });

        for (int i = 0; i < orderedCells.Count; i++)
        {
            if (!_cellViews.TryGetValue(orderedCells[i].Key, out var view)) continue;
            if (view.go != null)
                view.go.name = $"Cell({i + 1})";
        }
    }

    List<Transform> CollectSceneCellTransforms(Transform towerRoot)
    {
        var cells = new List<Transform>();
        var seen  = new HashSet<Transform>();
        if (towerRoot != null)
        {
            foreach (Transform child in towerRoot)
                if (child.GetComponent<BlockCell>() != null && seen.Add(child))
                    cells.Add(child);
        }

        foreach (Transform child in transform)
        {
            if (child == towerRoot) continue;
            var blockCell = child.GetComponent<BlockCell>();
            if (blockCell != null && seen.Add(child))
                cells.Add(child);
        }

        return cells;
    }

    void PruneOverlappingSceneCells(Transform towerRoot)
    {
        if (towerRoot == null)
            return;

        var occupied  = new HashSet<Vector2Int>();
        var sceneCells = CollectSceneCellTransforms(towerRoot);
        foreach (var child in sceneCells)
        {
            if (child == null || child.GetComponent<BlockCell>() == null)
                continue;

            var local = towerRoot.InverseTransformPoint(child.position);
            var cell  = new Vector2Int(
                Mathf.RoundToInt(local.x - 0.5f),
                Mathf.RoundToInt(local.y - 0.5f));

            if (occupied.Add(cell))
                continue;

            DestroyDuplicateSceneCell(child);
        }
    }

    void AttachSceneCellToTowerRoot(Transform cellTransform, Transform towerRoot)
    {
        if (cellTransform == null || towerRoot == null)
            return;

        if (cellTransform.parent != towerRoot)
            cellTransform.SetParent(towerRoot, true);
    }

    void MakeIceCellStatic(Transform ice)
    {
        if (ice == null)
            return;

        var rb = ice.GetComponent<Rigidbody>();
        if (rb != null)
            Destroy(rb);

        var worldPosition = ice.position;
        ice.SetParent(transform, worldPositionStays: true);
        ice.position = worldPosition;
        ice.localScale = Vector3.one;

        if (ice.TryGetComponent<SpriteRenderer>(out var sr))
        {
            sr.enabled = true;
            var sprite = _visualizer?.GetIceSprite();
            sr.sprite = _visualizer?.CreateBlockSprite();
            sr.color = sprite != null ? Color.clear : Util.IceBlockColor();
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingOrder = Mathf.Max(sr.sortingOrder, 1);
            if (sprite != null)
            {
                var overlay = _visualizer?.EnsureStandaloneNumberSpriteRenderer(ice);
                overlay.sprite = sprite;
                overlay.enabled = true;
                overlay.color = Color.white;
                overlay.sortingOrder = sr.sortingOrder + 1;
                _visualizer?.FitNumberSpriteToCell(overlay);
            }
        }

        if (ice.TryGetComponent<BoxCollider>(out var box))
        {
            box.enabled  = true;
            box.isTrigger = false;
            box.size   = Vector3.one;
            box.center = Vector3.zero;
        }

        var outline = ice.Find("FocusOutline");
        if (outline != null)
            DestroyLocal(outline.gameObject);

        var damage = ice.GetComponent<IceBlockCollisionDamage>();
        if (damage == null)
            damage = ice.gameObject.AddComponent<IceBlockCollisionDamage>();
        damage.Initialize(this);
    }

    void SnapIceCellTransform(Transform ice, Transform root, Vector2Int cell)
    {
        if (ice == null || root == null)
            return;

        var world = root.TransformPoint(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f));
        ice.position = new Vector3(world.x, world.y, ice.position.z);
        ice.localScale = Vector3.one;
    }

    #endregion

    #region Scene Object Management

    void EnsureEditorSceneObjectsVisible()
    {
        var root = towerRootTransform != null ? towerRootTransform : transform.Find("TowerRoot");
        if (root != null)
        {
            _towerRoot = root;
            PruneOverlappingSceneCells(root);
            if (_grid.Count == 0)
                TryRestoreRuntimeStateFromScene();
            placementController.EnsurePlacementZoneObjectVisible();
            SyncPlacementZoneFromObject();
            CreateFloor();
            CreateBoundaries();
            GetComponent<CameraController>()?.EnsureSecondaryViewObjects();
            return;
        }

        placementController.EnsurePlacementZoneObjectVisible();
        GetComponent<CameraController>()?.EnsureSecondaryViewObjects();
    }

    void ClearGenerated()
    {
        _physicsController?.ClearDetachedBlocks();

        if (_towerRoot == null && towerRootTransform == null)
        {
            var found = transform.Find("TowerRoot");
            if (found != null) _towerRoot = found;
        }

        if (_towerRoot != null)
        {
            if (towerRootTransform == null && IsGeneratedObject(_towerRoot.gameObject) &&
                !HasBlockCellChildren(_towerRoot))
            {
                DestroyLocal(_towerRoot.gameObject);
            }
            else
            {
                var children = new List<Transform>();
                foreach (Transform t in _towerRoot) children.Add(t);
                foreach (var t in children)
                {
                    if (t != null && IsGeneratedObject(t.gameObject) && t.GetComponent<BlockCell>() == null)
                        DestroyLocal(t.gameObject);
                }
            }

            _towerRoot = null;
            _physicsController?.ClearRigidbody();
        }

        if (_generatedFloor == null)
        {
            var f = Util.FindSceneObjectByName(transform, gameObject.scene, "Floor");
            if (f) _generatedFloor = f.gameObject;
        }

        if (_generatedScoreLabel == null)
        {
            var f = transform.Find("ScoreLabel");
            if (f) _generatedScoreLabel = f.gameObject;
        }

        if (_generatedScoreLabel != null)
        {
            DestroyLocal(_generatedScoreLabel);
            _generatedScoreLabel = null;
        }

        HideResultScreens();

        if (_presetOutlineRoot == null)
        {
            var f = transform.Find("PresetOutlinePreview");
            if (f) _presetOutlineRoot = f.gameObject;
        }

        if (_presetOutlineRoot != null)
        {
            DestroyLocal(_presetOutlineRoot);
            _presetOutlineRoot = null;
        }

        if (_bonusPreviewRoot == null)
        {
            var f = transform.Find("BonusPreview");
            if (f) _bonusPreviewRoot = f.gameObject;
        }

        if (_bonusPreviewRoot != null)
        {
            DestroyLocal(_bonusPreviewRoot);
            _bonusPreviewRoot = null;
        }

        var controlsHelp = transform.Find("ControlsHelp");
        if (controlsHelp != null) DestroyLocal(controlsHelp.gameObject);
    }

    void MarkGeneratedObject(GameObject go)
    {
        if (go != null)
            _generatedObjects.Add(go);
#if UNITY_EDITOR
        if (!Application.isPlaying)
            go.hideFlags = HideFlags.DontSaveInEditor;
#endif
    }

    void DestroyLocal(GameObject go)
    {
        if (!Application.isPlaying)
            return;

        _generatedObjects.Remove(go);
        Destroy(go);
    }

    bool IsGeneratedObject(GameObject go)
    {
        if (go == null) return false;
        if (_generatedObjects.Contains(go)) return true;
#if UNITY_EDITOR
        return (go.hideFlags & HideFlags.DontSaveInEditor) != 0;
#else
        return false;
#endif
    }

    bool HasBlockCellChildren(Transform root)
    {
        if (root == null)
            return false;

        foreach (Transform child in root)
            if (child != null && child.GetComponent<BlockCell>() != null)
                return true;
        return false;
    }

    void DestroyDuplicateSceneCell(Transform duplicate)
    {
        if (duplicate == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.DestroyObjectImmediate(duplicate.gameObject);
            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            return;
        }
#endif

        Destroy(duplicate.gameObject);
    }

    void SyncPlacementZoneFromObject(bool updateVisuals = true)
    {
        placementController.Configure(this, _towerRoot, _sceneBuilder != null ? _sceneBuilder.TowerStackDividerGO?.transform : null, _visualizer?.CreateBlockSprite());
        placementController.SyncPlacementZoneFromObject(updateVisuals);
    }

    void CreateFloor()       => _sceneBuilder?.CreateFloor();
    void CreateBoundaries()  => _sceneBuilder?.CreateBoundaries();
    void CreateGameOverScreen() => _sceneBuilder?.CreateGameOverScreen();
    void CreateScoreLabel()  => _sceneBuilder?.CreateScoreLabel();
    void UpdateTowerStackDivider() => _sceneBuilder?.UpdateTowerStackDivider();

    #endregion

    #region Extraction & Selection

    public void HandleClick()
    {
        _physicsController?.RefreshDetachedComponents();

        var worldPos = Util.MouseWorldPos();
        var local = _towerRoot.InverseTransformPoint(worldPos);
        var cell = new Vector2Int(
            Mathf.FloorToInt(local.x),
            Mathf.FloorToInt(local.y));

        if (!IsExtractableCell(cell))
            return;

        if (Selection.IsPresetSelectionActive)
        {
            ClearSelectedCellsOnly();
            ClearPresetOutlinePreview();
            Selection.CancelPresetSelection();
        }

        SetFocusCell(cell);

        if (Selection.Selected.Contains(cell))
        {
            Debug.Log($"[CLICK SAME CELL] {cell}");
            TryDeselect(cell);
            return;
        }

        if (Selection.Selected.Count > 0 && !Selection.IsAdjacentToSelected(cell))
            ClearSelectedCellsOnly();

        if (Selection.Selected.Count < 4)
        {
            SelectCell(cell);

            if (Selection.Selected.Count == 4)
                LiftBlocks();
        }
    }

    void SelectCell(Vector2Int cell)
    {
        if (!_grid.TryGetCell(cell, out var state)) return;
        if (!state.isOriginalTower) return;
        if (state.kind == CellKind.Ice) return;
        if (Selection.Selected.Contains(cell)) return;
        Selection.Selected.Add(cell);
        _visualizer?.ApplyCellVisual(cell);
    }

    void DeselectCell(Vector2Int cell)
    {
        Selection.Selected.Remove(cell);
        _visualizer?.ApplyCellVisual(cell);
    }

    bool IsExtractableCell(Vector2Int cell) => _grid.IsExtractableCell(cell);

    void TryDeselect(Vector2Int cell)
    {
        Debug.Log($"[TRY DESELECT] {cell}");
        Debug.Log($"[BEFORE] Count={Selection.Selected.Count}");

        var remaining = new List<Vector2Int>(Selection.Selected);
        remaining.Remove(cell);

        Debug.Log($"[AFTER REMOVE] Remaining={remaining.Count}");

        if (remaining.Count <= 1 || _grid.IsConnected(remaining))
        {
            Debug.Log("[DESELECT EXECUTED]");
            DeselectCell(cell);
        }
    }

    public void ClearSelection()
    {
        Selection.IsPresetSelectionActive = false;
        ClearPresetOutlinePreview();
        foreach (var c in new List<Vector2Int>(Selection.Selected))
            DeselectCell(c);
    }

    void ClearSelectedCellsOnly()
    {
        foreach (var c in new List<Vector2Int>(Selection.Selected))
            DeselectCell(c);
    }

    void LiftBlocks()
    {
        Selection.IsPresetSelectionActive = false;
        ClearPresetOutlinePreview();
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in Selection.Selected)
        {
            minX = Mathf.Min(minX, c.x);
            minY = Mathf.Min(minY, c.y);
            maxX = Mathf.Max(maxX, c.x);
            maxY = Mathf.Max(maxY, c.y);
        }

        var extractionScorePos = _towerRoot.TransformPoint(new Vector3(
            (minX + maxX + 1f) * 0.5f,
            (minY + maxY + 1f) * 0.5f,
            0f));
        Held.Center = new Vector2((maxX - minX + 1) * 0.5f, (maxY - minY + 1) * 0.5f);

        Held.RelPos.Clear();
        Held.Data.Clear();
        Held.SourceCells.Clear();
        Held.StartScore = _scoreController?.Score ?? 0;
        _lastExtractionCenter = new Vector2((minX + maxX + 1f) * 0.5f, (minY + maxY + 1f) * 0.5f);
        foreach (var c in Selection.Selected)
            Held.RelPos.Add(new Vector2Int(c.x - minX, c.y - minY));
        Held.MatchesBonus = TetrominoShapeUtil.ShapeMatchesPreset(Held.RelPos, _scoreController?.BonusTargetPreset ?? TetrominoPreset.I);

        foreach (var pair in _grid.AllCells)
        {
            Held.SourceCells.Add(new HeldSourceCell
            {
                cell            = pair.Key,
                number          = pair.Value.number,
                isOriginalTower = pair.Value.isOriginalTower,
                kind            = pair.Value.kind
            });
        }

        var changedCells = new List<Vector2Int>();
        var bombCells    = new List<Vector2Int>();
        foreach (var cell in Selection.Selected)
        {
            if (!_grid.TryGetCell(cell, out var state)) continue;
            if (!_cellViews.TryGetValue(cell, out var view)) continue;
            changedCells.Add(cell);
            if (state.kind == CellKind.Bomb)
                bombCells.Add(cell);
            state.number--;
            if (state.number <= 0)
            {
                Destroy(view.go);
                _grid.RemoveCell(cell);
                _cellViews.Remove(cell);
            }
            else
            {
                var bc = view.go.GetComponent<BlockCell>();
                if (bc != null)
                {
                    bc.Weight = state.number;
                    bc.IsOriginalTower = state.isOriginalTower;
                    bc.Kind = state.kind;
                }
                _visualizer?.UpdateCellDataVisuals(state, view);
            }
        }

        Selection.Selected.Clear();
        Selection.HasFocusedCell = false;
        foreach (var cell in changedCells)
            _visualizer?.ApplyCellVisual(cell);
        foreach (var bombCell in bombCells)
            _bombIceController?.TriggerBombAt(bombCell);

        Held.Root = new GameObject("HeldBlocks");
        MarkGeneratedObject(Held.Root);
        Held.Root.transform.SetParent(transform);

        var pc        = _visualizer?.PlacedBlockColor ?? new Color(0.55f, 0.58f, 0.60f, 1f);
        var heldColor = new Color(pc.r, pc.g, pc.b, 0.6f);

        for (int i = 0; i < Held.RelPos.Count; i++)
        {
            var rel = Held.RelPos[i];

            var go = new GameObject($"Held_{i}");
            MarkGeneratedObject(go);
            go.transform.SetParent(Held.Root.transform, false);
            go.transform.localPosition = new Vector3(
                rel.x + 0.5f - Held.Center.x,
                rel.y + 0.5f - Held.Center.y,
                0f);
            go.transform.localScale = Vector3.one * blockBodyScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _visualizer?.CreateBlockSprite();
            sr.color = heldColor;
            sr.sortingOrder = 20;
            var blurRenderers = _visualizer?.CreatePreviewBlur(go.transform) ?? new List<SpriteRenderer>();

            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one * (_physicsController?.LocalColliderSize() ?? 0.92f);
            box.sharedMaterial = _physicsController?.CreateFrictionMaterial();
            box.enabled = false;

            var bc = go.AddComponent<BlockCell>();
            bc.Weight = 1;
            bc.IsOriginalTower = false;

            var heldState = new CellState { number = 1, isOriginalTower = false };
            var heldView  = new CellView
            {
                go = go,
                sr = sr,
                outline = null,
                label   = null,
                previewBlurRenderers = blurRenderers
            };
            Held.Data.Add((heldState, heldView));
        }

        _physicsController?.CheckForDetachment();
        _physicsController?.UpdateTowerPhysicsState();

        AudioManager.PlaySound(_AudioLibrarySounds.Hold);
        Held.IsHolding = true;
        if (_heldPlacementController != null)
            Held.BaseCell = _heldPlacementController.GetDefaultHeldBaseCell();
        Held.UsingKeyboardPlacement = true;
        OnBlocksLifted?.Invoke();
        _scoreController?.AddScore(Held.MatchesBonus ? 2 : 1, extractionScorePos);
        _scoreController?.RollBonusTarget();
    }

    void RestoreHeldSourceCells()
    {
        foreach (var pair in _cellViews)
            if (pair.Value.go != null)
                DestroyLocal(pair.Value.go);

        _grid.Clear();
        _cellViews.Clear();
        _iceCellViews.Clear();

        _physicsController?.ClearDetachedBlocks();

        foreach (var source in Held.SourceCells)
        {
            var (state, view) = SpawnCell(source.cell, source.number, source.isOriginalTower);
            state.kind = source.kind;
            state.concealedByBomb = _bombIceController?.IsConcealedByBomb(source.cell) ?? false;
            _visualizer?.UpdateCellDataVisuals(state, view);
            _grid.AddCell(source.cell, state);
            _cellViews[source.cell] = view;
            _visualizer?.ApplyCellVisual(source.cell);
        }

        _scoreController?.SetScoreTo(Held.StartScore);
        _heldPlacementController?.ResetPlacementMemory();
        Selection.ClearFocus();
        UpdateExtractionTowerRowsFromCells();
    }

    public void CancelHold()
    {
        if (!Held.IsHolding) return;
        if (Held.Root != null) { Destroy(Held.Root); Held.Root = null; }
        RestoreHeldSourceCells();
        Held.RelPos.Clear();
        Held.Data.Clear();
        Held.SourceCells.Clear();
        Held.StartScore = 0;
        Held.MatchesBonus = false;
        Held.IsHolding = false;
        Held.UsingKeyboardPlacement = false;
        _physicsController?.UpdateTowerPhysicsState();
        OnHoldCancelled?.Invoke();
        FocusDefaultExtractionCell();
        _scoreController?.AddScore(-2, transform.position);
    }

    void CreatePresetOutlinePreview(List<Vector2Int> cells)
    {
        ClearPresetOutlinePreview();

        _presetOutlineRoot = new GameObject("PresetOutlinePreview");
        MarkGeneratedObject(_presetOutlineRoot);
        _presetOutlineRoot.transform.SetParent(_towerRoot, false);
        Util.SetNoPostLayer(_presetOutlineRoot);

        foreach (var cell in cells)
        {
            var go = new GameObject($"PresetOutline_{cell.x}_{cell.y}");
            MarkGeneratedObject(go);
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

        bool isFailing = Time.time < Held.FailEndTime;
        _presetOutlineRoot.transform.localPosition = Held.FailShakeOffset(isFailing);

        var color = isFailing ? _failFlashColor : (_visualizer?.SelectedOutlineColor ?? new Color(1f, 1f, 1f, 0.95f));
        foreach (var sr in _presetOutlineRoot.GetComponentsInChildren<SpriteRenderer>())
            sr.color = color;
    }

    public void ClearPresetOutlinePreview()
    {
        if (_presetOutlineRoot != null) { DestroyLocal(_presetOutlineRoot); _presetOutlineRoot = null; }
    }

    public void UpdateExtractionTowerRowsFromCells()
    {
        bool hasOriginal = false;
        foreach (var pair in _grid.AllCells)
            if (pair.Value.isOriginalTower) { hasOriginal = true; break; }

        if (!hasOriginal)
        {
            _extractionMinCol = 0; _extractionMaxCol = columns - 1;
            _extractionMinRow = 0; _extractionMaxRow = rows - 1;
            UpdateTowerStackDivider();
            return;
        }

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var pair in _grid.AllCells)
        {
            var cell = pair.Key;
            if (!_grid.IsExtractableCell(cell)) continue;
            minX = Mathf.Min(minX, cell.x); maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y); maxY = Mathf.Max(maxY, cell.y);
        }
        _extractionMinCol = minX == int.MaxValue ? 0           : minX;
        _extractionMaxCol = maxX == int.MinValue ? columns - 1 : maxX;
        _extractionMinRow = minY == int.MaxValue ? 0           : minY;
        _extractionMaxRow = maxY == int.MinValue ? -1          : maxY;
        UpdateTowerStackDivider();
    }

    bool IsInExtractionTowerRows(Vector2Int cell) =>
        cell.x >= _extractionMinCol && cell.x <= _extractionMaxCol &&
        cell.y >= _extractionMinRow && cell.y <= _extractionMaxRow;

    #endregion


    #region Focus

    void SetFocusCell(Vector2Int cell) => Selection.SetFocus(this, cell);

    public void FocusDefaultExtractionCell()
    {
        Selection.ClearFocus();
        if (TryFindFocusNearLastExtraction(ignoreLastPlaced: true, out var lastCell) ||
            TryFindFocusNearLastExtraction(ignoreLastPlaced: false, out lastCell))
        {
            SetFocusCell(lastCell);
            return;
        }

        if (TryFindDefaultFocusCell(ignoreLastPlaced: true, out var cell) ||
            TryFindDefaultFocusCell(ignoreLastPlaced: false, out cell))
        {
            SetFocusCell(cell);
        }
    }

    bool TryFindFocusNearLastExtraction(bool ignoreLastPlaced, out Vector2Int best)
    {
        best = default;
        bool found = false;
        float bestDistance = float.MaxValue;

        foreach (var pair in _grid.AllCells)
        {
            var cell = pair.Key;
            if (ignoreLastPlaced && _heldPlacementController.IsLastPlaced(cell)) continue;
            if (!_grid.IsExtractableCell(cell)) continue;
            if (!IsInExtractionTowerRows(cell)) continue;

            float distance = Vector2.SqrMagnitude(new Vector2(cell.x + 0.5f, cell.y + 0.5f) - _lastExtractionCenter);
            if (!found || distance < bestDistance ||
                Mathf.Approximately(distance, bestDistance) && cell.y > best.y)
            {
                best = cell;
                bestDistance = distance;
                found = true;
            }
        }

        return found;
    }

    bool TryFindDefaultFocusCell(bool ignoreLastPlaced, out Vector2Int cell)
    {
        cell = default;
        foreach (var pair in _grid.AllCells)
        {
            var c = pair.Key;
            if (ignoreLastPlaced && _heldPlacementController.IsLastPlaced(c)) continue;
            if (!_grid.IsExtractableCell(c)) continue;
            if (!IsInExtractionTowerRows(c)) continue;
            cell = c;
            return true;
        }
        return false;
    }

    #endregion

    #region Grid Queries & Coordinates

    public List<Vector2Int> GetPresetCells(Vector2Int anchor, TetrominoPreset preset, int rotation) =>
        TetrominoShapeUtil.GetCells(anchor, preset, rotation);

    public int  HighestOccupiedRow() => _grid.HighestOccupiedRow();

    public bool TryGetOccupiedGridBounds(out int minX, out int maxX, out int minY, out int maxY) =>
        _grid.TryGetOccupiedGridBounds(out minX, out maxX, out minY, out maxY);

    public bool TryWorldToGridCell(Vector3 worldPosition, out Vector2Int cell)
    {
        var local = _towerRoot.InverseTransformPoint(worldPosition);
        cell = new Vector2Int(
            Mathf.RoundToInt(local.x - 0.5f),
            Mathf.RoundToInt(local.y - 0.5f));

        int minGridX = Mathf.Min(placementMin.x, _extractionMinCol);
        int maxGridX = Mathf.Max(placementMax.x, _extractionMaxCol);
        return cell.x >= minGridX && cell.x <= maxGridX && cell.y >= 0;
    }

    #endregion

    #region Score & Game State

    public void TriggerGameOver() => _scoreController?.TriggerGameOver();

    public void PerformGameEndCleanup()
    {
        placementController.SetFrozen(true);
        if (Held.Root != null)
        {
            Destroy(Held.Root);
            Held.Root = null;
            Held.IsHolding = false;
        }
        _physicsController?.ClearDetachedBlocks();
    }
    
    public void HideResultScreens() => OnHudRebind?.Invoke();

    #endregion

    #region Ice & Bomb Effects

    public bool ApplyIceDamage(BlockCell iceBlockCell, Vector3 iceWorldPosition)
        => _bombIceController?.ApplyIceDamage(iceBlockCell, iceWorldPosition) ?? false;

    #endregion

    #region Selection Controller Bridge

    public bool SelectionHasCell(Vector2Int cell)                               => _grid.HasCell(cell);
    public bool SelectionIsExtractableCell(Vector2Int cell)                     => IsExtractableCell(cell);
    public void SelectionApplyCellVisual(Vector2Int cell)                       => _visualizer?.ApplyCellVisual(cell);
    public void SelectionFocusDefaultExtractionCell()                           => FocusDefaultExtractionCell();
    public void SelectionSelectCell(Vector2Int cell)                            => SelectCell(cell);
    public void SelectionTryDeselect(Vector2Int cell)                           => TryDeselect(cell);
    public void SelectionClearSelectedCellsOnly()                               => ClearSelectedCellsOnly();
    public void SelectionLiftBlocks()                                           => LiftBlocks();
    public void SelectionCreatePresetOutlinePreview(List<Vector2Int> cells)     => CreatePresetOutlinePreview(cells);
    public void SelectionPlayPlacementFailFeedback()                            => Held.PlayPlacementFailFeedback();

    #endregion
}

class CellData
{
    public int            number;
    public bool           isOriginalTower;
    public GameObject     go;
    public SpriteRenderer sr;
    public SpriteRenderer numberSpriteRenderer;
    public SpriteRenderer outline;
    public TextMeshPro    label;
    public List<SpriteRenderer> previewBlurRenderers;
    public CellKind kind;
    public bool concealedByBomb;
}

public class DetachedComponent
{
    public GameObject root;
    public Rigidbody rb;
    public float detachedAt;
    public int scorePenalty;
    public bool resolved;
    public bool iceDamageApplied;
}

class HeldSourceCell
{
    public Vector2Int cell;
    public int number;
    public bool isOriginalTower;
    public CellKind kind;
}