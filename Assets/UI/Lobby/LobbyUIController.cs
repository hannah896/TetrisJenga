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

    private readonly SettingPopupController setting = new SettingPopupController();

    private const string EndlessTitle = "Endless Mode";
    private const string EndlessDesc = "끝없이 블록을 쌓아 최고 점수에 도전합니다.";
    private const string StoryTitle = "Story";
    private const string StoryDesc = "스테이지를 하나씩 클리어하며 진행합니다.";
    private const string MultiTitle = "봇전 (CPU)";
    private const string MultiDesc = "준비 중입니다. (잠금)";

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
        setting.Initialize(root, settingImages, onRestart: ShowLobby);
        BindButtons();
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
            setting.Toggle();
            return;
        }

        // 모드 선택 화면에서 우클릭 시 로비로 복귀 (피그마 툴팁: 우클릭 = 뒤로)
        if (!setting.IsOpen && IsVisible(modeSelectScreen) && WasRightClickThisFrame())
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
        }

        Button exitButton = root.Q<Button>("exit-button");
        if (exitButton != null)
        {
            exitButton.clicked += QuitGame;
        }

        Button endlessButton = root.Q<Button>("endless-button");
        if (endlessButton != null)
        {
            endlessButton.clicked += () => LoadScene(endlessSceneName);
            endlessButton.RegisterCallback<PointerEnterEvent>(_ => SetPreview(EndlessTitle, EndlessDesc));
        }

        Button storyButton = root.Q<Button>("story-button");
        if (storyButton != null)
        {
            storyButton.clicked += () => LoadScene(storySceneName);
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
        setting.Hide();
        SetVisible(lobbyScreen, true);
        SetVisible(modeSelectScreen, false);
    }

    private void ShowModeSelect()
    {
        SetVisible(lobbyScreen, false);
        SetVisible(modeSelectScreen, true);
        SetPreview(EndlessTitle, EndlessDesc);
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
