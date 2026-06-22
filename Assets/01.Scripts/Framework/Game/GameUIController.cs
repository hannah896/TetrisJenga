using System;
using System.Collections;
using System.Collections.Generic;
using JSAM;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// HUD 책임. BlockTower 이벤트를 구독해 점수/보너스/결과 화면을 처리한다.
/// hudDocument / VisualTreeAsset / RuntimeHudImageLibrarySO 를 직접 소유한다.
/// </summary>
public class GameUIController : MonoBehaviour
{
    [SerializeField] BlockTower      _tower;
    [SerializeField] ScoreController _scoreController;
    [SerializeField] CameraController _camera;

    [Header("HUD 문서")]
    [SerializeField] UIDocument hudDocument;
    [SerializeField] VisualTreeAsset hudVisualTree;
    [SerializeField] VisualTreeAsset gameOverVisualTree;
    [SerializeField] VisualTreeAsset clearVisualTree;
    [SerializeField] VisualTreeAsset startOverlayVisualTree;
    [SerializeField] PanelSettings hudPanelSettings;
    [SerializeField] RuntimeHudImageLibrarySO hudImageLibrary;

    [SerializeField] SettingUIImageLibrarySO settingImageLibrary;

    [Header("Bonus Queue Animation")]
    [SerializeField, Min(0.05f)] float bonusQueueRiseDuration = 0.4f;
    [SerializeField, Min(0f)] float bonusQueueRiseFallbackDistance = 180f;

    [Header("클리어 연출")]
    [SerializeField] SpriteRenderer _clearBgRenderer;

    [Header("결과 화면 씬 이름")]
    [SerializeField] string _stageSelectSceneName = "StageScene";
    [SerializeField] string _lobbySceneName = "LobbyScene";
    [SerializeField] string _dialogueSceneName = "DialogueScene";

    // ── VisualElement 참조 (BindHud 이후 유효) ───────────────────────────
    Label _scoreTitle;
    Label _scoreValue;
    Label _targetScoreValue;
    Label _scorePopupText;
    VisualElement _hudScorePanel;
    VisualElement _hudTargetScorePanel;
    Label _bonusPreviewTitle;
    Label _bonusKeyLabel;
    Label _bonusNextKeyLabel;
    Label _bonusThirdKeyLabel;
    VisualElement _bonusPreview;
    VisualElement _bonusBackground;
    VisualElement _bonusSlot;
    VisualElement _bonusNextSlot;
    VisualElement _bonusThirdSlot;
    VisualElement _bonusCells;
    VisualElement _bonusNextCells;
    VisualElement _bonusThirdCells;
    VisualElement _secondaryViewPanel;
    VisualElement _secondaryViewImage;
    VisualElement _blockWeightGuide;
    VisualElement _slashBackground;
    readonly VisualElement[] _weightGuideImages = new VisualElement[6];
    readonly List<VisualElement> _bonusCellElements      = new();
    readonly List<VisualElement> _bonusNextCellElements  = new();
    readonly List<VisualElement> _bonusThirdCellElements = new();

    // 결과 화면 레이블
    Label _resultTitleLabel;
    Label _resultCurrentScoreLabel;
    Label _resultTargetScoreLabel;

    PanelSettings _runtimeHudPanelSettings;
    Font _runtimeHudFont;
    bool _showingResultHud;
    Coroutine _scorePopupRoutine;
    Coroutine _bonusQueueRoutine;
    bool _bonusPreviewInitialized;
    readonly UI_Setting_Controller _uiSetting = new();

    #region Lifecycle Function

    private void OnValidate()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
        if (_scoreController == null) _scoreController = GetComponent<ScoreController>();
        if (_camera == null) _camera = GetComponent<CameraController>();
        if (hudDocument == null) hudDocument = FindAnyObjectByType<UIDocument>();
    }

    void Awake()
    {
        if (_tower == null)           _tower           = GetComponent<BlockTower>();
        if (_scoreController == null) _scoreController = GetComponent<ScoreController>();
    }
    
    void Start()
    {
        BindHud();
        ShowStartOverlay();
    }

    void PlayStageBGM()
    {
        int idx = GameManager.Instance.CurrentStageIndex;
        if (idx >= 0 && idx <= 5)
            AudioPlayback.PlayMusic((_AudioLibraryMusic)((int)_AudioLibraryMusic.Stage1 + idx), stopCurrent: true);
        else
            AudioPlayback.PlayMusic(_AudioLibraryMusic.EndlessBGM, stopCurrent: true);
    }

    void OnEnable()
    {
        if (_tower != null)
            _tower.OnHudRebind += BindHud;
        if (_scoreController != null)
        {
            _scoreController.OnScoreChanged  += HandleScoreChanged;
            _scoreController.OnBonusRolled   += HandleBonusRolled;
            _scoreController.OnGameOver      += HandleGameOver;
            _scoreController.OnClear         += HandleClear;
            _scoreController.OnFloatingScore += HandleFloatingScore;
        }
        if (_camera != null) _camera.OnSecondaryViewTextureChanged += HandleSecondaryViewTexture;
    }

    void OnDisable()
    {
        if (_tower != null)
            _tower.OnHudRebind -= BindHud;
        if (_scoreController != null)
        {
            _scoreController.OnScoreChanged  -= HandleScoreChanged;
            _scoreController.OnBonusRolled   -= HandleBonusRolled;
            _scoreController.OnGameOver      -= HandleGameOver;
            _scoreController.OnClear         -= HandleClear;
            _scoreController.OnFloatingScore -= HandleFloatingScore;
        }
        if (_camera != null) _camera.OnSecondaryViewTextureChanged -= HandleSecondaryViewTexture;
    }

    void Update()
    {
        if (_showingResultHud) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            _uiSetting.Toggle();
    }
    #endregion
    
    // ── 시작 오버레이 ─────────────────────────────────────────────────────

    void ShowStartOverlay()
    {
        if (startOverlayVisualTree == null || hudDocument == null) { PlayStageBGM(); return; }
        var root = hudDocument.rootVisualElement;
        if (root == null) return;

        var overlay = startOverlayVisualTree.CloneTree();
        overlay.style.position = Position.Absolute;
        overlay.style.left     = 0;
        overlay.style.top      = 0;
        overlay.style.width    = new Length(100, LengthUnit.Percent);
        overlay.style.height   = new Length(100, LengthUnit.Percent);
        overlay.pickingMode    = PickingMode.Ignore;
        root.Add(overlay);

        StartCoroutine(RunStartOverlay(overlay));
    }

    IEnumerator RunStartOverlay(VisualElement overlay)
    {
        var shiny1Frames = new[]
        {
            overlay.Q<VisualElement>("Shiny1Frame1"),
            overlay.Q<VisualElement>("Shiny1Frame2"),
            overlay.Q<VisualElement>("Shiny1Frame3"),
            overlay.Q<VisualElement>("Shiny1Frame4"),
            overlay.Q<VisualElement>("Shiny1Frame5"),
        };
        var shiny2Frames = new[]
        {
            overlay.Q<VisualElement>("Shiny2Frame1"),
            overlay.Q<VisualElement>("Shiny2Frame2"),
            overlay.Q<VisualElement>("Shiny2Frame3"),
        };

        const float displayDuration = 1.0f;
        const float fadeDuration    = 0.5f;
        const float frameInterval   = 0.1f;

        float elapsed    = 0f;
        float frameTimer = 0f;
        int   shiny1Idx  = 0;
        int   shiny2Idx  = 0;

        while (elapsed < displayDuration)
        {
            elapsed    += Time.deltaTime;
            frameTimer += Time.deltaTime;

            if (frameTimer >= frameInterval)
            {
                frameTimer = 0f;
                for (int i = 0; i < shiny1Frames.Length; i++)
                    if (shiny1Frames[i] != null)
                        shiny1Frames[i].style.display = i == shiny1Idx ? DisplayStyle.Flex : DisplayStyle.None;
                shiny1Idx = (shiny1Idx + 1) % shiny1Frames.Length;

                for (int i = 0; i < shiny2Frames.Length; i++)
                    if (shiny2Frames[i] != null)
                        shiny2Frames[i].style.display = i == shiny2Idx ? DisplayStyle.Flex : DisplayStyle.None;
                shiny2Idx = (shiny2Idx + 1) % shiny2Frames.Length;
            }

            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            overlay.style.opacity = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        overlay.RemoveFromHierarchy();
        PlayStageBGM();
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────────────

    bool IsEndlessMode => _scoreController != null && _scoreController.TargetScore <= 0;

    void HandleScoreChanged(int score, int target)
    {
        if (_scoreValue != null) _scoreValue.text = score.ToString();
        if (!IsEndlessMode && _targetScoreValue != null) _targetScoreValue.text = target.ToString();
    }

    void HandleBonusRolled(TetrominoPreset current, TetrominoPreset next, TetrominoPreset third)
    {
        bool animate = _bonusPreviewInitialized && Application.isPlaying;
        SetLabelText(_bonusKeyLabel,      Util.PresetKeyText(current));
        SetLabelText(_bonusNextKeyLabel,  Util.PresetKeyText(next));
        SetLabelText(_bonusThirdKeyLabel, Util.PresetKeyText(third));
        UpdateBonusPreviewSlot(_bonusCells,      _bonusCellElements,      current);
        UpdateBonusPreviewSlot(_bonusNextCells,  _bonusNextCellElements,  next);
        UpdateBonusPreviewSlot(_bonusThirdCells, _bonusThirdCellElements, third);
        _bonusPreviewInitialized = true;

        if (animate)
            PlayBonusQueueRise();
    }

    void PlayBonusQueueRise()
    {
        if (_bonusSlot == null || _bonusNextSlot == null || _bonusThirdSlot == null) return;
        if (_bonusQueueRoutine != null)
            StopCoroutine(_bonusQueueRoutine);
        _bonusQueueRoutine = StartCoroutine(RunBonusQueueRise());
    }

    IEnumerator RunBonusQueueRise()
    {
        float measuredDistance = Mathf.Abs(_bonusNextSlot.worldBound.y - _bonusSlot.worldBound.y);
        float distance = measuredDistance > 1f ? measuredDistance : bonusQueueRiseFallbackDistance;
        var slots = new[] { _bonusSlot, _bonusNextSlot, _bonusThirdSlot };
        foreach (var slot in slots)
            slot.style.top = distance;

        float duration = Mathf.Max(0.05f, bonusQueueRiseDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float offset = Mathf.Lerp(distance, 0f, eased);
            foreach (var slot in slots)
                if (slot != null)
                    slot.style.top = offset;
            yield return null;
        }

        foreach (var slot in slots)
            if (slot != null)
                slot.style.top = 0f;
        _bonusQueueRoutine = null;
    }

    void HandleGameOver(int score, int target)
    {
        if (IsEndlessMode)
        {
            int best = GameManager.Instance != null ? (int)GameManager.Instance.GetBestScore(GameManager.EndlessStageIndex) : 0;
            ShowResultHud(gameOverVisualTree, "Assets/01.Scripts/UI/GameOverScreen.uxml", "GAME OVER", score, best, "BEST");
        }
        else
        {
            ShowResultHud(gameOverVisualTree, "Assets/01.Scripts/UI/GameOverScreen.uxml", "GAME OVER", score, target);
        }
    }

    void HandleClear(int score, int target)
    {
        if (_clearBgRenderer != null)
        {
            _clearBgRenderer.gameObject.SetActive(true);
            var c = _clearBgRenderer.color;
            c.a = 0f;
            _clearBgRenderer.color = c;

            LMotion.Create(0f, 1f, 1.2f)
                .WithEase(Ease.OutCubic)
                .WithOnComplete(() => ShowResultHud(clearVisualTree, "Assets/01.Scripts/UI/ClearScreen.uxml", "CLEAR", score, target))
                .BindToColorA(_clearBgRenderer)
                .AddTo(this);

            var cam = Camera.main;
            if (cam != null)
            {
                LMotion.Create(cam.transform.position.z, -15f, 1.2f)
                    .WithEase(Ease.OutCubic)
                    .BindToPositionZ(cam.transform)
                    .AddTo(this);
            }
        }
        else
        {
            ShowResultHud(clearVisualTree, "Assets/01.Scripts/UI/ClearScreen.uxml", "CLEAR", score, target);
        }
    }

    void HandleFloatingScore(int delta)
    {
        if (_scorePopupText == null) return;
        if (_scorePopupRoutine != null) StopCoroutine(_scorePopupRoutine);
        _scorePopupRoutine = StartCoroutine(AnimateScorePopup(delta));
    }

    void HandleSecondaryViewTexture(RenderTexture tex)
    {
        if (_secondaryViewImage == null) return;
        _secondaryViewImage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(tex));
        _secondaryViewImage.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
    }

    // ── HUD 바인딩 ────────────────────────────────────────────────────────

    void BindHud()
    {
        _showingResultHud = false;
        _bonusPreviewInitialized = false;
        EnsureHudDocument();
        if (hudDocument == null) return;
        EnsureHudPanelSettings();
        EnsureHudVisualTree();
        if (hudDocument.rootVisualElement == null) return;
        var root = hudDocument.rootVisualElement;

        _scoreTitle          = root.Q<Label>("ScoreTitle");
        _scoreValue          = root.Q<Label>("ScoreValue");
        _targetScoreValue    = root.Q<Label>("TargetScoreValue");
        _scorePopupText      = root.Q<Label>("ScorePopupText");
        _hudScorePanel       = root.Q<VisualElement>("HudScorePanel");
        _hudTargetScorePanel = root.Q<VisualElement>("HudTargetScorePanel");
        _bonusPreviewTitle   = root.Q<Label>("BonusPreviewTitle");
        _bonusKeyLabel       = root.Q<Label>("BonusPreview1Key");
        _bonusNextKeyLabel   = root.Q<Label>("BonusPreview2Key");
        _bonusThirdKeyLabel  = root.Q<Label>("BonusPreview3Key");
        _bonusPreview        = root.Q<VisualElement>("BonusPreview");
        _bonusBackground     = root.Q<VisualElement>("BonusPreviewBackground");
        _bonusSlot           = root.Q<VisualElement>("BonusPreview1");
        _bonusNextSlot       = root.Q<VisualElement>("BonusPreview2");
        _bonusThirdSlot      = root.Q<VisualElement>("BonusPreview3");
        _bonusCells          = root.Q<VisualElement>("BonusPreview1Cells") ?? root.Q<VisualElement>("BonusPreviewCells");
        _bonusNextCells      = root.Q<VisualElement>("BonusPreview2Cells");
        _bonusThirdCells     = root.Q<VisualElement>("BonusPreview3Cells");
        _secondaryViewPanel  = root.Q<VisualElement>("SubCameraPreviewPanel");
        _secondaryViewImage  = root.Q<VisualElement>("SubCameraPreviewImage");
        _blockWeightGuide    = root.Q<VisualElement>("BlockWeightGuide");
        for (int i = 0; i < _weightGuideImages.Length; i++)
            _weightGuideImages[i] = root.Q<VisualElement>($"WeightGuideImage{i + 1}");

        EnsureFallbackHudElements(root);

        _bonusCellElements.Clear();
        _bonusCells?.Query<VisualElement>(className: "bonus-preview-cell").ForEach(c => _bonusCellElements.Add(c));
        _bonusNextCellElements.Clear();
        _bonusNextCells?.Query<VisualElement>(className: "bonus-preview-cell").ForEach(c => _bonusNextCellElements.Add(c));
        _bonusThirdCellElements.Clear();
        _bonusThirdCells?.Query<VisualElement>(className: "bonus-preview-cell").ForEach(c => _bonusThirdCellElements.Add(c));

        ApplyHudInitialText();
        ApplyHudSprites();
        SyncWeightGuideImages();

        if (_bonusCells != null && _scoreController != null)
            root.schedule.Execute(() => HandleBonusRolled(
                _scoreController.BonusTargetPreset,
                _scoreController.NextBonusTargetPreset,
                _scoreController.ThirdBonusTargetPreset)).StartingIn(0);

        if (_scoreController != null)
            HandleScoreChanged(_scoreController.Score, _scoreController.TargetScore);

        _uiSetting.Initialize(root, settingImageLibrary,
            onRestart: () => SceneManager.LoadScene(_stageSelectSceneName));
    }

    void EnsureHudDocument()
    {
        if (hudDocument == null)
            hudDocument = GetComponent<UIDocument>() ?? FindAnyObjectByType<UIDocument>();
    }

    void EnsureHudPanelSettings()
    {
        if (hudDocument == null) return;
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
        if (hudDocument == null || hudVisualTree == null) return;
        if (hudDocument.visualTreeAsset != hudVisualTree)
            hudDocument.visualTreeAsset = hudVisualTree;
    }

    void EnsureFallbackHudElements(VisualElement root)
    {
        if (root == null) return;
        root.style.display    = DisplayStyle.Flex;
        root.style.visibility = Visibility.Visible;
        root.style.opacity    = 1f;
        root.style.position   = Position.Relative;
        root.pickingMode      = PickingMode.Ignore;

        _hudScorePanel       ??= CreatePanel(root, "HudScorePanel");
        _hudTargetScorePanel ??= CreatePanel(root, "HudTargetScorePanel");
        _blockWeightGuide    ??= CreatePanel(root, "BlockWeightGuide");

        if (_scoreValue     == null) _scoreValue     = CreateLabel(_hudScorePanel,       "ScoreValue",      "0");
        if (_targetScoreValue == null) _targetScoreValue = CreateLabel(_hudTargetScorePanel, "TargetScoreValue",
            _scoreController != null ? _scoreController.TargetScore.ToString() : "0");
        if (_scorePopupText == null) _scorePopupText = CreateLabel(_hudScorePanel, "ScorePopupText", string.Empty);

        for (int i = 0; i < _weightGuideImages.Length; i++)
        {
            _weightGuideImages[i] ??= _blockWeightGuide?.Q<VisualElement>($"WeightGuideImage{i + 1}");
            if (_weightGuideImages[i] == null && _blockWeightGuide != null)
            {
                _weightGuideImages[i] = new VisualElement { name = $"WeightGuideImage{i + 1}" };
                _blockWeightGuide.Add(_weightGuideImages[i]);
            }
        }

        MakeVisible(_hudScorePanel);
        MakeVisible(_hudTargetScorePanel);
        MakeVisible(_blockWeightGuide);
    }

    void ApplyHudInitialText()
    {
        ForceLabel(_scoreTitle,       "SCORE");
        ForceLabel(_scoreValue,       _scoreController != null ? _scoreController.Score.ToString() : "0");
        if (IsEndlessMode)
        {
            int best = GameManager.Instance != null ? (int)GameManager.Instance.GetBestScore(GameManager.EndlessStageIndex) : 0;
            ForceLabel(_targetScoreValue, best.ToString());
        }
        else
        {
            ForceLabel(_targetScoreValue, _scoreController != null ? _scoreController.TargetScore.ToString() : "0");
        }
        ForceLabel(_scorePopupText,   string.Empty);
        ForceLabel(_bonusPreviewTitle, "NEXT");
        if (_scoreController != null)
        {
            ForceBonusKeyLabel(_bonusKeyLabel,      Util.PresetKeyText(_scoreController.BonusTargetPreset));
            ForceBonusKeyLabel(_bonusNextKeyLabel,  Util.PresetKeyText(_scoreController.NextBonusTargetPreset));
            ForceBonusKeyLabel(_bonusThirdKeyLabel, Util.PresetKeyText(_scoreController.ThirdBonusTargetPreset));
        }
    }

    void ApplyHudSprites()
    {
        if (hudImageLibrary == null) return;
        UISprites.Apply(_hudScorePanel,       hudImageLibrary.scorePanel);
        UISprites.Apply(_hudTargetScorePanel, hudImageLibrary.targetScorePanel);
        UISprites.Apply(_bonusPreview,        hudImageLibrary.bonusPreviewPanel);
        UISprites.Apply(_blockWeightGuide,    hudImageLibrary.weightGuidePanel);
        UISprites.Apply(_secondaryViewPanel,  hudImageLibrary.subCameraPreviewPanel);
        ApplyBonusItemBackground(_bonusCells);
        ApplyBonusItemBackground(_bonusNextCells);
        ApplyBonusItemBackground(_bonusThirdCells);
        UISprites.Apply(_bonusKeyLabel,      hudImageLibrary.bonusKeyBadge);
        UISprites.Apply(_bonusNextKeyLabel,  hudImageLibrary.bonusKeyBadge);
        UISprites.Apply(_bonusThirdKeyLabel, hudImageLibrary.bonusKeyBadge);
    }

    void ApplyBonusItemBackground(VisualElement cells)
    {
        if (cells == null || cells.parent == null || hudImageLibrary == null) return;
        UISprites.Apply(cells.parent, hudImageLibrary.bonusPreviewItem);
        if (hudImageLibrary.bonusPreviewItem != null)
            cells.style.backgroundColor = Color.clear;
    }

    void SyncWeightGuideImages()
    {
        var spriteSet = _tower?.NumberSpriteSetAsset;
        if (spriteSet == null) return;
        for (int i = 0; i < _weightGuideImages.Length; i++)
            UISprites.Apply(_weightGuideImages[i], spriteSet.GetSprite(i + 1));
    }

    // ── 결과 화면 ─────────────────────────────────────────────────────────

    void ShowResultHud(VisualTreeAsset assignedAsset, string assetPath, string title, int score, int target, string targetPrefix = "TARGET")
    {
        EnsureHudDocument();
        if (hudDocument == null) return;
        EnsureHudPanelSettings();

        var tree = LoadVisualTree(assignedAsset, assetPath);
        if (tree == null) return;

        _showingResultHud = true;
        hudDocument.visualTreeAsset = tree;
        hudDocument.enabled = true;

        var root = hudDocument.rootVisualElement;
        if (root == null) return;

        root.style.display    = DisplayStyle.Flex;
        root.style.visibility = Visibility.Visible;
        root.style.opacity    = 1f;
        // BindHud에서 PickingMode.Ignore로 설정된 값을 복원해 버튼 클릭이 막히지 않게 한다.
        root.pickingMode = PickingMode.Position;

        _resultTitleLabel        = root.Q<Label>("Title");
        _resultCurrentScoreLabel = root.Q<Label>("CurrentScore");
        _resultTargetScoreLabel  = root.Q<Label>("TargetScore");

        if (_resultTitleLabel != null)        _resultTitleLabel.text        = title;
        if (_resultCurrentScoreLabel != null) _resultCurrentScoreLabel.text = $"SCORE: {score}";
        if (_resultTargetScoreLabel != null)  _resultTargetScoreLabel.text  = $"{targetPrefix}: {target}";

        var restartButton = root.Q<UnityEngine.UIElements.Button>("RestartButton");
        if (restartButton != null)
            restartButton.clicked += () => _tower?.Restart();

        var mainMenuButton = root.Q<UnityEngine.UIElements.Button>("MainMenuButton");
        if (mainMenuButton != null)
            mainMenuButton.clicked += () =>
            {
                if (IsEndlessMode) GameManager.Instance.GoToModeSelect = true;
                SceneManager.LoadScene(IsEndlessMode ? _lobbySceneName : _stageSelectSceneName);
            };

        var nextButton = root.Q<UnityEngine.UIElements.Button>("NextButton");
        if (nextButton != null)
            nextButton.clicked += GoToNextStage;
    }

    void GoToNextStage()
    {
        int nextIndex = GameManager.Instance.CurrentStageIndex + 1;
        if (nextIndex >= GameManager.Instance.StageCount)
        {
            GameManager.Instance.GoToModeSelect = true;
            SceneManager.LoadScene(_lobbySceneName);
            return;
        }
        GameManager.Instance.CurrentStageIndex = nextIndex;
        GameManager.Instance.PendingLevelScene = $"Level{nextIndex + 1}";
        SceneManager.LoadScene(_dialogueSceneName);
    }

    static VisualTreeAsset LoadVisualTree(VisualTreeAsset assigned, string assetPath)
    {
        if (assigned != null) return assigned;
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
#else
        return null;
#endif
    }

    // ── 점수 팝업 애니메이션 ──────────────────────────────────────────────

    IEnumerator AnimateScorePopup(int delta)
    {
        float duration = 0.85f;
        float elapsed  = 0f;

        _scorePopupText.text = delta > 0 ? $"+{delta}" : delta.ToString();
        _scorePopupText.style.color   = new Color(1f, 175f / 255f, 0f, 1f);
        _scorePopupText.style.display = DisplayStyle.Flex;

        while (elapsed < duration && _scorePopupText != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _scorePopupText.style.top     = -t * 18f;
            _scorePopupText.style.opacity = 1f - t;
            yield return null;
        }

        if (_scorePopupText != null)
        {
            _scorePopupText.text          = string.Empty;
            _scorePopupText.style.top     = 0f;
            _scorePopupText.style.opacity = 1f;
            _scorePopupText.style.display = DisplayStyle.None;
        }
        _scorePopupRoutine = null;
    }

    // ── 보너스 프리뷰 ────────────────────────────────────────────────────

    void UpdateBonusPreviewSlot(VisualElement container, List<VisualElement> elements, TetrominoPreset preset)
    {
        if (container == null) return;
        if (_tower != null && _tower.BonusTetrominoSpriteSet != null)
        {
            UpdateBonusPreviewImageSlot(container, elements,
                Util.BonusPresetSprite(_tower.BonusTetrominoSpriteSet, preset));
            return;
        }

        if (_tower == null) return;
        var cells = TetrominoShapeUtil.GetCells(Vector2Int.zero, preset, 0);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in cells)
        {
            minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y);
            maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y);
        }

        float fw = Util.BonusPreviewCellsFallbackWidth(container);
        float fh = Util.BonusPreviewCellsFallbackHeight(container);
        float cw = Util.ResolvedOrDefault(container.resolvedStyle.width,  fw);
        float ch = Util.ResolvedOrDefault(container.resolvedStyle.height, fh);
        float cs = Util.BonusPreviewCellSize(container, elements);
        float w  = (maxX - minX + 1) * cs;
        float h  = (maxY - minY + 1) * cs;
        cw = Mathf.Max(cw, w); ch = Mathf.Max(ch, h);
        float ox = (cw - w) * 0.5f, oy = (ch - h) * 0.5f;
        container.style.position = Position.Relative;

        var color = Util.TetrominoColor(preset);
        for (int i = 0; i < elements.Count; i++)
        {
            var el = elements[i];
            if (i >= cells.Count) { el.style.display = DisplayStyle.None; continue; }
            var cell = cells[i];
            el.style.display          = DisplayStyle.Flex;
            el.style.position         = Position.Absolute;
            el.style.left             = ox + (cell.x - minX) * cs;
            el.style.top              = oy + (maxY - cell.y) * cs;
            el.style.backgroundColor  = color;
            el.style.backgroundImage  = StyleKeyword.Null;
        }
    }

    static void UpdateBonusPreviewImageSlot(VisualElement container, List<VisualElement> elements, Sprite sprite)
    {
        container.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;
        container.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        if (elements == null) return;
        foreach (var el in elements) el.style.display = DisplayStyle.None;
    }

    // ── UI 헬퍼 ──────────────────────────────────────────────────────────

    static void SetLabelText(Label label, string text) { if (label != null) label.text = text; }

    static void ForceLabel(Label label, string text)
    {
        if (label == null) return;
        label.text             = text;
        label.style.display    = DisplayStyle.Flex;
        label.style.visibility = Visibility.Visible;
        label.style.opacity    = 1f;
        label.pickingMode      = PickingMode.Ignore;
    }

    static void ForceBonusKeyLabel(Label label, string text)
    {
        if (label == null) return;
        label.text             = text;
        label.style.display    = DisplayStyle.Flex;
        label.style.visibility = Visibility.Visible;
        label.style.opacity    = 1f;
        label.pickingMode      = PickingMode.Ignore;
    }

    static void MakeVisible(VisualElement panel)
    {
        if (panel == null) return;
        panel.style.display    = DisplayStyle.Flex;
        panel.style.visibility = Visibility.Visible;
        panel.style.opacity    = 1f;
        panel.pickingMode      = PickingMode.Ignore;
    }

    static VisualElement CreatePanel(VisualElement root, string name)
    {
        var panel = new VisualElement { name = name };
        panel.pickingMode = PickingMode.Ignore;
        root.Add(panel);
        return panel;
    }

    static Label CreateLabel(VisualElement parent, string name, string text)
    {
        var label = new Label(text) { name = name };
        label.pickingMode = PickingMode.Ignore;
        parent?.Add(label);
        return label;
    }
}
