using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(UIDocument))]
public class UIManager : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

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

    private Button singlePlayButton;
    private Button multiPlayButton;
    private Button[] stageButtons;

    private int selectedStage = 1;
    private bool isMultiPlay;
    private bool settingOpenedFromGame;

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

        BindLabels();
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

    private void BindLabels()
    {
        modeTitleLabel = root.Q<Label>("mode-title-label");
        modeDescriptionLabel = root.Q<Label>("mode-description-label");
        stageLabel = root.Q<Label>("stage-label");
        winnerLabel = root.Q<Label>("winner-label");
        scoreLabel = root.Q<Label>("score-label");
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

        singlePlayButton.clicked += () =>
        {
            SetMode(false);
            ShowStageSelect();
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

        modeTitleLabel.text = isMultiPlay ? "멀티" : "싱글플레이";
        modeDescriptionLabel.text = isMultiPlay
            ? "친구와 함께 같은 타워를 번갈아 쌓고 먼저 무너뜨리지 않는 사람이 승리합니다."
            : "혼자서 블록 타워를 쌓고 최고 점수에 도전합니다.";
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
