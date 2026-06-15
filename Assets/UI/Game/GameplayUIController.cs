using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// SampleScene 전용 UI 컨트롤러. 싱글/멀티 HUD를 전환하고 클리어/게임오버 결과 팝업과
/// 설정 팝업을 제어한다. 점수 갱신과 결과 표시는 게임 로직에서 public 메서드로 호출한다.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameplayUIController : MonoBehaviour
{
    public enum GameMode
    {
        Single,
        Multi
    }

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private GameMode startMode = GameMode.Single;

    // VContainer Loader가 Addressables 로드 후 Initialize로 주입한다.
    private GameplayUIImageLibrarySO images;
    private SettingUIImageLibrarySO settingImages;

    [Header("이동할 씬 이름")]
    [SerializeField] private string stageSceneName = "StageScene";

    private VisualElement root;
    private VisualElement singleHud;
    private VisualElement multiHud;
    private VisualElement clearPopup;
    private VisualElement gameoverPopup;

    private Label singleCurrentScore;
    private Label singleTargetScore;
    private Label singleScoreAdd;
    private Label clearScore;
    private Label gameoverScore;
    private Label gameoverTarget;

    private readonly SettingPopupController setting = new SettingPopupController();

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
    }

    /// <summary>VContainer Loader가 Addressables 로드 완료 후 호출한다.</summary>
    public void Initialize(GameplayUIImageLibrarySO gameplayImages, SettingUIImageLibrarySO settingLibrary)
    {
        images = gameplayImages;
        settingImages = settingLibrary;

        root = uiDocument.rootVisualElement;

        singleHud = root.Q<VisualElement>("single-hud");
        multiHud = root.Q<VisualElement>("multi-hud");
        clearPopup = root.Q<VisualElement>("clear-popup");
        gameoverPopup = root.Q<VisualElement>("gameover-popup");

        singleCurrentScore = root.Q<Label>("single-current-score");
        singleTargetScore = root.Q<Label>("single-target-score");
        singleScoreAdd = root.Q<Label>("single-score-add");
        clearScore = root.Q<Label>("clear-score");
        gameoverScore = root.Q<Label>("gameover-score");
        gameoverTarget = root.Q<Label>("gameover-target");

        if (singleHud == null || multiHud == null)
        {
            Debug.LogError("GameplayUIController: single-hud / multi-hud 요소를 찾지 못했습니다. UIDocument Source Asset이 GameplayUI.uxml인지 확인하세요.");
            enabled = false;
            return;
        }

        ApplySprites();
        setting.Initialize(root, settingImages, onRestart: ReloadScene);
        BindButtons();

        SetMode(startMode);
        HideResults();
    }

    private void Update()
    {
        if (root == null)
        {
            return;
        }

        if (WasEscapePressedThisFrame())
        {
            setting.Toggle();
        }
    }

    // ---------- 게임 로직에서 호출하는 public API ----------

    public void SetMode(GameMode mode)
    {
        SetVisible(singleHud, mode == GameMode.Single);
        SetVisible(multiHud, mode == GameMode.Multi);
    }

    /// <summary>싱글 모드 점수 갱신.</summary>
    public void SetSingleScore(int current, int target, int gained)
    {
        if (singleCurrentScore != null)
        {
            singleCurrentScore.text = $"{current:00000} /";
        }

        if (singleTargetScore != null)
        {
            singleTargetScore.text = $"{target:00000}";
        }

        if (singleScoreAdd != null)
        {
            singleScoreAdd.text = $"+{gained}";
        }
    }

    public void ShowClear(int score)
    {
        if (clearScore != null)
        {
            clearScore.text = $"{score:00000}";
        }

        SetVisible(clearPopup, true);
        SetVisible(gameoverPopup, false);
    }

    public void ShowGameOver(int score, int target)
    {
        if (gameoverScore != null)
        {
            gameoverScore.text = $"{score:00000}";
        }

        if (gameoverTarget != null)
        {
            gameoverTarget.text = $"목표 {target:00000}";
        }

        SetVisible(gameoverPopup, true);
        SetVisible(clearPopup, false);
    }

    public void HideResults()
    {
        SetVisible(clearPopup, false);
        SetVisible(gameoverPopup, false);
    }

    // ---------- 내부 ----------

    private void BindButtons()
    {
        Bind("clear-stage-button", () => LoadScene(stageSceneName));
        Bind("clear-retry-button", ReloadScene);
        Bind("clear-next-button", () => LoadScene(stageSceneName));
        Bind("gameover-stage-button", () => LoadScene(stageSceneName));
        Bind("gameover-retry-button", ReloadScene);
    }

    private void Bind(string buttonName, System.Action action)
    {
        Button button = root.Q<Button>(buttonName);
        if (button != null)
        {
            button.clicked += action;
        }
    }

    private void ApplySprites()
    {
        if (images == null)
        {
            return;
        }

        UISprites.Apply(root.Q<VisualElement>("single-game-view"), images.gameViewFrame);
        UISprites.Apply(root.Q<VisualElement>("multi-game-view"), images.gameViewFrame);
        UISprites.Apply(root.Q<VisualElement>("preset-guide-panel"), images.presetGuidePanel);
        UISprites.Apply(root.Q<VisualElement>("preset-guide-box"), images.presetGuideBox);
        UISprites.Apply(root.Q<VisualElement>("score-add"), images.scoreAddBox);
        UISprites.Apply(root.Q<VisualElement>("score-panel"), images.scorePanel);
        UISprites.Apply(root.Q<VisualElement>("player-info"), images.playerInfoPanel);
        UISprites.Apply(root.Q<VisualElement>("opponent-info"), images.opponentInfoPanel);

        root.Query<VisualElement>(className: "next-box").ForEach(box => UISprites.Apply(box, images.nextBox));

        UISprites.Apply(root.Q<VisualElement>("clear-panel"), images.resultPanel);
        UISprites.Apply(root.Q<VisualElement>("gameover-panel"), images.resultPanel);
        UISprites.Apply(root.Q<VisualElement>("clear-title-box"), images.resultTitleBox);
        UISprites.Apply(root.Q<VisualElement>("gameover-title-box"), images.resultTitleBox);
        UISprites.Apply(root.Q<VisualElement>("clear-score-box"), images.resultScoreBox);
        UISprites.Apply(root.Q<VisualElement>("gameover-score-box"), images.resultScoreBox);

        root.Query<Button>(className: "result-button").ForEach(button => UISprites.Apply(button, images.resultButton));
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("GameplayUIController: 이동할 씬 이름이 비어 있습니다.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        if (element == null)
        {
            return;
        }

        if (visible)
        {
            element.RemoveFromClassList("hidden");
        }
        else
        {
            element.AddToClassList("hidden");
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
}
