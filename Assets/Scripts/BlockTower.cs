using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class BlockTower : MonoBehaviour
{
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
    [SerializeField] bool usePlacementZoneObject = true;
    [SerializeField] Transform placementZoneTransform;

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
    [SerializeField] BlockNumberSpriteSetAsset numberSpriteSetAsset;
    [SerializeField, HideInInspector] BlockNumberSpriteSet numberSpriteSet;
    [SerializeField] BonusTetrominoSpriteSet bonusTetrominoSpriteSet;

    [Header("Keyboard Controls")]
    [SerializeField] bool keyboardControlsEnabled = true;
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

    [Header("Special Blocks")]
    [SerializeField] Sprite bombObscureSprite;
    [SerializeField] Color bombObscureColor = new(0.45f, 0.45f, 0.45f, 0.92f);
    [SerializeField] int goldFishDeadlineScore = 20;

    [Header("Score")]
    [SerializeField] int targetScore = 30;

    [Header("Initial Tower Numbers")]
    [SerializeField] int initialTowerNumberTotal = 160;
    [SerializeField] bool randomizeInitialTowerNumbersOnPlay = true;

    [Header("Scene References (optional)")]
    [SerializeField] Transform   towerRootTransform;
    [SerializeField] Transform   floorTransform;
    [SerializeField] TextMeshPro scoreLabel;
    [SerializeField] UIDocument  hudDocument;
    [SerializeField] VisualTreeAsset hudVisualTree;
    [SerializeField] VisualTreeAsset gameOverVisualTree;
    [SerializeField] VisualTreeAsset clearVisualTree;
    [SerializeField] PanelSettings hudPanelSettings;

    Transform      _towerRoot;
    Rigidbody      _rb;
    Sprite         _blockSprite;
    GameObject     _generatedFloor;
    GameObject     _generatedScoreLabel;
    GameObject     _leftBoundary;
    GameObject     _rightBoundary;
    GameObject     _towerStackDivider;
    GameObject     _placementZoneObject;
    GameOverScreen _gameOverScreen;
    GameOverScreen _clearScreen;
    GameObject     _bonusPreviewRoot;
    GameObject     _presetOutlineRoot;
    Label          _builderScoreTitle;
    Label          _builderScoreValue;
    Label          _builderTargetScoreValue;
    Label          _builderScorePopupText;
    Label          _resultTitleLabel;
    Label          _resultCurrentScoreLabel;
    Label          _resultTargetScoreLabel;
    Label          _builderBonusPreviewTitle;
    Label          _builderBonusKeyLabel;
    Label          _builderBonusNextKeyLabel;
    Label          _builderBonusThirdKeyLabel;
    VisualElement  _builderHudScorePanel;
    VisualElement  _builderHudTargetScorePanel;
    VisualElement  _builderBonusPreview;
    VisualElement  _builderBonusBackground;
    VisualElement  _builderBonusCells;
    VisualElement  _builderBonusNextCells;
    VisualElement  _builderBonusThirdCells;
    VisualElement  _builderSecondaryViewPanel;
    VisualElement  _builderSecondaryViewImage;
    VisualElement  _builderBlockWeightGuide;
    readonly VisualElement[] _builderWeightGuideImages = new VisualElement[6];
    TextMeshProUGUI _canvasScoreText;
    TextMeshProUGUI _canvasTargetScoreText;
    TextMeshProUGUI _canvasFloatingScoreText;
    TextMeshProUGUI _canvasBonusKeyText;
    TextMeshProUGUI _canvasBonusNextKeyText;
    TextMeshProUGUI _canvasBonusThirdKeyText;
    readonly List<VisualElement> _builderBonusCellElements = new();
    readonly List<VisualElement> _builderBonusNextCellElements = new();
    readonly List<VisualElement> _builderBonusThirdCellElements = new();
    readonly HashSet<GameObject> _generatedObjects = new();
    readonly List<RectInt> _placementExclusions = new();
    readonly List<GameObject> _placementZoneFillObjects = new();
    string         _placementZoneVisualSignature;
    float          _placementZoneTopLimitLocal = float.NaN;
    bool           _builderBonusPreviewNeedsRefresh;
    Coroutine      _builderScorePopupRoutine;
    Coroutine      _canvasFloatingScoreRoutine;
    PanelSettings  _runtimeHudPanelSettings;
    Font           _runtimeHudFont;
    int            _score;
    bool           _isGameOver;
    Sprite         _outlineSprite;
    TetrominoPreset _bonusTargetPreset;
    TetrominoPreset _nextBonusTargetPreset;
    TetrominoPreset _thirdBonusTargetPreset;
    bool           _bonusQueueInitialized;
    readonly List<TetrominoPreset> _bonusPresetBag = new();
    int            _bonusPresetBagIndex;
    bool           _freezePlacementZoneVisuals;
    bool           _initialTowerNumbersRandomizedThisPlay;
    bool           _showingResultHud;

    // ── 셀 데이터 ─────────────────────────────────────────────────────────
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
        public BlockCell.CellKind kind;
        public bool concealedByBomb;
    }

    readonly Dictionary<Vector2Int, CellData> _cells    = new();
    readonly Dictionary<Vector2Int, CellData> _iceCells = new();
    readonly List<Vector2Int>                  _selected = new();
    readonly HashSet<Vector2Int>               _lastPlacedCells = new();
    Vector2                                    _lastPlacementCenter;
    bool                                       _hasLastPlacementCenter;
    Vector2                                    _lastExtractionCenter;
    readonly List<DetachedComponent>           _detachedComponents = new();

    class DetachedComponent
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
        public BlockCell.CellKind kind;
    }

    readonly HashSet<Vector2Int> _bombConcealedCells = new();
    readonly Dictionary<Vector2Int, GameObject> _bombObscureBlocks = new();
    readonly Dictionary<Vector2Int, BlockNumberSpriteSetAsset.BombObscureKind> _bombObscureKinds = new();

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
        MigrateSecondaryViewDefaults();
        BindBuilderHud();
        if (!Application.isPlaying)
        {
            EnsureEditorSceneObjectsVisible();
            return;
        }

        if (Application.isPlaying)
        {
            _initialTowerNumbersRandomizedThisPlay = false;
            if (_cells.Count > 0)
            {
                SetupRuntimeSceneObjectsAfterTowerRestore();
                return;
            }
            if (TryRestoreRuntimeStateFromScene())
            {
                SetupRuntimeSceneObjectsAfterTowerRestore();
                return;
            }
        }
        RebuildEmptyTowerOnly();
        EnsureSecondaryViewObjects();
        UpdateSecondaryViewCamera();
    }

    void SetupRuntimeSceneObjectsAfterTowerRestore()
    {
        RandomizeInitialTowerNumbersIfNeeded();
        foreach (var pair in _cells)
            UpdateCellDataVisuals(pair.Value);
        RenameTowerCellsSequentially();
        EnsurePlacementZoneObjectVisible();
        SyncPlacementZoneFromObject();
        CreateFloor();
        CreateBoundaries();
        CreateScoreLabel();
        CreateGameOverScreen();
        FitCamera();
        EnsureSecondaryViewObjects();
        UpdateSecondaryViewCamera();
    }

    void OnDisable()
    {
        if (secondaryViewCamera != null && secondaryViewCamera.targetTexture == _secondaryViewTexture)
            secondaryViewCamera.targetTexture = null;
        if (secondaryViewImage != null && secondaryViewImage.texture == _secondaryViewTexture)
            secondaryViewImage.texture = null;
        if (_secondaryViewTexture != null)
        {
            _secondaryViewTexture.Release();
            if (Application.isPlaying)
                Destroy(_secondaryViewTexture);
            else
                DestroyImmediate(_secondaryViewTexture);
            _secondaryViewTexture = null;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
            return;

        MigrateSecondaryViewDefaults();
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
        MigrateSecondaryViewDefaults();
        var root = towerRootTransform != null ? towerRootTransform : transform.Find("TowerRoot");
        if (root != null)
        {
            _towerRoot = root;
            PruneOverlappingSceneCells(root);
            if (_cells.Count == 0)
                TryRestoreRuntimeStateFromScene();
            EnsurePlacementZoneObjectVisible();
            SyncPlacementZoneFromObject();
            CreateFloor();
            CreateBoundaries();
            EnsureSecondaryViewObjects();
            return;
        }

        EnsurePlacementZoneObjectVisible();
        EnsureSecondaryViewObjects();
    }

    bool BindBuilderHud()
    {
        if (_showingResultHud)
            return false;

        if (hudDocument == null)
            hudDocument = GetComponent<UIDocument>();
        if (hudDocument == null)
            hudDocument = FindAnyObjectByType<UIDocument>();
        if (hudDocument == null)
            return false;

        EnsureHudPanelSettings();
        EnsureHudVisualTree();
        if (hudDocument.rootVisualElement == null)
            return false;

        var root = hudDocument.rootVisualElement;
        _builderScoreTitle = root.Q<Label>("ScoreTitle");
        _builderScoreValue = root.Q<Label>("ScoreValue");
        _builderTargetScoreValue = root.Q<Label>("TargetScoreValue");
        _builderScorePopupText = root.Q<Label>("ScorePopupText");
        _builderHudScorePanel = root.Q<VisualElement>("HudScorePanel");
        _builderHudTargetScorePanel = root.Q<VisualElement>("HudTargetScorePanel");
        _builderBonusPreviewTitle = root.Q<Label>("BonusPreviewTitle");
        _builderBonusKeyLabel = root.Q<Label>("BonusPreview1Key");
        _builderBonusNextKeyLabel = root.Q<Label>("BonusPreview2Key");
        _builderBonusThirdKeyLabel = root.Q<Label>("BonusPreview3Key");
        _builderBonusPreview = root.Q<VisualElement>("BonusPreview");
        _builderBonusBackground = root.Q<VisualElement>("BonusPreviewBackground");
        _builderBonusCells = root.Q<VisualElement>("BonusPreview1Cells") ?? root.Q<VisualElement>("BonusPreviewCells");
        _builderBonusNextCells = root.Q<VisualElement>("BonusPreview2Cells");
        _builderBonusThirdCells = root.Q<VisualElement>("BonusPreview3Cells");
        _builderSecondaryViewPanel = root.Q<VisualElement>("SubCameraPreviewPanel");
        _builderSecondaryViewImage = root.Q<VisualElement>("SubCameraPreviewImage");
        _builderBlockWeightGuide = root.Q<VisualElement>("BlockWeightGuide");
        for (int i = 0; i < _builderWeightGuideImages.Length; i++)
            _builderWeightGuideImages[i] = root.Q<VisualElement>($"WeightGuideImage{i + 1}");
        EnsureRuntimeHudElements(root);
        _builderBonusCellElements.Clear();
        _builderBonusCells?.Query<VisualElement>(className: "bonus-preview-cell")
            .ForEach(cell => _builderBonusCellElements.Add(cell));
        _builderBonusNextCellElements.Clear();
        _builderBonusNextCells?.Query<VisualElement>(className: "bonus-preview-cell")
            .ForEach(cell => _builderBonusNextCellElements.Add(cell));
        _builderBonusThirdCellElements.Clear();
        _builderBonusThirdCells?.Query<VisualElement>(className: "bonus-preview-cell")
            .ForEach(cell => _builderBonusThirdCellElements.Add(cell));

        SyncBlockWeightGuideImages();
        ApplyHudTextStyles();
        _builderBonusPreviewNeedsRefresh = _builderBonusCells != null;
        if (_builderBonusCells != null)
            root.schedule.Execute(UpdateBuilderBonusPreview).StartingIn(0);
        return _builderScoreValue != null || _builderBonusCells != null;
    }

    void ApplyHudTextStyles()
    {
        ForceVisibleHudLabel(_builderScoreTitle, "SCORE");
        ForceVisibleHudLabel(_builderScoreValue, _score.ToString());
        ForceVisibleHudLabel(_builderTargetScoreValue, targetScore.ToString());
        ForceVisibleHudLabel(_builderScorePopupText, _builderScorePopupText?.text ?? string.Empty);
        ForceVisibleHudLabel(_builderBonusPreviewTitle, "NEXT");
        ForceVisibleBonusKeyLabel(_builderBonusKeyLabel, PresetKeyText(_bonusTargetPreset));
        ForceVisibleBonusKeyLabel(_builderBonusNextKeyLabel, PresetKeyText(_nextBonusTargetPreset));
        ForceVisibleBonusKeyLabel(_builderBonusThirdKeyLabel, PresetKeyText(_thirdBonusTargetPreset));
    }

    void EnsureRuntimeHudElements(VisualElement root)
    {
        if (root == null) return;

        root.style.display = DisplayStyle.Flex;
        root.style.visibility = Visibility.Visible;
        root.style.opacity = 1f;
        root.style.position = Position.Relative;
        root.pickingMode = PickingMode.Ignore;

        _builderHudScorePanel ??= CreateHudPanel(root, "HudScorePanel");
        _builderHudTargetScorePanel ??= CreateHudPanel(root, "HudTargetScorePanel");
        _builderBlockWeightGuide ??= CreateHudPanel(root, "BlockWeightGuide");

        if (_builderScoreValue == null)
            _builderScoreValue = CreateHudLabel(_builderHudScorePanel, "ScoreValue", "0");
        if (_builderTargetScoreValue == null)
            _builderTargetScoreValue = CreateHudLabel(_builderHudTargetScorePanel, "TargetScoreValue", targetScore.ToString());
        if (_builderScorePopupText == null)
            _builderScorePopupText = CreateHudLabel(_builderHudScorePanel, "ScorePopupText", string.Empty);

        for (int i = 0; i < _builderWeightGuideImages.Length; i++)
        {
            _builderWeightGuideImages[i] ??= _builderBlockWeightGuide.Q<VisualElement>($"WeightGuideImage{i + 1}");
            if (_builderWeightGuideImages[i] == null)
            {
                _builderWeightGuideImages[i] = new VisualElement { name = $"WeightGuideImage{i + 1}" };
                _builderBlockWeightGuide.Add(_builderWeightGuideImages[i]);
            }
        }

        PreserveBuilderHudPanel(_builderHudScorePanel);
        PreserveBuilderHudPanel(_builderHudTargetScorePanel);
        PreserveBuilderHudPanel(_builderBlockWeightGuide);
    }

    VisualElement CreateHudPanel(VisualElement root, string name)
    {
        var panel = new VisualElement { name = name };
        panel.pickingMode = PickingMode.Ignore;
        root.Add(panel);
        return panel;
    }

    Label CreateHudLabel(VisualElement parent, string name, string text)
    {
        var label = new Label(text) { name = name };
        label.pickingMode = PickingMode.Ignore;
        parent?.Add(label);
        return label;
    }

    void ForceVisibleHudPanel(VisualElement panel, float width, float height)
    {
        if (panel == null) return;

        panel.style.display = DisplayStyle.Flex;
        panel.style.visibility = Visibility.Visible;
        panel.style.opacity = 1f;
        panel.style.position = Position.Absolute;
        panel.style.width = width;
        panel.style.height = height;
        panel.pickingMode = PickingMode.Ignore;
    }

    void PreserveBuilderHudPanel(VisualElement panel)
    {
        if (panel == null) return;

        panel.style.display = DisplayStyle.Flex;
        panel.style.visibility = Visibility.Visible;
        panel.style.opacity = 1f;
        panel.pickingMode = PickingMode.Ignore;
    }

    void StyleHudScorePanel()
    {
        PreserveBuilderHudPanel(_builderHudScorePanel);
        if (_builderHudScorePanel == null) return;

        if (_builderScoreValue != null)
            SetHudLabelTextOnly(_builderScoreValue, _score.ToString());
        if (_builderScorePopupText != null)
        {
            _builderScorePopupText.style.display = string.IsNullOrEmpty(_builderScorePopupText.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
    }

    void StyleHudTargetScorePanel()
    {
        PreserveBuilderHudPanel(_builderHudTargetScorePanel);
        if (_builderHudTargetScorePanel == null) return;

        if (_builderTargetScoreValue != null)
            SetHudLabelTextOnly(_builderTargetScoreValue, targetScore.ToString());
    }

    void StyleBlockWeightGuidePanel()
    {
        PreserveBuilderHudPanel(_builderBlockWeightGuide);
        if (_builderBlockWeightGuide == null) return;
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
    }

    void ForceVisibleHudLabel(Label label, string text)
    {
        if (label == null) return;

        label.text = text;
        label.style.display = DisplayStyle.Flex;
        label.style.visibility = Visibility.Visible;
        label.style.opacity = 1f;
        label.pickingMode = PickingMode.Ignore;
    }

    void ForceVisibleBonusKeyLabel(Label label, string text)
    {
        if (label == null) return;

        label.text = text;
        label.style.display = DisplayStyle.Flex;
        label.style.visibility = Visibility.Visible;
        label.style.opacity = 1f;
        label.pickingMode = PickingMode.Ignore;
    }

    void BindCanvasHudText()
    {
        if (_canvasScoreText == null)
            _canvasScoreText = FindCanvasText("Score");
        if (_canvasTargetScoreText == null)
            _canvasTargetScoreText = FindCanvasText("Target Score");
        if (_canvasFloatingScoreText == null)
        {
            _canvasFloatingScoreText = FindCanvasText("Floating Score") ?? FindCanvasText("Floating Score ");
            if (_canvasFloatingScoreText != null)
            {
                _canvasFloatingScoreText.gameObject.name = "Floating Score";
                _canvasFloatingScoreText.text = string.Empty;
                _canvasFloatingScoreText.alpha = 1f;
                _canvasFloatingScoreText.gameObject.SetActive(false);
            }
        }

        RemoveCanvasBonusKeyTexts();
    }

    TextMeshProUGUI FindCanvasText(string objectName)
    {
        foreach (var text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (text == null || text.name != objectName) continue;
            if (text.gameObject.scene != gameObject.scene) continue;
            if (HasParentNamed(text.transform, "HudTextFallbackCanvas")) continue;
            return text;
        }
        return null;
    }

    bool HasParentNamed(Transform target, string parentName)
    {
        for (var t = target; t != null; t = t.parent)
            if (t.name == parentName)
                return true;
        return false;
    }

    void UpdateCanvasHudText()
    {
        if (_canvasScoreText != null)
            _canvasScoreText.text = _score.ToString();
        if (_canvasTargetScoreText != null)
            _canvasTargetScoreText.text = targetScore.ToString();
    }

    void RemoveLegacyHudTextFallback()
    {
        if (!Application.isPlaying) return;

        DestroyNamedSceneObject("HudTextFallbackCanvas");
        DestroyNamedSceneObject("ScoreTitleText");
        DestroyNamedSceneObject("ScoreValueText");
        DestroyNamedSceneObject("TargetScoreTitleText");
        DestroyNamedSceneObject("TargetScoreValueText");
        RemoveCanvasBonusKeyTexts();
    }

    void DisableLegacyCanvasHudObjects()
    {
        if (!Application.isPlaying) return;

        DisableNamedSceneObject("SubCameraPreviewPanel");
        DisableNamedSceneObject("block weight guide");
        DisableNamedSceneObject("Block Weight Guide");
        DisableCanvasPanelObjects();
    }

    void DisableCanvasPanelObjects()
    {
        foreach (var panel in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (panel == null || panel.name != "Panel") continue;
            if (panel.gameObject.scene != gameObject.scene) continue;
            if (panel.GetComponentInParent<Canvas>(true) == null) continue;
            panel.gameObject.SetActive(false);
        }
    }

    void DisableNamedSceneObject(string objectName)
    {
        var target = FindSceneObjectByName(objectName);
        if (target == null) return;
        if (_builderSecondaryViewPanel != null && target.name == _builderSecondaryViewPanel.name) return;
        target.gameObject.SetActive(false);
    }

    void RemoveCanvasBonusKeyTexts()
    {
        _canvasBonusKeyText = null;
        _canvasBonusNextKeyText = null;
        _canvasBonusThirdKeyText = null;

        if (!Application.isPlaying) return;

        DestroyNamedSceneObject("BonusPreview1KeyText");
        DestroyNamedSceneObject("BonusPreview2KeyText");
        DestroyNamedSceneObject("BonusPreview3KeyText");
    }

    Canvas FindSceneCanvas()
    {
        Canvas fallback = null;
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (canvas == null) continue;
            if (canvas.gameObject.scene != gameObject.scene) continue;
            if (canvas.name == "HudTextFallbackCanvas") continue;
            if (!canvas.gameObject.activeInHierarchy || !canvas.enabled) continue;
            if (canvas.name == "Canvas")
                return canvas;
            fallback ??= canvas;
        }
        return fallback;
    }

    void DestroyNamedSceneObject(string objectName)
    {
        foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform == null || transform.name != objectName) continue;
            if (transform.gameObject.scene != gameObject.scene) continue;
            Destroy(transform.gameObject);
        }
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
        if (hudDocument == null)
            return;

        if (hudDocument.panelSettings == null)
        {
            if (hudPanelSettings != null)
            {
                hudDocument.panelSettings = hudPanelSettings;
            }
            else
            {
                if (_runtimeHudPanelSettings == null)
                {
                    _runtimeHudPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                    _runtimeHudPanelSettings.name = "RuntimeHudPanelSettings";
                }
                hudDocument.panelSettings = _runtimeHudPanelSettings;
            }
        }

        if (hudDocument.panelSettings != null)
        {
            hudDocument.panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            hudDocument.panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            hudDocument.panelSettings.sortingOrder = 40000000;
        }

        hudDocument.sortingOrder = 40000000;
        hudDocument.enabled = true;
    }

    void EnsureHudVisualTree()
    {
        if (hudDocument == null || hudVisualTree == null)
            return;

        if (hudDocument.visualTreeAsset != hudVisualTree)
            hudDocument.visualTreeAsset = hudVisualTree;
    }

    VisualTreeAsset LoadResultVisualTree(VisualTreeAsset assignedAsset, string assetPath)
    {
        if (assignedAsset != null)
            return assignedAsset;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
#else
        return null;
#endif
    }

    void RestoreRuntimeHudDocument()
    {
        _showingResultHud = false;
        if (hudDocument == null)
            hudDocument = GetComponent<UIDocument>() ?? FindAnyObjectByType<UIDocument>();
        EnsureHudPanelSettings();
        EnsureHudVisualTree();
        _resultTitleLabel = null;
        _resultCurrentScoreLabel = null;
        _resultTargetScoreLabel = null;
        BindBuilderHud();
    }

    bool ShowResultHud(VisualTreeAsset assignedAsset, string assetPath, string title, int score, int target, System.Action onRestart)
    {
        if (hudDocument == null)
            hudDocument = GetComponent<UIDocument>() ?? FindAnyObjectByType<UIDocument>();
        if (hudDocument == null)
            return false;

        EnsureHudPanelSettings();
        var tree = LoadResultVisualTree(assignedAsset, assetPath);
        if (tree == null)
            return false;

        _showingResultHud = true;
        hudDocument.visualTreeAsset = tree;
        hudDocument.enabled = true;

        var root = hudDocument.rootVisualElement;
        if (root == null)
            return false;

        root.style.display = DisplayStyle.Flex;
        root.style.visibility = Visibility.Visible;
        root.style.opacity = 1f;
        _resultTitleLabel = root.Q<Label>("Title");
        _resultCurrentScoreLabel = root.Q<Label>("CurrentScore");
        _resultTargetScoreLabel = root.Q<Label>("TargetScore");

        if (_resultTitleLabel != null)
            _resultTitleLabel.text = title;
        if (_resultCurrentScoreLabel != null)
            _resultCurrentScoreLabel.text = $"SCORE: {score}";
        if (_resultTargetScoreLabel != null)
            _resultTargetScoreLabel.text = $"TARGET: {target}";

        var restartButton = root.Q<UnityEngine.UIElements.Button>("RestartButton");
        if (restartButton != null)
        {
            restartButton.clicked -= Rebuild;
            restartButton.clicked += () => onRestart?.Invoke();
        }

        return true;
    }

    bool TryRestoreRuntimeStateFromScene()
    {
        var root = towerRootTransform != null ? towerRootTransform : transform.Find("TowerRoot");
        if (root == null) return false;

        var restored = new Dictionary<Vector2Int, CellData>();
        var restoredIce = new Dictionary<Vector2Int, CellData>();
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

            bool isIce = blockCell.Kind == BlockCell.CellKind.Ice;
            if (isIce)
            {
                SnapIceCellTransform(child, root, cell);
                if (restoredIce.ContainsKey(cell))
                    continue;
            }
            else
            {
                if (restored.ContainsKey(cell))
                    continue;

                if (Application.isPlaying && child.parent != root)
                    AttachSceneCellToTowerRoot(child, root);
            }

            var outline = isIce ? null : child.Find("FocusOutline")?.GetComponent<SpriteRenderer>();
            if (!isIce && outline == null)
                outline = SpawnCellOutline(child);
            if (outline != null)
                outline.enabled = false;

            var label = child.GetComponentInChildren<TextMeshPro>();
            if (label == null)
                label = SpawnLabel(Mathf.Max(1, Mathf.RoundToInt(blockCell.Weight)), child);
            var box = child.GetComponent<BoxCollider>();
            if (box == null)
                box = child.gameObject.AddComponent<BoxCollider>();
            box.size = Vector3.one * LocalColliderSize();
            box.sharedMaterial = CreateFrictionMaterial();
            box.enabled = Application.isPlaying;

            if (Application.isPlaying && isIce)
                MakeIceCellStatic(child);

            var data = new CellData
            {
                number = Mathf.Max(1, Mathf.RoundToInt(blockCell.Weight)),
                isOriginalTower = blockCell.IsOriginalTower,
                go = child.gameObject,
                sr = sr,
                numberSpriteRenderer = child.Find("NumberSprite")?.GetComponent<SpriteRenderer>(),
                outline = outline,
                label = label,
                kind = blockCell.Kind,
                concealedByBomb = _bombConcealedCells.Contains(cell)
            };

            if (isIce)
                restoredIce[cell] = data;
            else
                restored[cell] = data;
        }

        if (restored.Count == 0 && restoredIce.Count == 0)
            return false;

        _towerRoot = root;
        _rb = root.GetComponent<Rigidbody>();
        ConfigureTowerRigidbody();
        _cells.Clear();
        _iceCells.Clear();
        foreach (var pair in restored)
            _cells[pair.Key] = pair.Value;
        foreach (var pair in restoredIce)
            _iceCells[pair.Key] = pair.Value;

        RandomizeInitialTowerNumbersIfNeeded();

        foreach (var pair in _cells)
        {
            UpdateCellDataVisuals(pair.Value);
        }
        foreach (var pair in _iceCells)
            UpdateCellDataVisuals(pair.Value);
        RenameTowerCellsSequentially();

        _selected.Clear();
        _lastPlacedCells.Clear();
        _hasLastPlacementCenter = false;
        _detachedComponents.Clear();
        _bombConcealedCells.Clear();
        _bombObscureBlocks.Clear();
        _bombObscureKinds.Clear();
        _heldRelPos.Clear();
        _heldData.Clear();
        _heldSourceCells.Clear();
        _isHolding = false;
        _isGameOver = false;
        _freezePlacementZoneVisuals = false;
        _hasFocusedCell = false;
        _presetSelectionActive = false;
        _usingKeyboardPlacement = false;
        ResetMoveRepeat();
        _hasCameraTarget = false;
        _score = 0;
        _heldStartScore = 0;
        _heldMatchesBonus = false;
        _bonusQueueInitialized = false;
        ResetBonusPresetBag();
        HideResultScreens();

        if (_generatedFloor == null) { var f = FindSceneObjectByName("Floor"); if (f) _generatedFloor = f.gameObject; }
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
            var sprite = GetIceSprite();
            sr.sprite = CreateBlockSprite();
            sr.color = sprite != null ? Color.clear : IceBlockColor();
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingOrder = Mathf.Max(sr.sortingOrder, 1);
            if (sprite != null)
            {
                var overlay = EnsureStandaloneNumberSpriteRenderer(ice);
                overlay.sprite = sprite;
                overlay.enabled = true;
                overlay.color = Color.white;
                overlay.sortingOrder = sr.sortingOrder + 1;
                FitNumberSpriteToCell(overlay);
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
        if (!Application.isPlaying || !randomizeInitialTowerNumbersOnPlay || _initialTowerNumbersRandomizedThisPlay || _cells.Count == 0)
            return;

        var cells = new List<CellData>();
        foreach (var data in _cells.Values)
            if (data.kind == BlockCell.CellKind.Normal)
                cells.Add(data);
        int count = cells.Count;
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
            cells[i].number = numbers[i];
        _initialTowerNumbersRandomizedThisPlay = true;
    }

    void RenameTowerCellsSequentially()
    {
        var orderedCells = new List<KeyValuePair<Vector2Int, CellData>>(_cells);
        orderedCells.Sort((a, b) =>
        {
            int yCompare = a.Key.y.CompareTo(b.Key.y);
            return yCompare != 0 ? yCompare : a.Key.x.CompareTo(b.Key.x);
        });

        for (int i = 0; i < orderedCells.Count; i++)
        {
            var go = orderedCells[i].Value.go;
            if (go != null)
                go.name = $"Cell({i + 1})";
        }
    }

    void RebuildEmptyTowerOnly()
    {
        if (TryRestoreRuntimeStateFromScene())
        {
            EnsurePlacementZoneObjectVisible();
            SyncPlacementZoneFromObject();
            CreateFloor();
            CreateBoundaries();

            if (Application.isPlaying)
            {
                CreateScoreLabel();
                CreateGameOverScreen();
                FitCamera();
            }
            return;
        }

        if (_heldRoot != null) { DestroyLocal(_heldRoot); _heldRoot = null; }
        _heldRelPos.Clear();
        _heldData.Clear();
        _heldSourceCells.Clear();
        ClearGenerated();
        _cells.Clear();
        _iceCells.Clear();
        _selected.Clear();
        _lastPlacedCells.Clear();
        _hasLastPlacementCenter = false;
        _detachedComponents.Clear();
        _isHolding  = false;
        _isGameOver = false;
        _freezePlacementZoneVisuals = false;
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
        ResetBonusPresetBag();
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
        ClearDetachedBlocks();

        if (_towerRoot == null && towerRootTransform == null)
        {
            var found = transform.Find("TowerRoot");
            if (found != null) _towerRoot = found;
        }
        if (_towerRoot != null)
        {
            if (towerRootTransform == null && IsGeneratedObject(_towerRoot.gameObject) && !HasBlockCellChildren(_towerRoot))
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
            _rb = null;
        }

        if (_generatedFloor == null) { var f = FindSceneObjectByName("Floor"); if (f) _generatedFloor = f.gameObject; }

        if (_generatedScoreLabel == null) { var f = transform.Find("ScoreLabel"); if (f) _generatedScoreLabel = f.gameObject; }
        if (_generatedScoreLabel != null && scoreLabel == null) { DestroyLocal(_generatedScoreLabel); _generatedScoreLabel = null; scoreLabel = null; }

        if (_leftBoundary  == null) { var f = FindSceneObjectByName("BoundaryLeft");  if (f) _leftBoundary  = f.gameObject; }
        if (_rightBoundary == null) { var f = FindSceneObjectByName("BoundaryRight"); if (f) _rightBoundary = f.gameObject; }

        _gameOverScreen = FindSceneObjectByName("GameOverScreen")?.GetComponent<GameOverScreen>();
        _clearScreen = FindSceneObjectByName("ClearScreen")?.GetComponent<GameOverScreen>();
        HideResultScreens();
        if (_presetOutlineRoot == null) { var f = transform.Find("PresetOutlinePreview"); if (f) _presetOutlineRoot = f.gameObject; }
        if (_presetOutlineRoot != null) { DestroyLocal(_presetOutlineRoot); _presetOutlineRoot = null; }
        if (_bonusPreviewRoot == null) { var f = transform.Find("BonusPreview"); if (f) _bonusPreviewRoot = f.gameObject; }
        if (_bonusPreviewRoot != null) { DestroyLocal(_bonusPreviewRoot); _bonusPreviewRoot = null; }
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

    Transform FindChildByNameRecursive(Transform root, string objectName)
    {
        if (root == null) return null;
        if (root.name == objectName) return root;

        foreach (Transform child in root)
        {
            var found = FindChildByNameRecursive(child, objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    Transform FindSceneObjectByName(string objectName)
    {
        var local = FindChildByNameRecursive(transform, objectName);
        if (IsUsableTransform(local)) return local;

        foreach (var candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!IsUsableTransform(candidate)) continue;
            if (candidate.name != objectName) continue;
            if (candidate.gameObject.scene != gameObject.scene) continue;
            return candidate;
        }
        return null;
    }

    Transform FindBlockTowerChild(string objectName)
    {
        var direct = transform.Find(objectName);
        if (IsUsableTransform(direct))
            return direct;
        return FindSceneObjectByName(objectName);
    }

    void ParentToBlockTowerPreserveWorld(Transform child)
    {
        if (child != null && child.parent != transform)
            child.SetParent(transform, worldPositionStays: true);
    }

    bool IsUsableTransform(Transform target)
    {
        if (target == null)
            return false;
        try
        {
            var go = target.gameObject;
            return go != null && go.scene.IsValid();
        }
        catch (MissingReferenceException)
        {
            return false;
        }
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

    void EnsurePlacementZoneObjectVisible()
    {
        if (!usePlacementZoneObject || Application.isPlaying)
            return;

        if (placementZoneTransform == null)
        {
            var existing = FindBlockTowerChild("PlacementZone");
            if (existing != null)
                placementZoneTransform = existing;
        }
        if (placementZoneTransform != null)
        {
            ParentToBlockTowerPreserveWorld(placementZoneTransform);
            _placementZoneObject = placementZoneTransform.gameObject;
            return;
        }

        var zone = new GameObject("PlacementZone");
        float minX = placementMin.x;
        float maxX = placementMax.x + 1f;
        float minY = placementMin.y;
        float maxY = placementMax.y + 1f;
        var zoneLocalCenter = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, -0.04f);
        zone.transform.SetParent(transform, worldPositionStays: true);
        zone.transform.position = TowerGridLocalToWorld(zoneLocalCenter);
        zone.transform.localScale = new Vector3(Mathf.Max(0.1f, maxX - minX), Mathf.Max(0.1f, maxY - minY), 1f);

        var sr = zone.AddComponent<SpriteRenderer>();
        sr.sprite = CreateBlockSprite();
        sr.color = new Color(1f, 0.92f, 0.02f, 0.16f);
        sr.sortingOrder = -5;

        placementZoneTransform = zone.transform;
        _placementZoneObject = zone;
    }

    Vector3 TowerGridLocalToWorld(Vector3 local)
    {
        if (_towerRoot != null)
            return _towerRoot.TransformPoint(local);
        return new Vector3(-columns * 0.5f, -rows * 0.5f, 0f) + local;
    }

    void SyncPlacementZoneFromObject(bool updateVisuals = true)
    {
        _placementExclusions.Clear();
        if (!usePlacementZoneObject)
            return;

        if (placementZoneTransform == null)
        {
            var existing = FindBlockTowerChild("PlacementZone");
            if (existing != null)
                placementZoneTransform = existing;
        }
        if (placementZoneTransform == null || _towerRoot == null)
            return;

        ParentToBlockTowerPreserveWorld(placementZoneTransform);
        _placementZoneObject = placementZoneTransform.gameObject;
        RectInt zoneRect = default;
        if (TryTransformToGridRect(placementZoneTransform, out zoneRect))
        {
            zoneRect = MatchRectToDividerX(zoneRect);
            placementMin = new Vector2Int(zoneRect.xMin, zoneRect.yMin);
            placementMax = new Vector2Int(zoneRect.xMax - 1, zoneRect.yMax - 1);
        }

        ParentPlacementExclusionsToBlockTower();
        foreach (var exclusionTransform in FindPlacementExclusionTransforms())
        {
            if (TryTransformToGridRect(exclusionTransform, out var exclusion))
                _placementExclusions.Add(exclusion);
        }

        if (updateVisuals && zoneRect.width > 0 && zoneRect.height > 0)
            UpdatePlacementZoneVisuals(zoneRect);
    }

    void ParentPlacementExclusionsToBlockTower()
    {
        if (placementZoneTransform == null)
            return;

        var exclusions = new List<Transform>();
        foreach (Transform child in placementZoneTransform)
        {
            if (child == null) continue;
            if (!child.name.Contains("Exclude")) continue;
            exclusions.Add(child);
        }

        foreach (var exclusion in exclusions)
            ParentToBlockTowerPreserveWorld(exclusion);
    }

    List<Transform> FindPlacementExclusionTransforms()
    {
        var exclusions = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child == null || !child.gameObject.activeInHierarchy) continue;
            if (!child.name.Contains("Exclude")) continue;
            exclusions.Add(child);
        }
        return exclusions;
    }

    bool TryTransformToGridRect(Transform source, out RectInt rect)
    {
        rect = default;
        if (!TryGetTowerLocalBounds(source, out var minX, out var maxX, out var minY, out var maxY))
            return false;

        const float gridEpsilon = 0.001f;
        int xMin = Mathf.FloorToInt(minX + gridEpsilon);
        int yMin = Mathf.FloorToInt(minY + gridEpsilon);
        int xMax = Mathf.CeilToInt(maxX - gridEpsilon);
        int yMax = Mathf.CeilToInt(maxY - gridEpsilon);
        if (xMax <= xMin || yMax <= yMin)
            return false;

        rect = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        return true;
    }

    RectInt MatchRectToDividerX(RectInt rect)
    {
        if (rect.width <= 0 || _towerStackDivider == null || _towerRoot == null)
            return rect;

        if (!TryGetTowerLocalBounds(_towerStackDivider.transform, out var dividerMinX, out var dividerMaxX, out _, out _))
            return rect;

        int minX = Mathf.CeilToInt(dividerMinX - 0.001f);
        int maxX = Mathf.FloorToInt(dividerMaxX + 0.001f);
        if (maxX <= minX)
            return new RectInt(minX, rect.yMin, 0, rect.height);

        return new RectInt(minX, rect.yMin, maxX - minX, rect.height);
    }

    void UpdatePlacementZoneVisuals(RectInt zoneRect)
    {
        if (_freezePlacementZoneVisuals)
        {
            SetPlacementZoneFillsActive(false);
            return;
        }

        if (placementZoneTransform == null)
            return;

        var sourceRenderer = placementZoneTransform.GetComponent<SpriteRenderer>();
        if (sourceRenderer == null)
            return;

        zoneRect = MatchRectToDividerX(zoneRect);
        if (zoneRect.width <= 0 || zoneRect.height <= 0)
        {
            sourceRenderer.enabled = false;
            ClearPlacementZoneFillObjects();
            return;
        }

        string signature = PlacementZoneVisualSignature(zoneRect, sourceRenderer);
        if (_placementZoneVisualSignature == signature)
            return;

        ClearPlacementZoneFillObjects();
        _placementZoneVisualSignature = signature;

        sourceRenderer.enabled = false;
        var remaining = new List<RectInt> { zoneRect };
        foreach (var exclusion in _placementExclusions)
            remaining = SubtractRectList(remaining, ClipRect(exclusion, zoneRect));

        int index = 0;
        foreach (var rect in remaining)
        {
            if (rect.width <= 0 || rect.height <= 0) continue;
            var go = new GameObject($"__PlacementZoneFill_{index++}");
            MarkGeneratedObject(go);
            go.transform.SetParent(placementZoneTransform, false);

            var fillWorldCenter = TowerGridLocalToWorld(new Vector3(
                rect.xMin + rect.width * 0.5f,
                rect.yMin + rect.height * 0.5f,
                -0.04f));
            go.transform.position = fillWorldCenter;

            var parentScale = placementZoneTransform.lossyScale;
            go.transform.localScale = new Vector3(
                SafeDivideScale(rect.width, parentScale.x),
                SafeDivideScale(rect.height, parentScale.y),
                SafeDivideScale(1f, parentScale.z));

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sourceRenderer.sprite != null ? sourceRenderer.sprite : CreateBlockSprite();
            renderer.color = sourceRenderer.color;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder;
            _placementZoneFillObjects.Add(go);
        }
    }

    float SafeDivideScale(float value, float scale)
    {
        return Mathf.Abs(scale) > 0.0001f ? value / scale : value;
    }

    string PlacementZoneVisualSignature(RectInt zoneRect, SpriteRenderer sourceRenderer)
    {
        var signature = $"{zoneRect.xMin},{zoneRect.yMin},{zoneRect.width},{zoneRect.height}|{sourceRenderer.color}|{sourceRenderer.sortingOrder}";
        foreach (var exclusion in _placementExclusions)
            signature += $"|{exclusion.xMin},{exclusion.yMin},{exclusion.width},{exclusion.height}";
        return signature;
    }

    void ClearPlacementZoneFillObjects()
    {
        for (int i = _placementZoneFillObjects.Count - 1; i >= 0; i--)
            DestroyPlacementZoneFillObject(_placementZoneFillObjects[i]);
        _placementZoneFillObjects.Clear();

        if (placementZoneTransform == null) return;
        var stale = new List<GameObject>();
        foreach (Transform child in placementZoneTransform)
            if (child != null && child.name.StartsWith("__PlacementZoneFill_"))
                stale.Add(child.gameObject);
        foreach (var go in stale)
            DestroyPlacementZoneFillObject(go);
        _placementZoneVisualSignature = null;
    }

    void SetPlacementZoneFillsActive(bool active)
    {
        foreach (var go in _placementZoneFillObjects)
            if (go != null)
                go.SetActive(active);

        if (placementZoneTransform == null) return;
        foreach (Transform child in placementZoneTransform)
            if (child != null && child.name.StartsWith("__PlacementZoneFill_"))
                child.gameObject.SetActive(active);
        if (_towerRoot != null)
        {
            foreach (Transform child in _towerRoot)
                if (child != null && child.name.StartsWith("__PlacementZoneFill_"))
                    child.gameObject.SetActive(active);
        }
    }

    void DestroyPlacementZoneFillObject(GameObject go)
    {
        if (go == null) return;
        _generatedObjects.Remove(go);
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    void BindResultScreens()
    {
        if (_gameOverScreen == null)
            _gameOverScreen = FindSceneObjectByName("GameOverScreen")?.GetComponent<GameOverScreen>();
        if (_clearScreen == null)
            _clearScreen = FindSceneObjectByName("ClearScreen")?.GetComponent<GameOverScreen>();
    }

    void HideResultScreens()
    {
        RestoreRuntimeHudDocument();
        BindResultScreens();
        _gameOverScreen?.Hide();
        _clearScreen?.Hide();
    }

    void EnsureResultScreenObjectsVisible()
    {
        if (Application.isPlaying)
            return;

        var canvas = FindSceneCanvas();
        if (canvas == null)
            return;

        _gameOverScreen = EnsureResultScreen(canvas.transform, "GameOverScreen", "GAME OVER");
        _clearScreen = EnsureResultScreen(canvas.transform, "ClearScreen", "CLEAR");
    }

    GameOverScreen EnsureResultScreen(Transform canvasTransform, string screenName, string title)
    {
        try
        {
            if (!IsUsableTransform(canvasTransform))
                return null;

            var screenTransform = FindSceneObjectByName(screenName);
            if (!IsUsableTransform(screenTransform))
            {
                var go = new GameObject(screenName);
                go.layer = canvasTransform.gameObject.layer;
                go.transform.SetParent(canvasTransform, false);
                screenTransform = go.transform;
            }

            var rect = screenTransform.GetComponent<RectTransform>();
            if (rect == null)
                rect = screenTransform.gameObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var image = screenTransform.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
                image = screenTransform.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0f, 0f, 0f, 0.72f);
            image.raycastTarget = true;

            var screen = screenTransform.GetComponent<GameOverScreen>();
            if (screen == null)
                screen = screenTransform.gameObject.AddComponent<GameOverScreen>();

            EnsureResultLabel(screenTransform, "Title", title, new Vector2(0f, 150f), 72f, Color.white);
            EnsureResultLabel(screenTransform, "CurrentScore", "SCORE: 0", new Vector2(0f, 45f), 42f, new Color(1f, 0.85f, 0.1f));
            EnsureResultLabel(screenTransform, "TargetScore", $"TARGET: {targetScore}", new Vector2(0f, -20f), 36f, new Color(0.85f, 0.92f, 1f));
            EnsureResultLabel(screenTransform, "RestartHint", "Click to Restart", new Vector2(0f, -115f), 26f, new Color(0.75f, 0.75f, 0.75f));
            screenTransform.gameObject.SetActive(false);
            return screen;
        }
        catch (MissingReferenceException)
        {
            return null;
        }
    }

    TextMeshProUGUI EnsureResultLabel(Transform parent, string objectName, string text, Vector2 position, float fontSize, Color color)
    {
        try
        {
            if (!IsUsableTransform(parent))
                return null;

            Transform labelTransform = null;
            foreach (Transform child in parent)
            {
                if (IsUsableTransform(child) && child.name == objectName)
                {
                    labelTransform = child;
                    break;
                }
            }

            if (!IsUsableTransform(labelTransform))
            {
                var go = new GameObject(objectName);
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                labelTransform = go.transform;
            }

            var rect = labelTransform.GetComponent<RectTransform>();
            if (rect == null)
                rect = labelTransform.gameObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(760f, 100f);

            var label = labelTransform.GetComponent<TextMeshProUGUI>();
            if (label == null)
                label = labelTransform.gameObject.AddComponent<TextMeshProUGUI>();
            if (_canvasScoreText != null && _canvasScoreText.font != null)
                label.font = _canvasScoreText.font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }
        catch (MissingReferenceException)
        {
            return null;
        }
    }

    List<RectInt> SubtractRectList(List<RectInt> sources, RectInt cut)
    {
        if (cut.width <= 0 || cut.height <= 0)
            return sources;

        var result = new List<RectInt>();
        foreach (var source in sources)
            result.AddRange(SubtractRect(source, cut));
        return result;
    }

    IEnumerable<RectInt> SubtractRect(RectInt source, RectInt cut)
    {
        var overlap = ClipRect(cut, source);
        if (overlap.width <= 0 || overlap.height <= 0)
        {
            yield return source;
            yield break;
        }

        if (overlap.yMax < source.yMax)
            yield return new RectInt(source.xMin, overlap.yMax, source.width, source.yMax - overlap.yMax);
        if (overlap.yMin > source.yMin)
            yield return new RectInt(source.xMin, source.yMin, source.width, overlap.yMin - source.yMin);
        if (overlap.xMin > source.xMin)
            yield return new RectInt(source.xMin, overlap.yMin, overlap.xMin - source.xMin, overlap.height);
        if (overlap.xMax < source.xMax)
            yield return new RectInt(overlap.xMax, overlap.yMin, source.xMax - overlap.xMax, overlap.height);
    }

    RectInt ClipRect(RectInt rect, RectInt bounds)
    {
        int xMin = Mathf.Max(rect.xMin, bounds.xMin);
        int yMin = Mathf.Max(rect.yMin, bounds.yMin);
        int xMax = Mathf.Min(rect.xMax, bounds.xMax);
        int yMax = Mathf.Min(rect.yMax, bounds.yMax);
        return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
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

    // ── 스코어 ───────────────────────────────────────────────────────────

    void AddScore(int delta)
    {
        if (_isGameOver) return;
        _score += delta;
        UpdateScoreDisplay();
        if (delta > 0)
            CheckClearCondition();
    }

    void AddScore(int delta, Vector3 worldPosition)
    {
        AddScore(delta);
        SpawnFloatingScoreText(delta);
    }

    public void AwardGoldFishDeadlineScore(Vector3 worldPosition)
    {
        AddScore(goldFishDeadlineScore, worldPosition);
    }

    void UpdateScoreDisplay()
    {
        BindBuilderHud();
        if (_builderScoreValue != null)
            _builderScoreValue.text = _score.ToString();
        if (_builderTargetScoreValue != null)
            _builderTargetScoreValue.text = targetScore.ToString();
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

        if (_canvasFloatingScoreText != null)
        {
            if (_canvasFloatingScoreRoutine != null)
                StopCoroutine(_canvasFloatingScoreRoutine);
            _canvasFloatingScoreRoutine = StartCoroutine(AnimateCanvasFloatingScoreText(delta));
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

    IEnumerator AnimateCanvasFloatingScoreText(int delta)
    {
        float duration = 0.85f;
        float elapsed = 0f;

        _canvasFloatingScoreText.gameObject.name = "Floating Score";
        _canvasFloatingScoreText.text = delta > 0 ? $"+{delta}" : delta.ToString();
        _canvasFloatingScoreText.color = delta > 0 ? Color.white : Color.red;
        _canvasFloatingScoreText.alpha = 1f;
        _canvasFloatingScoreText.gameObject.SetActive(true);

        var rect = _canvasFloatingScoreText.rectTransform;
        var startPos = rect.anchoredPosition;
        while (elapsed < duration && _canvasFloatingScoreText != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = startPos + new Vector2(0f, t * 30f);
            _canvasFloatingScoreText.alpha = 1f - t;
            yield return null;
        }

        if (_canvasFloatingScoreText != null)
        {
            rect.anchoredPosition = startPos;
            _canvasFloatingScoreText.text = string.Empty;
            _canvasFloatingScoreText.alpha = 1f;
            _canvasFloatingScoreText.gameObject.SetActive(false);
        }
        _canvasFloatingScoreRoutine = null;
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

    [Header("Cross View Camera")]
    [SerializeField] Camera secondaryViewCamera;
    [SerializeField] RawImage secondaryViewImage;
    [SerializeField] Vector2 secondaryViewPanelSize = new(540f, 360f);
    [SerializeField] Vector2 secondaryViewPanelPosition = new(40f, 0f);
    [SerializeField] float secondaryViewOrthographicSize = 3f;
    [SerializeField] int secondaryViewTextureSize = 768;
    [SerializeField, HideInInspector] bool secondaryViewDefaultsMigrated;

    float _floorY;
    float _cameraTargetY;
    float _cameraTargetSize;
    bool  _hasCameraTarget;
    RenderTexture _secondaryViewTexture;
    int   _extractionMinCol;
    int   _extractionMaxCol;
    int   _extractionMinRow;
    int   _extractionMaxRow;

    void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateEditorPlacementZonePreview();
            return;
        }
        if (_isGameOver) return;
        SyncPlacementZoneFromObject();
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
                float minY = _floorY + CurrentCameraHalfHeight(cam);
                float newY = Mathf.Max(cam.transform.position.y + Mathf.Sign(scroll) * scrollSpeed, minY);
                cam.transform.position = new Vector3(cam.transform.position.x, newY, cam.transform.position.z);
            }
        }

        UpdateCameraTarget();
        UpdateSecondaryViewCamera();
        if (_builderBonusPreviewNeedsRefresh || _builderBonusCells == null)
            CreateOrUpdateBonusPreview();
        UpdateBonusPreviewPosition();
        UpdatePresetOutlineFeedback();
    }

    void UpdateEditorPlacementZonePreview()
    {
        if (!usePlacementZoneObject || !gameObject.scene.IsValid())
            return;

        _freezePlacementZoneVisuals = false;

        if (_towerRoot == null)
        {
            var root = towerRootTransform != null ? towerRootTransform : transform.Find("TowerRoot");
            if (root != null)
                _towerRoot = root;
        }

        if (placementZoneTransform == null)
        {
            var existing = FindBlockTowerChild("PlacementZone");
            if (existing != null)
                placementZoneTransform = existing;
        }

        if (_towerRoot == null || placementZoneTransform == null)
            return;

        SyncPlacementZoneFromObject();
    }

    // ── 게임오버 ─────────────────────────────────────────────────────────

    void TriggerGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        _freezePlacementZoneVisuals = true;
        SetPlacementZoneFillsActive(false);
        if (_heldRoot != null) { Destroy(_heldRoot); _heldRoot = null; _isHolding = false; }
        ClearDetachedBlocks();
        BindResultScreens();
        _clearScreen?.Hide();
        if (!ShowResultHud(gameOverVisualTree, "Assets/01.Scripts/UI/GameOverScreen.uxml", "GAME OVER", _score, targetScore, Rebuild))
            _gameOverScreen?.ShowGameOver(_score, targetScore, Rebuild);
    }

    void TriggerClear()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        _freezePlacementZoneVisuals = true;
        SetPlacementZoneFillsActive(false);
        if (_heldRoot != null) { Destroy(_heldRoot); _heldRoot = null; _isHolding = false; }
        ClearDetachedBlocks();
        BindResultScreens();
        _gameOverScreen?.Hide();
        if (!ShowResultHud(clearVisualTree, "Assets/01.Scripts/UI/ClearScreen.uxml", "CLEAR", _score, targetScore, Rebuild))
            _clearScreen?.ShowClear(_score, targetScore, Rebuild);
    }

    void CheckClearCondition()
    {
        if (_isGameOver) return;
        if (targetScore > 0 && _score >= targetScore)
            TriggerClear();
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
        bool hasPresetHalfTurn = ReadHalfTurnPressed(keyboard);

        if (!hasMove && !hasConfirm && !hasCancel && !hasPreset && !hasTab && !hasPresetRotate && !hasPresetHalfTurn) return;

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
            HandlePresetSelectionInput(hasMove, dir, hasConfirm, hasPreset, preset, hasTab, hasPresetRotate, hasPresetHalfTurn);
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

        if (ReadHalfTurnPressed(keyboard))
        {
            RotateHeldBlocks(clockwise: true);
            RotateHeldBlocks(clockwise: true);
        }
        else if (ReadHeldRotationPressed(keyboard, out var clockwise))
        {
            RotateHeldBlocks(clockwise);
        }

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

    bool ReadHalfTurnPressed(Keyboard keyboard)
    {
        return keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame;
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
        int maxFocusY = Mathf.Max(PlacementCeilingY(), HighestOccupiedRow());
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
        bool hasPresetRotate,
        bool hasPresetHalfTurn)
    {
        if (hasTab)
        {
            ExitPresetSelectionToFocus();
            return;
        }

        if (hasPreset)
        {
            if (_presetSelectionPreset == preset)
                TrySetPresetSelection(_presetSelectionAnchor, _presetSelectionPreset, (_presetSelectionRotation + 1) % 4);
            else
                TrySetPresetSelection(_presetSelectionAnchor, preset, 0);
            return;
        }

        if (hasPresetHalfTurn)
        {
            TrySetPresetSelection(_presetSelectionAnchor, _presetSelectionPreset, (_presetSelectionRotation + 2) % 4);
            return;
        }

        if (hasPresetRotate)
        {
            TrySetPresetSelection(_presetSelectionAnchor, _presetSelectionPreset, (_presetSelectionRotation + 1) % 4);
            return;
        }

        if (hasMove)
        {
            TrySetPresetSelection(_presetSelectionAnchor + dir, _presetSelectionPreset, _presetSelectionRotation);
        }

        if (hasConfirm)
            ConfirmPresetSelection();
    }

    bool TrySetPresetSelection(Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        if (!PresetOverlapsAnyExtractableCell(anchor, preset, rotation))
        {
            PlayPlacementFailFeedback();
            ApplyPresetSelection();
            return false;
        }

        _presetSelectionAnchor = anchor;
        _presetSelectionPreset = preset;
        _presetSelectionRotation = ((rotation % 4) + 4) % 4;
        ApplyPresetSelection();
        return true;
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
        SetNoPostLayer(_presetOutlineRoot);

        foreach (var cell in cells)
        {
            var go = new GameObject($"PresetOutline_{cell.x}_{cell.y}");
            MarkGeneratedObject(go);
            go.transform.SetParent(_presetOutlineRoot.transform, false);
            SetNoPostLayer(go);
            go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0.04f);
            go.transform.localScale = Vector3.one * selectedOutlineScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateOutlineSprite();
            sr.color = selectedOutlineColor;
            sr.sortingOrder = 5;
        }
    }

    static void SetNoPostLayer(GameObject go)
    {
        if (go == null)
            return;

        int layer = LayerMask.NameToLayer("BlockTowerNoPost");
        if (layer < 0)
            return;

        foreach (var transform in go.GetComponentsInChildren<Transform>(true))
            transform.gameObject.layer = layer;
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

    bool PresetOverlapsAnyExtractableCell(Vector2Int anchor, TetrominoPreset preset, int rotation)
    {
        foreach (var cell in GetTetrominoPresetCells(anchor, preset, rotation))
            if (IsExtractableCell(cell))
                return true;
        return false;
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
        if (data.kind == BlockCell.CellKind.Ice) return;
        _selected.Add(cell);
        ApplyCellVisual(cell);
    }

    bool IsExtractableCell(Vector2Int cell)
    {
        return _cells.TryGetValue(cell, out var data) &&
               data.isOriginalTower &&
               data.kind != BlockCell.CellKind.Ice;
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
        if (data.concealedByBomb)
        {
            if (data.sr != null)
            {
                var visualCell = FindCellForData(data);
                bool hasCustomObscure = visualCell.HasValue && HasBombObscureSprite(visualCell.Value);
                var sprite = visualCell.HasValue ? BombObscureSprite(visualCell.Value) : BombObscureSprite();
                if (hasCustomObscure)
                {
                    ApplyFittedOverlaySprite(data, sprite, data.sr.sortingOrder + 1);
                    data.sr.color = Color.clear;
                }
                else
                {
                    DisableNumberSpriteRenderer(data);
                    data.sr.sprite = sprite;
                    data.sr.color = bombObscureColor;
                }
            }
            ApplyCellOutline(data, false, false);
            return;
        }
        var color = data.isOriginalTower ? NumberColor(data.number) : PlacedNumberColor(data.number);
        if (data.kind == BlockCell.CellKind.Bomb)
            color = Color.black;
        else if (data.kind == BlockCell.CellKind.Ice)
            color = new Color(0.44f, 0.78f, 0.92f, 1f);
        if (isSelected)
            color = Color.Lerp(color, Color.white, 0.45f);
        if (isFocused)
            color = Color.Lerp(color, focusedCellColor, 0.8f);

        bool hasNumberSprite = data.numberSpriteRenderer != null &&
                               data.numberSpriteRenderer.enabled &&
                               data.numberSpriteRenderer.sprite != null;
        if (hasNumberSprite)
        {
            data.sr.color = Color.clear;
            data.numberSpriteRenderer.color = Color.white;
        }
        else if (TryGetSpecialBlockSprite(data.kind, out var specialSprite))
        {
            ApplyFittedOverlaySprite(data, specialSprite, data.sr.sortingOrder + 1);
            data.sr.color = Color.Lerp(Color.clear, color, isSelected || isFocused ? 0.35f : 0f);
        }
        else
        {
            if (data.numberSpriteRenderer != null)
            {
                data.numberSpriteRenderer.enabled = false;
                data.numberSpriteRenderer.sprite = null;
            }
            data.sr.color = color;
        }
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

    void TriggerBombAt(Vector2Int center)
    {
        foreach (var offset in BombBoxOffsets())
        {
            var cell = center + offset;
            _bombConcealedCells.Add(cell);
            _bombObscureKinds[cell] = GetBombObscureKind(offset);
            EnsureBombObscureBlock(cell);
            if (_cells.TryGetValue(cell, out var data))
            {
                data.concealedByBomb = true;
                UpdateCellDataVisuals(data);
                ApplyCellVisual(cell);
            }
        }
    }

    static IEnumerable<Vector2Int> BombBoxOffsets()
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
                yield return new Vector2Int(x, y);
        }
    }

    void EnsureBombObscureBlock(Vector2Int cell)
    {
        if (_bombObscureBlocks.ContainsKey(cell))
            return;

        var go = new GameObject($"BombObscure_{cell.x}_{cell.y}");
        MarkGeneratedObject(go);
        go.transform.SetParent(transform, worldPositionStays: true);
        go.transform.position = TowerGridLocalToWorld(new Vector3(cell.x + 0.5f, cell.y + 0.5f, -0.08f));
        go.transform.localScale = Vector3.one;

        var sr = go.AddComponent<SpriteRenderer>();
        var sprite = BombObscureSprite(cell);
        sr.sprite = sprite;
        sr.color = HasBombObscureSprite(cell) ? Color.white : bombObscureColor;
        FitRendererObjectToCell(sr);
        sr.sortingOrder = 30;
        _bombObscureBlocks[cell] = go;
    }

    Sprite BombObscureSprite()
    {
        return bombObscureSprite != null ? bombObscureSprite : CreateBlockSprite();
    }

    Sprite BombObscureSprite(Vector2Int cell)
    {
        EnsureNumberSpriteSet();
        if (numberSpriteSetAsset != null &&
            _bombObscureKinds.TryGetValue(cell, out var kind))
        {
            var sprite = numberSpriteSetAsset.GetBombObscureSprite(kind);
            if (sprite != null)
                return sprite;
        }

        return BombObscureSprite();
    }

    bool HasBombObscureSprite(Vector2Int cell)
    {
        EnsureNumberSpriteSet();
        return numberSpriteSetAsset != null &&
               _bombObscureKinds.TryGetValue(cell, out var kind) &&
               numberSpriteSetAsset.GetBombObscureSprite(kind) != null;
    }

    BlockNumberSpriteSetAsset.BombObscureKind GetBombObscureKind(Vector2Int offset)
    {
        if (offset == Vector2Int.zero)
            return BlockNumberSpriteSetAsset.BombObscureKind.Center;

        return offset.x == 0 || offset.y == 0
            ? BlockNumberSpriteSetAsset.BombObscureKind.Edge
            : BlockNumberSpriteSetAsset.BombObscureKind.Corner;
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
        _lastExtractionCenter = new Vector2((minX + maxX + 1f) * 0.5f, (minY + maxY + 1f) * 0.5f);
        foreach (var c in _selected)
            _heldRelPos.Add(new Vector2Int(c.x - minX, c.y - minY));
        _heldMatchesBonus = ShapeMatchesPreset(_heldRelPos, _bonusTargetPreset);

        foreach (var pair in _cells)
        {
            _heldSourceCells.Add(new HeldSourceCell
            {
                cell = pair.Key,
                number = pair.Value.number,
                isOriginalTower = pair.Value.isOriginalTower,
                kind = pair.Value.kind
            });
        }

        var changedCells = new List<Vector2Int>();
        var bombCells = new List<Vector2Int>();
        foreach (var cell in _selected)
        {
            if (!_cells.TryGetValue(cell, out var data)) continue;
            changedCells.Add(cell);
            if (data.kind == BlockCell.CellKind.Bomb)
                bombCells.Add(cell);
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
                    bc.Kind = data.kind;
                }

                UpdateCellDataVisuals(data);
            }
        }

        _selected.Clear();
        _hasFocusedCell = false;
        foreach (var cell in changedCells)
            ApplyCellVisual(cell);
        foreach (var bombCell in bombCells)
            TriggerBombAt(bombCell);

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
            FocusCameraOnHeldCenter();
        AddScore(_heldMatchesBonus ? 2 : 1, extractionScorePos);
        RollBonusTarget();
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
        if (_hasLastPlacementCenter)
        {
            var lastBase = new Vector2Int(
                Mathf.RoundToInt(_lastPlacementCenter.x - _heldCenter.x),
                Mathf.RoundToInt(_lastPlacementCenter.y - _heldCenter.y));
            return ClampHeldBase(lastBase);
        }

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
        int placementFloorY = PlacementFloorY();
        int minBaseY = placementFloorY - minRelY;
        int maxBaseY = PlacementCeilingY() - maxRelY;

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
        SyncPlacementZoneFromObject(updateVisuals: false);
        var detachedCells = CollectStableDetachedCells();
        targets = new List<Vector2Int>(_heldRelPos.Count);
        var snappedLocalPos = new Vector3(baseCell.x + _heldCenter.x, baseCell.y + _heldCenter.y, 0f);
        snappedWorldPos = _towerRoot.TransformPoint(snappedLocalPos);

        foreach (var rel in _heldRelPos)
        {
            var target = new Vector2Int(baseCell.x + rel.x, baseCell.y + rel.y);
            if (target.x < placementMin.x || target.x > placementMax.x ||
                target.y < PlacementFloorY() || target.y > PlacementCeilingY()) return false;
            if (IsPlacementExcluded(target)) return false;
            if (detachedCells.Contains(target)) return false;
            if (_cells.TryGetValue(target, out var existing))
            {
                if (existing.kind == BlockCell.CellKind.Ice) return false;
                if (existing.number >= 6) return false;
            }
            targets.Add(target);
        }

        bool adjacent = false;
        foreach (var t in targets)
        {
            if (IsMergeableCell(t))
            {
                adjacent = true;
                break;
            }

            foreach (var n in Neighbors(t))
            {
                if (IsMergeableCell(n) || detachedCells.Contains(n)) { adjacent = true; break; }
            }
            if (adjacent) break;
        }
        if (!adjacent && (_cells.Count > 0 || detachedCells.Count > 0)) return false;

        // 중력 체크: 그룹 내 최소 1개의 블럭이 바닥(row 0) 또는 기존 타워 블럭 위에 놓여야 함
        // (위쪽 블럭에만 붙어서 공중에 배치하는 것 방지)
        bool hasBottomSupport = false;
        foreach (var t in targets)
        {
            if (IsMergeableCell(t) ||
                t.y == PlacementFloorY() ||
                IsMergeableCell(new Vector2Int(t.x, t.y - 1)) ||
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
        return _cells.ContainsKey(cell) || detachedCells.Contains(cell);
    }

    bool IsMergeableCell(Vector2Int cell)
    {
        return _cells.TryGetValue(cell, out var data) &&
               data.kind != BlockCell.CellKind.Ice;
    }

    bool IsPlacementExcluded(Vector2Int cell)
    {
        foreach (var rect in _placementExclusions)
            if (rect.Contains(cell))
                return true;
        return false;
    }

    int PlacementFloorY()
    {
        return Mathf.Max(placementMin.y, _extractionMaxRow + 1);
    }

    int PlacementCeilingY()
    {
        return PlacementFloorY() + Mathf.Max(0, placementMax.y - placementMin.y);
    }

    int LowestHeldRelativeY()
    {
        int minRelY = 0;
        bool found = false;
        foreach (var rel in _heldRelPos)
        {
            minRelY = found ? Mathf.Min(minRelY, rel.y) : rel.y;
            found = true;
        }
        return minRelY;
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
        int minBaseY = PlacementFloorY() - LowestHeldRelativeY();
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
            var data   = _heldData[i];

            ClearPreviewBlur(data);
            if (_cells.TryGetValue(target, out var existing))
            {
                existing.number = Mathf.Min(6, existing.number + 1);
                UpdateCellDataVisuals(existing);
                ApplyCellVisual(target);
                _lastPlacedCells.Add(target);
                if (data.go != null)
                    Destroy(data.go);
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
            if (data.label == null)
                data.label = SpawnLabel(data.number, data.go.transform);

            var bc = data.go.GetComponent<BlockCell>();
            if (bc != null)
            {
                bc.Weight = data.number;
                bc.IsOriginalTower = data.isOriginalTower;
            }

            UpdateCellDataVisuals(data);
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

        foreach (var cell in _cells.Keys)
        {
            if (ignoreLastPlaced && _lastPlacedCells.Contains(cell)) continue;
            if (!IsExtractableCell(cell)) continue;
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
            var data = SpawnCell(source.cell, source.number, source.isOriginalTower);
            data.kind = source.kind;
            data.concealedByBomb = _bombConcealedCells.Contains(source.cell);
            UpdateCellDataVisuals(data);
            _cells[source.cell] = data;
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

        DetachOriginalTowerBlocksSupportedOnlyByTop();

        var components = FindConnectedComponents();
        if (components.Count <= 1) return;

        int mainIdx = FindMainTowerComponentIndex(components);

        for (int i = 0; i < components.Count; i++)
        {
            if (i == mainIdx) continue;
            DetachComponent(components[i]);
        }
    }

    void DetachOriginalTowerBlocksSupportedOnlyByTop()
    {
        var components = FindOriginalTowerComponents();
        if (components.Count <= 1) return;

        int mainIdx = FindMainTowerComponentIndex(components);
        for (int i = 0; i < components.Count; i++)
        {
            if (i == mainIdx) continue;
            if (TouchesPlacedTopBlock(components[i]))
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

    List<List<Vector2Int>> FindOriginalTowerComponents()
    {
        var unvisited = new HashSet<Vector2Int>();
        foreach (var (cell, data) in _cells)
            if (data.isOriginalTower)
                unvisited.Add(cell);

        var components = new List<List<Vector2Int>>();
        while (unvisited.Count > 0)
        {
            var en = unvisited.GetEnumerator();
            en.MoveNext();
            var start = en.Current;
            en.Dispose();

            var component = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            unvisited.Remove(start);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                component.Add(c);
                foreach (var n in Neighbors(c))
                {
                    if (!unvisited.Contains(n)) continue;
                    if (!_cells.TryGetValue(n, out var data) || !data.isOriginalTower) continue;
                    unvisited.Remove(n);
                    queue.Enqueue(n);
                }
            }
            components.Add(component);
        }
        return components;
    }

    bool TouchesPlacedTopBlock(List<Vector2Int> component)
    {
        foreach (var cell in component)
        {
            foreach (var neighbor in Neighbors(cell))
            {
                if (!_cells.TryGetValue(neighbor, out var data)) continue;
                if (!data.isOriginalTower)
                    return true;
            }
        }
        return false;
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
            if (canTryReattach && ApplyIceColumnDamageToDetached(detached))
            {
                detached.resolved = true;
                _detachedComponents.Remove(detached);
                yield break;
            }
            if (canTryReattach && TryAbsorbDetachedComponent(detached.root, detached.rb))
            {
                detached.resolved = true;
                _detachedComponents.Remove(detached);
                yield break;
            }
            if (canTryReattach && TriggerDetachedBombLanding(detached))
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

            bool canTryReattach = CanTryReattach(detached);
            if (canTryReattach && ApplyIceColumnDamageToDetached(detached))
            {
                detached.resolved = true;
                _detachedComponents.RemoveAt(i);
                continue;
            }

            if (canTryReattach && TryAbsorbDetachedComponent(detached.root, detached.rb))
            {
                detached.resolved = true;
                _detachedComponents.RemoveAt(i);
                continue;
            }
            if (canTryReattach && TriggerDetachedBombLanding(detached))
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

    bool TriggerDetachedBombLanding(DetachedComponent detached)
    {
        if (detached == null || detached.root == null)
            return false;

        bool triggered = false;
        foreach (Transform child in detached.root.transform)
        {
            if (child == null) continue;
            var blockCell = child.GetComponent<BlockCell>();
            if (blockCell == null || blockCell.Kind != BlockCell.CellKind.Bomb) continue;
            if (TryWorldToGridCell(child.position, out var cell))
            {
                TriggerBombAt(cell);
                triggered = true;
            }
        }

        if (!triggered)
            return false;

        Destroy(detached.root);
        return true;
    }

    bool ApplyIceColumnDamageToDetached(DetachedComponent detached)
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
            if (!TryWorldToGridCell(child.position, out var cell)) continue;
            if (!TryFindIceBelowInColumn(cell, out var iceCell)) continue;
            if (!damagedIce.Add(iceCell)) continue;
            if (ApplyIceDamageInternal(iceCell, _iceCells[iceCell].go, _iceCells[iceCell].go != null ? _iceCells[iceCell].go.GetComponent<BlockCell>() : null))
                changed = true;
        }

        if (!changed)
            return false;

        detached.iceDamageApplied = true;

        return false;
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
            if (_cells.TryGetValue(cell, out var existing) && existing.kind == BlockCell.CellKind.Ice)
                return false;

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
                kind = blockCell != null ? blockCell.Kind : BlockCell.CellKind.Normal,
                concealedByBomb = _bombConcealedCells.Contains(cell),
                go = child.gameObject,
                sr = sr,
                outline = child.Find("FocusOutline")?.GetComponent<SpriteRenderer>(),
                label = label
            };
            _cells[cell] = data;
            ApplyIceColumnLandingDamage(cell, data);
            if (data.go == null)
                continue;
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
                if (IsMergeableCell(neighbor))
                    return true;
            }
        }
        return false;
    }

    void ApplyIceColumnLandingDamage(Vector2Int landedCell, CellData landedData)
    {
        if (landedData == null || landedData.kind == BlockCell.CellKind.Ice)
            return;

        if (TryFindIceBelowInColumn(landedCell, out var iceCell))
            ApplyIceDamageInternal(iceCell, _iceCells[iceCell].go, _iceCells[iceCell].go != null ? _iceCells[iceCell].go.GetComponent<BlockCell>() : null);
    }

    public bool ApplyIceContactDamage(BlockCell blockCell)
    {
        if (blockCell == null || blockCell.Kind == BlockCell.CellKind.Ice)
            return false;

        Vector2Int? runtimeCell = null;
        foreach (var pair in _cells)
        {
            var data = pair.Value;
            if (data.go == null || data.go != blockCell.gameObject)
                continue;

            runtimeCell = pair.Key;
            break;
        }

        if (runtimeCell.HasValue && _cells.TryGetValue(runtimeCell.Value, out var runtimeData))
        {
            runtimeData.number--;
            if (runtimeData.number <= 0)
            {
                _cells.Remove(runtimeCell.Value);
                Destroy(runtimeData.go);
            }
            else
            {
                UpdateCellDataVisuals(runtimeData);
                ApplyCellVisual(runtimeCell.Value);
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
        if (_towerRoot == null)
            return false;

        var local = _towerRoot.InverseTransformPoint(iceWorldPosition);
        int iceX = Mathf.RoundToInt(local.x - 0.5f);
        int iceY = Mathf.RoundToInt(local.y - 0.5f);

        Vector2Int? targetCell = null;
        int bestY = int.MaxValue;
        foreach (var pair in _cells)
        {
            if (pair.Key.x != iceX || pair.Key.y <= iceY)
                continue;

            if (pair.Key.y < bestY)
            {
                bestY = pair.Key.y;
                targetCell = pair.Key;
            }
        }

        if (!targetCell.HasValue || !_cells.TryGetValue(targetCell.Value, out var data))
            return false;

        data.number--;
        if (data.number <= 0)
        {
            _cells.Remove(targetCell.Value);
            if (data.go != null)
                Destroy(data.go);
        }
        else
        {
            UpdateCellDataVisuals(data);
            ApplyCellVisual(targetCell.Value);
        }
        return true;
    }

    public bool ApplyIceDamage(BlockCell iceBlockCell, Vector3 iceWorldPosition)
    {
        if (iceBlockCell == null || iceBlockCell.Kind != BlockCell.CellKind.Ice)
            return ApplyIceDamageAtWorldPosition(iceWorldPosition);

        Vector2Int? iceCell = null;
        foreach (var pair in _iceCells)
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
        if (_towerRoot == null)
            return false;

        var local = _towerRoot.InverseTransformPoint(iceWorldPosition);
        var cell = new Vector2Int(
            Mathf.RoundToInt(local.x - 0.5f),
            Mathf.RoundToInt(local.y - 0.5f));

        if (_iceCells.TryGetValue(cell, out var data))
            return ApplyIceDamageInternal(cell, data.go, data.go != null ? data.go.GetComponent<BlockCell>() : null);

        return false;
    }

    bool ApplyIceDamageInternal(Vector2Int? iceCell, GameObject iceObject, BlockCell iceBlockCell)
    {
        if (iceObject == null || iceBlockCell == null)
            return false;

        int number = Mathf.Max(1, Mathf.RoundToInt(iceBlockCell.Weight)) - 1;
        if (number <= 0)
        {
            if (iceCell.HasValue)
                _iceCells.Remove(iceCell.Value);
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

        if (iceCell.HasValue && _iceCells.TryGetValue(iceCell.Value, out var data))
        {
            data.number = number;
            UpdateCellDataVisuals(data);
        }

        return true;
    }

    void UpdateLooseBlockNumberVisual(BlockCell blockCell, int number)
    {
        if (blockCell == null)
            return;

        blockCell.Weight = number;

        var label = blockCell.GetComponentInChildren<TextMeshPro>();
        if (label != null)
        {
            label.text = number.ToString();
            label.fontSize = 6f;
            label.gameObject.SetActive(true);
        }

        var sr = blockCell.GetComponent<SpriteRenderer>();
        if (sr == null)
            return;

        EnsureNumberSpriteSet();
        var numberSprite = GetNumberSprite(number);
        var numberRenderer = EnsureStandaloneNumberSpriteRenderer(blockCell.transform);

        if (numberSprite != null)
        {
            sr.color = Color.clear;
            numberRenderer.sprite = numberSprite;
            numberRenderer.enabled = true;
            numberRenderer.color = Color.white;
            numberRenderer.sortingOrder = sr.sortingOrder + 1;
            FitNumberSpriteToCell(numberRenderer);
        }
        else
        {
            if (numberRenderer != null)
            {
                numberRenderer.enabled = false;
                numberRenderer.sprite = null;
            }

            sr.color = blockCell.IsOriginalTower ? NumberColor(number) : PlacedNumberColor(number);
        }
    }

    bool HasIceBelowInColumn(Vector2Int landedCell)
    {
        return TryFindIceBelowInColumn(landedCell, out _);
    }

    bool TryFindIceBelowInColumn(Vector2Int landedCell, out Vector2Int iceCell)
    {
        iceCell = default;
        bool found = false;
        int bestY = int.MinValue;
        foreach (var pair in _iceCells)
        {
            if (pair.Key.x == landedCell.x &&
                pair.Key.y < landedCell.y &&
                pair.Key.y > bestY)
            {
                bestY = pair.Key.y;
                iceCell = pair.Key;
                found = true;
            }
        }

        return found;
    }

    Vector3 CalculateCenterOfMass()
    {
        float   totalWeight = 0f;
        Vector2 weightedSum = Vector2.zero;
        foreach (var (cell, data) in _cells)
        {
            if (data.kind == BlockCell.CellKind.Ice)
                continue;

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

        foreach (var pair in _cells)
        {
            if (pair.Value.kind == BlockCell.CellKind.Ice) continue;
            minY = Mathf.Min(minY, pair.Key.y);
        }

        if (minY == int.MaxValue) return false;

        foreach (var pair in _cells)
        {
            if (pair.Value.kind == BlockCell.CellKind.Ice) continue;
            var cell = pair.Key;
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
        if (TryRestoreRuntimeStateFromScene())
        {
            EnsurePlacementZoneObjectVisible();
            SyncPlacementZoneFromObject();
            CreateFloor();
            CreateBoundaries();

            if (!Application.isPlaying) return;

            CreateScoreLabel();
            CreateGameOverScreen();
            FitCamera();
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
            ConfigureTowerRigidbody();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var cell   = new Vector2Int(col, row);

                _cells[cell] = SpawnCell(cell, 2, isOriginalTower: true);
            }
        }

        RandomizeInitialTowerNumbersIfNeeded();
        foreach (var pair in _cells)
            UpdateCellDataVisuals(pair.Value);
        RenameTowerCellsSequentially();

        if (Application.isPlaying)
            _rb.centerOfMass = CalculateCenterOfMass();

        UpdateExtractionTowerRowsFromCells();
        EnsurePlacementZoneObjectVisible();
        SyncPlacementZoneFromObject();
        CreateFloor();
        CreateBoundaries();

        if (!Application.isPlaying) return;

        CreateScoreLabel();
        RollBonusTarget();
        CreateGameOverScreen();
        FitCamera();
    }

    void ConfigureTowerRigidbody()
    {
        if (!Application.isPlaying || _towerRoot == null)
            return;

        if (!_towerRoot.TryGetComponent(out _rb))
            _rb = _towerRoot.gameObject.AddComponent<Rigidbody>();
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
        bc.Kind = BlockCell.CellKind.Normal;

        var outline = SpawnCellOutline(go.transform);
        var label = SpawnLabel(number, go.transform);
        var data = new CellData
        {
            number = number,
            isOriginalTower = isOriginalTower,
            go = go,
            sr = sr,
            outline = outline,
            label = label,
            kind = BlockCell.CellKind.Normal
        };
        UpdateCellDataVisuals(data);
        return data;
    }

    void UpdateCellDataVisuals(CellData data)
    {
        EnsureNumberSpriteSet();

        if (data.label == null && data.go != null)
            data.label = SpawnLabel(data.number, data.go.transform);

        var blockCell = data.go.GetComponent<BlockCell>();
        if (blockCell != null)
        {
            blockCell.Weight = data.number;
            blockCell.IsOriginalTower = data.isOriginalTower;
            blockCell.Kind = data.kind;
        }

        if (data.label != null)
        {
            data.label.text = data.kind == BlockCell.CellKind.Bomb ? "X" : data.number.ToString();
            data.label.fontSize = 6f;
            data.label.gameObject.SetActive(!data.concealedByBomb);
        }
        if (data.sr != null)
        {
            var numberSprite = GetNumberSprite(data.number);
            if (data.concealedByBomb)
            {
                if (data.numberSpriteRenderer != null)
                {
                    data.numberSpriteRenderer.enabled = false;
                    data.numberSpriteRenderer.sprite = null;
                }
                var cell = FindCellForData(data);
                bool hasCustomObscure = cell.HasValue && HasBombObscureSprite(cell.Value);
                var sprite = cell.HasValue ? BombObscureSprite(cell.Value) : BombObscureSprite();
                if (hasCustomObscure)
                {
                    ApplyFittedOverlaySprite(data, sprite, data.sr.sortingOrder + 1);
                    data.sr.color = Color.clear;
                }
                else
                {
                    DisableNumberSpriteRenderer(data);
                    data.sr.sprite = sprite;
                    data.sr.color = bombObscureColor;
                }
                data.sr.drawMode = SpriteDrawMode.Simple;
            }
            else if (data.kind == BlockCell.CellKind.Bomb)
            {
                var sprite = GetBombSprite();
                if (sprite != null)
                {
                    ApplyFittedOverlaySprite(data, sprite, data.sr.sortingOrder + 1);
                    data.sr.color = Color.clear;
                }
                else
                {
                    DisableNumberSpriteRenderer(data);
                    data.sr.sprite = CreateBlockSprite();
                    data.sr.color = Color.black;
                }
                data.sr.drawMode = SpriteDrawMode.Simple;
            }
            else if (data.kind == BlockCell.CellKind.Ice)
            {
                var sprite = GetIceSprite();
                if (sprite != null)
                {
                    ApplyFittedOverlaySprite(data, sprite, data.sr.sortingOrder + 1);
                    data.sr.color = Color.clear;
                }
                else
                {
                    DisableNumberSpriteRenderer(data);
                    data.sr.sprite = CreateBlockSprite();
                    data.sr.color = IceBlockColor();
                }
                data.sr.drawMode = SpriteDrawMode.Simple;
            }
            else if (data.isOriginalTower && numberSprite != null)
            {
                data.sr.sprite = CreateBlockSprite();
                data.sr.color = Color.clear;
                data.sr.drawMode = SpriteDrawMode.Simple;
                var numberRenderer = EnsureNumberSpriteRenderer(data);
                numberRenderer.sprite = numberSprite;
                numberRenderer.enabled = true;
                numberRenderer.color = Color.white;
                numberRenderer.sortingOrder = data.sr.sortingOrder + 1;
                FitNumberSpriteToCell(numberRenderer);
            }
            else
            {
                if (data.numberSpriteRenderer != null)
                {
                    data.numberSpriteRenderer.enabled = false;
                    data.numberSpriteRenderer.sprite = null;
                }
                data.sr.sprite = CreateBlockSprite();
                data.sr.color = data.isOriginalTower ? NumberColor(data.number) : PlacedNumberColor(data.number);
                data.sr.drawMode = SpriteDrawMode.Simple;
            }
        }
    }

    SpriteRenderer EnsureNumberSpriteRenderer(CellData data)
    {
        data.numberSpriteRenderer = EnsureStandaloneNumberSpriteRenderer(data.go.transform);
        return data.numberSpriteRenderer;
    }

    void ApplyFittedOverlaySprite(CellData data, Sprite sprite, int sortingOrder)
    {
        if (data == null || data.sr == null || sprite == null)
            return;

        data.sr.sprite = CreateBlockSprite();
        data.sr.drawMode = SpriteDrawMode.Simple;
        var overlay = EnsureNumberSpriteRenderer(data);
        overlay.sprite = sprite;
        overlay.enabled = true;
        overlay.color = Color.white;
        overlay.sortingOrder = sortingOrder;
        FitNumberSpriteToCell(overlay);
    }

    void DisableNumberSpriteRenderer(CellData data)
    {
        if (data?.numberSpriteRenderer == null)
            return;

        data.numberSpriteRenderer.enabled = false;
        data.numberSpriteRenderer.sprite = null;
    }

    SpriteRenderer EnsureStandaloneNumberSpriteRenderer(Transform parent)
    {
        var existing = parent.Find("NumberSpriteImage");
        if (existing != null && existing.TryGetComponent<SpriteRenderer>(out var existingRenderer))
            return existingRenderer;

        var old = parent.Find("NumberSprite");
        if (old != null && old.TryGetComponent<SpriteRenderer>(out var oldRenderer))
        {
            oldRenderer.enabled = false;
            oldRenderer.sprite = null;
        }

        var go = new GameObject("NumberSpriteImage");
        MarkGeneratedObject(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        return go.AddComponent<SpriteRenderer>();
    }

    void FitNumberSpriteToCell(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        renderer.drawMode = SpriteDrawMode.Simple;
        var size = renderer.sprite.bounds.size;
        float scaleX = size.x > 0.0001f ? 1f / size.x : 1f;
        float scaleY = size.y > 0.0001f ? 1f / size.y : 1f;
        renderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        renderer.transform.localPosition = Vector3.zero;
    }

    void FitRendererObjectToCell(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        renderer.drawMode = SpriteDrawMode.Simple;
        var size = renderer.sprite.bounds.size;
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

    Sprite GetNumberSprite(int number)
    {
        EnsureNumberSpriteSet();
        return GetNumberSpriteRaw(number);
    }

    Sprite GetNumberSpriteRaw(int number)
    {
        if (numberSpriteSetAsset != null)
        {
            var sprite = numberSpriteSetAsset.GetSprite(number);
            if (sprite != null)
                return sprite;
        }

        return numberSpriteSet != null ? numberSpriteSet.GetSprite(number) : null;
    }

    Sprite GetBombSprite()
    {
        EnsureNumberSpriteSet();
        return numberSpriteSetAsset != null ? numberSpriteSetAsset.BombSprite : null;
    }

    Sprite GetIceSprite()
    {
        EnsureNumberSpriteSet();
        return numberSpriteSetAsset != null ? numberSpriteSetAsset.IceSprite : null;
    }

    bool TryGetSpecialBlockSprite(BlockCell.CellKind kind, out Sprite sprite)
    {
        sprite = kind switch
        {
            BlockCell.CellKind.Bomb => GetBombSprite(),
            BlockCell.CellKind.Ice => GetIceSprite(),
            _ => null
        };

        return sprite != null;
    }

    Vector2Int? FindCellForData(CellData data)
    {
        if (data == null)
            return null;

        foreach (var pair in _cells)
        {
            if (ReferenceEquals(pair.Value, data))
                return pair.Key;
        }

        foreach (var pair in _iceCells)
        {
            if (ReferenceEquals(pair.Value, data))
                return pair.Key;
        }

        return null;
    }

#if UNITY_EDITOR
    BlockNumberSpriteSetAsset FindDefaultNumberSpriteSetAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:BlockNumberSpriteSetAsset");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<BlockNumberSpriteSetAsset>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }
#endif

    void SyncBlockWeightGuideImages()
    {
        if (numberSpriteSetAsset == null && numberSpriteSet == null)
            return;

        for (int number = 1; number <= _builderWeightGuideImages.Length; number++)
        {
            PreserveBuilderHudPanel(_builderBlockWeightGuide);

            var element = _builderWeightGuideImages[number - 1];
            if (element == null)
                continue;

            element.style.display = DisplayStyle.Flex;
            element.style.visibility = Visibility.Visible;
            element.style.opacity = 1f;

            var sprite = GetNumberSpriteRaw(number);
            element.style.backgroundImage = sprite != null
                ? new StyleBackground(sprite)
                : StyleKeyword.None;
        }
    }

    // ── 바닥 ─────────────────────────────────────────────────────────────

    void CreateFloor()
    {
        float floorY     = _floorY = -rows * 0.5f - 1.5f;
        float floorWidth = columns + 4f;

        GameObject floorGO;
        bool created = false;
        if (floorTransform != null)
        {
            floorGO = floorTransform.gameObject;
        }
        else
        {
            if (_generatedFloor == null)
            {
                var existing = FindSceneObjectByName("Floor");
                if (existing != null)
                    _generatedFloor = existing.gameObject;
            }

            if (_generatedFloor != null)
            {
                floorGO = _generatedFloor;
            }
            else
            {
                _floorY = floorY;
                return;
            }
        }

        if (created)
        {
            floorGO.transform.position = new Vector3(0f, floorY, 0f);
            floorGO.transform.localScale = new Vector3(floorWidth, 1f, 1f);
        }
        else
        {
            _floorY = floorGO.transform.position.y;
        }

        if (!floorGO.TryGetComponent<SpriteRenderer>(out var sr))
            sr = floorGO.AddComponent<SpriteRenderer>();
        if (sr.sprite == null)
        {
            sr.sprite = CreateBlockSprite();
            sr.color  = new Color(0.2f, 0.2f, 0.2f, 1f);
        }

        if (Application.isPlaying)
        {
            bool authored = !IsGeneratedObject(floorGO);
            if (!floorGO.TryGetComponent<BoxCollider>(out var col))
            {
                col = floorGO.AddComponent<BoxCollider>();
                col.size = Vector3.one;
            }
            else if (!authored)
            {
                col.size = Vector3.one;
            }
            col.enabled = true;
            col.isTrigger = false;
            if (col.size.y < 1f)
                col.size = new Vector3(col.size.x, 1f, Mathf.Max(col.size.z, 1f));
            col.sharedMaterial = CreateFrictionMaterial();
        }
    }

    // ── 경계선 ────────────────────────────────────────────────────────────

    void CreateBoundaries()
    {
        SyncPlacementZoneFromObject();
        float lineHeight  = 50f;
        float lineHalfH   = lineHeight * 0.5f;
        float lineWidth   = 0.15f;
        float offsetX     = columns * 0.5f + 2f;
        float centerY     = _floorY + lineHalfH;
        var   lineColor   = new Color(1f, 0.25f, 0.25f, 0.7f);

        if (_leftBoundary == null)
        {
            var existing = FindSceneObjectByName("BoundaryLeft");
            if (existing != null) _leftBoundary = existing.gameObject;
        }
        if (_rightBoundary == null)
        {
            var existing = FindSceneObjectByName("BoundaryRight");
            if (existing != null) _rightBoundary = existing.gameObject;
        }

        _leftBoundary ??= SpawnBoundary("BoundaryLeft", new Vector3(-offsetX, centerY, 0f), lineWidth, lineHeight, lineColor);
        _rightBoundary ??= SpawnBoundary("BoundaryRight", new Vector3(offsetX, centerY, 0f), lineWidth, lineHeight, lineColor);
        ConfigureBoundary(_leftBoundary, lineColor);
        ConfigureBoundary(_rightBoundary, lineColor);
        UpdateTowerStackDivider();
    }

    void ConfigureBoundary(GameObject boundary, Color fallbackColor)
    {
        if (boundary == null) return;
        bool authored = !IsGeneratedObject(boundary);

        if (!boundary.TryGetComponent<SpriteRenderer>(out var sr))
            sr = boundary.AddComponent<SpriteRenderer>();
        if (sr.sprite == null)
        {
            sr.sprite = CreateBlockSprite();
            sr.color = fallbackColor;
        }
        if (!authored)
            sr.sortingOrder = 1;

        if (!Application.isPlaying) return;

        if (!boundary.TryGetComponent<Rigidbody>(out var rb))
            rb = boundary.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        if (!boundary.TryGetComponent<BoxCollider>(out var col))
        {
            col = boundary.AddComponent<BoxCollider>();
            col.size = Vector3.one;
        }
        else if (!authored)
        {
            col.size = Vector3.one;
        }
        col.isTrigger = true;

        if (!boundary.TryGetComponent<BoundaryLine>(out var bl))
            bl = boundary.AddComponent<BoundaryLine>();
        bl.OnBlockTouched = TriggerGameOver;
    }

    void UpdateTowerStackDivider()
    {
        if (_towerRoot == null) return;

        SyncPlacementZoneFromObject(updateVisuals: false);

        if (_towerStackDivider == null)
        {
            var existing = FindBlockTowerChild("TowerStackDivider");
            if (existing != null)
                _towerStackDivider = existing.gameObject;
        }

        bool created = false;
        if (_towerStackDivider == null)
        {
            _towerStackDivider = new GameObject("TowerStackDivider");
            if (Application.isPlaying)
                MarkGeneratedObject(_towerStackDivider);
            _towerStackDivider.transform.SetParent(transform, worldPositionStays: true);
            created = true;
        }
        else
        {
            ParentToBlockTowerPreserveWorld(_towerStackDivider.transform);
        }

        float minX = placementMin.x;
        float maxX = placementMax.x + 1f;
        float width = Mathf.Max(0.1f, maxX - minX);
        float y = _extractionMaxRow + 1f;

        bool authoredDivider = !created && !IsGeneratedObject(_towerStackDivider);
        var dividerLocalPosition = new Vector3((minX + maxX) * 0.5f, y, 0.02f);
        if (authoredDivider)
        {
            var authoredLocal = _towerRoot.InverseTransformPoint(_towerStackDivider.transform.position);
            authoredLocal.y = y;
            _towerStackDivider.transform.position = _towerRoot.TransformPoint(authoredLocal);
        }
        else
        {
            ParentToBlockTowerPreserveWorld(_towerStackDivider.transform);
            _towerStackDivider.transform.position = _towerRoot.TransformPoint(dividerLocalPosition);
        }
        if (!authoredDivider)
            _towerStackDivider.transform.localScale = new Vector3(width, 0.12f, 1f);

        var renderer = _towerStackDivider.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = _towerStackDivider.AddComponent<SpriteRenderer>();

        if (renderer != null)
        {
            if (renderer.sprite == null)
            {
                renderer.sprite = CreateBlockSprite();
                renderer.color = new Color(1f, 0f, 0f, 0.85f);
            }
            else if (created)
            {
                renderer.color = new Color(1f, 0f, 0f, 0.85f);
            }
            if (created)
                renderer.sortingOrder = 2;
        }

        AlignPlacementZoneWidthToDivider();
        AlignPlacementZoneBottomToDivider(y);
        SyncPlacementZoneFromObject();
    }

    void AlignPlacementZoneWidthToDivider()
    {
        if (!usePlacementZoneObject || placementZoneTransform == null || _towerStackDivider == null || _towerRoot == null)
            return;

        if (!TryGetTowerLocalBounds(_towerStackDivider.transform, out var dividerMinX, out var dividerMaxX, out _, out _))
            return;
        if (!TryGetTowerLocalBounds(placementZoneTransform, out var zoneMinX, out var zoneMaxX, out _, out _))
            return;

        float dividerWidth = Mathf.Max(0.1f, dividerMaxX - dividerMinX);
        float zoneWidth = Mathf.Max(0.001f, zoneMaxX - zoneMinX);
        if (Mathf.Abs(dividerWidth - zoneWidth) > 0.001f)
        {
            var scale = placementZoneTransform.localScale;
            scale.x *= dividerWidth / zoneWidth;
            placementZoneTransform.localScale = scale;
        }

        if (!TryGetTowerLocalBounds(placementZoneTransform, out zoneMinX, out zoneMaxX, out _, out _))
            return;

        float dividerCenterX = (dividerMinX + dividerMaxX) * 0.5f;
        float zoneCenterX = (zoneMinX + zoneMaxX) * 0.5f;
        float deltaX = dividerCenterX - zoneCenterX;
        if (Mathf.Abs(deltaX) > 0.001f)
            placementZoneTransform.position += _towerRoot.TransformVector(new Vector3(deltaX, 0f, 0f));

        ClampPlacementZoneTransformInsideDividerX();
        ForcePlacementZoneRectToDividerX();
    }

    void ForcePlacementZoneRectToDividerX()
    {
        if (placementZoneTransform == null || _towerStackDivider == null || _towerRoot == null)
            return;

        if (!TryGetTowerLocalBounds(_towerStackDivider.transform, out var dividerMinX, out var dividerMaxX, out _, out _))
            return;
        if (!TryGetTowerLocalBounds(placementZoneTransform, out var zoneMinX, out var zoneMaxX, out _, out _))
            return;

        float dividerWidth = Mathf.Max(0.1f, dividerMaxX - dividerMinX);
        float zoneWidth = Mathf.Max(0.001f, zoneMaxX - zoneMinX);
        var scale = placementZoneTransform.localScale;
        scale.x *= dividerWidth / zoneWidth;
        placementZoneTransform.localScale = scale;

        if (!TryGetTowerLocalBounds(placementZoneTransform, out zoneMinX, out zoneMaxX, out _, out _))
            return;

        float dividerCenterX = (dividerMinX + dividerMaxX) * 0.5f;
        float zoneCenterX = (zoneMinX + zoneMaxX) * 0.5f;
        placementZoneTransform.position += _towerRoot.TransformVector(new Vector3(dividerCenterX - zoneCenterX, 0f, 0f));
    }

    void ClampPlacementZoneTransformInsideDividerX()
    {
        if (placementZoneTransform == null || _towerStackDivider == null || _towerRoot == null)
            return;

        if (!TryGetTowerLocalBounds(_towerStackDivider.transform, out var dividerMinX, out var dividerMaxX, out _, out _))
            return;
        if (!TryGetTowerLocalBounds(placementZoneTransform, out var zoneMinX, out var zoneMaxX, out _, out _))
            return;

        float deltaX = 0f;
        if (zoneMinX < dividerMinX)
            deltaX = dividerMinX - zoneMinX;
        if (zoneMaxX + deltaX > dividerMaxX)
            deltaX += dividerMaxX - (zoneMaxX + deltaX);

        if (Mathf.Abs(deltaX) > 0.001f)
            placementZoneTransform.position += _towerRoot.TransformVector(new Vector3(deltaX, 0f, 0f));
    }

    void AlignPlacementZoneBottomToDivider(float dividerLocalY)
    {
        if (!usePlacementZoneObject || placementZoneTransform == null || _towerRoot == null)
            return;

        if (!TryGetTowerLocalBounds(placementZoneTransform, out _, out _, out var minY, out var maxY))
            return;

        if (float.IsNaN(_placementZoneTopLimitLocal) || !Application.isPlaying)
            _placementZoneTopLimitLocal = maxY;

        float targetTop = Mathf.Max(_placementZoneTopLimitLocal, dividerLocalY + 0.1f);
        float currentHeight = Mathf.Max(0.001f, maxY - minY);
        float targetHeight = Mathf.Max(0.1f, targetTop - dividerLocalY);
        if (Mathf.Abs(targetHeight - currentHeight) > 0.001f)
        {
            var scale = placementZoneTransform.localScale;
            scale.y *= targetHeight / currentHeight;
            placementZoneTransform.localScale = scale;
        }

        if (!TryGetTowerLocalBounds(placementZoneTransform, out _, out _, out minY, out _))
            return;

        float deltaY = dividerLocalY - minY;
        if (Mathf.Abs(deltaY) < 0.001f)
        {
            SyncPlacementZoneFromObject();
            return;
        }

        placementZoneTransform.position += _towerRoot.TransformVector(new Vector3(0f, deltaY, 0f));
        SyncPlacementZoneFromObject();
    }

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

    GameObject SpawnBoundary(string name, Vector3 worldPos, float width, float height, Color color)
    {
        var go = new GameObject(name);
        if (Application.isPlaying)
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
        BindResultScreens();
        HideResultScreens();
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
            _bonusTargetPreset = DrawBonusPreset();
            _nextBonusTargetPreset = DrawBonusPreset();
            _thirdBonusTargetPreset = DrawBonusPreset();
            _bonusQueueInitialized = true;
        }
        else
        {
            _bonusTargetPreset = _nextBonusTargetPreset;
            _nextBonusTargetPreset = _thirdBonusTargetPreset;
            _thirdBonusTargetPreset = DrawBonusPreset();
        }
        CreateOrUpdateBonusPreview();
    }

    void ResetBonusPresetBag()
    {
        _bonusPresetBag.Clear();
        _bonusPresetBagIndex = 0;
    }

    TetrominoPreset DrawBonusPreset()
    {
        if (_bonusPresetBagIndex >= _bonusPresetBag.Count)
            RefillBonusPresetBag();

        return _bonusPresetBag[_bonusPresetBagIndex++];
    }

    void RefillBonusPresetBag()
    {
        _bonusPresetBag.Clear();
        _bonusPresetBag.Add(TetrominoPreset.I);
        _bonusPresetBag.Add(TetrominoPreset.Z);
        _bonusPresetBag.Add(TetrominoPreset.S);
        _bonusPresetBag.Add(TetrominoPreset.O);
        _bonusPresetBag.Add(TetrominoPreset.L);
        _bonusPresetBag.Add(TetrominoPreset.T);
        _bonusPresetBag.Add(TetrominoPreset.J);

        for (int i = _bonusPresetBag.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (_bonusPresetBag[i], _bonusPresetBag[swapIndex]) = (_bonusPresetBag[swapIndex], _bonusPresetBag[i]);
        }
        _bonusPresetBagIndex = 0;
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
        if (cam == null) return;

        float distance = Mathf.Abs(CameraPlaneZ() - cam.transform.position.z);
        var pos = cam.ViewportToWorldPoint(new Vector3(0.76f, 0.78f, distance));
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

        if (bonusTetrominoSpriteSet != null)
        {
            UpdateBuilderBonusPreviewImageSlot(container, elements, BonusPresetSprite(preset));
            return;
        }

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

        float fallbackContainerWidth = BonusPreviewCellsFallbackWidth(container);
        float fallbackContainerHeight = BonusPreviewCellsFallbackHeight(container);
        float containerWidth = ResolvedOrDefault(container.resolvedStyle.width, fallbackContainerWidth);
        float containerHeight = ResolvedOrDefault(container.resolvedStyle.height, fallbackContainerHeight);
        float cellSize = BonusPreviewCellSize(container, elements);
        float width = (maxX - minX + 1) * cellSize;
        float height = (maxY - minY + 1) * cellSize;
        containerWidth = Mathf.Max(containerWidth, width);
        containerHeight = Mathf.Max(containerHeight, height);
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
            element.style.backgroundColor = color;
            element.style.backgroundImage = StyleKeyword.Null;
        }
    }

    void UpdateBuilderBonusPreviewImageSlot(VisualElement container, List<VisualElement> elements, Sprite sprite)
    {
        container.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;
        container.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

        if (elements == null)
            return;

        foreach (var element in elements)
            element.style.display = DisplayStyle.None;
    }

    Sprite BonusPresetSprite(TetrominoPreset preset)
    {
        if (bonusTetrominoSpriteSet == null)
            return null;

        return preset switch
        {
            TetrominoPreset.I => bonusTetrominoSpriteSet.I,
            TetrominoPreset.J => bonusTetrominoSpriteSet.J,
            TetrominoPreset.L => bonusTetrominoSpriteSet.L,
            TetrominoPreset.O => bonusTetrominoSpriteSet.O,
            TetrominoPreset.S => bonusTetrominoSpriteSet.S,
            TetrominoPreset.T => bonusTetrominoSpriteSet.T,
            TetrominoPreset.Z => bonusTetrominoSpriteSet.Z,
            _ => null
        };
    }

    float BonusPreviewCellSize(VisualElement container, List<VisualElement> elements)
    {
        if (elements != null)
        {
            foreach (var element in elements)
            {
                if (element == null) continue;
                float width = ResolvedOrDefault(element.resolvedStyle.width, 0f);
                if (width > 0.01f)
                    return width;
            }
        }

        return container != null && (container.name == "BonusPreview2Cells" || container.name == "BonusPreview3Cells")
            ? 40f
            : 60f;
    }

    float BonusPreviewCellsFallbackWidth(VisualElement container)
    {
        return container != null && (container.name == "BonusPreview2Cells" || container.name == "BonusPreview3Cells")
            ? 160f
            : 240f;
    }

    float BonusPreviewCellsFallbackHeight(VisualElement container)
    {
        return container != null && (container.name == "BonusPreview2Cells" || container.name == "BonusPreview3Cells")
            ? 80f
            : 120f;
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

    // ── 카메라 ────────────────────────────────────────────────────────────

    void EnsureSecondaryViewObjects()
    {
        MigrateSecondaryViewDefaults();
        if (secondaryViewCamera == null)
        {
            var existing = FindSceneObjectByName("SubViewCamera");
            if (existing != null)
                secondaryViewCamera = existing.GetComponent<Camera>();
        }

        if (secondaryViewCamera == null)
        {
            var go = new GameObject("SubViewCamera");
            go.transform.SetParent(transform, false);
            secondaryViewCamera = go.AddComponent<Camera>();
        }

        ConfigureSecondaryViewCamera();
        EnsureSecondaryViewPanel();
        EnsureSecondaryViewTexture();
    }

    void MigrateSecondaryViewDefaults()
    {
        if (secondaryViewDefaultsMigrated)
            return;

        if (Mathf.Approximately(secondaryViewPanelSize.x, 360f) &&
            Mathf.Approximately(secondaryViewPanelSize.y, 240f))
        {
            secondaryViewPanelSize = new Vector2(540f, 360f);
        }

        if (secondaryViewOrthographicSize <= 0.01f)
            secondaryViewOrthographicSize = 3f;

        secondaryViewDefaultsMigrated = true;
    }

    void ConfigureSecondaryViewCamera()
    {
        if (secondaryViewCamera == null) return;

        var main = Camera.main;
        if (main != null && main != secondaryViewCamera)
        {
            secondaryViewCamera.clearFlags = main.clearFlags;
            secondaryViewCamera.backgroundColor = main.backgroundColor;
            secondaryViewCamera.cullingMask = main.cullingMask;
            secondaryViewCamera.orthographic = false;
            secondaryViewCamera.nearClipPlane = main.nearClipPlane;
            secondaryViewCamera.farClipPlane = main.farClipPlane;
            secondaryViewCamera.fieldOfView = main.fieldOfView;
            secondaryViewCamera.transform.rotation = main.transform.rotation;
            secondaryViewCamera.transform.position = new Vector3(main.transform.position.x, main.transform.position.y, main.transform.position.z);
        }
        else
        {
            secondaryViewCamera.orthographic = false;
        }

        secondaryViewCamera.depth = -10f;
        secondaryViewCamera.enabled = Application.isPlaying;

        var listener = secondaryViewCamera.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = false;
    }

    void EnsureSecondaryViewPanel()
    {
        if (_builderSecondaryViewPanel != null && _builderSecondaryViewImage != null)
        {
            _builderSecondaryViewPanel.style.width = secondaryViewPanelSize.x;
            _builderSecondaryViewPanel.style.height = secondaryViewPanelSize.y;
            _builderSecondaryViewPanel.style.left = secondaryViewPanelPosition.x;
        }
    }

    void EnsureSecondaryViewTexture()
    {
        bool hasToolkitPreview = _builderSecondaryViewImage != null;
        if (!Application.isPlaying || secondaryViewCamera == null || !hasToolkitPreview)
            return;

        int width = Mathf.Max(64, secondaryViewTextureSize);
        int height = Mathf.Max(64, Mathf.RoundToInt(width * Mathf.Max(0.1f, secondaryViewPanelSize.y) / Mathf.Max(0.1f, secondaryViewPanelSize.x)));
        if (_secondaryViewTexture == null || _secondaryViewTexture.width != width || _secondaryViewTexture.height != height)
        {
            if (_secondaryViewTexture != null)
                _secondaryViewTexture.Release();
            _secondaryViewTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            _secondaryViewTexture.name = "SubViewCameraTexture";
            _secondaryViewTexture.Create();
        }

        secondaryViewCamera.targetTexture = _secondaryViewTexture;
        if (_builderSecondaryViewImage != null)
        {
            _builderSecondaryViewImage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_secondaryViewTexture));
            _builderSecondaryViewImage.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        }
    }

    void UpdateSecondaryViewCamera()
    {
        if (!Application.isPlaying || _towerRoot == null)
            return;

        EnsureSecondaryViewObjects();
        if (secondaryViewCamera == null)
            return;

        float targetX = 0f;
        float targetY;
        float targetSize;
        bool showTowerOverview = _isHolding;
        float aspect = SecondaryViewAspect();
        bool hasView = showTowerOverview
            ? TryCalculateOccupiedRowsCameraView(extractionViewPadding, aspect, out targetY, out targetSize)
            : TryCalculatePlacementCameraView(aspect, out targetY, out targetSize);

        if (!hasView)
            return;

        var main = Camera.main;
        if (!showTowerOverview && secondaryViewOrthographicSize > 0.01f)
            targetSize = secondaryViewOrthographicSize;
        secondaryViewCamera.orthographic = false;
        if (main != null)
            secondaryViewCamera.fieldOfView = main.fieldOfView;
        float z = CameraZForHalfHeight(secondaryViewCamera, targetSize);
        secondaryViewCamera.transform.position = new Vector3(targetX, targetY, z);
    }

    float SecondaryViewAspect()
    {
        if (_builderSecondaryViewPanel != null)
        {
            float width = ResolvedOrDefault(_builderSecondaryViewPanel.resolvedStyle.width, secondaryViewPanelSize.x);
            float height = ResolvedOrDefault(_builderSecondaryViewPanel.resolvedStyle.height, secondaryViewPanelSize.y);
            if (width > 1f && height > 1f)
                return width / height;
        }

        if (secondaryViewImage != null)
        {
            var rect = secondaryViewImage.rectTransform.rect;
            if (rect.width > 1f && rect.height > 1f)
                return rect.width / rect.height;
        }

        return Mathf.Max(0.1f, secondaryViewPanelSize.x) / Mathf.Max(0.1f, secondaryViewPanelSize.y);
    }

    bool TryCalculateOccupiedRowsCameraView(float padding, float aspect, out float centerY, out float size)
    {
        centerY = 0f;
        size = 0f;
        if (!TryCalculateOccupiedGridBounds(out var minX, out var maxX, out var minY, out var maxY))
            return false;

        return TryCalculateGridBoundsCameraView(minX, maxX, minY, maxY, padding, aspect, out centerY, out size);
    }

    bool TryCalculateOccupiedGridBounds(out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = int.MaxValue;
        maxX = int.MinValue;
        minY = int.MaxValue;
        maxY = int.MinValue;

        if (_towerRoot == null || _cells.Count == 0)
            return false;

        foreach (var cell in _cells.Keys)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        return minX <= maxX && minY <= maxY;
    }

    bool TryCalculateRowsCameraView(int minGridY, int maxGridY, float padding, float aspect, out float centerY, out float size)
    {
        int minGridX = _extractionMinCol;
        int maxGridX = _extractionMaxCol;
        if (minGridX > maxGridX)
        {
            minGridX = 0;
            maxGridX = columns - 1;
        }
        return TryCalculateGridBoundsCameraView(minGridX, maxGridX, minGridY, maxGridY, padding, aspect, out centerY, out size);
    }

    bool TryCalculateGridBoundsCameraView(int minGridX, int maxGridX, int minGridY, int maxGridY, float padding, float aspect, out float centerY, out float size)
    {
        centerY = 0f;
        size = 0f;
        if (_towerRoot == null)
            return false;

        float left = _towerRoot.position.x + minGridX - padding;
        float right = _towerRoot.position.x + maxGridX + 1f + padding;
        float halfW = Mathf.Max(columns * 0.5f + 1f, (right - left) * 0.5f);
        float sizeForWidth = halfW / Mathf.Max(0.1f, aspect);
        float bottom = _towerRoot.position.y + minGridY - padding;
        float top = _towerRoot.position.y + maxGridY + 1f + padding;
        centerY = (bottom + top) * 0.5f;
        float halfH = (top - bottom) * 0.5f;
        size = Mathf.Max(sizeForWidth, halfH);
        return size > 0.01f;
    }

    bool TryCalculatePlacementCameraView(float aspect, out float centerY, out float size)
    {
        centerY = 0f;
        size = 0f;
        if (_towerRoot == null)
            return false;

        int targetGridY = _hasLastPlacementCenter
            ? Mathf.RoundToInt(_lastPlacementCenter.y)
            : HighestOccupiedRow() + 1;

        var main = Camera.main;
        float currentSize = main != null ? CurrentCameraHalfHeight(main) : 6f;
        float worldY = _towerRoot.position.y + targetGridY + 0.5f + placementViewTopOffset;
        centerY = Mathf.Max(_floorY + currentSize, worldY);

        float halfW = columns * 0.5f + 3.5f;
        size = Mathf.Max(currentSize, halfW / Mathf.Max(0.1f, aspect));
        return true;
    }

    void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = false;

        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        if (TryCalculateOccupiedRowsCameraView(extractionViewPadding, aspect, out var centerY, out var halfHeight))
        {
            _cameraTargetY = centerY;
            _cameraTargetSize = halfHeight;
            ApplyCameraView(cam, 0f, _cameraTargetY, _cameraTargetSize, adjustFieldOfView: true);
            _hasCameraTarget = false;
            return;
        }

        FitCameraToGridRows(0, rows - 1, extractionViewPadding, immediate: true);
    }

    void FitCameraToGridRows(int minGridY, int maxGridY, float padding, bool immediate)
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = false;

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
            ApplyCameraView(cam, 0f, _cameraTargetY, _cameraTargetSize, adjustFieldOfView: true);
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
        if (cam == null || _towerRoot == null) return;
        cam.orthographic = false;

        float worldY = _towerRoot.position.y + _heldBaseCell.y + _heldCenter.y;
        float halfHeight = CurrentCameraHalfHeight(cam);
        float minY = _floorY + halfHeight;
        _cameraTargetY = Mathf.Max(minY, worldY);
        _cameraTargetSize = halfHeight;
        _hasCameraTarget = true;
    }

    void FocusCameraOnGridY(int gridY, bool immediate, float padding)
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = false;

        float worldY = _towerRoot.position.y + gridY + 0.5f;
        float halfHeight = CurrentCameraHalfHeight(cam);
        float minY = _floorY + halfHeight;
        _cameraTargetY = Mathf.Max(minY, worldY + padding);
        _cameraTargetSize = halfHeight;
        if (immediate)
        {
            ApplyCameraView(cam, cam.transform.position.x, _cameraTargetY, _cameraTargetSize);
            _hasCameraTarget = false;
            return;
        }

        _hasCameraTarget = true;
    }

    void UpdateCameraTarget()
    {
        if (!_hasCameraTarget) return;

        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = false;

        var pos = cam.transform.position;
        float t = 1f - Mathf.Exp(-cameraFocusSpeed * Time.deltaTime);
        float nextY = Mathf.Lerp(pos.y, _cameraTargetY, t);
        float nextSize = _cameraTargetSize > 0f
            ? Mathf.Lerp(CurrentCameraHalfHeight(cam), _cameraTargetSize, t)
            : CurrentCameraHalfHeight(cam);
        ApplyCameraView(cam, pos.x, nextY, nextSize);

        bool reachedY = Mathf.Abs(nextY - _cameraTargetY) < 0.01f;
        bool reachedSize = _cameraTargetSize <= 0f || Mathf.Abs(nextSize - _cameraTargetSize) < 0.01f;
        if (reachedY && reachedSize)
            _hasCameraTarget = false;
    }

    void ApplyCameraView(Camera cam, float x, float y, float halfHeight, bool adjustFieldOfView = false)
    {
        if (cam == null) return;
        cam.orthographic = false;
        if (adjustFieldOfView)
            cam.fieldOfView = FieldOfViewForHalfHeight(cam, halfHeight);
        cam.transform.position = new Vector3(x, y, CameraZForHalfHeight(cam, halfHeight));
    }

    float FieldOfViewForHalfHeight(Camera cam, float halfHeight)
    {
        if (cam == null)
            return 60f;

        float distance = Mathf.Abs(CameraPlaneZ() - cam.transform.position.z);
        if (distance <= 0.001f)
            return cam.fieldOfView;

        float fov = 2f * Mathf.Atan(Mathf.Max(0.01f, halfHeight) / distance) * Mathf.Rad2Deg;
        return Mathf.Clamp(fov, 20f, 100f);
    }

    float CurrentCameraHalfHeight(Camera cam)
    {
        if (cam == null)
            return 6f;
        if (cam.orthographic)
            return cam.orthographicSize;

        float distance = Mathf.Abs(CameraPlaneZ() - cam.transform.position.z);
        float fovRad = Mathf.Max(1f, cam.fieldOfView) * Mathf.Deg2Rad;
        return Mathf.Max(0.1f, distance * Mathf.Tan(fovRad * 0.5f));
    }

    float CameraZForHalfHeight(Camera cam, float halfHeight)
    {
        float fovRad = Mathf.Max(1f, cam != null ? cam.fieldOfView : 60f) * Mathf.Deg2Rad;
        float distance = Mathf.Max(0.1f, halfHeight) / Mathf.Tan(fovRad * 0.5f);
        return CameraPlaneZ() - distance;
    }

    float CameraPlaneZ()
    {
        return _towerRoot != null ? _towerRoot.position.z : 0f;
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
            UpdateTowerStackDivider();
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
        UpdateTowerStackDivider();
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

#if UNITY_EDITOR
        _blockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Contents/box.png");
        if (_blockSprite != null)
            return _blockSprite;
#endif

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

    static Color IceBlockColor() => new Color32(0x71, 0xC0, 0xC0, 0xFF);

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
