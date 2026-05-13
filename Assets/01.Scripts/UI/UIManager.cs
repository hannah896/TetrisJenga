using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(UIDocument))]
public class UIManager : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private UIImageLibrarySO lobbyImageLibrary;
    [SerializeField] private UIImageLibrarySO settingImageLibrary;

    private VisualElement root;
    private VisualElement titleScreen;
    private VisualElement modeSelectScreen;
    private VisualElement stageSelectScreen;
    private VisualElement gameScreen;
    private VisualElement resultScreen;
    private VisualElement settingPopup;
    private VisualElement resultPopup;

    private Label modeTitleLabel;
    private Label modeDescriptionLabel;
    private Label stageLabel;
    private Label winnerLabel;
    private Label scoreLabel;

    private Slider audioSlider;
    private Slider bgmSlider;
    private Slider sfxSlider;

    private Button singlePlayButton;
    private Button multiPlayButton;
    private Button[] stageButtons;

    private int selectedStage = 1;
    private bool isMultiPlay;
    private bool settingOpenedFromGame;

    private const string SingleModeTitle = "싱글플레이";
    private const string SingleModeDescription = "혼자서 블록 타워를 쌓고 최고 점수에 도전합니다.";
    private const string MultiModeTitle = "멀티플레이";
    private const string MultiModeDescription = "친구와 함께 같은 타워를 번갈아 쌓고 먼저 무너뜨리지 않는 사람이 승리합니다.";
    private const string AudioVolumeKey = "Settings.AudioVolume";
    private const string BgmVolumeKey = "Settings.BgmVolume";
    private const string SfxVolumeKey = "Settings.SfxVolume";
    private const string StageSceneName = "StageScene";
    private const float DefaultVolume = 1f;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (uiDocument == null)
        {
            Debug.LogError("UIManager needs a UIDocument on the same GameObject or assigned in the Inspector.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;

        BindScreens();
        if (!enabled)
        {
            return;
        }

        BindImages();
        BindLabels();
        BindSliders();
        if (!enabled)
        {
            return;
        }

        BindButtons();
        SelectStage(1);
        SetMode(false);
        ShowTitle();
    }

    private void Update()
    {
        if (WasEscapePressedThisFrame())
        {
            if (!settingPopup.ClassListContains("hidden"))
            {
                HideSettingPopup();
                return;
            }

            settingOpenedFromGame = !gameScreen.ClassListContains("hidden");
            ShowSettingPopup();
        }
    }

    private bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private void BindScreens()
    {
        titleScreen = root.Q<VisualElement>("title-screen");
        modeSelectScreen = root.Q<VisualElement>("mode-select-screen");
        stageSelectScreen = root.Q<VisualElement>("stage-select-screen");
        gameScreen = root.Q<VisualElement>("game-screen");
        resultScreen = root.Q<VisualElement>("result-screen");
        settingPopup = root.Q<VisualElement>("setting-popup");
        resultPopup = root.Q<VisualElement>("result-popup");

        if (titleScreen == null || modeSelectScreen == null || stageSelectScreen == null || gameScreen == null ||
            resultScreen == null || settingPopup == null || resultPopup == null)
        {
            Debug.LogError("UIManager could not find one or more screens. Check that UIDocument Source Asset is GameUI.uxml.");
            enabled = false;
        }
    }

    private void BindImages()
    {
        SetBackground(titleScreen, lobbyImageLibrary, UIImageId.LobbyBackground);
        SetBackground(modeSelectScreen, lobbyImageLibrary, UIImageId.LobbyBackground);
        SetBackground(root.Q<VisualElement>(className: "settings-art-popup"), settingImageLibrary, UIImageId.SettingBackground);

        SetBackgroundForClass("title-sprite--abyss", lobbyImageLibrary, UIImageId.TitleAbyss);
        SetBackgroundForClass("title-sprite--stack", lobbyImageLibrary, UIImageId.TitleStack);

        SetBackground(root.Q<Button>("play-button"), lobbyImageLibrary, UIImageId.PlayButton);
        SetBackground(root.Q<Button>("setting-button"), lobbyImageLibrary, UIImageId.SettingButton);
        SetBackground(root.Q<Button>("exit-button"), lobbyImageLibrary, UIImageId.ExitButton);
        SetBackground(root.Q<Button>("single-play-button"), lobbyImageLibrary, UIImageId.SinglePlayButton);
        SetBackground(root.Q<Button>("multi-play-button"), lobbyImageLibrary, UIImageId.MultiPlayButton);
    }

    private void SetBackgroundForClass(string className, UIImageLibrarySO imageLibrary, UIImageId imageId)
    {
        root.Query<VisualElement>(className: className)
            .ForEach(element => SetBackground(element, imageLibrary, imageId));
    }

    private static void SetBackground(VisualElement element, UIImageLibrarySO imageLibrary, UIImageId imageId)
    {
        if (element == null || imageLibrary == null)
        {
            return;
        }

        if (imageLibrary.TryGetSprite(imageId, out Sprite sprite) && sprite != null)
        {
            element.style.backgroundImage = new StyleBackground(sprite);
        }
    }

    private void BindLabels()
    {
        modeTitleLabel = root.Q<Label>("mode-title-label");
        modeDescriptionLabel = root.Q<Label>("mode-description-label");
        stageLabel = root.Q<Label>("stage-label");
        winnerLabel = root.Q<Label>("winner-label");
        scoreLabel = root.Q<Label>("score-label");
    }

    private void BindSliders()
    {
        audioSlider = root.Q<Slider>("audio-slider");
        bgmSlider = root.Q<Slider>("bgm-slider");
        sfxSlider = root.Q<Slider>("sfx-slider");

        if (audioSlider == null || bgmSlider == null || sfxSlider == null)
        {
            Debug.LogError("UIManager could not find one or more setting sliders. Check audio-slider, bgm-slider, and sfx-slider in GameUI.uxml.");
            enabled = false;
            return;
        }

        LoadVolumeSlider(audioSlider, AudioVolumeKey);
        LoadVolumeSlider(bgmSlider, BgmVolumeKey);
        LoadVolumeSlider(sfxSlider, SfxVolumeKey);

        audioSlider.RegisterValueChangedCallback(evt => SaveVolume(AudioVolumeKey, evt.newValue));
        bgmSlider.RegisterValueChangedCallback(evt => SaveVolume(BgmVolumeKey, evt.newValue));
        sfxSlider.RegisterValueChangedCallback(evt => SaveVolume(SfxVolumeKey, evt.newValue));
    }

    private void LoadVolumeSlider(Slider slider, string prefsKey)
    {
        float value = PlayerPrefs.GetFloat(prefsKey, DefaultVolume);
        slider.SetValueWithoutNotify(Mathf.Clamp01(value));
    }

    private void SaveVolume(string prefsKey, float value)
    {
        PlayerPrefs.SetFloat(prefsKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }

    private void BindButtons()
    {
        root.Q<Button>("play-button").clicked += ShowModeSelect;
        root.Q<Button>("setting-button").clicked += () =>
        {
            settingOpenedFromGame = false;
            ShowSettingPopup();
        };
        root.Q<Button>("exit-button").clicked += ExitGame;

        root.Q<Button>("setting-exit-button").clicked += HideSettingPopup;
        root.Q<Button>("setting-home-button").clicked += ShowTitle;

        singlePlayButton = root.Q<Button>("single-play-button");
        multiPlayButton = root.Q<Button>("multi-play-button");
        RegisterModeHoverEvents();

        singlePlayButton.clicked += () =>
        {
            SetMode(false);
            LoadStageScene();
        };

        multiPlayButton.clicked += () =>
        {
            SetMode(true);
            ShowStageSelect();
        };

        stageButtons = new Button[6];
        for (int i = 0; i < stageButtons.Length; i++)
        {
            int stageIndex = i + 1;
            Button stageButton = root.Q<Button>($"stage-{stageIndex}-button");
            stageButtons[i] = stageButton;

            if (stageButton != null)
            {
                stageButton.clicked += () => SelectStage(stageIndex);
            }
        }

        root.Q<Button>("stage-play-button").clicked += StartGame;
        root.Q<Button>("retry-button").clicked += RetryGame;
        root.Q<Button>("result-exit-button").clicked += ShowResultScreen;
        root.Q<Button>("home-button").clicked += ShowTitle;
        root.Q<Button>("next-button").clicked += NextStage;
        root.Q<Button>("final-exit-button").clicked += ExitGame;
    }

    private void RegisterModeHoverEvents()
    {
        singlePlayButton.RegisterCallback<PointerEnterEvent>(_ => PreviewMode(false));
        multiPlayButton.RegisterCallback<PointerEnterEvent>(_ => PreviewMode(true));
        singlePlayButton.RegisterCallback<PointerLeaveEvent>(_ => ClearModePreview());
        multiPlayButton.RegisterCallback<PointerLeaveEvent>(_ => ClearModePreview());
    }

    private void ShowOnly(VisualElement target)
    {
        titleScreen.AddToClassList("hidden");
        modeSelectScreen.AddToClassList("hidden");
        stageSelectScreen.AddToClassList("hidden");
        gameScreen.AddToClassList("hidden");
        resultScreen.AddToClassList("hidden");

        target.RemoveFromClassList("hidden");
    }

    private void SetMode(bool multiPlay)
    {
        isMultiPlay = multiPlay;
        singlePlayButton.EnableInClassList("selected", !isMultiPlay);
        multiPlayButton.EnableInClassList("selected", isMultiPlay);
        ClearModePreview();
    }

    private void PreviewMode(bool multiPlay)
    {
        modeTitleLabel.text = multiPlay ? MultiModeTitle : SingleModeTitle;
        modeDescriptionLabel.text = multiPlay ? MultiModeDescription : SingleModeDescription;
    }

    private void ClearModePreview()
    {
        modeTitleLabel.text = string.Empty;
        modeDescriptionLabel.text = string.Empty;
    }

    public void ShowTitle()
    {
        settingOpenedFromGame = false;
        HideSettingPopup();
        HideResultPopup();
        ShowOnly(titleScreen);
    }

    public void ShowModeSelect()
    {
        HideResultPopup();
        ShowOnly(modeSelectScreen);
    }

    public void ShowStageSelect()
    {
        HideResultPopup();
        ShowOnly(stageSelectScreen);
    }

    private void LoadStageScene()
    {
        HideSettingPopup();
        HideResultPopup();
        SceneManager.LoadScene(StageSceneName);
    }

    public void StartGame()
    {
        HideSettingPopup();
        HideResultPopup();
        stageLabel.text = $"Stage {selectedStage}";
        ShowOnly(gameScreen);

        Debug.Log($"Start game / Stage: {selectedStage} / Multi: {isMultiPlay}");
        // Connect this point to the real gameplay system when it is ready.
        // GameManager.Instance.StartStage(selectedStage, isMultiPlay);
    }

    public void SelectStage(int stage)
    {
        selectedStage = Mathf.Clamp(stage, 1, stageButtons?.Length ?? 6);

        if (stageButtons == null)
        {
            return;
        }

        for (int i = 0; i < stageButtons.Length; i++)
        {
            stageButtons[i]?.EnableInClassList("selected", i + 1 == selectedStage);
        }
    }

    public void RetryGame()
    {
        HideResultPopup();
        StartGame();
    }

    public void NextStage()
    {
        SelectStage(selectedStage + 1);
        ShowStageSelect();
        Debug.Log($"Next stage: {selectedStage}");
    }

    public void ShowSettingPopup()
    {
        settingPopup.RemoveFromClassList("hidden");
    }

    public void HideSettingPopup()
    {
        settingPopup.AddToClassList("hidden");

        if (!settingOpenedFromGame)
        {
            settingOpenedFromGame = false;
        }
    }

    public void ShowResultPopup(string winner)
    {
        winnerLabel.text = $"{winner} 승리!";
        resultPopup.RemoveFromClassList("hidden");
    }

    public void HideResultPopup()
    {
        resultPopup.AddToClassList("hidden");
    }

    public void ShowResultScreen()
    {
        ShowResultScreen(31000);
    }

    public void ShowResultScreen(int score)
    {
        HideSettingPopup();
        HideResultPopup();
        ShowOnly(resultScreen);
        scoreLabel.text = $"{score}점";
    }

    public void ExitGame()
    {
        Debug.Log("Exit game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
