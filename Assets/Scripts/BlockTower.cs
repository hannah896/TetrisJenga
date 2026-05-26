using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using TMPro;

[ExecuteAlways]
public class BlockTower : MonoBehaviour
{
    public enum ControlPreset
    {
        Wasd,
        ArrowKeys
    }

    enum TetrominoPreset
    {
        I,
        J,
        L,
        O,
        S,
        T,
        Z
    }

    [Header("Grid")]
    public int columns = 4;
    public int rows    = 10;

    [Header("Placement Zone")]
    public Vector2Int placementMin = new(-1, 0);
    public Vector2Int placementMax = new(4, 14);

    [Header("Placement Preview")]
    [SerializeField] bool  previewBlurEnabled = true;
    [SerializeField, Range(0f, 0.5f)] float previewBlurRadius = 0.12f;
    [SerializeField, Range(0f, 1f)]   float previewBlurAlpha  = 0.10f;
    [SerializeField, Range(1, 8)]     int   previewBlurCopies = 8;

    [Header("Placement Feedback")]
    [SerializeField, Range(0f, 0.5f)] float placementFailDuration = 0.18f;
    [SerializeField, Range(0f, 0.5f)] float placementFailShakeDistance = 0.12f;
    [SerializeField, Range(1, 8)]     int   placementFailShakeCount = 3;
    [SerializeField] Color placedBlockColor = new(0.55f, 0.58f, 0.60f, 1f);

    [Header("Keyboard Controls")]
    [SerializeField] bool keyboardControlsEnabled = true;
    [SerializeField] ControlPreset controlPreset = ControlPreset.Wasd;
    [SerializeField] Color focusedCellColor = new(1f, 0.92f, 0.25f, 1f);
    [SerializeField, Range(0.05f, 0.5f)] float moveHoldInitialDelay = 0.22f;
    [SerializeField, Range(0.02f, 0.25f)] float moveHoldRepeatInterval = 0.08f;

    [Header("Focus Feedback")]
    [SerializeField] Color focusedOutlineColor = new(1f, 0.95f, 0.05f, 1f);
    [SerializeField] Color selectedOutlineColor = new(1f, 1f, 1f, 0.95f);
    [SerializeField, Range(1f, 1.4f)] float focusedOutlineScale = 1.18f;
    [SerializeField, Range(1f, 1.3f)] float selectedOutlineScale = 1.10f;

    [Header("Physics")]
    public float blockFriction = 1f;
    [SerializeField] float toppleTorque = 4f;
    [SerializeField] float toppleMargin = 0.05f;

    [Header("Detached Blocks")]
    [SerializeField] float detachedReattachStableTime = 0.15f;
    [SerializeField] float detachedReattachVelocity = 0.55f;
    [SerializeField] float detachedMinAirTime = 0.35f;
    [SerializeField] float detachedPenaltyDelay = 2f;
    [SerializeField, Range(0.85f, 1f)] float blockBodyScale = 0.94f;
    [SerializeField, Range(0.85f, 1f)] float blockColliderScale = 0.92f;

    [Header("Scene References (optional)")]
    [SerializeField] Transform   towerRootTransform;
    [SerializeField] Transform   floorTransform;
    [SerializeField] TextMeshPro scoreLabel;
    [SerializeField] UIDocument  hudDocument;
    [SerializeField] VisualTreeAsset hudVisualTree;
    [SerializeField] PanelSettings hudPanelSettings;

    Transform      _towerRoot;
    Rigidbody      _rb;
    Sprite         _blockSprite;
    GameObject     _generatedFloor;
    GameObject     _generatedScoreLabel;
    GameObject     _leftBoundary;
    GameObject     _rightBoundary;
    GameOverScreen _gameOverScreen;
    GameObject     _bonusPreviewRoot;
    GameObject     _presetOutlineRoot;
    Label          _builderScoreTitle;
    Label          _builderScoreValue;
    Label          _builderScorePopupText;
    Label          _builderBonusPreviewTitle;
    Label          _builderBonusKeyLabel;
    Label          _builderBonusNextKeyLabel;
    Label          _builderBonusThirdKeyLabel;
    VisualElement  _builderBonusPreview;
    VisualElement  _builderBonusBackground;
    VisualElement  _builderBonusCells;
    VisualElement  _builderBonusNextCells;
    VisualElement  _builderBonusThirdCells;
    readonly List<VisualElement> _builderBonusCellElements = new();
    readonly List<VisualElement> _builderBonusNextCellElements = new();
    readonly List<VisualElement> _builderBonusThirdCellElements = new();
    bool           _builderBonusPreviewNeedsRefresh;
    Coroutine      _builderScorePopupRoutine;
    PanelSettings  _runtimeHudPanelSettings;
    Font           _runtimeHudFont;
    int            _score;
    bool           _isGameOver;
    Sprite         _outlineSprite;
    TetrominoPreset _bonusTargetPreset;
    TetrominoPreset _nextBonusTargetPreset;
    TetrominoPreset _thirdBonusTargetPreset;
    bool           _bonusQueueInitialized;

    // ── 셀 데이터 ─────────────────────────────────────────────────────────
    class CellData
    {
        public int            number;
        public bool           isOriginalTower;
        public GameObject     go;
        public SpriteRenderer sr;
        public SpriteRenderer outline;
        public TextMeshPro    label;
        public List<SpriteRenderer> previewBlurRenderers;
    }

    readonly Dictionary<Vector2Int, CellData> _cells    = new();
    readonly List<Vector2Int>                  _selected = new();
    readonly HashSet<Vector2Int>               _lastPlacedCells = new();
    readonly List<DetachedComponent>           _detachedComponents = new();

    class DetachedComponent
    {
        public GameObject root;
        public Rigidbody rb;
        public float detachedAt;
        public int scorePenalty;
        public bool resolved;
    }

    class HeldSourceCell
    {
        public Vector2Int cell;
        public int number;
        public bool isOriginalTower;
    }

    // ── 들기 상태 ─────────────────────────────────────────────────────────
    bool             _isHolding;
    GameObject       _heldRoot;
    List<Vector2Int> _heldRelPos = new();
    List<CellData>   _heldData   = new();
    readonly List<HeldSourceCell> _heldSourceCells = new();
    Vector2          _heldCenter;
    readonly Color   _validHoldColor   = new(0.55f, 0.85f, 0.6f, 0.5f);
    readonly Color   _invalidHoldColor = new(1f, 0.25f, 0.25f, 0.6f);
    readonly Color   _failFlashColor   = new(1f, 0.08f, 0.08f, 0.85f);
    float            _placementFailStartTime = -1f;
    float            _placementFailEndTime   = -1f;
    Vector2Int       _heldBaseCell;
    bool             _usingKeyboardPlacement;
    int              _heldStartScore;
    bool             _heldMatchesBonus;

    bool       _hasFocusedCell;
    Vector2Int _focusedCell;
    bool             _presetSelectionActive;
    TetrominoPreset  _presetSelectionPreset;
    Vector2Int       _presetSelectionAnchor;
    int              _presetSelectionRotation;
    Vector2Int       _moveRepeatDir;
    float            _nextMoveRepeatTime;

    // ─────────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        BindBuilderHud();
        if (Application.isPlaying)
        {
            if (_cells.Count > 0) return;
            if (TryRestoreRuntimeStateFromScene()) return;
        }
        Rebuild();
    }

    bool BindBuilderHud()
    {
        if (hudDocument == null)
            hudDocument = GetComponent<UIDocument>();
        if (hudDocument == null)
            hudDocument = FindObjectOfType<UIDocument>();
        if (hudDocument == null)
            return false;

        EnsureHudPanelSettings();
        EnsureHudVisualTree();
        if (hudDocument.rootVisualElement == null)
            return false;

        var root = hudDocument.rootVisualElement;
        _builderScoreTitle = root.Q<Label>("ScoreTitle");
        _builderScoreValue = root.Q<Label>("ScoreValue");
        _builderScorePopupText = root.Q<Label>("ScorePopupText");
        _builderBonusPreviewTitle = root.Q<Label>("BonusPreviewTitle");
        _builderBonusKeyLabel = root.Q<Label>("BonusPreview1Key");
        _builderBonusNextKeyLabel = root.Q<Label>("BonusPreview2Key");
        _builderBonusThirdKeyLabel = root.Q<Label>("BonusPreview3Key");
        _builderBonusPreview = root.Q<VisualElement>("BonusPreview");
        _builderBonusBackground = root.Q<VisualElement>("BonusPreviewBackground");
        _builderBonusCells = root.Q<VisualElement>("BonusPreview1Cells") ?? root.Q<VisualElement>("BonusPreviewCells");
        _builderBonusNextCells = root.Q<VisualElement>("BonusPreview2Cells");
        _builderBonusThirdCells = root.Q<VisualElement>("BonusPreview3Cells");
        _builderBonusCellElements.Clear();
        _builderBonusCells?.Query<VisualElement>(className: "bonus-preview-cell")
            .ForEach(cell => _builderBonusCellElements.Add(cell));
        _builderBonusNextCellElements.Clear();
        _builderBonusNextCells?.Query<VisualElement>(className: "bonus-preview-cell")
            .ForEach(cell => _builderBonusNextCellElements.Add(cell));
        _builderBonusThirdCellElements.Clear();
        _builderBonusThirdCells?.Query<VisualElement>(className: "bonus-preview-cell")
            .ForEach(cell => _builderBonusThirdCellElements.Add(cell));

        ApplyHudTextStyles();
        _builderBonusPreviewNeedsRefresh = _builderBonusCells != null;
        if (_builderBonusCells != null)
            root.schedule.Execute(UpdateBuilderBonusPreview).StartingIn(0);
        return _builderScoreValue != null || _builderBonusCells != null;
    }

    void ApplyHudTextStyles()
    {
        StyleHudLabel(_builderScoreTitle, "SCORE", 48f, 336f, 66f, Color.white);
        StyleHudLabel(_builderScoreValue, _score.ToString(), 96f, 336f, 120f, Color.white);
        StyleHudLabel(_builderBonusPreviewTitle, "NEXT", 33f, 300f, 60f, new Color(0.21f, 0.84f, 0.79f, 1f));
        StyleHudLabel(_builderScorePopupText, _builderScorePopupText?.text ?? string.Empty, 30f, 336f, 60f, Color.white);
        SetHudLabelTextOnly(_builderBonusKeyLabel, PresetKeyText(_bonusTargetPreset));
        SetHudLabelTextOnly(_builderBonusNextKeyLabel, PresetKeyText(_nextBonusTargetPreset));
        SetHudLabelTextOnly(_builderBonusThirdKeyLabel, PresetKeyText(_thirdBonusTargetPreset));
    }

    void StyleHudLabel(Label label, string text, float fontSize, float width, float height, Color color)
    {
        if (label == null) return;

        label.text = text;
        label.style.display = DisplayStyle.Flex;
        label.style.visibility = Visibility.Visible;
        label.style.width = width;
        label.style.height = height;
        label.style.fontSize = fontSize;
        label.style.color = color;
        var font = GetRuntimeHudFont();
        if (font != null)
            label.style.unityFont = font;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.flexShrink = 0f;
        label.style.opacity = 1f;
    }

    void SetHudLabelTextOnly(Label label, string text)
    {
        if (label == null) return;

        label.text = text;
        label.style.display = DisplayStyle.Flex;
        label.style.visibility = Visibility.Visible;
        label.style.color = Color.white;
        var font = GetRuntimeHudFont();
        if (font != null)
            label.style.unityFont = font;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.flexShrink = 0f;
        label.style.opacity = 1f;
    }

    Font GetRuntimeHudFont()
    {
        if (_runtimeHudFont != null)
            return _runtimeHudFont;

        _runtimeHudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_runtimeHudFont == null)
            _runtimeHudFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _runtimeHudFont;
    }

    void EnsureHudPanelSettings()
    {
        if (hudDocument == null || hudDocument.panelSettings != null)
            return;

        if (hudPanelSettings != null)
        {
            hudDocument.panelSettings = hudPanelSettings;
            return;
        }

        if (_runtimeHudPanelSettings == null)
        {
            _runtimeHudPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _runtimeHudPanelSettings.name = "RuntimeHudPanelSettings";
            _runtimeHudPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _runtimeHudPanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _runtimeHudPanelSettings.sortingOrder = 10;
        }
        hudDocument.panelSettings = _runtimeHudPanelSettings;
    }

    void EnsureHudVisualTree()
    {
        if (hudDocument == null || hudVisualTree == null)
            return;

        if (hudDocument.visualTreeAsset != hudVisualTree)
            hudDocument.visualTreeAsset = hudVisualTree;
    }

#if UNITY_EDITOR
    void OnValidate() =>
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this && gameObject.scene.IsValid() && !Application.isPlaying)
                Rebuild();
        };
#endif

    bool TryRestoreRuntimeStateFromScene()
    {
        var root = towerRootTransform != null ? towerRootTransform : transform.Find("TowerRoot");
        if (root == null) return false;

        var restored = new Dictionary<Vector2Int, CellData>();
        foreach (Transform child in root)
        {
            var blockCell = child.GetComponent<BlockCell>();
            var sr = child.GetComponent<SpriteRenderer>();
            if (blockCell == null || sr == null) continue;

            var local = child.localPosition;
            var cell = new Vector2Int(
                Mathf.RoundToInt(local.x - 0.5f),
                Mathf.RoundToInt(local.y - 0.5f));
            if (restored.ContainsKey(cell)) continue;

            var outline = child.Find("FocusOutline")?.GetComponent<SpriteRenderer>();
            if (outline == null)
                outline = SpawnCellOutline(child);
            outline.enabled = false;

            var label = child.GetComponentInChildren<TextMeshPro>();
            restored[cell] = new CellData
            {
                number = Mathf.Max(1, Mathf.RoundToInt(blockCell.Weight)),
                isOriginalTower = blockCell.IsOriginalTower,
                go = child.gameObject,
                sr = sr,
                outline = outline,
                label = label
            };
        }

        if (restored.Count == 0) return false;

        _towerRoot = root;
        _rb = root.GetComponent<Rigidbody>();
        _cells.Clear();
        foreach (var pair in restored)
            _cells[pair.Key] = pair.Value;

        _selected.Clear();
        _lastPlacedCells.Clear();
        _detachedComponents.Clear();
        _heldRelPos.Clear();
        _heldData.Clear();
        _heldSourceCells.Clear();
        _isHolding = false;
        _isGameOver = false;
        _hasFocusedCell = false;
        _presetSelectionActive = false;
        _usingKeyboardPlacement = false;
        ResetMoveRepeat();
        _hasCameraTarget = false;
        _score = 0;
        _heldStartScore = 0;
        _heldMatchesBonus = false;
        _bonusQueueInitialized = false;

        if (_generatedFloor == null) { var f = transform.Find("Floor"); if (f) _generatedFloor = f.gameObject; }
        if (_generatedScoreLabel == null) { var f = transform.Find("ScoreLabel"); if (f) _generatedScoreLabel = f.gameObject; }
        if (scoreLabel == null && _generatedScoreLabel != null)
            scoreLabel = _generatedScoreLabel.GetComponent<TextMeshPro>();
        if (_bonusPreviewRoot == null) { var f = transform.Find("BonusPreview"); if (f) _bonusPreviewRoot = f.gameObject; }

        UpdateExtractionTowerRowsFromCells();
        UpdateScoreDisplay();
        RollBonusTarget();
        UpdateTowerPhysicsState();
        return true;
    }

    void Rebuild()
    {
        if (_heldRoot != null) { DestroyLocal(_heldRoot); _heldRoot = null; }
        _heldRelPos.Clear();
        _heldData.Clear();
        _heldSourceCells.Clear();
        ClearGenerated();
        _cells.Clear();
        _selected.Clear();
        _lastPlacedCells.Clear();
        _detachedComponents.Clear();
        _isHolding  = false;
        _isGameOver = false;
        _hasFocusedCell = false;
        _presetSelectionActive = false;
        _presetSelectionAnchor = Vector2Int.zero;
        _presetSelectionRotation = 0;
        _usingKeyboardPlacement = false;
        ResetMoveRepeat();
        _hasCameraTarget = false;
        _cameraTargetSize = 0f;
        _extractionMinCol = 0;
        _extractionMaxCol = columns - 1;
        _extractionMinRow = 0;
        _extractionMaxRow = rows - 1;
        _blockSprite = null;
        _outlineSprite = null;
        _score = 0;
        _heldStartScore = 0;
        _heldMatchesBonus = false;
        _bonusQueueInitialized = false;
        BuildTower();
    }

    void ClearGenerated()
    {
        ClearDetachedBlocks();

        if (_towerRoot == null && towerRootTransform == null)
        {
            var found = transform.Find("TowerRoot");
            if (found != null) _towerRoot = found;
        }
        if (_towerRoot != null)
        {
            if (towerRootTransform == null)
                DestroyLocal(_towerRoot.gameObject);
            else
            {
                var children = new List<Transform>();
                foreach (Transform t in _towerRoot) children.Add(t);
                foreach (var t in children) DestroyLocal(t.gameObject);
            }
            _towerRoot = null;
            _rb = null;
        }

        if (_generatedFloor == null) { var f = transform.Find("Floor"); if (f) _generatedFloor = f.gameObject; }
        if (_generatedFloor != null) { DestroyLocal(_generatedFloor); _generatedFloor = null; }

        if (_generatedScoreLabel == null) { var f = transform.Find("ScoreLabel"); if (f) _generatedScoreLabel = f.gameObject; }
        if (_generatedScoreLabel != null && scoreLabel == null) { DestroyLocal(_generatedScoreLabel); _generatedScoreLabel = null; scoreLabel = null; }

        if (_leftBoundary  == null) { var f = transform.Find("BoundaryLeft");  if (f) _leftBoundary  = f.gameObject; }
        if (_rightBoundary == null) { var f = transform.Find("BoundaryRight"); if (f) _rightBoundary = f.gameObject; }
        if (_leftBoundary  != null) { DestroyLocal(_leftBoundary);  _leftBoundary  = null; }
        if (_rightBoundary != null) { DestroyLocal(_rightBoundary); _rightBoundary = null; }

        if (_gameOverScreen != null) { DestroyLocal(_gameOverScreen.gameObject); _gameOverScreen = null; }
        if (_presetOutlineRoot == null) { var f = transform.Find("PresetOutlinePreview"); if (f) _presetOutlineRoot = f.gameObject; }
        if (_presetOutlineRoot != null) { DestroyLocal(_presetOutlineRoot); _presetOutlineRoot = null; }
        if (_bonusPreviewRoot == null) { var f = transform.Find("BonusPreview"); if (f) _bonusPreviewRoot = f.gameObject; }
        if (_bonusPreviewRoot != null) { DestroyLocal(_bonusPreviewRoot); _bonusPreviewRoot = null; }
        var controlsHelp = transform.Find("ControlsHelp");
        if (controlsHelp != null) DestroyLocal(controlsHelp.gameObject);
    }

    void ClearDetachedBlocks()
    {
        var roots = new HashSet<GameObject>();
        foreach (var detached in _detachedComponents)
        {
            detached.resolved = true;
            if (detached.root != null)
                roots.Add(detached.root);
        }
        _detachedComponents.Clear();

        var staleDetachedRoots = new List<Transform>();
        foreach (Transform child in transform)
            if (child.name == "DetachedBlocks")
                staleDetachedRoots.Add(child);

        foreach (var child in staleDetachedRoots)
            roots.Add(child.gameObject);

        foreach (var root in roots)
        {
            if (root != null)
                DestroyLocal(root);
        }
    }

    void DestroyLocal(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    void MarkGeneratedObject(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            go.hideFlags = HideFlags.DontSaveInEditor;
#endif
    }

    // ── 스코어 ───────────────────────────────────────────────────────────

    void AddScore(int delta)
    {
        _score += delta;
        UpdateScoreDisplay();
    }

    void AddScore(int delta, Vector3 worldPosition)
    {
        AddScore(delta);
        SpawnFloatingScoreText(delta);
    }

    void UpdateScoreDisplay()
    {
        BindBuilderHud();
        if (_builderScoreValue != null)
            _builderScoreValue.text = _score.ToString();
        if (scoreLabel != null)
            scoreLabel.text = $"SCORE\n{_score}";
    }

    void SpawnFloatingScoreText(int delta)
    {
        if (delta == 0 || !Application.isPlaying) return;
        BindBuilderHud();
        if (_builderScorePopupText != null)
        {
            if (_builderScorePopupRoutine != null)
                StopCoroutine(_builderScorePopupRoutine);
            _builderScorePopupRoutine = StartCoroutine(AnimateBuilderScoreText(delta));
            return;
        }

        var go = new GameObject("FloatingScoreText");
        MarkGeneratedObject(go);
        if (scoreLabel != null)
        {
            go.transform.SetParent(scoreLabel.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        }
        else
        {
            go.transform.SetParent(transform);
            go.transform.position = transform.position + new Vector3(0f, 2f, 0f);
        }

        var tmp = go.AddComponent<TextMeshPro>();
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(3f, 1.2f);

        tmp.text = delta > 0 ? $"+{delta}" : delta.ToString();
        tmp.fontSize = 10f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = delta > 0 ? Color.white : Color.red;
        tmp.fontStyle = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;

        var r = go.GetComponent<Renderer>();
        if (r != null) r.sortingOrder = 30;

        StartCoroutine(AnimateFloatingScoreText(go, tmp));
    }

    IEnumerator AnimateFloatingScoreText(GameObject go, TextMeshPro tmp)
    {
        float duration = 0.85f;
        float elapsed = 0f;
        var startPos = go.transform.localPosition;
        var startColor = tmp.color;

        while (elapsed < duration && go != null && tmp != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            go.transform.localPosition = startPos + new Vector3(0f, t * 0.9f, 0f);
            tmp.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            yield return null;
        }

        if (go != null)
            Destroy(go);
    }

    IEnumerator AnimateBuilderScoreText(int delta)
    {
        float duration = 0.85f;
        float elapsed = 0f;

        _builderScorePopupText.text = delta > 0 ? $"+{delta}" : delta.ToString();
        _builderScorePopupText.style.color = delta > 0 ? Color.white : Color.red;
        _builderScorePopupText.style.display = DisplayStyle.Flex;

        while (elapsed < duration && _builderScorePopupText != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _builderScorePopupText.style.top = -t * 18f;
            _builderScorePopupText.style.opacity = 1f - t;
            yield return null;
        }

        if (_builderScorePopupText != null)
        {
            _builderScorePopupText.text = string.Empty;
            _builderScorePopupText.style.top = 0f;
            _builderScorePopupText.style.opacity = 1f;
            _builderScorePopupText.style.display = DisplayStyle.None;
        }
        _builderScorePopupRoutine = null;
    }

    // ─────────────────────────────────────────────────────────────────────

    [Header("Camera Scroll")]
    public float scrollSpeed = 1.5f;
    [SerializeField] bool  autoFocusCameraOnLift = true;
    [SerializeField] bool  autoReturnCameraAfterPlace = true;
    [SerializeField] float cameraFocusSpeed = 8f;
    [SerializeField] float cameraTopPadding = 2f;
    [SerializeField, Range(0f, 1f)] float cameraReturnBodyAnchor = 0.55f;
    [SerializeField] float extractionViewPadding = 1.25f;
    [SerializeField] float placementViewTopOffset = 1f;

    float _floorY;
    float _cameraTargetY;
    float _cameraTargetSize;
    bool  _hasCameraTarget;
    int   _extractionMinCol;
    int   _extractionMaxCol;
    int   _extractionMinRow;
    int   _extractionMaxRow;

    void Update()
    {
        if (!Application.isPlaying) return;
        if (_isGameOver) return;
        CheckTowerBoundaryGameOver();
        if (_isGameOver) return;

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null && keyboard == null) return;

        if (_isHolding)
        {
            HandleHeldKeyboardInput(keyboard);
            if (!_isHolding)
            {
                UpdateCameraTarget();
                return;
            }
            UpdateHeldPosition();
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) CancelHold();
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) CancelHold();
        }
        else
        {
            HandleSelectionKeyboardInput(keyboard);
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)  HandleClick();
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) ClearSelection();
        }

        float scroll = mouse?.scroll.ReadValue().y ?? 0f;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                _hasCameraTarget = false;
                float minY = _floorY + cam.orthographicSize;
                float newY = Mathf.Max(cam.transform.position.y + Mathf.Sign(scroll) * scrollSpeed, minY);
                cam.transform.position = new Vector3(cam.transform.position.x, newY, cam.transform.position.z);
            }
        }

        UpdateCameraTarget();
        if (_builderBonusPreviewNeedsRefresh || _builderBonusCells == null)
            CreateOrUpdateBonusPreview();
        UpdateBonusPreviewPosition();
        UpdatePresetOutlineFeedback();
    }

    // ── 게임오버 ─────────────────────────────────────────────────────────

    void TriggerGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        if (_heldRoot != null) { Destroy(_heldRoot); _heldRoot = null; _isHolding = false; }
        ClearDetachedBlocks();
        _gameOverScreen?.Show(_score, Rebuild);
    }

    // ── 유틸: 마우스 → 월드 좌표 ─────────────────────────────────────────

    void CheckTowerBoundaryGameOver()
    {
        if (_leftBoundary == null || _rightBoundary == null) return;

        var leftCollider = _leftBoundary.GetComponent<Collider>();
        var rightCollider = _rightBoundary.GetComponent<Collider>();
        if (leftCollider == null || rightCollider == null) return;

        var leftBounds = leftCollider.bounds;
        var rightBounds = rightCollider.bounds;
        foreach (var data in _cells.Values)
        {
            if (data.go == null) continue;

            var cellCollider = data.go.GetComponent<Collider>();
            if (cellCollider == null) continue;

            var bounds = cellCollider.bounds;
            if (bounds.Intersects(leftBounds) || bounds.Intersects(rightBounds))
            {
                TriggerGameOver();
                return;
            }
        }
    }

    static Vector3 MouseWorldPos()
    {
        var cam    = Camera.main;
        var mouse  = Mouse.current.position.ReadValue();
        var screen = new Vector3(mouse.x, mouse.y, -cam.transform.position.z);
        var pos    = cam.ScreenToWorldPoint(screen);
        pos.z = 0f;
        return pos;
    }

    void HandleSelectionKeyboardInput(Keyboard keyboard)
    {
        if (!keyboardControlsEnabled || keyboard == null) return;
        RefreshDetachedComponents();

        bool hasMove = ReadMoveHeld(keyboard, allowWasd: false, out var dir);
        bool hasConfirm = ConfirmPressed(keyboard);
        bool hasCancel = CancelPressed(keyboard);
        bool hasPreset = ReadTetrominoPresetPressed(keyboard, out var preset);
        bool hasTab = keyboard.tabKey.wasPressedThisFrame;
        bool hasPresetRotate = ReadPresetRotationPressed(keyboard);

        if (!hasMove && !hasConfirm && !hasCancel && !hasPreset && !hasTab && !hasPresetRotate) return;

        if (hasCancel)
        {
            ClearSelection();
            _presetSelectionActive = false;
            ClearPresetOutlinePreview();
            ClearKeyboardFocus();
            return;
        }

        EnsureFocusedCell();

        if (_presetSelectionActive)
        {
            HandlePresetSelectionInput(hasMove, dir, hasConfirm, hasPreset, preset, hasTab, hasPresetRotate);
            return;
        }

        if (hasPreset && _hasFocusedCell)
        {
            BeginPresetSelection(preset);
            return;
        }

        if (hasMove)
        {
            MoveFocus(dir);
        }

        if (hasConfirm && _hasFocusedCell)
            ToggleFocusedSelection();
    }

    void HandleHeldKeyboardInput(Keyboard keyboard)
    {
        if (!keyboardControlsEnabled || keyboard == null) return;

        if (ReadMoveHeld(keyboard, allowWasd: true, out var dir))
        {
            if (!_usingKeyboardPlacement)
            {
                _heldBaseCell = ClampHeldBase(_heldBaseCell);
                _usingKeyboardPlacement = true;
            }
            MoveHeldBase(dir);
        }

        if (ReadHeldRotationPressed(keyboard, out var clockwise))
            RotateHeldBlocks(clockwise);

        if (ConfirmPressed(keyboard))
            DropHeldToNearestSurfaceAndPlace();

        if (CancelPressed(keyboard))
            CancelHold();
    }

    bool ReadMoveHeld(Keyboard keyboard, bool allowWasd, out Vector2Int dir)
    {
        dir = CurrentMoveDirection(keyboard, allowWasd);
        if (dir == Vector2Int.zero)
        {
            ResetMoveRepeat();
            return false;
        }

        bool pressedThisFrame = MoveDirectionPressedThisFrame(keyboard, dir, allowWasd);
        if (pressedThisFrame || dir != _moveRepeatDir)
        {
            _moveRepeatDir = dir;
            _nextMoveRepeatTime = Time.time + moveHoldInitialDelay;
            return true;
        }

        if (Time.time < _nextMoveRepeatTime)
            return false;

        _nextMoveRepeatTime = Time.time + moveHoldRepeatInterval;
        return true;
    }

    Vector2Int CurrentMoveDirection(Keyboard keyboard, bool allowWasd)
    {
        if (keyboard.upArrowKey.isPressed) return Vector2Int.up;
        if (keyboard.downArrowKey.isPressed) return Vector2Int.down;
        if (keyboard.leftArrowKey.isPressed) return Vector2Int.left;
        if (keyboard.rightArrowKey.isPressed) return Vector2Int.right;

        if (allowWasd)
        {
            if (keyboard.wKey.isPressed) return Vector2Int.up;
            if (keyboard.sKey.isPressed) return Vector2Int.down;
            if (keyboard.aKey.isPressed) return Vector2Int.left;
            if (keyboard.dKey.isPressed) return Vector2Int.right;
        }

        return Vector2Int.zero;
    }

    bool MoveDirectionPressedThisFrame(Keyboard keyboard, Vector2Int dir, bool allowWasd)
    {
        if (dir == Vector2Int.up)
            return keyboard.upArrowKey.wasPressedThisFrame || allowWasd && keyboard.wKey.wasPressedThisFrame;
        if (dir == Vector2Int.down)
            return keyboard.downArrowKey.wasPressedThisFrame || allowWasd && keyboard.sKey.wasPressedThisFrame;
        if (dir == Vector2Int.left)
            return keyboard.leftArrowKey.wasPressedThisFrame || allowWasd && keyboard.aKey.wasPressedThisFrame;
        if (dir == Vector2Int.right)
            return keyboard.rightArrowKey.wasPressedThisFrame || allowWasd && keyboard.dKey.wasPressedThisFrame;

        return false;
    }

    void ResetMoveRepeat()
    {
        _moveRepeatDir = Vector2Int.zero;
        _nextMoveRepeatTime = 0f;
    }

    bool ReadHeldRotationPressed(Keyboard keyboard, out bool clockwise)
    {
        if (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame)
        {
            clockwise = true;
            return true;
        }

        clockwise = false;
        return false;
    }

    bool ReadPresetRotationPressed(Keyboard keyboard)
    {
        return keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame;
    }

    bool ReadMovePressed(Keyboard keyboard, out Vector2Int dir)
    {
        dir = Vector2Int.zero;

        if (keyboard.upArrowKey.wasPressedThisFrame) dir = Vector2Int.up;
        else if (keyboard.downArrowKey.wasPressedThisFrame) dir = Vector2Int.down;
        else if (keyboard.leftArrowKey.wasPressedThisFrame) dir = Vector2Int.left;
        else if (keyboard.rightArrowKey.wasPressedThisFrame) dir = Vector2Int.right;

        return dir != Vector2Int.zero;
    }

    bool ConfirmPressed(Keyboard keyboard)
    {
        return keyboard.spaceKey.wasPressedThisFrame ||
               keyboard.enterKey.wasPressedThisFrame ||
               keyboard.numpadEnterKey.wasPressedThisFrame;
    }

    bool CancelPressed(Keyboard keyboard)
    {
        return keyboard.escapeKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame;
    }

    bool ReadTetrominoPresetPressed(Keyboard keyboard, out TetrominoPreset preset)
    {
        if (keyboard.qKey.wasPressedThisFrame) { preset = TetrominoPreset.I; return true; }
        if (keyboard.wKey.wasPressedThisFrame) { preset = TetrominoPreset.J; return true; }
        if (keyboard.eKey.wasPressedThisFrame) { preset = TetrominoPreset.L; return true; }
        if (keyboard.rKey.wasPressedThisFrame) { preset = TetrominoPreset.O; return true; }
        if (keyboard.aKey.wasPressedThisFrame) { preset = TetrominoPreset.S; return true; }
        if (keyboard.sKey.wasPressedThisFrame) { preset = TetrominoPreset.T; return true; }
        if (keyboard.dKey.wasPressedThisFrame) { preset = TetrominoPreset.Z; return true; }

        preset = default;
        return false;
    }

    void EnsureFocusedCell()
    {
        if (_hasFocusedCell && IsExtractableCell(_focusedCell)) return;
        if (_cells.Count == 0) { _hasFocusedCell = false; return; }

        if (TryFindDefaultFocusCell(ignoreLastPlaced: true, out var best) ||
            TryFindDefaultFocusCell(ignoreLastPlaced: false, out best))
        {
            SetFocusCell(best);
        }
    }

    bool TryFindDefaultFocusCell(bool ignoreLastPlaced, out Vector2Int best)
    {
        if (TryFindDefaultFocusCell(ignoreLastPlaced, originalTowerOnly: true, out best))
            return true;

        return TryFindDefaultFocusCell(ignoreLastPlaced, originalTowerOnly: false, out best);
    }

    bool TryFindDefaultFocusCell(bool ignoreLastPlaced, bool originalTowerOnly, out Vector2Int best)
    {
        best = default;
        bool found = false;
        int targetY = 0;
        float targetX = (_extractionMinCol + _extractionMaxCol) * 0.5f;

        if (TryFindTowerBodyRows(ignoreLastPlaced, originalTowerOnly, out int minY, out int maxY))
            targetY = Mathf.RoundToInt(Mathf.Lerp(minY, maxY, cameraReturnBodyAnchor));

        foreach (var cell in _cells.Keys)
        {
            if (ignoreLastPlaced && _lastPlacedCells.Contains(cell)) continue;
            if (!IsExtractableCell(cell)) continue;
            if (originalTowerOnly && !IsInExtractionTowerRows(cell)) continue;

            int distance = Mathf.Abs(cell.y - targetY);
            int bestDistance = found ? Mathf.Abs(best.y - targetY) : int.MaxValue;
            float xDistance = Mathf.Abs(cell.x - targetX);
            float bestXDistance = found ? Mathf.Abs(best.x - targetX) : float.MaxValue;
            if (!found ||
                distance < bestDistance ||
                distance == bestDistance && cell.y > best.y ||
                distance == bestDistance && cell.y == best.y && xDistance < bestXDistance ||
                distance == bestDistance && cell.y == best.y && Mathf.Approximately(xDistance, bestXDistance) && cell.x < best.x)
            {
                best = cell;
                found = true;
            }
        }
        return found;
    }

    void MoveFocus(Vector2Int dir)
    {
        if (!_hasFocusedCell) return;

        var candidate = _focusedCell + dir;
        int maxFocusY = Mathf.Max(placementMax.y, HighestOccupiedRow());
        int minFocusX = Mathf.Min(placementMin.x, _extractionMinCol);
        int maxFocusX = Mathf.Max(placementMax.x, _extractionMaxCol);
        while (candidate.x >= minFocusX && candidate.x <= maxFocusX &&
               candidate.y >= 0 && candidate.y <= maxFocusY)
        {
            if (IsExtractableCell(candidate))
            {
                SetFocusCell(candidate);
                return;
            }
            candidate += dir;
        }
    }

    void SetFocusCell(Vector2Int cell)
    {
        var oldFocus = _focusedCell;
        bool hadFocus = _hasFocusedCell;

        _focusedCell = cell;
        _hasFocusedCell = true;

        if (hadFocus) ApplyCellVisual(oldFocus);
        ApplyCellVisual(_focusedCell);
    }

    void ClearKeyboardFocus()
    {
        if (!_hasFocusedCell) return;

        var oldFocus = _focusedCell;
        _hasFocusedCell = false;
        ApplyCellVisual(oldFocus);
    }

    void ClearSelectedCellsOnly()
    {
        foreach (var c in new List<Vector2Int>(_selected))
            DeselectCell(c);
    }

    void ClearPresetOutlinePreview()
    {
        if (_presetOutlineRoot == null) return;
        DestroyLocal(_presetOutlineRoot);
        _presetOutlineRoot = null;
    }

    void ToggleFocusedSelection()
    {
        _presetSelectionActive = false;
        ClearPresetOutlinePreview();
        if (!IsExtractableCell(_focusedCell)) return;

        if (_selected.Contains(_focusedCell))
            TryDeselect(_focusedCell);
        else if (_selected.Count < 4 && (_selected.Count == 0 || IsAdjacentToSelected(_focusedCell)))
        {
            SelectCell(_focusedCell);
            if (_selected.Count == 4) LiftBlocks();
        }
    }

    // ── 일반 클릭 ─────────────────────────────────────────────────────────

    void BeginPresetSelection(TetrominoPreset preset)
    {
        ClearSelectedCellsOnly();
        _presetSelectionActive = true;
        _presetSelectionPreset = preset;
        _presetSelectionAnchor = _focusedCell;
        _presetSelectionRotation = 0;
        ApplyPresetSelection();
    }

    void HandlePresetSelectionInput(
        bool hasMove,
        Vector2Int dir,
        bool hasConfirm,
        bool hasPreset,
        TetrominoPreset preset,
        bool hasTab,
        bool hasPresetRotate)
    {
        if (hasTab)
        {
            ExitPresetSelectionToFocus();
            return;
        }

        if (hasPreset)
        {
            _presetSelectionPreset = preset;
            _presetSelectionRotation = 0;
            ApplyPresetSelection();
            return;
        }

        if (hasPresetRotate)
        {
            _presetSelectionRotation = (_presetSelectionRotation + 1) % 4;
            ApplyPresetSelection();
            return;
        }

        if (hasMove)
        {
            _presetSelectionAnchor += dir;
            ApplyPresetSelection();
        }

        if (hasConfirm)
            ConfirmPresetSelection();
    }

    void ExitPresetSelectionToFocus()
    {
        var anchor = _presetSelectionAnchor;
        ClearSelectedCellsOnly();
        ClearPresetOutlinePreview();
        _presetSelectionActive = false;
        if (IsExtractableCell(anchor))
            SetFocusCell(anchor);
    }

    bool ApplyPresetSelection()
    {
        if (!_presetSelectionActive) return false;

        ClearSelectedCellsOnly();
        SetFocusCell(_presetSelectionAnchor);
        var presetCells = GetTetrominoPresetCells(_presetSelectionAnchor, _presetSelectionPreset, _presetSelectionRotation);
        CreatePresetOutlinePreview(presetCells);
        if (CanApplyPresetSelection(_presetSelectionAnchor, _presetSelectionPreset, _presetSelectionRotation))
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

        foreach (var cell in cells)
        {
            var go = new GameObject($"PresetOutline_{cell.x}_{cell.y}");
            MarkGeneratedObject(go);
            go.transform.SetParent(_presetOutlineRoot.transform, false);
            go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0.04f);
            go.transform.localScale = Vector3.one * selectedOutlineScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateOutlineSprite();
            sr.color = selectedOutlineColor;
            sr.sortingOrder = 5;
        }
    }

    void UpdatePresetOutlineFeedback()
    {
        if (_presetOutlineRoot == null) return;

        bool isFailing = Time.time < _placementFailEndTime;
        _presetOutlineRoot.transform.localPosition = FailShakeOffset(isFailing);

        var color = isFailing ? _failFlashColor : selectedOutlineColor;
        foreach (var sr in _presetOutlineRoot.GetComponentsInChildren<SpriteRenderer>())
            sr.color = color;
    }

    bool CanApplyPresetSelection(Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        foreach (var cell in GetTetrominoPresetCells(anchor, preset, rotation))
            if (!IsExtractableCell(cell))
                return false;
        return true;
    }

    void ConfirmPresetSelection()
    {
        if (_selected.Count > 0)
        {
            _presetSelectionActive = false;
            LiftBlocks();
        }
        else
            PlayPlacementFailFeedback();
    }

    void TryLiftTetrominoPreset(TetrominoPreset preset)
    {
        var cells = GetTetrominoPresetCells(_focusedCell, preset);
        foreach (var cell in cells)
        {
            if (!IsExtractableCell(cell))
            {
                PlayPlacementFailFeedback();
                return;
            }
        }

        ClearSelection();
        foreach (var cell in cells)
            SelectCell(cell);

        LiftBlocks();
    }

    List<Vector2Int> GetTetrominoPresetCells(Vector2Int anchor, TetrominoPreset preset)
    {
        return GetTetrominoPresetCells(anchor, preset, 0);
    }

    Vector2Int[] GetTetrominoPresetOffsets(TetrominoPreset preset)
    {
        return preset switch
        {
            TetrominoPreset.I => new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(2, 0), new Vector2Int(3, 0)
            },
            TetrominoPreset.J => new[]
            {
                new Vector2Int(0, 1), new Vector2Int(0, 0),
                new Vector2Int(1, 0), new Vector2Int(2, 0)
            },
            TetrominoPreset.L => new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(2, 0), new Vector2Int(2, 1)
            },
            TetrominoPreset.O => new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 1)
            },
            TetrominoPreset.S => new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(1, 1), new Vector2Int(2, 1)
            },
            TetrominoPreset.T => new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(2, 0), new Vector2Int(1, 1)
            },
            TetrominoPreset.Z => new[]
            {
                new Vector2Int(0, 1), new Vector2Int(1, 1),
                new Vector2Int(1, 0), new Vector2Int(2, 0)
            },
            _ => new[] { Vector2Int.zero }
        };
    }

    List<Vector2Int> GetTetrominoPresetCells(Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        if (preset == TetrominoPreset.O)
            rotation = 0;

        var offsets = GetTetrominoPresetOffsets(preset);
        var pivot = GetBaseRotationPivotForPreset(preset);

        var cells = new List<Vector2Int>(offsets.Length);
        foreach (var offset in offsets)
        {
            var rel = RotateCellAroundPivot(offset, pivot, rotation) - pivot;
            cells.Add(anchor + FloorToCell(rel));
        }
        return cells;
    }

    bool ShapeMatchesPreset(List<Vector2Int> shape, TetrominoPreset preset)
    {
        return CanonicalShape(shape) == CanonicalShape(GetTetrominoPresetCells(Vector2Int.zero, preset));
    }

    bool TryGetMatchingTetrominoPreset(List<Vector2Int> shape, out TetrominoPreset preset, out int rotation)
    {
        var shapeKey = NormalizedShapeKey(shape);
        for (int i = 0; i <= (int)TetrominoPreset.Z; i++)
        {
            var candidate = (TetrominoPreset)i;
            for (int candidateRotation = 0; candidateRotation < 4; candidateRotation++)
            {
                var candidateKey = NormalizedShapeKey(GetTetrominoPresetCells(Vector2Int.zero, candidate, candidateRotation));
                if (shapeKey != candidateKey) continue;

                preset = candidate;
                rotation = candidateRotation;
                return true;
            }
        }

        preset = TetrominoPreset.I;
        rotation = 0;
        return false;
    }

    Vector2 RotationPivotForPreset(TetrominoPreset preset, int rotation)
    {
        var pivot = GetBaseRotationPivotForPreset(preset);

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        foreach (var offset in GetTetrominoPresetOffsets(preset))
        {
            var rotatedOffset = RotateCellAroundPivot(offset, pivot, rotation);
            var cell = RoundToCell(rotatedOffset);
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
        }

        return pivot - new Vector2(minX, minY);
    }

    Vector2 GetBaseRotationPivotForPreset(TetrominoPreset preset)
    {
        return preset switch
        {
            TetrominoPreset.I => new Vector2(1.5f, 0.5f),
            TetrominoPreset.O => new Vector2(1f, 1f),
            TetrominoPreset.S or TetrominoPreset.Z => new Vector2(1f, 1f),
            _ => new Vector2(1f, 0f)
        };
    }

    Vector2 RotateCellAroundPivot(Vector2Int cell, Vector2 pivot, int quarterTurns)
    {
        var rotated = new Vector2(cell.x, cell.y);
        int turns = ((quarterTurns % 4) + 4) % 4;
        for (int i = 0; i < turns; i++)
            rotated = new Vector2(pivot.x + rotated.y - pivot.y, pivot.y - rotated.x + pivot.x);

        return rotated;
    }

    Vector2Int RoundToCell(Vector2 value)
    {
        return new Vector2Int(
            Mathf.FloorToInt(value.x + 0.5f),
            Mathf.FloorToInt(value.y + 0.5f));
    }

    Vector2Int FloorToCell(Vector2 value)
    {
        return new Vector2Int(
            Mathf.FloorToInt(value.x),
            Mathf.FloorToInt(value.y));
    }

    string CanonicalShape(List<Vector2Int> cells)
    {
        string best = null;
        for (int rotation = 0; rotation < 4; rotation++)
        {
            var rotated = new List<Vector2Int>(cells.Count);
            foreach (var cell in cells)
                rotated.Add(RotateCell(cell, rotation));

            var key = NormalizedShapeKey(rotated);
            if (best == null || string.CompareOrdinal(key, best) < 0)
                best = key;
        }
        return best;
    }

    Vector2Int RotateCell(Vector2Int cell, int quarterTurns)
    {
        return quarterTurns switch
        {
            1 => new Vector2Int(-cell.y, cell.x),
            2 => new Vector2Int(-cell.x, -cell.y),
            3 => new Vector2Int(cell.y, -cell.x),
            _ => cell
        };
    }

    string NormalizedShapeKey(List<Vector2Int> cells)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        foreach (var cell in cells)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
        }

        var normalized = new List<Vector2Int>(cells.Count);
        foreach (var cell in cells)
            normalized.Add(new Vector2Int(cell.x - minX, cell.y - minY));

        normalized.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        var key = "";
        foreach (var cell in normalized)
            key += $"{cell.x},{cell.y};";
        return key;
    }

    void HandleClick()
    {
        RefreshDetachedComponents();
        var worldPos = MouseWorldPos();
        var local = _towerRoot.InverseTransformPoint(worldPos);
        var cell  = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));

        if (!IsExtractableCell(cell)) return;
        _presetSelectionActive = false;
        SetFocusCell(cell);

        if (_selected.Contains(cell))
            TryDeselect(cell);
        else if (_selected.Count < 4 && (_selected.Count == 0 || IsAdjacentToSelected(cell)))
        {
            SelectCell(cell);
            if (_selected.Count == 4) LiftBlocks();
        }
    }

    // ── 선택 / 해제 ───────────────────────────────────────────────────────

    void SelectCell(Vector2Int cell)
    {
        if (!_cells.TryGetValue(cell, out var data)) return;
        if (!data.isOriginalTower) return;
        _selected.Add(cell);
        ApplyCellVisual(cell);
    }

    bool IsExtractableCell(Vector2Int cell)
    {
        return _cells.TryGetValue(cell, out var data) && data.isOriginalTower;
    }

    void TryDeselect(Vector2Int cell)
    {
        var remaining = new List<Vector2Int>(_selected);
        remaining.Remove(cell);
        if (remaining.Count <= 1 || IsConnected(remaining))
            DeselectCell(cell);
    }

    void DeselectCell(Vector2Int cell)
    {
        _selected.Remove(cell);
        ApplyCellVisual(cell);
    }

    void ClearSelection()
    {
        _presetSelectionActive = false;
        ClearPresetOutlinePreview();
        foreach (var c in new List<Vector2Int>(_selected))
            DeselectCell(c);
    }

    void ApplyCellVisual(Vector2Int cell)
    {
        if (!_cells.TryGetValue(cell, out var data)) return;

        bool isSelected = _selected.Contains(cell);
        bool isFocused = !_presetSelectionActive && _hasFocusedCell && _focusedCell == cell;
        bool showSelectedOutline = isSelected && !_presetSelectionActive;
        var color = data.isOriginalTower ? NumberColor(data.number) : PlacedNumberColor(data.number);
        if (isSelected)
            color = Color.Lerp(color, Color.white, 0.45f);
        if (isFocused)
            color = Color.Lerp(color, focusedCellColor, 0.8f);

        data.sr.color = color;
        ApplyCellOutline(data, isFocused, showSelectedOutline);
    }

    void ApplyCellOutline(CellData data, bool isFocused, bool isSelected)
    {
        if (data.outline == null) return;

        data.outline.enabled = isFocused || isSelected;
        data.outline.color = isFocused ? focusedOutlineColor : selectedOutlineColor;
        data.outline.transform.localScale = Vector3.one * (isFocused ? focusedOutlineScale : selectedOutlineScale);
        data.outline.sortingOrder = isFocused ? 4 : 3;
    }

    // ── 블럭 들어올리기 ───────────────────────────────────────────────────

    void LiftBlocks()
    {
        _presetSelectionActive = false;
        ClearPresetOutlinePreview();
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in _selected)
        {
            minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y);
            maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y);
        }
        var extractionScorePos = _towerRoot.TransformPoint(new Vector3(
            (minX + maxX + 1f) * 0.5f,
            (minY + maxY + 1f) * 0.5f,
            0f));

        _heldCenter = new Vector2((maxX - minX + 1) * 0.5f, (maxY - minY + 1) * 0.5f);

        _heldRelPos.Clear();
        _heldData.Clear();
        _heldSourceCells.Clear();
        _heldStartScore = _score;
        foreach (var c in _selected)
            _heldRelPos.Add(new Vector2Int(c.x - minX, c.y - minY));
        _heldMatchesBonus = ShapeMatchesPreset(_heldRelPos, _bonusTargetPreset);

        foreach (var pair in _cells)
        {
            _heldSourceCells.Add(new HeldSourceCell
            {
                cell = pair.Key,
                number = pair.Value.number,
                isOriginalTower = pair.Value.isOriginalTower
            });
        }

        var changedCells = new List<Vector2Int>();
        foreach (var cell in _selected)
        {
            if (!_cells.TryGetValue(cell, out var data)) continue;
            changedCells.Add(cell);
            data.number--;
            if (data.number <= 0)
            {
                Destroy(data.go);
                _cells.Remove(cell);
            }
            else
            {
                var bc = data.go.GetComponent<BlockCell>();
                if (bc != null)
                {
                    bc.Weight = data.number;
                    bc.IsOriginalTower = data.isOriginalTower;
                }

                data.sr.color = NumberColor(data.number);
                data.label.text = data.number.ToString();
            }
        }

        _selected.Clear();
        _hasFocusedCell = false;
        foreach (var cell in changedCells)
            ApplyCellVisual(cell);

        _heldRoot = new GameObject("HeldBlocks");
        MarkGeneratedObject(_heldRoot);
        _heldRoot.transform.SetParent(transform);

        var heldColor = new Color(placedBlockColor.r, placedBlockColor.g, placedBlockColor.b, 0.6f);

        for (int i = 0; i < _heldRelPos.Count; i++)
        {
            var rel = _heldRelPos[i];

            var go = new GameObject($"Held_{i}");
            MarkGeneratedObject(go);
            go.transform.SetParent(_heldRoot.transform, false);
            go.transform.localPosition = new Vector3(
                rel.x + 0.5f - _heldCenter.x,
                rel.y + 0.5f - _heldCenter.y,
                0f);
            go.transform.localScale = Vector3.one * blockBodyScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = CreateBlockSprite();
            sr.color        = heldColor;
            sr.sortingOrder = 20;
            var blurRenderers = CreatePreviewBlur(go.transform);

            var box = go.AddComponent<BoxCollider>();
            box.size           = Vector3.one * LocalColliderSize();
            box.sharedMaterial = CreateFrictionMaterial();
            box.enabled        = false;

            var bc = go.AddComponent<BlockCell>();
            bc.Weight = 1;
            bc.IsOriginalTower = false;

            _heldData.Add(new CellData
            {
                number = 1,
                isOriginalTower = false,
                go = go,
                sr = sr,
                outline = null,
                label = null,
                previewBlurRenderers = blurRenderers
            });
        }

        CheckForDetachment();
        UpdateTowerPhysicsState();

        _isHolding = true;
        _heldBaseCell = GetDefaultHeldBaseCell();
        _usingKeyboardPlacement = true;
        if (autoFocusCameraOnLift)
            ShowPlacementCameraView(immediate: false);
        AddScore(1, extractionScorePos);
        if (_heldMatchesBonus)
            AddScore(2, _bonusPreviewRoot != null ? _bonusPreviewRoot.transform.position : extractionScorePos);
    }

    // ── 커서 추적 ─────────────────────────────────────────────────────────

    void UpdateHeldPosition()
    {
        bool canPlace = CanPlaceHeldBlocks(out _, out var snappedWorldPos);
        bool isFailing = Time.time < _placementFailEndTime;
        var previewColor = isFailing
            ? _failFlashColor
            : canPlace ? _validHoldColor : _invalidHoldColor;

        _heldRoot.transform.position = snappedWorldPos + FailShakeOffset(isFailing);
        SetHeldPreviewColor(previewColor, canPlace && !isFailing);
    }

    Vector3 FailShakeOffset(bool isFailing)
    {
        if (!isFailing || placementFailDuration <= 0f) return Vector3.zero;

        float progress = Mathf.Clamp01((Time.time - _placementFailStartTime) / placementFailDuration);
        float fade = 1f - progress;
        float wave = Mathf.Sin(progress * Mathf.PI * 2f * placementFailShakeCount);
        return new Vector3(wave * placementFailShakeDistance * fade, 0f, 0f);
    }

    void PlayPlacementFailFeedback()
    {
        _placementFailStartTime = Time.time;
        _placementFailEndTime = Time.time + placementFailDuration;
#if UNITY_ANDROID || UNITY_IOS
        if (Application.isPlaying)
            Handheld.Vibrate();
#endif
    }

    Vector2Int GetDefaultHeldBaseCell()
    {
        float placementCenterX = (placementMin.x + placementMax.x + 1f) * 0.5f;
        int baseX = Mathf.RoundToInt(placementCenterX - _heldCenter.x);
        var baseCell = new Vector2Int(baseX, HighestOccupiedRow() + 1);
        return ClampHeldBase(baseCell);
    }

    void MoveHeldBase(Vector2Int dir)
    {
        _heldBaseCell += dir;
        _heldBaseCell = ClampHeldBase(_heldBaseCell);
        if (dir.y != 0)
            FocusCameraOnHeldCenter();
    }

    void RotateHeldBlocks(bool clockwise)
    {
        if (_heldRelPos.Count == 0) return;

        if (TryGetMatchingTetrominoPreset(_heldRelPos, out var preset, out var presetRotation))
        {
            if (preset == TetrominoPreset.O)
                return;

            var pivot = RotationPivotForPreset(preset, presetRotation);
            var pivotWorld = _heldBaseCell + pivot;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            var rotated = new List<Vector2Int>(_heldRelPos.Count);

            foreach (var rel in _heldRelPos)
            {
                var next = clockwise
                    ? new Vector2(pivot.x + rel.y - pivot.y, pivot.y - rel.x + pivot.x)
                    : new Vector2(pivot.x - rel.y + pivot.y, pivot.y + rel.x - pivot.x);
                var nextCell = RoundToCell(next);

                rotated.Add(nextCell);
                minX = Mathf.Min(minX, nextCell.x);
                minY = Mathf.Min(minY, nextCell.y);
            }

            var normalizeOffset = new Vector2Int(minX, minY);
            for (int i = 0; i < rotated.Count; i++)
                _heldRelPos[i] = rotated[i] - normalizeOffset;

            if (_usingKeyboardPlacement)
            {
                var normalizedPivot = pivot - normalizeOffset;
                _heldBaseCell = ClampHeldBase(RoundToCell(pivotWorld - normalizedPivot));
            }

            RecalculateHeldCenter();
            UpdateHeldChildLocalPositions();
            return;
        }

        int maxX = 0, maxY = 0;
        foreach (var rel in _heldRelPos)
        {
            maxX = Mathf.Max(maxX, rel.x);
            maxY = Mathf.Max(maxY, rel.y);
        }

        for (int i = 0; i < _heldRelPos.Count; i++)
        {
            var rel = _heldRelPos[i];
            _heldRelPos[i] = clockwise
                ? new Vector2Int(maxY - rel.y, rel.x)
                : new Vector2Int(rel.y, maxX - rel.x);
        }

        RecalculateHeldCenter();
        UpdateHeldChildLocalPositions();

        if (_usingKeyboardPlacement)
            _heldBaseCell = ClampHeldBase(_heldBaseCell);
    }

    void RecalculateHeldCenter()
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var rel in _heldRelPos)
        {
            minX = Mathf.Min(minX, rel.x);
            minY = Mathf.Min(minY, rel.y);
            maxX = Mathf.Max(maxX, rel.x);
            maxY = Mathf.Max(maxY, rel.y);
        }
        _heldCenter = new Vector2((maxX - minX + 1) * 0.5f, (maxY - minY + 1) * 0.5f);
    }

    void UpdateHeldChildLocalPositions()
    {
        for (int i = 0; i < _heldRelPos.Count && i < _heldData.Count; i++)
        {
            var rel = _heldRelPos[i];
            _heldData[i].go.transform.localPosition = new Vector3(
                rel.x + 0.5f - _heldCenter.x,
                rel.y + 0.5f - _heldCenter.y,
                0f);
        }
    }

    Vector2Int ClampHeldBase(Vector2Int baseCell)
    {
        int minRelX = int.MaxValue, minRelY = int.MaxValue;
        int maxRelX = int.MinValue, maxRelY = int.MinValue;

        foreach (var rel in _heldRelPos)
        {
            minRelX = Mathf.Min(minRelX, rel.x);
            minRelY = Mathf.Min(minRelY, rel.y);
            maxRelX = Mathf.Max(maxRelX, rel.x);
            maxRelY = Mathf.Max(maxRelY, rel.y);
        }

        int minBaseX = placementMin.x - minRelX;
        int maxBaseX = placementMax.x - maxRelX;
        int minBaseY = placementMin.y - minRelY;
        int maxBaseY = placementMax.y - maxRelY;

        return new Vector2Int(
            Mathf.Clamp(baseCell.x, minBaseX, maxBaseX),
            Mathf.Clamp(baseCell.y, minBaseY, maxBaseY));
    }

    void SetHeldPreviewColor(Color color, bool showBlur)
    {
        foreach (var data in _heldData)
        {
            if (data.sr != null)
                data.sr.color = color;

            if (data.previewBlurRenderers == null) continue;

            var blurColor = new Color(color.r, color.g, color.b, previewBlurAlpha);
            bool blurVisible = false;
            foreach (var blur in data.previewBlurRenderers)
            {
                if (blur == null) continue;
                blur.enabled = blurVisible;
                blur.color = blurColor;
            }
        }
    }

    List<SpriteRenderer> CreatePreviewBlur(Transform parent)
    {
        var renderers = new List<SpriteRenderer>();
        if (!previewBlurEnabled) return renderers;

        int copies = Mathf.Max(1, previewBlurCopies);
        for (int i = 0; i < copies; i++)
        {
            float angle = Mathf.PI * 2f * i / copies;
            var go = new GameObject($"PreviewBlur_{i}");
            MarkGeneratedObject(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * previewBlurRadius,
                Mathf.Sin(angle) * previewBlurRadius,
                0f);
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

    void ClearPreviewBlur(CellData data)
    {
        if (data.previewBlurRenderers == null) return;

        foreach (var blur in data.previewBlurRenderers)
            if (blur != null)
                DestroyLocal(blur.gameObject);

        data.previewBlurRenderers = null;
    }

    // ── 블럭 배치 ─────────────────────────────────────────────────────────

    bool CanPlaceHeldBlocks(out List<Vector2Int> targets, out Vector3 snappedWorldPos)
    {
        RefreshDetachedComponents();
        return CanPlaceHeldBlocks(_heldBaseCell, out targets, out snappedWorldPos);
    }

    bool CanPlaceHeldBlocks(Vector2Int baseCell, out List<Vector2Int> targets, out Vector3 snappedWorldPos)
    {
        var detachedCells = CollectStableDetachedCells();
        targets = new List<Vector2Int>(_heldRelPos.Count);
        var snappedLocalPos = new Vector3(baseCell.x + _heldCenter.x, baseCell.y + _heldCenter.y, 0f);
        snappedWorldPos = _towerRoot.TransformPoint(snappedLocalPos);

        foreach (var rel in _heldRelPos)
        {
            var target = new Vector2Int(baseCell.x + rel.x, baseCell.y + rel.y);
            if (target.x < placementMin.x || target.x > placementMax.x ||
                target.y < placementMin.y || target.y > placementMax.y) return false;
            if (detachedCells.Contains(target)) return false;
            if (_cells.TryGetValue(target, out var existing) && existing.number >= 6) return false;
            targets.Add(target);
        }

        bool adjacent = false;
        foreach (var t in targets)
        {
            if (_cells.ContainsKey(t))
            {
                adjacent = true;
                break;
            }

            foreach (var n in Neighbors(t))
            {
                if (IsOccupiedCell(n, detachedCells)) { adjacent = true; break; }
            }
            if (adjacent) break;
        }
        if (!adjacent && (_cells.Count > 0 || detachedCells.Count > 0)) return false;

        // 중력 체크: 그룹 내 최소 1개의 블럭이 바닥(row 0) 또는 기존 타워 블럭 위에 놓여야 함
        // (위쪽 블럭에만 붙어서 공중에 배치하는 것 방지)
        bool hasBottomSupport = false;
        foreach (var t in targets)
        {
            if (_cells.ContainsKey(t) ||
                t.y == 0 ||
                IsOccupiedCell(new Vector2Int(t.x, t.y - 1), detachedCells))
            {
                hasBottomSupport = true;
                break;
            }
        }
        return hasBottomSupport;
    }

    bool IsOccupiedCell(Vector2Int cell, HashSet<Vector2Int> detachedCells)
    {
        return _cells.ContainsKey(cell) || detachedCells.Contains(cell);
    }

    void DropHeldToNearestSurfaceAndPlace()
    {
        if (!TryFindNearestDropBase(_heldBaseCell, out var dropBase))
        {
            PlayPlacementFailFeedback();
            return;
        }

        _heldBaseCell = dropBase;
        TryPlaceBlocks();
    }

    bool TryFindNearestDropBase(Vector2Int startBase, out Vector2Int dropBase)
    {
        startBase = ClampHeldBase(startBase);
        for (int y = startBase.y; y >= placementMin.y - 4; y--)
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

        var placementScorePos = AverageCellWorldPosition(targets);
        _lastPlacedCells.Clear();
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var data   = _heldData[i];

            ClearPreviewBlur(data);
            if (_cells.TryGetValue(target, out var existing))
            {
                existing.number = Mathf.Min(6, existing.number + 1);
                UpdateCellDataVisuals(existing);
                ApplyCellVisual(target);
                if (data.go != null)
                    Destroy(data.go);
                _lastPlacedCells.Add(target);
                continue;
            }

            data.go.transform.SetParent(_towerRoot, false);
            data.go.transform.localPosition = new Vector3(target.x + 0.5f, target.y + 0.5f, 0f);
            data.go.transform.localScale = Vector3.one * blockBodyScale;

            var box = data.go.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.size = Vector3.one * LocalColliderSize();
                box.enabled = true;
            }

            data.sr.sortingOrder = 0;
            data.sr.color = PlacedNumberColor(data.number);
            if (data.label == null)
                data.label = SpawnLabel(data.number, data.go.transform);

            var bc = data.go.GetComponent<BlockCell>();
            if (bc != null)
            {
                bc.Weight = data.number;
                bc.IsOriginalTower = data.isOriginalTower;
            }

            _cells[target] = data;
            _lastPlacedCells.Add(target);
        }

        Destroy(_heldRoot);
        _heldRoot = null;
        _heldRelPos.Clear();
        _heldData.Clear();
        _heldSourceCells.Clear();
        _heldStartScore = 0;
        _heldMatchesBonus = false;
        _isHolding = false;
        _usingKeyboardPlacement = false;
        ClearKeyboardFocus();

        UpdateTowerPhysicsState();
        if (autoReturnCameraAfterPlace)
        {
            UpdateExtractionTowerRowsFromCells();
            ShowExtractionCameraView(immediate: true);
            FocusDefaultExtractionCell();
        }
        else if (autoFocusCameraOnLift)
            ShowPlacementCameraView(immediate: false);
        AddScore(1, placementScorePos);
        RollBonusTarget();
    }

    Vector3 AverageCellWorldPosition(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
            return transform.position;

        var sum = Vector3.zero;
        foreach (var cell in cells)
            sum += _towerRoot.TransformPoint(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f));
        return sum / cells.Count;
    }

    void FocusDefaultExtractionCell()
    {
        ClearKeyboardFocus();
        if (TryFindDefaultFocusCell(ignoreLastPlaced: true, out var cell) ||
            TryFindDefaultFocusCell(ignoreLastPlaced: false, out cell))
        {
            SetFocusCell(cell);
        }
    }

    // ── 들기 취소 ─────────────────────────────────────────────────────────

    void CancelHold()
    {
        if (!_isHolding) return;

        if (_heldRoot != null)
            Destroy(_heldRoot);
        _heldRoot = null;
        RestoreHeldSourceCells();
        _heldRelPos.Clear();
        _heldData.Clear();
        _heldSourceCells.Clear();
        _heldStartScore = 0;
        _heldMatchesBonus = false;
        _isHolding = false;
        _usingKeyboardPlacement = false;
        ResetMoveRepeat();

        UpdateTowerPhysicsState();
        ShowExtractionCameraView(immediate: true);
        FocusDefaultExtractionCell();
        AddScore(-2, scoreLabel != null ? scoreLabel.transform.position : transform.position);
    }

    void RestoreHeldSourceCells()
    {
        foreach (var pair in _cells)
        {
            if (pair.Value.go != null)
                DestroyLocal(pair.Value.go);
        }
        _cells.Clear();

        ClearDetachedBlocks();

        foreach (var source in _heldSourceCells)
        {
            _cells[source.cell] = SpawnCell(source.cell, source.number, source.isOriginalTower);
            ApplyCellVisual(source.cell);
        }
        _score = _heldStartScore;
        _lastPlacedCells.Clear();
        ClearKeyboardFocus();
        UpdateExtractionTowerRowsFromCells();
    }

    // ── 연결 요소 분리 ────────────────────────────────────────────────────

    void CheckForDetachment()
    {
        if (_cells.Count == 0) return;

        var components = FindConnectedComponents();
        if (components.Count <= 1) return;

        int mainIdx = FindMainTowerComponentIndex(components);

        for (int i = 0; i < components.Count; i++)
        {
            if (i == mainIdx) continue;
            DetachComponent(components[i]);
        }
    }

    int FindMainTowerComponentIndex(List<List<Vector2Int>> components)
    {
        int bestIdx = 0;
        for (int i = 1; i < components.Count; i++)
        {
            if (IsBetterMainTowerComponent(components[i], components[bestIdx]))
                bestIdx = i;
        }
        return bestIdx;
    }

    bool IsBetterMainTowerComponent(List<Vector2Int> candidate, List<Vector2Int> current)
    {
        bool candidateGrounded = TouchesGround(candidate);
        bool currentGrounded = TouchesGround(current);
        if (candidateGrounded != currentGrounded)
            return candidateGrounded;

        int candidateMinY = MinComponentY(candidate);
        int currentMinY = MinComponentY(current);
        if (candidateMinY != currentMinY)
            return candidateMinY < currentMinY;

        return candidate.Count > current.Count;
    }

    bool TouchesGround(List<Vector2Int> component)
    {
        foreach (var cell in component)
            if (cell.y == 0)
                return true;
        return false;
    }

    int MinComponentY(List<Vector2Int> component)
    {
        int minY = int.MaxValue;
        foreach (var cell in component)
            minY = Mathf.Min(minY, cell.y);
        return minY;
    }

    List<List<Vector2Int>> FindConnectedComponents()
    {
        var unvisited  = new HashSet<Vector2Int>(_cells.Keys);
        var components = new List<List<Vector2Int>>();

        while (unvisited.Count > 0)
        {
            var en = unvisited.GetEnumerator();
            en.MoveNext();
            var start = en.Current;
            en.Dispose();

            var component = new List<Vector2Int>();
            var queue     = new Queue<Vector2Int>();
            queue.Enqueue(start);
            unvisited.Remove(start);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                component.Add(c);
                foreach (var n in Neighbors(c))
                    if (unvisited.Remove(n))
                        queue.Enqueue(n);
            }
            components.Add(component);
        }
        return components;
    }

    void DetachComponent(List<Vector2Int> component)
    {
        var centroid = Vector3.zero;
        int valid    = 0;
        foreach (var cell in component)
        {
            if (_cells.TryGetValue(cell, out var d))
            { centroid += d.go.transform.position; valid++; }
        }
        if (valid == 0) return;
        centroid /= valid;

        var orphanGO = new GameObject("DetachedBlocks");
        MarkGeneratedObject(orphanGO);
        orphanGO.transform.SetParent(transform);
        orphanGO.transform.position = centroid;

        var orphanRb = orphanGO.AddComponent<Rigidbody>();
        orphanRb.useGravity             = true;
        orphanRb.interpolation          = RigidbodyInterpolation.Interpolate;
        orphanRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        orphanRb.linearVelocity         = _rb.linearVelocity;
        orphanRb.angularVelocity        = _rb.angularVelocity;
        orphanRb.linearDamping          = 0.3f;
        orphanRb.angularDamping         = 1f;
        orphanRb.constraints            = RigidbodyConstraints.FreezePositionZ
                                        | RigidbodyConstraints.FreezeRotationX
                                        | RigidbodyConstraints.FreezeRotationY;

        float   totalWeight = 0f;
        Vector2 weightedSum = Vector2.zero;

        foreach (var cell in component)
        {
            if (!_cells.TryGetValue(cell, out var data)) continue;

            data.go.transform.SetParent(orphanGO.transform, worldPositionStays: true);

            var localPos = (Vector2)data.go.transform.localPosition;
            weightedSum += localPos * data.number;
            totalWeight += data.number;

            _cells.Remove(cell);
        }

        if (totalWeight > 0f)
            orphanRb.centerOfMass = new Vector3(weightedSum.x / totalWeight, weightedSum.y / totalWeight, 0f);

        // 분리 조각은 삭제하지 않고 물리 오브젝트로 남겨 둔다.
        // 떨어지며 메인 타워에 다시 얹히거나, 충격으로 타워를 무너뜨릴 수 있다.
        if (Application.isPlaying)
        {
            var detached = new DetachedComponent
            {
                root = orphanGO,
                rb = orphanRb,
                detachedAt = Time.time,
                scorePenalty = Mathf.RoundToInt(totalWeight)
            };
            _detachedComponents.Add(detached);
            StartCoroutine(TryReattachDetachedComponent(detached));
        }
    }

    float LocalColliderSize()
    {
        return blockBodyScale > 0.001f ? blockColliderScale / blockBodyScale : blockColliderScale;
    }

    IEnumerator TryReattachDetachedComponent(DetachedComponent detached)
    {
        float stable = 0f;

        while (!detached.resolved && detached.root != null && detached.rb != null && !_isGameOver)
        {
            stable = IsDetachedStable(detached.rb) ? stable + Time.deltaTime : 0f;

            bool canTryReattach = Time.time - detached.detachedAt >= detachedMinAirTime &&
                                  stable >= detachedReattachStableTime;
            if (canTryReattach && TryAbsorbDetachedComponent(detached.root, detached.rb))
            {
                detached.resolved = true;
                _detachedComponents.Remove(detached);
                yield break;
            }

            if (Time.time - detached.detachedAt >= detachedPenaltyDelay)
            {
                ApplyDetachedPenalty(detached);
                yield break;
            }

            yield return null;
        }
    }

    void RefreshDetachedComponents()
    {
        for (int i = _detachedComponents.Count - 1; i >= 0; i--)
        {
            var detached = _detachedComponents[i];
            if (detached.root == null || detached.rb == null)
            {
                _detachedComponents.RemoveAt(i);
                continue;
            }
            if (detached.resolved)
            {
                _detachedComponents.RemoveAt(i);
                continue;
            }

            if (CanTryReattach(detached) && TryAbsorbDetachedComponent(detached.root, detached.rb))
            {
                detached.resolved = true;
                _detachedComponents.RemoveAt(i);
                continue;
            }

            if (Time.time - detached.detachedAt >= detachedPenaltyDelay)
                ApplyDetachedPenalty(detached);
        }
    }

    void ApplyDetachedPenalty(DetachedComponent detached)
    {
        if (detached.resolved) return;
        detached.resolved = true;
        var scorePosition = detached.root != null ? detached.root.transform.position : transform.position;
        _detachedComponents.Remove(detached);
        if (detached.root != null)
            Destroy(detached.root);
        AddScore(-Mathf.Max(0, detached.scorePenalty), scorePosition);
    }

    HashSet<Vector2Int> CollectStableDetachedCells()
    {
        var cells = new HashSet<Vector2Int>();
        foreach (var detached in _detachedComponents)
        {
            if (detached.root == null || detached.rb == null) continue;
            if (!IsDetachedStable(detached.rb)) continue;

            foreach (Transform child in detached.root.transform)
            {
                if (TryWorldToGridCell(child.position, out var cell))
                    cells.Add(cell);
            }
        }
        return cells;
    }

    bool IsDetachedStable(Rigidbody rb)
    {
        return rb.linearVelocity.sqrMagnitude <= detachedReattachVelocity * detachedReattachVelocity &&
               rb.angularVelocity.sqrMagnitude <= detachedReattachVelocity * detachedReattachVelocity;
    }

    bool CanTryReattach(DetachedComponent detached)
    {
        return detached.root != null &&
               detached.rb != null &&
               Time.time - detached.detachedAt >= detachedMinAirTime &&
               IsDetachedStable(detached.rb);
    }

    bool TryWorldToGridCell(Vector3 worldPosition, out Vector2Int cell)
    {
        var local = _towerRoot.InverseTransformPoint(worldPosition);
        cell = new Vector2Int(
            Mathf.RoundToInt(local.x - 0.5f),
            Mathf.RoundToInt(local.y - 0.5f));

        int minGridX = Mathf.Min(placementMin.x, _extractionMinCol);
        int maxGridX = Mathf.Max(placementMax.x, _extractionMaxCol);
        return cell.x >= minGridX && cell.x <= maxGridX && cell.y >= 0;
    }

    bool TryAbsorbDetachedComponent(GameObject detachedRoot, Rigidbody detachedRb)
    {
        var children = new List<Transform>();
        foreach (Transform child in detachedRoot.transform)
            children.Add(child);
        if (children.Count == 0) return false;

        var attach = new List<(Transform child, Vector2Int cell)>(children.Count);
        var duplicates = new List<Transform>();
        var used = new HashSet<Vector2Int>();
        foreach (var child in children)
        {
            if (!TryWorldToGridCell(child.position, out var cell)) return false;
            if (_cells.ContainsKey(cell) || !used.Add(cell))
            {
                duplicates.Add(child);
                continue;
            }
            attach.Add((child, cell));
        }
        if (attach.Count == 0 && duplicates.Count == 0) return false;
        if (!HasDetachedFaceContact(used)) return false;

        detachedRb.isKinematic = true;
        foreach (var duplicate in duplicates)
            Destroy(duplicate.gameObject);

        foreach (var item in attach)
        {
            var child = item.child;
            var cell = item.cell;
            child.SetParent(_towerRoot, worldPositionStays: false);
            child.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
            child.localRotation = Quaternion.identity;

            var sr = child.GetComponent<SpriteRenderer>();
            var label = child.GetComponentInChildren<TextMeshPro>();
            var blockCell = child.GetComponent<BlockCell>();
            var data = new CellData
            {
                number = Mathf.Max(1, blockCell?.Weight is float w ? Mathf.RoundToInt(w) : 1),
                isOriginalTower = blockCell != null && blockCell.IsOriginalTower,
                go = child.gameObject,
                sr = sr,
                outline = child.Find("FocusOutline")?.GetComponent<SpriteRenderer>(),
                label = label
            };
            _cells[cell] = data;
            ApplyCellVisual(cell);
        }

        Destroy(detachedRoot);
        UpdateTowerPhysicsState();
        UpdateExtractionTowerRowsFromCells();
        if (!_isHolding)
        {
            ShowExtractionCameraView(immediate: true);
            FocusDefaultExtractionCell();
        }
        return true;
    }

    // ── 메인 타워 무게 중심 ───────────────────────────────────────────────

    bool HasDetachedFaceContact(HashSet<Vector2Int> detachedCells)
    {
        foreach (var cell in detachedCells)
        {
            foreach (var neighbor in Neighbors(cell))
            {
                if (detachedCells.Contains(neighbor)) continue;
                if (_cells.ContainsKey(neighbor))
                    return true;
            }
        }
        return false;
    }

    Vector3 CalculateCenterOfMass()
    {
        float   totalWeight = 0f;
        Vector2 weightedSum = Vector2.zero;
        foreach (var (cell, data) in _cells)
        {
            var localPos = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            weightedSum += localPos * data.number;
            totalWeight += data.number;
        }
        var com = totalWeight > 0f
            ? weightedSum / totalWeight
            : new Vector2(columns * 0.5f, rows * 0.5f);
        return new Vector3(com.x, com.y, 0f);
    }

    void UpdateTowerPhysicsState()
    {
        if (!Application.isPlaying || _rb == null || _cells.Count == 0) return;

        var centerOfMass = CalculateCenterOfMass();
        _rb.centerOfMass = centerOfMass;
        _rb.WakeUp();
        ApplyToppleTorqueIfUnsupported(centerOfMass);
    }

    void ApplyToppleTorqueIfUnsupported(Vector3 centerOfMass)
    {
        if (toppleTorque <= 0f) return;
        if (!TryGetLowestSupportRange(out float supportMinX, out float supportMaxX)) return;

        float torqueSign = 0f;
        if (centerOfMass.x < supportMinX - toppleMargin)
            torqueSign = 1f;
        else if (centerOfMass.x > supportMaxX + toppleMargin)
            torqueSign = -1f;

        if (Mathf.Approximately(torqueSign, 0f)) return;

        _rb.AddTorque(Vector3.forward * torqueSign * toppleTorque, ForceMode.Impulse);
    }

    bool TryGetLowestSupportRange(out float minX, out float maxX)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        int minY = int.MaxValue;

        foreach (var cell in _cells.Keys)
            minY = Mathf.Min(minY, cell.y);

        if (minY == int.MaxValue) return false;

        foreach (var cell in _cells.Keys)
        {
            if (cell.y != minY) continue;
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x + 1f);
        }

        return minX <= maxX;
    }

    // ── 유틸리티 ─────────────────────────────────────────────────────────

    bool IsAdjacentToSelected(Vector2Int cell)
    {
        foreach (var s in _selected)
            if (Mathf.Abs(cell.x - s.x) + Mathf.Abs(cell.y - s.y) == 1)
                return true;
        return false;
    }

    bool IsConnected(List<Vector2Int> cells)
    {
        if (cells.Count <= 1) return true;
        var set     = new HashSet<Vector2Int>(cells);
        var visited = new HashSet<Vector2Int> { cells[0] };
        var queue   = new Queue<Vector2Int>();
        queue.Enqueue(cells[0]);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var n in Neighbors(c))
                if (set.Contains(n) && visited.Add(n))
                    queue.Enqueue(n);
        }
        return visited.Count == cells.Count;
    }

    static IEnumerable<Vector2Int> Neighbors(Vector2Int c)
    {
        yield return new Vector2Int(c.x + 1, c.y);
        yield return new Vector2Int(c.x - 1, c.y);
        yield return new Vector2Int(c.x, c.y + 1);
        yield return new Vector2Int(c.x, c.y - 1);
    }

    // ── 타워 초기화 ───────────────────────────────────────────────────────

    void BuildTower()
    {
        GameObject towerRootGO;
        if (towerRootTransform != null)
        {
            towerRootGO = towerRootTransform.gameObject;
        }
        else
        {
            towerRootGO = new GameObject("TowerRoot");
            MarkGeneratedObject(towerRootGO);
            towerRootGO.transform.SetParent(transform);
        }
        towerRootGO.transform.position = new Vector3(-columns * 0.5f, -rows * 0.5f, 0f);
        _towerRoot = towerRootGO.transform;

        if (Application.isPlaying)
        {
            if (!towerRootGO.TryGetComponent(out _rb))
                _rb = towerRootGO.AddComponent<Rigidbody>();
            _rb.useGravity             = true;
            _rb.isKinematic            = false;
            _rb.interpolation          = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.linearDamping          = 0.3f;
            _rb.angularDamping         = 2f;
            _rb.constraints            = RigidbodyConstraints.FreezePositionZ
                                       | RigidbodyConstraints.FreezeRotationX
                                       | RigidbodyConstraints.FreezeRotationY;
        }

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                int number = Random.Range(2, 7);
                var cell   = new Vector2Int(col, row);

                _cells[cell] = SpawnCell(cell, number, isOriginalTower: true);
            }
        }

        if (Application.isPlaying)
            _rb.centerOfMass = CalculateCenterOfMass();

        UpdateExtractionTowerRowsFromCells();
        CreateFloor();
        CreateScoreLabel();
        RollBonusTarget();

        CreateBoundaries();

        if (Application.isPlaying)
            CreateGameOverScreen();

        FitCamera();
    }

    CellData SpawnCell(Vector2Int cell, int number, bool isOriginalTower)
    {
        var go = new GameObject($"Cell_{cell.x}_{cell.y}");
        MarkGeneratedObject(go);
        go.transform.SetParent(_towerRoot, false);
        go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        go.transform.localScale = Vector3.one * blockBodyScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = CreateBlockSprite();
        sr.sortingOrder = 0;

        var box = go.AddComponent<BoxCollider>();
        box.size           = Vector3.one * LocalColliderSize();
        box.sharedMaterial = CreateFrictionMaterial();

        var bc = go.AddComponent<BlockCell>();
        bc.Weight = number;
        bc.IsOriginalTower = isOriginalTower;

        var outline = SpawnCellOutline(go.transform);
        var label = SpawnLabel(number, go.transform);
        var data = new CellData
        {
            number = number,
            isOriginalTower = isOriginalTower,
            go = go,
            sr = sr,
            outline = outline,
            label = label
        };
        UpdateCellDataVisuals(data);
        return data;
    }

    void UpdateCellDataVisuals(CellData data)
    {
        var blockCell = data.go.GetComponent<BlockCell>();
        if (blockCell != null)
        {
            blockCell.Weight = data.number;
            blockCell.IsOriginalTower = data.isOriginalTower;
        }

        if (data.label != null)
            data.label.text = data.number.ToString();
        if (data.sr != null)
            data.sr.color = data.isOriginalTower ? NumberColor(data.number) : PlacedNumberColor(data.number);
    }

    // ── 바닥 ─────────────────────────────────────────────────────────────

    void CreateFloor()
    {
        float floorY     = _floorY = -rows * 0.5f - 1.5f;
        float floorWidth = columns + 4f;

        GameObject floorGO;
        if (floorTransform != null)
        {
            floorGO = floorTransform.gameObject;
        }
        else
        {
            floorGO = new GameObject("Floor");
            MarkGeneratedObject(floorGO);
            floorGO.transform.SetParent(transform);
            _generatedFloor = floorGO;
        }
        floorGO.transform.position   = new Vector3(0f, floorY, 0f);
        floorGO.transform.localScale = new Vector3(floorWidth, 1f, 1f);

        if (!floorGO.TryGetComponent<SpriteRenderer>(out var sr))
            sr = floorGO.AddComponent<SpriteRenderer>();
        sr.sprite = CreateBlockSprite();
        sr.color  = new Color(0.2f, 0.2f, 0.2f, 1f);

        if (Application.isPlaying)
        {
            if (!floorGO.TryGetComponent<BoxCollider>(out var col))
                col = floorGO.AddComponent<BoxCollider>();
            col.size           = Vector3.one;
            col.sharedMaterial = CreateFrictionMaterial();
        }
    }

    // ── 경계선 ────────────────────────────────────────────────────────────

    void CreateBoundaries()
    {
        float lineHeight  = 50f;
        float lineHalfH   = lineHeight * 0.5f;
        float lineWidth   = 0.15f;
        float offsetX     = columns * 0.5f + 2f;
        float centerY     = _floorY + lineHalfH;
        var   lineColor   = new Color(1f, 0.25f, 0.25f, 0.7f);

        _leftBoundary  = SpawnBoundary("BoundaryLeft",  new Vector3(-offsetX, centerY, 0f), lineWidth, lineHeight, lineColor);
        _rightBoundary = SpawnBoundary("BoundaryRight", new Vector3( offsetX, centerY, 0f), lineWidth, lineHeight, lineColor);
    }

    GameObject SpawnBoundary(string name, Vector3 worldPos, float width, float height, Color color)
    {
        var go = new GameObject(name);
        MarkGeneratedObject(go);
        go.transform.SetParent(transform);
        go.transform.position   = worldPos;
        go.transform.localScale = new Vector3(width, height, 1f);

        var sr    = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateBlockSprite();
        sr.color  = color;
        sr.sortingOrder = 1;

        if (Application.isPlaying)
        {
            var rb         = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var col       = go.AddComponent<BoxCollider>();
            col.size      = Vector3.one;
            col.isTrigger = true;

            var bl = go.AddComponent<BoundaryLine>();
            bl.OnBlockTouched = TriggerGameOver;
        }

        return go;
    }

    // ── 게임오버 화면 ────────────────────────────────────────────────────

    void CreateGameOverScreen()
    {
        var go = new GameObject("GameOverScreen");
        MarkGeneratedObject(go);
        go.transform.SetParent(transform);
        _gameOverScreen = go.AddComponent<GameOverScreen>();
    }

    // ── 스코어 라벨 ──────────────────────────────────────────────────────

    void CreateScoreLabel()
    {
        BindBuilderHud();
        if (_builderScoreValue != null) { UpdateScoreDisplay(); return; }
        if (scoreLabel != null) { UpdateScoreDisplay(); return; }
    }

    // ── 레이블 ────────────────────────────────────────────────────────────

    void RollBonusTarget()
    {
        if (!_bonusQueueInitialized)
        {
            _bonusTargetPreset = RandomBonusPreset();
            _nextBonusTargetPreset = RandomBonusPreset();
            _thirdBonusTargetPreset = RandomBonusPreset();
            _bonusQueueInitialized = true;
        }
        else
        {
            _bonusTargetPreset = _nextBonusTargetPreset;
            _nextBonusTargetPreset = _thirdBonusTargetPreset;
            _thirdBonusTargetPreset = RandomBonusPreset();
        }
        CreateOrUpdateBonusPreview();
    }

    TetrominoPreset RandomBonusPreset()
    {
        return (TetrominoPreset)Random.Range(0, 7);
    }

    void CreateOrUpdateBonusPreview()
    {
        BindBuilderHud();
        if (_builderBonusCells != null)
        {
            UpdateBuilderBonusPreview();
            return;
        }
    }

    void UpdateBonusPreviewPosition()
    {
        if (_builderBonusPreview != null) return;
        if (_bonusPreviewRoot == null) return;

        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        var pos = cam.ViewportToWorldPoint(new Vector3(0.76f, 0.78f, -cam.transform.position.z));
        _bonusPreviewRoot.transform.position = new Vector3(pos.x, pos.y, 0f);
        _bonusPreviewRoot.transform.localScale = Vector3.one;
    }

    void UpdateBuilderBonusPreview()
    {
        if (_builderBonusCells == null) return;
        UpdateBuilderBonusPreviewSlot(_builderBonusCells, _builderBonusCellElements, _bonusTargetPreset);
        if (_builderBonusNextCells != null)
            UpdateBuilderBonusPreviewSlot(_builderBonusNextCells, _builderBonusNextCellElements, _nextBonusTargetPreset);
        if (_builderBonusThirdCells != null)
            UpdateBuilderBonusPreviewSlot(_builderBonusThirdCells, _builderBonusThirdCellElements, _thirdBonusTargetPreset);
        SetHudLabelTextOnly(_builderBonusKeyLabel, PresetKeyText(_bonusTargetPreset));
        SetHudLabelTextOnly(_builderBonusNextKeyLabel, PresetKeyText(_nextBonusTargetPreset));
        SetHudLabelTextOnly(_builderBonusThirdKeyLabel, PresetKeyText(_thirdBonusTargetPreset));
        _builderBonusPreviewNeedsRefresh = false;
    }

    void UpdateBuilderBonusPreviewSlot(VisualElement container, List<VisualElement> elements, TetrominoPreset preset)
    {
        if (container == null) return;

        var cells = GetTetrominoPresetCells(Vector2Int.zero, preset);
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var cell in cells)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxX = Mathf.Max(maxX, cell.x);
            maxY = Mathf.Max(maxY, cell.y);
        }

        float cellSize = 60f;
        float width = (maxX - minX + 1) * cellSize;
        float height = (maxY - minY + 1) * cellSize;
        float containerWidth = Mathf.Max(ResolvedOrDefault(container.resolvedStyle.width, 240f), width);
        float containerHeight = Mathf.Max(ResolvedOrDefault(container.resolvedStyle.height, 180f), height);
        float offsetX = (containerWidth - width) * 0.5f;
        float offsetY = (containerHeight - height) * 0.5f;
        container.style.position = Position.Relative;

        var color = TetrominoColor(preset);
        for (int i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            if (i >= cells.Count)
            {
                element.style.display = DisplayStyle.None;
                continue;
            }

            var cell = cells[i];
            element.style.display = DisplayStyle.Flex;
            element.style.position = Position.Absolute;
            element.style.left = offsetX + (cell.x - minX) * cellSize;
            element.style.top = offsetY + (maxY - cell.y) * cellSize;
            element.style.width = cellSize;
            element.style.height = cellSize;
            element.style.backgroundColor = color;
        }
    }

    float ResolvedOrDefault(float value, float fallback)
    {
        return float.IsNaN(value) || value <= 0.01f ? fallback : value;
    }

    string PresetKeyText(TetrominoPreset preset) => preset switch
    {
        TetrominoPreset.I => "Q",
        TetrominoPreset.J => "W",
        TetrominoPreset.L => "E",
        TetrominoPreset.O => "R",
        TetrominoPreset.S => "A",
        TetrominoPreset.T => "S",
        TetrominoPreset.Z => "D",
        _ => string.Empty
    };

    SpriteRenderer SpawnCellOutline(Transform parent)
    {
        var go = new GameObject("FocusOutline");
        MarkGeneratedObject(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        go.transform.localScale = Vector3.one * selectedOutlineScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateOutlineSprite();
        sr.color = selectedOutlineColor;
        sr.sortingOrder = 3;
        sr.enabled = false;
        return sr;
    }

    TextMeshPro SpawnLabel(int number, Transform parent)
    {
        var go = new GameObject("Label");
        MarkGeneratedObject(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var tmp  = go.AddComponent<TextMeshPro>();
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.one;

        tmp.text             = number.ToString();
        tmp.fontSize         = 4f;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = Color.white;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Overflow;

        var r = go.GetComponent<Renderer>();
        if (r != null) r.sortingOrder = 2;

        return tmp;
    }

    // ── 카메라 ────────────────────────────────────────────────────────────

    void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        FitCameraToGridRows(0, rows - 1, extractionViewPadding, immediate: true);
    }

    void FitCameraToGridRows(int minGridY, int maxGridY, float padding, bool immediate)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float aspect = (float)Screen.width / Screen.height;

        // 가로: 열 + 여백
        float halfW        = columns * 0.5f + 3.5f;
        float sizeForWidth = halfW / aspect;

        float bottom  = _towerRoot.position.y + minGridY - padding;
        float top     = _towerRoot.position.y + maxGridY + 1f + padding;
        float centerY = (bottom + top) * 0.5f;
        float halfH   = (top - bottom) * 0.5f;

        _cameraTargetY = centerY;
        _cameraTargetSize = Mathf.Max(sizeForWidth, halfH);
        if (immediate)
        {
            cam.orthographicSize = _cameraTargetSize;
            cam.transform.position = new Vector3(0f, _cameraTargetY, cam.transform.position.z);
            _hasCameraTarget = false;
            return;
        }

        _hasCameraTarget = true;
    }

    void FocusCameraOnTowerTop()
    {
        ShowPlacementCameraView(immediate: false);
    }

    void ShowExtractionCameraView(bool immediate)
    {
        FitCameraToGridRows(_extractionMinRow, _extractionMaxRow, extractionViewPadding, immediate);
    }

    void ShowPlacementCameraView(bool immediate)
    {
        FocusCameraOnGridY(HighestOccupiedRow() + 1, immediate, placementViewTopOffset);
    }

    bool TryFindTowerBodyRows(bool ignoreLastPlaced, bool originalTowerOnly, out int minY, out int maxY)
    {
        minY = int.MaxValue;
        maxY = int.MinValue;
        bool found = false;

        foreach (var cell in _cells.Keys)
        {
            if (ignoreLastPlaced && _lastPlacedCells.Contains(cell)) continue;
            if (!IsExtractableCell(cell)) continue;
            if (originalTowerOnly && !IsInExtractionTowerRows(cell)) continue;

            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
            found = true;
        }

        return found;
    }

    bool IsInExtractionTowerRows(Vector2Int cell)
    {
        return cell.x >= _extractionMinCol && cell.x <= _extractionMaxCol &&
               cell.y >= _extractionMinRow && cell.y <= _extractionMaxRow;
    }

    void FocusCameraOnCell(Vector2Int cell)
    {
        FocusCameraOnGridY(cell.y);
    }

    void FocusCameraOnGridY(int gridY)
    {
        FocusCameraOnGridY(gridY, immediate: false, padding: cameraTopPadding);
    }

    void FocusCameraOnHeldCenter()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic || _towerRoot == null) return;

        float worldY = _towerRoot.position.y + _heldBaseCell.y + _heldCenter.y;
        float minY = _floorY + cam.orthographicSize;
        _cameraTargetY = Mathf.Max(minY, worldY);
        _cameraTargetSize = cam.orthographicSize;
        _hasCameraTarget = true;
    }

    void FocusCameraOnGridY(int gridY, bool immediate, float padding)
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float worldY = _towerRoot.position.y + gridY + 0.5f;
        float minY = _floorY + cam.orthographicSize;
        _cameraTargetY = Mathf.Max(minY, worldY + padding);
        _cameraTargetSize = cam.orthographicSize;
        if (immediate)
        {
            var pos = cam.transform.position;
            cam.orthographicSize = _cameraTargetSize;
            cam.transform.position = new Vector3(pos.x, _cameraTargetY, pos.z);
            _hasCameraTarget = false;
            return;
        }

        _hasCameraTarget = true;
    }

    void UpdateCameraTarget()
    {
        if (!_hasCameraTarget) return;

        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        var pos = cam.transform.position;
        float t = 1f - Mathf.Exp(-cameraFocusSpeed * Time.deltaTime);
        float nextY = Mathf.Lerp(pos.y, _cameraTargetY, t);
        float nextSize = _cameraTargetSize > 0f
            ? Mathf.Lerp(cam.orthographicSize, _cameraTargetSize, t)
            : cam.orthographicSize;
        cam.orthographicSize = nextSize;
        cam.transform.position = new Vector3(pos.x, nextY, pos.z);

        bool reachedY = Mathf.Abs(nextY - _cameraTargetY) < 0.01f;
        bool reachedSize = _cameraTargetSize <= 0f || Mathf.Abs(nextSize - _cameraTargetSize) < 0.01f;
        if (reachedY && reachedSize)
            _hasCameraTarget = false;
    }

    int HighestOccupiedRow()
    {
        int top = 0;
        foreach (var cell in _cells.Keys)
            top = Mathf.Max(top, cell.y);
        return top;
    }

    void UpdateExtractionTowerRowsFromCells()
    {
        bool foundOriginalTowerCell = false;
        foreach (var data in _cells.Values)
        {
            if (data.isOriginalTower)
            {
                foundOriginalTowerCell = true;
                break;
            }
        }

        if (!foundOriginalTowerCell)
        {
            _extractionMinCol = 0;
            _extractionMaxCol = columns - 1;
            _extractionMinRow = 0;
            _extractionMaxRow = rows - 1;
            return;
        }

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;
        foreach (var cell in _cells.Keys)
        {
            if (!IsExtractableCell(cell)) continue;

            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        _extractionMinCol = minX;
        _extractionMaxCol = maxX;
        _extractionMinRow = minY;
        _extractionMaxRow = maxY;
    }

    // ── 스프라이트 ────────────────────────────────────────────────────────

    PhysicsMaterial CreateFrictionMaterial()
    {
        var mat             = new PhysicsMaterial("BlockFriction");
        mat.dynamicFriction = blockFriction;
        mat.staticFriction  = blockFriction;
        mat.bounciness      = 0f;
        return mat;
    }

    Sprite CreateBlockSprite()
    {
        if (_blockSprite != null) return _blockSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.DontSave;
        tex.filterMode = FilterMode.Point;

        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % size, y = i / size;
            bool border = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
            pixels[i] = border ? new Color(0f, 0f, 0f, 0.3f) : Color.white;
        }
        tex.SetPixels(pixels);
        tex.Apply();

        _blockSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                                     new Vector2(0.5f, 0.5f), size);
        _blockSprite.hideFlags = HideFlags.DontSave;
        return _blockSprite;
    }

    Sprite CreateOutlineSprite()
    {
        if (_outlineSprite != null) return _outlineSprite;

        const int size = 32;
        const int thickness = 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.DontSave;
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

        _outlineSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                                       new Vector2(0.5f, 0.5f), size);
        _outlineSprite.hideFlags = HideFlags.DontSave;
        return _outlineSprite;
    }

    // ── 기즈모 ────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        // TowerRoot 기준 오프셋 계산
        var origin = new Vector3(-columns * 0.5f, -rows * 0.5f, 0f);

        float x0 = origin.x + placementMin.x;
        float y0 = origin.y + placementMin.y;
        float x1 = origin.x + placementMax.x + 1f;
        float y1 = origin.y + placementMax.y + 1f;

        // 반투명 채우기
        Gizmos.color = new Color(1f, 0.92f, 0.02f, 0.08f);
        Gizmos.DrawCube(
            new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f),
            new Vector3(x1 - x0, y1 - y0, 0.01f));

        // 테두리
        Gizmos.color = new Color(1f, 0.92f, 0.02f, 0.9f);
        Gizmos.DrawLine(new Vector3(x0, y0), new Vector3(x1, y0));
        Gizmos.DrawLine(new Vector3(x1, y0), new Vector3(x1, y1));
        Gizmos.DrawLine(new Vector3(x1, y1), new Vector3(x0, y1));
        Gizmos.DrawLine(new Vector3(x0, y1), new Vector3(x0, y0));
    }

    static Color NumberColor(int n) => n switch
    {
        1 => new Color(0.95f, 0.43f, 0.68f),
        2 => new Color(0.93f, 0.27f, 0.27f),
        3 => new Color(0.26f, 0.65f, 0.96f),
        4 => new Color(0.22f, 0.80f, 0.45f),
        5 => new Color(0.98f, 0.73f, 0.15f),
        6 => new Color(0.72f, 0.38f, 0.92f),
        _ => Color.gray
    };

    static Color PlacedNumberColor(int n)
    {
        float t = Mathf.InverseLerp(1f, 6f, Mathf.Clamp(n, 1, 6));
        return Color.Lerp(new Color32(160, 160, 160, 255), new Color32(50, 50, 50, 255), t);
    }

    static Color TetrominoColor(TetrominoPreset preset) => preset switch
    {
        TetrominoPreset.I => new Color(0f, 0.85f, 0.90f),
        TetrominoPreset.J => new Color(0.05f, 0.13f, 0.95f),
        TetrominoPreset.L => new Color(0.95f, 0.60f, 0.05f),
        TetrominoPreset.O => new Color(0.90f, 0.95f, 0.02f),
        TetrominoPreset.S => new Color(0.10f, 0.95f, 0.15f),
        TetrominoPreset.T => new Color(0.65f, 0.12f, 0.95f),
        TetrominoPreset.Z => new Color(0.95f, 0.05f, 0.04f),
        _ => Color.white
    };
}
