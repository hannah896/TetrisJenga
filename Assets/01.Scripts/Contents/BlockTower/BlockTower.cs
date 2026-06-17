using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;
using JSAM;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

[RequireComponent(typeof(BlockNumberSpriteSet))]
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
[RequireComponent(typeof(GameUIController))]
[RequireComponent(typeof(ScoreController))]
[ExecuteAlways]
public class BlockTower : MonoBehaviour
{
    [Header("Grid")] public int columns = 4;
    public int rows = 10;
    
    # region Placement

    [SerializeField] private PlacementZoneController placementController;
    [SerializeField] private HeldBlockController heldController;
    [SerializeField] private TetrominoSelectionController selectionController;

    [SerializeField] BonusTetrominoSpriteSet bonusTetrominoSpriteSet;
    # endregion

    [Header("Special Blocks")] [SerializeField]
    TowerPhysicsController _physicsController;
    [SerializeField] BombIceEffectController _bombIceController;
    [SerializeField] TowerCellVisualizer _visualizer;
    [SerializeField] TowerSceneBuilder _sceneBuilder;

    float blockBodyScale => _physicsController?.BlockBodyScale ?? 0.94f;

    [Header("Initial Tower Numbers")] [SerializeField]
    int initialTowerNumberTotal = 160;

    [SerializeField] bool randomizeInitialTowerNumbersOnPlay = true;

    [Header("Scene References (optional)")] 

    [SerializeField] Transform towerRootTransform;
    [SerializeField] Transform floorTransform;

    [SerializeField] ScoreController           _scoreController;

    Transform _towerRoot;
    GameObject _generatedFloor;
    GameObject _generatedScoreLabel;
    GameObject _leftBoundary;
    GameObject _rightBoundary;
    GameObject _towerStackDivider;
    GameObject _bonusPreviewRoot;
    GameObject _presetOutlineRoot;
    readonly HashSet<GameObject> _generatedObjects = new();
    
    bool _initialTowerNumbersRandomizedThisPlay;

    # region  Grid
    readonly TowerGridModel _grid = new();
    readonly Dictionary<Vector2Int, CellView> _cellViews    = new();
    private readonly Dictionary<Vector2Int, CellView> _iceCellViews = new();
    
    # endregion
    
    readonly HashSet<Vector2Int> _lastPlacedCells = new();
    Vector2 _lastPlacementCenter;
    bool _hasLastPlacementCenter;
    Vector2 _lastExtractionCenter;


    
    readonly Color _validHoldColor = new(0.55f, 0.85f, 0.6f, 0.5f);
    readonly Color _invalidHoldColor = new(1f, 0.25f, 0.25f, 0.6f);
    readonly Color _failFlashColor = new(1f, 0.08f, 0.08f, 0.85f);


    float _floorY;
    int _extractionMinRow;
    int _extractionMaxRow;
    int _extractionMinCol;
    int _extractionMaxCol;
    
    public bool IsGameOver  => _scoreController?.IsGameOver ?? false;
    public bool IsHolding              => Held.IsHolding;
    public bool HasFocusedCell         => Selection.HasFocusedCell;
    public bool IsPresetSelectionActive => Selection.IsPresetSelectionActive;
    public bool IsUsingKeyboardPlacement => Held.UsingKeyboardPlacement;
    public int  SelectionCount           => Selection.Selected.Count;
    public BonusTetrominoSpriteSet BonusTetrominoSpriteSet => bonusTetrominoSpriteSet;

    public System.Action OnHudRebind;

    public event System.Action OnTowerReady;
    public event System.Action OnTowerReset;
    public event System.Action OnBlocksLifted;
    public event System.Action OnBlocksPlaced;
    public event System.Action OnHoldCancelled;

    // 분리된 컨트롤러들이 접근하는 내부 API
    public TowerGridModel                   Grid               => _grid;
    public Dictionary<Vector2Int, CellView> CellViews          => _cellViews;
    public Dictionary<Vector2Int, CellView> IceCellViews       => _iceCellViews;
    public BlockNumberSpriteSetAsset        NumberSpriteSetAsset => _visualizer?.NumberSpriteSetAsset;
    public Vector2Int                       FocusedCell        => Selection.FocusedCell;
    public bool IsSelectedCell(Vector2Int cell)                  => Selection.Selected.Contains(cell);
    public void  TrackGeneratedObject(GameObject go)             => MarkGeneratedObject(go);
    public Vector3 LocalToWorld(Vector3 local)                   => _towerRoot != null ? _towerRoot.TransformPoint(local) : new Vector3(-columns * 0.5f, -rows * 0.5f, 0f) + local;

    public float      FloorY                => _floorY;
    public Transform  TowerRoot             => _towerRoot;
    public ScoreController  Score           => _scoreController;
    public GameObject       GeneratedFloor  => _generatedFloor;
    public Transform        FloorTransformRef => floorTransform;
    public void DestroyTracked(GameObject go) => DestroyLocal(go);

    // TowerSceneBuilder 공개 API
    public void SetFloorY(float y)                    => _floorY = y;
    public void SetGeneratedFloor(GameObject floor)   => _generatedFloor = floor;
    public GameObject LeftBoundary                    => _leftBoundary;
    public void SetLeftBoundary(GameObject b)         => _leftBoundary = b;
    public GameObject RightBoundary                   => _rightBoundary;
    public void SetRightBoundary(GameObject b)        => _rightBoundary = b;
    public GameObject TowerStackDividerGO             => _towerStackDivider;
    public void SetTowerStackDividerGO(GameObject go) => _towerStackDivider = go;
    public bool IsTrackedObject(GameObject go)        => IsGeneratedObject(go);
    public bool       HasLastPlacementCenter => _hasLastPlacementCenter;
    public Vector2    LastPlacementCenter   => _lastPlacementCenter;
    public int        ExtractionMinRow      => _extractionMinRow;
    public int        ExtractionMaxRow      => _extractionMaxRow;

    Vector2Int placementMin => placementController.placementMin;
    Vector2Int placementMax => placementController.placementMax;
    Transform placementZoneTransform => placementController.PlacementZoneTransform;
    bool usePlacementZoneObject => placementController.UsePlacementZoneObject;
    HeldBlockController Held => heldController;
    TetrominoSelectionController Selection => selectionController;

    public List<Vector2Int> GetPresetCells(Vector2Int anchor, TetrominoPreset preset, int rotation) =>
        TetrominoShapeUtil.GetCells(anchor, preset, rotation);

    public int  HighestOccupiedRow() => _grid.HighestOccupiedRow();
    public bool TryGetOccupiedGridBounds(out int minX, out int maxX, out int minY, out int maxY)
        => _grid.TryGetOccupiedGridBounds(out minX, out maxX, out minY, out maxY);

    public void Restart() => Rebuild();

    public void InitKeyboardPlacement()
    {
        Held.BaseCell = ClampHeldBase(Held.BaseCell);
        Held.UsingKeyboardPlacement = true;
    }

    void OnEnable()
    {
        if (_physicsController == null)
            _physicsController = GetComponent<TowerPhysicsController>() ?? gameObject.AddComponent<TowerPhysicsController>();
        if (_visualizer == null)
            _visualizer = GetComponent<TowerCellVisualizer>() ?? gameObject.AddComponent<TowerCellVisualizer>();
        if (_bombIceController == null)
            _bombIceController = GetComponent<BombIceEffectController>() ?? gameObject.AddComponent<BombIceEffectController>();
        if (_sceneBuilder == null)
            _sceneBuilder = GetComponent<TowerSceneBuilder>() ?? gameObject.AddComponent<TowerSceneBuilder>();
        if (placementController == null)
            placementController = GetComponent<PlacementZoneController>();
        if (heldController == null)
            heldController = GetComponent<HeldBlockController>();
        if (selectionController == null)
            selectionController = GetComponent<TetrominoSelectionController>();
        if (_scoreController == null)
            _scoreController = GetComponent<ScoreController>();

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
                number           = Mathf.Max(1, Mathf.RoundToInt(blockCell.Weight)),
                isOriginalTower  = blockCell.IsOriginalTower,
                kind             = blockCell.Kind,
                concealedByBomb  = _bombIceController?.IsConcealedByBomb(cell) ?? false
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
        _lastPlacedCells.Clear();
        _hasLastPlacementCenter = false;
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

    List<Transform> CollectSceneCellTransforms(Transform towerRoot)
    {
        var cells = new List<Transform>();
        var seen = new HashSet<Transform>();
        if (towerRoot != null)
        {
            foreach (Transform child in towerRoot)
            {
                if (child.GetComponent<BlockCell>() != null && seen.Add(child))
                    cells.Add(child);
            }
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

        var occupied = new HashSet<Vector2Int>();
        var sceneCells = CollectSceneCellTransforms(towerRoot);
        foreach (var child in sceneCells)
        {
            if (child == null || child.GetComponent<BlockCell>() == null)
                continue;

            var local = towerRoot.InverseTransformPoint(child.position);
            var cell = new Vector2Int(
                Mathf.RoundToInt(local.x - 0.5f),
                Mathf.RoundToInt(local.y - 0.5f));

            if (occupied.Add(cell))
                continue;

            DestroyDuplicateSceneCell(child);
        }
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
            box.enabled = true;
            box.isTrigger = false;
            box.size = Vector3.one;
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
        _lastPlacedCells.Clear();
        _hasLastPlacementCenter = false;
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

    void Rebuild()
    {
        if (Application.isPlaying)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        RebuildEmptyTowerOnly();
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

        if (_leftBoundary == null)
        {
            var f = Util.FindSceneObjectByName(transform, gameObject.scene, "BoundaryLeft");
            if (f) _leftBoundary = f.gameObject;
        }

        if (_rightBoundary == null)
        {
            var f = Util.FindSceneObjectByName(transform, gameObject.scene, "BoundaryRight");
            if (f) _rightBoundary = f.gameObject;
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

    bool HasBlockCellChildren(Transform root)
    {
        if (root == null)
            return false;

        foreach (Transform child in root)
            if (child != null && child.GetComponent<BlockCell>() != null)
                return true;
        return false;
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

    void SyncPlacementZoneFromObject(bool updateVisuals = true)
    {
        placementController.Configure(this, _towerRoot, _towerStackDivider != null ? _towerStackDivider.transform : null, _visualizer?.CreateBlockSprite());
        placementController.SyncPlacementZoneFromObject(updateVisuals);
    }



    public void HideResultScreens() => OnHudRebind?.Invoke();
    
    void MarkGeneratedObject(GameObject go)
    {
        if (go != null)
            _generatedObjects.Add(go);
#if UNITY_EDITOR
        if (!Application.isPlaying)
            go.hideFlags = HideFlags.DontSaveInEditor;
#endif
    }
    
    public void AwardGoldFishDeadlineScore(Vector3 worldPosition)
    {
        _scoreController?.AwardGoldFishDeadlineScore(worldPosition);
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
    
    public void BeginPresetSelection(TetrominoPreset preset)
    {
        Selection.BeginPresetSelection(this, preset);
    }

    public void HandlePresetSelectionInput(
        bool hasMove,
        Vector2Int dir,
        bool hasConfirm,
        bool hasPreset,
        TetrominoPreset preset,
        bool hasTab,
        bool hasPresetRotate,
        bool hasPresetHalfTurn)
    {
        Selection.HandlePresetSelectionInput(this, hasMove, dir, hasConfirm, hasPreset, preset, hasTab, hasPresetRotate, hasPresetHalfTurn);
    }

    bool TrySetPresetSelection(Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        if (!PresetOverlapsAnyExtractableCell(anchor, preset, rotation))
        {
            PlayPlacementFailFeedback();
            ApplyPresetSelection();
            return false;
        }

        Selection.Anchor = anchor;
        Selection.Preset = preset;
        Selection.Rotation = ((rotation % 4) + 4) % 4;
        ApplyPresetSelection();
        return true;
    }

    void ExitPresetSelectionToFocus()
    {
        var anchor = Selection.Anchor;
        ClearSelectedCellsOnly();
        ClearPresetOutlinePreview();
        Selection.IsPresetSelectionActive = false;
        if (IsExtractableCell(anchor))
            SetFocusCell(anchor);
    }

    bool ApplyPresetSelection()
    {
        if (!Selection.IsPresetSelectionActive) return false;

        ClearSelectedCellsOnly();
        SetFocusCell(Selection.Anchor);
        var presetCells =
            TetrominoShapeUtil.GetCells(Selection.Anchor, Selection.Preset, Selection.Rotation);
        CreatePresetOutlinePreview(presetCells);
        if (CanApplyPresetSelection(Selection.Anchor, Selection.Preset, Selection.Rotation))
        {
            foreach (var cell in presetCells)
                SelectCell(cell);
        }

        return true;
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
            go.transform.localScale = Vector3.one * (_visualizer?.SelectedOutlineScale ?? 1.10f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _visualizer?.CreateOutlineSprite();
            sr.color = _visualizer?.SelectedOutlineColor ?? new Color(1f, 1f, 1f, 0.95f);
            sr.sortingOrder = 5;
        }
    }

    void UpdatePresetOutlineFeedback()
    {
        if (_presetOutlineRoot == null) return;

        bool isFailing = Time.time < Held.FailEndTime;
        _presetOutlineRoot.transform.localPosition = FailShakeOffset(isFailing);

        var color = isFailing ? _failFlashColor : (_visualizer?.SelectedOutlineColor ?? new Color(1f, 1f, 1f, 0.95f));
        foreach (var sr in _presetOutlineRoot.GetComponentsInChildren<SpriteRenderer>())
            sr.color = color;
    }

    bool CanApplyPresetSelection(Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        foreach (var cell in TetrominoShapeUtil.GetCells(anchor, preset, rotation))
            if (!IsExtractableCell(cell))
                return false;
        return true;
    }

    bool PresetOverlapsAnyExtractableCell(Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        foreach (var cell in TetrominoShapeUtil.GetCells(anchor, preset, rotation))
            if (IsExtractableCell(cell))
                return true;
        return false;
    }

    void ConfirmPresetSelection()
    {
        if (Selection.Selected.Count > 0)
        {
            Selection.IsPresetSelectionActive = false;
            LiftBlocks();
        }
        else
            PlayPlacementFailFeedback();
    }


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

        // 프리셋 선택 모드 종료
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

        if (Selection.Selected.Count > 0 &&
            !Selection.IsAdjacentToSelected(cell))
        {
            ClearSelectedCellsOnly();
        }

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
    void DeselectCell(Vector2Int cell)
    {
        Selection.Selected.Remove(cell);
        _visualizer?.ApplyCellVisual(cell);
    }

    public void ClearSelection()
    {
        Selection.IsPresetSelectionActive = false;
        ClearPresetOutlinePreview();
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
                cell = pair.Key,
                number = pair.Value.number,
                isOriginalTower = pair.Value.isOriginalTower,
                kind = pair.Value.kind
            });
        }

        var changedCells = new List<Vector2Int>();
        var bombCells = new List<Vector2Int>();
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

        var pc = _visualizer?.PlacedBlockColor ?? new Color(0.55f, 0.58f, 0.60f, 1f);
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
            var heldView = new CellView
            {
                go = go,
                sr = sr,
                outline = null,
                label = null,
                previewBlurRenderers = blurRenderers
            };
            Held.Data.Add((heldState, heldView));
        }

        _physicsController?.CheckForDetachment();
        _physicsController?.UpdateTowerPhysicsState();

        AudioManager.PlaySound(_AudioLibrarySounds.Hold);
        Held.IsHolding = true;
        Held.BaseCell = GetDefaultHeldBaseCell();
        Held.UsingKeyboardPlacement = true;
        OnBlocksLifted?.Invoke();
        _scoreController?.
            AddScore(Held.MatchesBonus ? 2 : 1, extractionScorePos);
        _scoreController?.RollBonusTarget();
    }
    
    public void UpdateHeldPosition()
    {
        bool canPlace = CanPlaceHeldBlocks(out _, out var snappedWorldPos);
        bool isFailing = Time.time < Held.FailEndTime;
        var previewColor = isFailing
            ? _failFlashColor
            : canPlace
                ? _validHoldColor
                : _invalidHoldColor;

        Held.Root.transform.position = snappedWorldPos + FailShakeOffset(isFailing);
        SetHeldPreviewColor(previewColor, canPlace && !isFailing);
    }

    public void UpdateHeldBaseFromMousePosition()
    {
        if (_towerRoot == null) return;
        var local = _towerRoot.InverseTransformPoint(Util.MouseWorldPos());
        Held.BaseCell = ClampHeldBase(new Vector2Int(
            Mathf.RoundToInt(local.x - Held.Center.x),
            Mathf.RoundToInt(local.y - Held.Center.y)));
    }

    public void TryPlaceHeldBlocks() => TryPlaceBlocks();

    public void SetMousePlacementMode() => Held.UsingKeyboardPlacement = false;

    Vector3 FailShakeOffset(bool isFailing)
    {
        return Held.FailShakeOffset(isFailing);
    }

    void PlayPlacementFailFeedback()
    {
        Held.PlayPlacementFailFeedback();
    }

    Vector2Int GetDefaultHeldBaseCell()
    {
        if (_hasLastPlacementCenter)
        {
            var lastBase = new Vector2Int(
                Mathf.RoundToInt(_lastPlacementCenter.x - Held.Center.x),
                Mathf.RoundToInt(_lastPlacementCenter.y - Held.Center.y));
            return ClampHeldBase(lastBase);
        }

        float placementCenterX = (placementController.placementMin.x + placementController.placementMax.x + 1f) * 0.5f;
        int baseX = Mathf.RoundToInt(placementCenterX - Held.Center.x);
        var baseCell = new Vector2Int(baseX, _grid.HighestOccupiedRow() + 1);
        return ClampHeldBase(baseCell);
    }

    public void MoveHeldBase(Vector2Int dir)
    {
        Held.BaseCell += dir;
        Held.BaseCell = ClampHeldBase(Held.BaseCell);
        if (dir.y != 0)
            OnBlocksLifted?.Invoke();
    }

    public void RotateHeldBlocks(bool clockwise)
    {
        if (Held.RelPos.Count == 0) return;

        if (TetrominoShapeUtil.TryGetMatchingPreset(Held.RelPos, out var preset, out var presetRotation))
        {
            if (preset == TetrominoPreset.O)
                return;

            var pivot = TetrominoShapeUtil.GetRotationPivot(preset, presetRotation);
            var pivotWorld = Held.BaseCell + pivot;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            var rotated = new List<Vector2Int>(Held.RelPos.Count);

            foreach (var rel in Held.RelPos)
            {
                var next = clockwise
                    ? new Vector2(pivot.x + rel.y - pivot.y, pivot.y - rel.x + pivot.x)
                    : new Vector2(pivot.x - rel.y + pivot.y, pivot.y + rel.x - pivot.x);
                var nextCell = TetrominoShapeUtil.RoundToCell(next);

                rotated.Add(nextCell);
                minX = Mathf.Min(minX, nextCell.x);
                minY = Mathf.Min(minY, nextCell.y);
            }

            var normalizeOffset = new Vector2Int(minX, minY);
            for (int i = 0; i < rotated.Count; i++)
                Held.RelPos[i] = rotated[i] - normalizeOffset;

            if (Held.UsingKeyboardPlacement)
            {
                var normalizedPivot = pivot - normalizeOffset;
                Held.BaseCell = ClampHeldBase(TetrominoShapeUtil.RoundToCell(pivotWorld - normalizedPivot));
            }

            RecalculateHeldCenter();
            UpdateHeldChildLocalPositions();
            return;
        }

        int maxX = 0, maxY = 0;
        foreach (var rel in Held.RelPos)
        {
            maxX = Mathf.Max(maxX, rel.x);
            maxY = Mathf.Max(maxY, rel.y);
        }

        for (int i = 0; i < Held.RelPos.Count; i++)
        {
            var rel = Held.RelPos[i];
            Held.RelPos[i] = clockwise
                ? new Vector2Int(maxY - rel.y, rel.x)
                : new Vector2Int(rel.y, maxX - rel.x);
        }

        RecalculateHeldCenter();
        UpdateHeldChildLocalPositions();

        if (Held.UsingKeyboardPlacement)
            Held.BaseCell = ClampHeldBase(Held.BaseCell);
    }

    void RecalculateHeldCenter()
    {
        Held.RecalculateCenter();
    }

    void UpdateHeldChildLocalPositions()
    {
        Held.UpdateChildLocalPositions();
    }

    Vector2Int ClampHeldBase(Vector2Int baseCell)
    {
        return Held.ClampBase(baseCell, placementMin, placementMax, placementController.PlacementFloorY, placementController.PlacementCeilingY);
    }

    void SetHeldPreviewColor(Color color, bool showBlur)
    {
        Held.SetPreviewColor(color, _visualizer?.PreviewBlurAlpha ?? 0.10f);
    }

    bool CanPlaceHeldBlocks(out List<Vector2Int> targets, out Vector3 snappedWorldPos)
    {
        _physicsController?.RefreshDetachedComponents();
        return CanPlaceHeldBlocks(Held.BaseCell, out targets, out snappedWorldPos);
    }

    bool CanPlaceHeldBlocks(Vector2Int baseCell, out List<Vector2Int> targets, out Vector3 snappedWorldPos)
    {
        SyncPlacementZoneFromObject(updateVisuals: false);
        var detachedCells = _physicsController?.CollectStableDetachedCells() ?? new HashSet<Vector2Int>();
        targets = new List<Vector2Int>(Held.RelPos.Count);
        var snappedLocalPos = new Vector3(baseCell.x + Held.Center.x, baseCell.y + Held.Center.y, 0f);
        snappedWorldPos = _towerRoot.TransformPoint(snappedLocalPos);

        foreach (var rel in Held.RelPos)
        {
            var target = new Vector2Int(baseCell.x + rel.x, baseCell.y + rel.y);
            if (target.x < placementMin.x || target.x > placementMax.x ||
                target.y < placementController.PlacementFloorY || target.y > placementController.PlacementCeilingY) return false;
            if (IsPlacementExcluded(target)) return false;
            if (detachedCells.Contains(target)) return false;
            if (_grid.TryGetCell(target, out var existing))
            {
                if (existing.kind == CellKind.Ice) return false;
                if (existing.number >= 6) return false;
            }

            targets.Add(target);
        }

        bool adjacent = false;
        foreach (var t in targets)
        {
            if (_grid.IsMergeableCell(t))
            {
                adjacent = true;
                break;
            }

            foreach (var n in Util.Neighbors(t))
            {
                if (_grid.IsMergeableCell(n) || detachedCells.Contains(n))
                {
                    adjacent = true;
                    break;
                }
            }

            if (adjacent) break;
        }

        if (!adjacent && (_grid.Count > 0 || detachedCells.Count > 0)) return false;
        
        bool hasBottomSupport = false;
        foreach (var t in targets)
        {
            if (_grid.IsMergeableCell(t) ||
                t.y == placementController.PlacementFloorY ||
                _grid.IsMergeableCell(new Vector2Int(t.x, t.y - 1)) ||
                detachedCells.Contains(new Vector2Int(t.x, t.y - 1)))
            {
                hasBottomSupport = true;
                break;
            }
        }

        return hasBottomSupport;
    }

    bool IsOccupiedCell(Vector2Int cell, HashSet<Vector2Int> detachedCells)
    {
        return _grid.HasCell(cell) || detachedCells.Contains(cell);
    }

    bool IsPlacementExcluded(Vector2Int cell)
    {
        return placementController.IsPlacementExcluded(cell);
    }

    int LowestHeldRelativeY()
    {
        return Held.LowestRelativeY();
    }

    public void DropHeldToNearestSurfaceAndPlace()
    {
        if (!TryFindNearestDropBase(Held.BaseCell, out var dropBase))
        {
            PlayPlacementFailFeedback();
            return;
        }

        Held.BaseCell = dropBase;
        TryPlaceBlocks();
    }

    bool TryFindNearestDropBase(Vector2Int startBase, out Vector2Int dropBase)
    {
        startBase = ClampHeldBase(startBase);
        int minBaseY = placementController.PlacementFloorY - LowestHeldRelativeY();
        for (int y = startBase.y; y >= minBaseY; y--)
        {
            var candidate = new Vector2Int(startBase.x, y);
            if (CanPlaceHeldBlocks(candidate, out _, out _))
            {
                dropBase = candidate;
                return true;
            }
        }

        dropBase = startBase;
        return false;
    }

    void TryPlaceBlocks()
    {
        if (!CanPlaceHeldBlocks(out var targets, out _))
        {
            PlayPlacementFailFeedback();
            return;
        }

        _lastPlacedCells.Clear();
        RememberLastPlacementCenter(targets);
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var (heldState, heldView) = Held.Data[i];

            _visualizer?.ClearPreviewBlur(heldView);
            if (_grid.TryGetCell(target, out var existing))
            {
                existing.number = Mathf.Min(6, existing.number + 1);
                if (_cellViews.TryGetValue(target, out var existingView))
                    _visualizer?.UpdateCellDataVisuals(existing, existingView);
                _visualizer?.ApplyCellVisual(target);
                _lastPlacedCells.Add(target);
                if (heldView.go != null)
                    Destroy(heldView.go);
                continue;
            }

            heldView.go.transform.SetParent(_towerRoot, false);
            heldView.go.transform.localPosition = new Vector3(target.x + 0.5f, target.y + 0.5f, 0f);
            heldView.go.transform.localScale = Vector3.one * blockBodyScale;

            var box = heldView.go.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.size = Vector3.one * (_physicsController?.LocalColliderSize() ?? 0.92f);
                box.enabled = true;
            }

            heldView.sr.sortingOrder = 0;
            if (heldView.label == null)
                heldView.label = _visualizer?.SpawnLabel(heldState.number, heldView.go.transform);

            var bc = heldView.go.GetComponent<BlockCell>();
            if (bc != null)
            {
                bc.Weight = heldState.number;
                bc.IsOriginalTower = heldState.isOriginalTower;
            }

            _visualizer?.UpdateCellDataVisuals(heldState, heldView);
            _grid.AddCell(target, heldState);
            _cellViews[target] = heldView;
            _lastPlacedCells.Add(target);
        }

        Destroy(Held.Root);
        Held.Root = null;
        Held.RelPos.Clear();
        Held.Data.Clear();
        Held.SourceCells.Clear();
        Held.StartScore = 0;
        Held.MatchesBonus = false;
        Held.IsHolding = false;
        Held.UsingKeyboardPlacement = false;
        Selection.ClearFocus();

        AudioManager.PlaySound(_AudioLibrarySounds.Drop);
        _physicsController?.UpdateTowerPhysicsState();
        UpdateExtractionTowerRowsFromCells();
        OnBlocksPlaced?.Invoke();
        FocusDefaultExtractionCell();
    }

    void RememberLastPlacementCenter(List<Vector2Int> targets)
    {
        if (targets == null || targets.Count == 0)
        {
            _hasLastPlacementCenter = false;
            return;
        }

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var target in targets)
        {
            minX = Mathf.Min(minX, target.x);
            minY = Mathf.Min(minY, target.y);
            maxX = Mathf.Max(maxX, target.x);
            maxY = Mathf.Max(maxY, target.y);
        }

        _lastPlacementCenter = new Vector2((minX + maxX + 1f) * 0.5f, (minY + maxY + 1f) * 0.5f);
        _hasLastPlacementCenter = true;
    }


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
            if (ignoreLastPlaced && _lastPlacedCells.Contains(cell)) continue;
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

    void RestoreHeldSourceCells()
    {
        foreach (var pair in _cellViews)
        {
            if (pair.Value.go != null)
                DestroyLocal(pair.Value.go);
        }

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
        _lastPlacedCells.Clear();
        Selection.ClearFocus();
        UpdateExtractionTowerRowsFromCells();
    }

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

    public bool ApplyIceContactDamage(BlockCell blockCell)
        => _bombIceController?.ApplyIceContactDamage(blockCell) ?? false;

    public bool ApplyIceColumnContactDamage(Vector3 iceWorldPosition)
        => _bombIceController?.ApplyIceColumnContactDamage(iceWorldPosition) ?? false;

    public bool ApplyIceDamage(BlockCell iceBlockCell, Vector3 iceWorldPosition)
        => _bombIceController?.ApplyIceDamage(iceBlockCell, iceWorldPosition) ?? false;
    
    static IEnumerable<Vector2Int> Neighbors(Vector2Int c)
    {
        yield return new Vector2Int(c.x + 1, c.y);
        yield return new Vector2Int(c.x - 1, c.y);
        yield return new Vector2Int(c.x, c.y + 1);
        yield return new Vector2Int(c.x, c.y - 1);
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
        var label = _visualizer?.SpawnLabel(number, go.transform);

        var state = new CellState
        {
            number = number,
            isOriginalTower = isOriginalTower,
            kind = CellKind.Normal
        };
        var view = new CellView
        {
            go = go,
            sr = sr,
            outline = outline,
            label = label
        };

        _visualizer?.UpdateCellDataVisuals(state, view);
        return (state, view);
    }

    void CreateFloor() => _sceneBuilder?.CreateFloor();
    void CreateBoundaries() => _sceneBuilder?.CreateBoundaries();

    void UpdateTowerStackDivider() => _sceneBuilder?.UpdateTowerStackDivider();
    
    bool TryGetTowerLocalBounds(Transform source, out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = minY = float.PositiveInfinity;
        maxX = maxY = float.NegativeInfinity;
        if (source == null || _towerRoot == null)
            return false;

        Vector3[] corners;
        var spriteRenderer = source.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            var bounds = spriteRenderer.sprite.bounds;
            var min = bounds.min;
            var max = bounds.max;
            corners = new[]
            {
                source.TransformPoint(new Vector3(min.x, min.y, 0f)),
                source.TransformPoint(new Vector3(min.x, max.y, 0f)),
                source.TransformPoint(new Vector3(max.x, min.y, 0f)),
                source.TransformPoint(new Vector3(max.x, max.y, 0f))
            };
        }
        else if (source.TryGetComponent<Renderer>(out var renderer))
        {
            var bounds = renderer.bounds;
            var min = bounds.min;
            var max = bounds.max;
            corners = new[]
            {
                new Vector3(min.x, min.y, source.position.z),
                new Vector3(min.x, max.y, source.position.z),
                new Vector3(max.x, min.y, source.position.z),
                new Vector3(max.x, max.y, source.position.z)
            };
        }
        else
        {
            var halfRight = source.right * (source.lossyScale.x * 0.5f);
            var halfUp = source.up * (source.lossyScale.y * 0.5f);
            corners = new[]
            {
                source.position - halfRight - halfUp,
                source.position - halfRight + halfUp,
                source.position + halfRight - halfUp,
                source.position + halfRight + halfUp
            };
        }

        foreach (var corner in corners)
        {
            var local = _towerRoot.InverseTransformPoint(corner);
            minX = Mathf.Min(minX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxX = Mathf.Max(maxX, local.x);
            maxY = Mathf.Max(maxY, local.y);
        }

        return maxX > minX && maxY > minY;
    }

    void CreateGameOverScreen() => _sceneBuilder?.CreateGameOverScreen();

    void CreateScoreLabel() => _sceneBuilder?.CreateScoreLabel();
    
    Vector3 MouseWorldPos() => Util.MouseWorldPos();
    
    void SetFocusCell(Vector2Int cell)
    {
        Selection.SetFocus(this, cell);
    }

    public void ClearKeyboardFocus() => Selection.ClearFocus();

    public void EnsureFocusedCell()
    {
        Selection.EnsureFocusedCell(this);
    }

    public void MoveFocus(Vector2Int dir)
    {
        Selection.MoveFocus(this, dir);
    }

    public void ToggleFocusedSelection()
    {
        Selection.ToggleFocusedSelection(this);
    }

    void ClearSelectedCellsOnly()
    {
        foreach (var c in new List<Vector2Int>(Selection.Selected))
            DeselectCell(c);
    }

    public void ClearPresetOutlinePreview()
    {
        if (_presetOutlineRoot != null) { DestroyLocal(_presetOutlineRoot); _presetOutlineRoot = null; }
    }

    public bool SelectionHasCell(Vector2Int cell) => _grid.HasCell(cell);
    public bool SelectionIsExtractableCell(Vector2Int cell) => IsExtractableCell(cell);
    public void SelectionApplyCellVisual(Vector2Int cell) => _visualizer?.ApplyCellVisual(cell);
    public void SelectionFocusDefaultExtractionCell() => FocusDefaultExtractionCell();
    public void SelectionSelectCell(Vector2Int cell) => SelectCell(cell);
    public void SelectionTryDeselect(Vector2Int cell) => TryDeselect(cell);
    public void SelectionClearSelectedCellsOnly() => ClearSelectedCellsOnly();
    public void SelectionLiftBlocks() => LiftBlocks();
    public void SelectionCreatePresetOutlinePreview(List<Vector2Int> cells) => CreatePresetOutlinePreview(cells);
    public void SelectionPlayPlacementFailFeedback() => PlayPlacementFailFeedback();
    
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

    bool TryFindDefaultFocusCell(bool ignoreLastPlaced, out Vector2Int cell)
    {
        cell = default;
        foreach (var pair in _grid.AllCells)
        {
            var c = pair.Key;
            if (ignoreLastPlaced && _lastPlacedCells.Contains(c)) continue;
            if (!_grid.IsExtractableCell(c)) continue;
            if (!IsInExtractionTowerRows(c)) continue;
            cell = c;
            return true;
        }
        return false;
    }
    
}