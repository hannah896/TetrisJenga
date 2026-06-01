using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// StageScene 전용 UI 컨트롤러. 스테이지 미리보기/시작 화면과 설정 팝업을 제어한다.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class StageUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    // VContainer Loader가 Addressables 로드 후 Initialize로 주입한다.
    private StageUIImageLibrarySO images;
    private SettingUIImageLibrarySO settingImages;

    [Header("이동할 씬 이름")]
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private VisualElement root;
    private VisualElement stageScreen;

    private readonly SettingPopupController setting = new SettingPopupController();

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
    }

    /// <summary>VContainer Loader가 Addressables 로드 완료 후 호출한다.</summary>
    public void Initialize(StageUIImageLibrarySO stageImages, SettingUIImageLibrarySO settingLibrary)
    {
        images = stageImages;
        settingImages = settingLibrary;

        root = uiDocument.rootVisualElement;
        stageScreen = root.Q<VisualElement>("stage-screen");

        if (stageScreen == null)
        {
            Debug.LogError("StageUIController: stage-screen 요소를 찾지 못했습니다. UIDocument Source Asset이 StageUI.uxml인지 확인하세요.");
            enabled = false;
            return;
        }

        ApplySprites();
        setting.Initialize(root, settingImages, onRestart: GoToLobby);

        Button startButton = root.Q<Button>("start-button");
        if (startButton != null)
        {
            startButton.clicked += () => LoadScene(gameSceneName);
        }
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

        if (!setting.IsOpen && WasRightClickThisFrame())
        {
            GoToLobby();
        }
    }

    private void ApplySprites()
    {
        if (images == null)
        {
            return;
        }

        UISprites.Apply(stageScreen, images.background);
        UISprites.Apply(root.Q<VisualElement>("stage-title"), images.title);
        UISprites.Apply(root.Q<VisualElement>("stage-view"), images.stageView);
        UISprites.Apply(root.Q<VisualElement>("preview-image"), images.previewImage);
        UISprites.Apply(root.Q<VisualElement>("explain-box"), images.explainBox);
        UISprites.Apply(root.Q<VisualElement>("start-button"), images.startButton);
    }

    private void GoToLobby()
    {
        LoadScene(lobbySceneName);
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("StageUIController: 이동할 씬 이름이 비어 있습니다.");
            return;
        }

        SceneManager.LoadScene(sceneName);
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
