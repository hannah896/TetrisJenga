using JSAM;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// LobbyScene 전용 UI 컨트롤러. 로비 화면과 모드 선택 화면을 토글하고
/// 설정 팝업을 공통 SettingPopupController로 제어한다.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LobbyUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    // VContainer Loader가 Addressables 로드 후 Initialize로 주입한다.
    private LobbyUIImageLibrarySO images;
    private SettingUIImageLibrarySO settingImages;

    [Header("이동할 씬 이름")]
    [SerializeField] private string storySceneName = "StageScene";
    [SerializeField] private string endlessSceneName = "Endless";

    private VisualElement root;
    private VisualElement lobbyScreen;
    private VisualElement modeSelectScreen;
    private VisualElement previewImage;
    private Label previewLabel;
    private Label modeExplainText;

    private readonly UI_Setting_Controller _uiSetting = new UI_Setting_Controller();

    private const string EndlessTitle = "Endless Mode";
    private const string EndlessDesc = "끝없이 블록을 쌓아 최고 점수에 도전합니다.";
    private const string StoryTitle = "Story";
    private const string StoryDesc = "스테이지를 하나씩 클리어하며 진행합니다.";
    private const string MultiTitle = "봇전 (CPU)";
    private const string MultiDesc = "준비 중입니다. (잠금)";

    private const string EndlessUnlockShownKey = "endless_unlock_notified";
    private Button _endlessButton;
    private bool _endlessUnlocked;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
    }

    /// <summary>VContainer Loader가 Addressables 로드 완료 후 호출한다.</summary>
    public void Initialize(LobbyUIImageLibrarySO lobbyImages, SettingUIImageLibrarySO settingLibrary)
    {
        images = lobbyImages;
        settingImages = settingLibrary;

        root = uiDocument.rootVisualElement;

        lobbyScreen = root.Q<VisualElement>("lobby-screen");
        modeSelectScreen = root.Q<VisualElement>("mode-select-screen");
        previewImage = root.Q<VisualElement>("preview-image");
        previewLabel = root.Q<Label>("preview-label");
        modeExplainText = root.Q<Label>("mode-explain-text");

        if (lobbyScreen == null || modeSelectScreen == null)
        {
            Debug.LogError("LobbyUIController: lobby-screen / mode-select-screen 요소를 찾지 못했습니다. UIDocument Source Asset이 LobbyUI.uxml인지 확인하세요.");
            enabled = false;
            return;
        }

        ApplySprites();
        _uiSetting.Initialize(root, settingImages, onRestart: ShowLobby);
        BindButtons();
        RefreshEndlessUnlockState();
        ShowLobby();
    }

    private void Update()
    {
        if (root == null)
        {
            return;
        }

        if (WasEscapePressedThisFrame())
        {
            _uiSetting.Toggle();
            return;
        }

        // 모드 선택 화면에서 우클릭 시 로비로 복귀 (피그마 툴팁: 우클릭 = 뒤로)
        if (!_uiSetting.IsOpen && IsVisible(modeSelectScreen) && WasRightClickThisFrame())
        {
            ShowLobby();
        }
    }

    private void BindButtons()
    {
        Button playButton = root.Q<Button>("play-button");
        if (playButton != null)
        {
            playButton.clicked += ShowModeSelect;
            playButton.clicked += () => AudioPlayback.PlaySound(_AudioLibrarySounds.Play);
        }

        Button exitButton = root.Q<Button>("exit-button");
        if (exitButton != null)
        {
            exitButton.clicked += QuitGame;
            exitButton.clicked += () => AudioPlayback.PlaySound(_AudioLibrarySounds.BtnClick);
        }

        _endlessButton = root.Q<Button>("endless-button");
        if (_endlessButton != null)
        {
            _endlessButton.clicked += () => { if (_endlessUnlocked) { GameManager.Instance.CurrentStageIndex = GameManager.EndlessStageIndex; LoadScene(endlessSceneName); } };
            _endlessButton.clicked += () => { if (_endlessUnlocked) AudioPlayback.PlaySound(_AudioLibrarySounds.BtnClick); };
            _endlessButton.RegisterCallback<PointerEnterEvent>(_ => SetPreview(EndlessTitle, EndlessDesc));
        }

        Button storyButton = root.Q<Button>("story-button");
        if (storyButton != null)
        {
            storyButton.clicked += () => LoadScene(storySceneName);
            storyButton.clicked+= () => AudioPlayback.PlaySound(_AudioLibrarySounds.BtnClick);
            storyButton.RegisterCallback<PointerEnterEvent>(_ => SetPreview(StoryTitle, StoryDesc));
        }

        // 멀티(봇전)는 현재 잠금이라 hover 설명만 표시한다.
        Button multiButton = root.Q<Button>("multi-button");
        multiButton?.RegisterCallback<PointerEnterEvent>(_ => SetPreview(MultiTitle, MultiDesc));
    }

    private void ApplySprites()
    {
        if (images != null)
        {
            UISprites.Apply(lobbyScreen, images.lobbyBackground);
            UISprites.Apply(modeSelectScreen, images.mainMenuBackground);
            UISprites.Apply(root.Q<VisualElement>("play-button"), images.playButton);
            UISprites.Apply(root.Q<VisualElement>("exit-button"), images.exitButton);
            UISprites.Apply(root.Q<VisualElement>("main-title"), images.mainTitle);
            UISprites.Apply(root.Q<VisualElement>("endless-button"), images.endlessButton);
            UISprites.Apply(root.Q<VisualElement>("story-button"), images.storyButton);
            UISprites.Apply(root.Q<VisualElement>("multi-button"), images.multiButton);
            UISprites.Apply(previewImage, images.previewImage);
            UISprites.Apply(root.Q<VisualElement>("mode-explain"), images.modeExplainBox);
        }

        // 타이틀 로고 스프라이트가 있으면 텍스트를 숨기고 이미지로 표시
        if (images != null && images.titleLogo != null)
        {
            Label title = root.Q<Label>("lobby-title");
            if (title != null)
            {
                title.text = string.Empty;
                UISprites.Apply(title, images.titleLogo);
            }
        }
    }

    private void SetPreview(string title, string description)
    {
        if (previewLabel != null)
        {
            previewLabel.text = title;
        }

        if (modeExplainText != null)
        {
            modeExplainText.text = description;
        }
    }

    private void ShowLobby()
    {
        _uiSetting.Hide();
        SetVisible(lobbyScreen, true);
        SetVisible(modeSelectScreen, false);
    }

    private void ShowModeSelect()
    {
        SetVisible(lobbyScreen, false);
        SetVisible(modeSelectScreen, true);
        SetPreview(EndlessTitle, EndlessDesc);

        if (_endlessUnlocked && PlayerPrefs.GetInt(EndlessUnlockShownKey, 0) == 0)
        {
            PlayerPrefs.SetInt(EndlessUnlockShownKey, 1);
            PlayerPrefs.Save();
            ShowUnlockNotification("엔드리스 모드가 해금되었습니다!");
        }
    }

    private void RefreshEndlessUnlockState()
    {
        int stageCount = GameManager.Instance != null ? GameManager.Instance.StageCount : 6;
        _endlessUnlocked = StageProgress.IsAllCleared(stageCount);

        if (_endlessButton == null) return;

        if (_endlessUnlocked)
            _endlessButton.RemoveFromClassList("btn--disabled");
        else
            _endlessButton.AddToClassList("btn--disabled");
    }

    private void ShowUnlockNotification(string message)
    {
        var notify = new Label(message);
        notify.style.position = Position.Absolute;
        notify.style.top = 40;
        notify.style.left = 0;
        notify.style.right = 0;
        notify.style.unityTextAlign = TextAnchor.UpperCenter;
        notify.style.fontSize = 22;
        notify.style.color = new UnityEngine.Color(1f, 0.92f, 0.3f, 1f);
        notify.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
        notify.style.opacity = 1f;
        modeSelectScreen.Add(notify);

        float elapsed = 0f;
        const float duration = 2.5f;
        const float fadeStart = 1.5f;
        root.schedule.Execute(() =>
        {
            elapsed += 0.016f;
            float t = Mathf.Clamp01((elapsed - fadeStart) / (duration - fadeStart));
            notify.style.opacity = 1f - t;
            if (elapsed >= duration)
            {
                notify.RemoveFromHierarchy();
                return;
            }
        }).Every(16).Until(() => elapsed >= duration);
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("LobbyUIController: 이동할 씬 이름이 비어 있습니다.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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

    private static bool IsVisible(VisualElement element)
    {
        return element != null && !element.ClassListContains("hidden");
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

    private bool WasRightClickThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }
}
