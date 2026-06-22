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
[RequireComponent(typeof(BlockExtractionController))]
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
    [SerializeField] HeldPlacementController     _heldPlacementController;
    [SerializeField] BlockExtractionController   _extractionController;

    [Header("Initial Tower Numbers")]
    [SerializeField] int initialTowerNumberTotal = 160;
    [SerializeField] bool randomizeInitialTowerNumbersOnPlay = true;

    [Header("Scene References (optional)")]
    [SerializeField] Transform towerRootTransform;
    [SerializeField] Transform floorTransform;
    [SerializeField] ScoreController _scoreController;
    [SerializeField] bool followTowerRootMovement = true;

    #endregion

    #region Fields — Runtime State

    Transform _towerRoot;
    GameObject _generatedFloor;
    GameObject _generatedScoreLabel;
    GameObject _bonusPreviewRoot;
    readonly HashSet<GameObject> _generatedObjects = new();
    bool _initialTowerNumbersRandomizedThisPlay;
    bool _hasTowerRootFollowState;
    Vector3 _lastTowerRootPosition;
    Vector3 _lastBlockTowerPosition;

    readonly TowerGridModel _grid = new();
    readonly Dictionary<Vector2Int, CellView> _cellViews    = new();
    readonly Dictionary<Vector2Int, CellView> _iceCellViews = new();

    float _floorY;

    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Events

    public UnityAction OnHudRebind;

    public event UnityAction OnTowerReady;
    public event UnityAction OnTowerReset;
    public UnityAction OnBlocksLifted;
    public UnityAction OnBlocksPlaced;
    public UnityAction OnHoldCancelled;

    #endregion
    // ─────────────────────────────────────────────────────────────
    #region Properties — Public

    public bool IsGameOver               => _scoreController?.IsGameOver ?? false;
    public bool IsHolding                => Held.IsHolding;
    public bool HasFocusedCell           => Selection.HasFocusedCell;
    public bool IsPresetSelectionActive  => Selection.IsPresetSelectionActive;
    public bool IsResolvingTopPuyo       => _heldPlacementController != null && _heldPlacementController.IsResolvingTopPuyo;
    public bool HasLastPlacementCenter   => _heldPlacementController != null && _heldPlacementController.HasLastPlacementCenter;
    public Vector2 LastPlacementCenter   => _heldPlacementController?.LastPlacementCenter ?? Vector2.zero;
    public int  ExtractionMinRow         => _extractionController?.ExtractionMinRow ?? 0;
    public int  ExtractionMaxRow         => _extractionController?.ExtractionMaxRow ?? (rows - 1);
    public float     FloorY              => _floorY;
    public Transform TowerRoot           => _towerRoot;
    public Transform FloorTransformRef   => floorTransform;
    public Vector2Int FocusedCell        => Selection.FocusedCell;
    public ScoreController  Score        => _scoreController;
    public GameObject GeneratedFloor     => _generatedFloor;
    public BonusTetrominoSpriteSet BonusTetrominoSpriteSet => bonusTetrominoSpriteSet;
    public bool ApplyIceDamage(BlockCell iceBlockCell, Vector3 iceWorldPosition)
        => _bombIceController?.ApplyIceDamage(iceBlockCell, iceWorldPosition) ?? false;

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
        if (_extractionController == null)
            _extractionController = GetComponent<BlockExtractionController>();

        if (Application.isPlaying)
            return;
    }
#endif
    
    void Start()
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
    }

    void LateUpdate()
    {
        SyncBlockTowerToTowerRootMovement();
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
            box.enabled = true;

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
        CaptureTowerRootFollowState();
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
        CaptureTowerRootFollowState();

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
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Destroy(rb);
        }

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
            CaptureTowerRootFollowState();
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
            ResetTowerRootFollowState();
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

        _extractionController?.ClearPresetOutlinePreview();

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

    void SyncBlockTowerToTowerRootMovement()
    {
        if (!followTowerRootMovement || _towerRoot == null || _towerRoot == transform)
        {
            ResetTowerRootFollowState();
            return;
        }

        var rootPosition = _towerRoot.position;
        var blockPosition = transform.position;

        if (!_hasTowerRootFollowState)
        {
            _lastTowerRootPosition = rootPosition;
            _lastBlockTowerPosition = blockPosition;
            _hasTowerRootFollowState = true;
            return;
        }

        var rootDelta = rootPosition - _lastTowerRootPosition;
        var blockDelta = blockPosition - _lastBlockTowerPosition;
        var rootOnlyDelta = rootDelta - blockDelta;

        if (rootOnlyDelta.sqrMagnitude > 0.0000001f)
        {
            var iceWorldPositions = new Dictionary<Transform, Vector3>();
            foreach (var pair in _iceCellViews)
                if (pair.Value.go != null)
                    iceWorldPositions[pair.Value.go.transform] = pair.Value.go.transform.position;

            transform.position += rootOnlyDelta;
            _towerRoot.position = rootPosition;
            foreach (var pair in iceWorldPositions)
                if (pair.Key != null)
                    pair.Key.position = pair.Value;
            RefreshFloorYFromKnownFloor();
            SyncPlacementZoneFromObject(false);
        }
        else if (blockDelta.sqrMagnitude > 0.0000001f)
        {
            RefreshFloorYFromKnownFloor();
        }

        _lastTowerRootPosition = _towerRoot.position;
        _lastBlockTowerPosition = transform.position;
    }

    void CaptureTowerRootFollowState()
    {
        if (_towerRoot == null)
        {
            ResetTowerRootFollowState();
            return;
        }

        _lastTowerRootPosition = _towerRoot.position;
        _lastBlockTowerPosition = transform.position;
        _hasTowerRootFollowState = true;
    }

    void ResetTowerRootFollowState()
    {
        _hasTowerRootFollowState = false;
        _lastTowerRootPosition = Vector3.zero;
        _lastBlockTowerPosition = Vector3.zero;
    }

    void RefreshFloorYFromKnownFloor()
    {
        var floor = floorTransform != null ? floorTransform : _generatedFloor != null ? _generatedFloor.transform : null;
        if (floor != null)
            _floorY = floor.position.y;
    }

    void CreateFloor()       => _sceneBuilder?.CreateFloor();
    void CreateBoundaries()  => _sceneBuilder?.CreateBoundaries();
    void CreateGameOverScreen() => _sceneBuilder?.CreateGameOverScreen();
    void CreateScoreLabel()  => _sceneBuilder?.CreateScoreLabel();
    void UpdateTowerStackDivider() => _sceneBuilder?.UpdateTowerStackDivider();

    #endregion

    #region Extraction & Focus — Passthroughs

    public void UpdateExtractionTowerRowsFromCells() => _extractionController?.UpdateExtractionTowerRowsFromCells();
    public void FocusDefaultExtractionCell()         => _extractionController?.FocusDefaultExtractionCell();

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

        int minGridX = Mathf.Min(placementMin.x, _extractionController?.ExtractionMinCol ?? 0);
        int maxGridX = Mathf.Max(placementMax.x, _extractionController?.ExtractionMaxCol ?? (columns - 1));
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

}

public class DetachedComponent
{
    public GameObject root;
    public Rigidbody rb;
    public float detachedAt;
    public int scorePenalty;
    public bool preventReattach;
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
