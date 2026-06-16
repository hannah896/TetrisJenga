using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// StageScene 전용 UI 컨트롤러.
/// 스크롤 맵 + 거품 경로 + 스테이지 노드 버튼을 관리한다.
///
/// [SplineContainer 좌표 규칙]
///   SplineContainer 오브젝트는 Position(0,0,0), Rotation(0,0,0), Scale(1,1,1) 유지.
///   노트 좌표: X ∈ [0,1], Y ∈ [0,1].
///   X=0 = 맵 왼쪽, X=1 = 맵 오른쪽.
///   Y=0 = 맵 하단(Stage 1 위치), Y=1 = 맵 상단(마지막 스테이지).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class StageUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    /// <summary>스테이지 경로를 정의하는 SplineContainer. 씬에 배치 후 할당.</summary>
    [SerializeField] private SplineContainer mapSpline;

    [Header("이동할 씬 이름")]
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private StageUIImageLibrarySO images;
    private SettingUIImageLibrarySO settingImages;
    private StageMapSO mapData;

    private VisualElement root;
    private VisualElement stageMap;
    private ScrollView stageMapScroll;

    private int selectedNodeIndex = -1;
    private readonly List<Button> nodeButtons = new();

    private readonly SettingPopupController setting = new();

    // 맵 콘텐츠 크기 (USS .stage-map 과 동기화)
    private const float MapWidth = 1127f;
    private const float MapHeight = 3000f;

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    /// <summary>VContainer Loader가 Addressables 로드 완료 후 호출한다.</summary>
    public void Initialize(StageUIImageLibrarySO stageImages, SettingUIImageLibrarySO settingLib, StageMapSO map)
    {
        images = stageImages;
        settingImages = settingLib;
        mapData = map;

        root = uiDocument.rootVisualElement;

        if (root.Q<VisualElement>("stage-screen") == null)
        {
            Debug.LogError("StageUIController: stage-screen 요소를 찾지 못했습니다.");
            enabled = false;
            return;
        }

        stageMapScroll = root.Q<ScrollView>("stage-map-scroll");
        stageMap = root.Q<VisualElement>("stage-map");

        ApplyStaticSprites();
        setting.Initialize(root, settingImages, onRestart: GoToLobby);

        var startButton = root.Q<Button>("start-button");
        if (startButton != null)
        {
            startButton.clicked += StartSelectedStage;
            startButton.style.display = DisplayStyle.None;
        }

        if (mapSpline != null && mapData != null && mapData.nodes.Count > 0)
            BuildMap();
    }

    private void BuildMap()
    {
        const int denseCount = 150;
        var densePath = new List<Vector2>(denseCount);
        var spline = mapSpline.Spline;

        for (int i = 0; i < denseCount; i++)
        {
            float t = (float)i / (denseCount - 1);
            float3 pt = SplineUtility.EvaluatePosition(spline, t);
            // Y 축 반전: 스플라인 Y=0(하단) → UI Y=MapHeight(맵 아래쪽)
            densePath.Add(new Vector2(pt.x * MapWidth, (1f - pt.y) * MapHeight));
        }

        var pathElem = new StageMapElement();
        stageMap.Insert(0, pathElem);

        int unlockedIdx = StageProgress.GetHighestUnlockedIndex(mapData.nodes.Count);

        // 잠금 해제된 구간까지만 경로 표시
        int activeEndIndex = unlockedIdx >= 1
            ? Mathf.RoundToInt(mapData.nodes[unlockedIdx].splineT * (denseCount - 1))
            : -1;
        pathElem.SetPath(densePath, activeEndIndex);

        // 노드 버튼 생성
        nodeButtons.Clear();
        for (int i = 0; i < mapData.nodes.Count; i++)
        {
            var node = mapData.nodes[i];
            bool isVisible = i <= unlockedIdx;
            bool isCleared = StageProgress.IsCleared(i);

            float3 pt = SplineUtility.EvaluatePosition(spline, node.splineT);
            var uiPos = new Vector2(pt.x * MapWidth, (1f - pt.y) * MapHeight);

            var btn = CreateNodeButton(i, uiPos, isVisible, isCleared, node);
            stageMap.Add(btn);
            nodeButtons.Add(btn);
        }

        // 잠금 해제된 마지막 스테이지로 스크롤
        ScrollToNode(unlockedIdx);

        // 기본 선택
        SelectStage(unlockedIdx);
    }

    private Button CreateNodeButton(int index, Vector2 uiPos, bool isVisible, bool isCleared, StageMapNodeData node)
    {
        var btn = new Button();
        btn.AddToClassList("stage-node-btn");

        if (!isVisible)
            btn.AddToClassList("stage-node-btn--locked");
        else if (isCleared)
            btn.AddToClassList("stage-node-btn--cleared");
        else
            btn.AddToClassList("stage-node-btn--current");

        btn.style.position = Position.Absolute;
        btn.style.left = uiPos.x - 40f;
        btn.style.top = uiPos.y - 40f;

        Sprite sprite = isVisible
            ? (isCleared ? mapData.nodeCleared : mapData.nodeNormal)
            : mapData.nodeLocked;
        if (sprite != null) UISprites.Apply(btn, sprite);

        var label = new Label(isVisible ? $"{index + 1}" : "?");
        label.AddToClassList("stage-node-btn__label");
        btn.Add(label);

        if (isVisible)
        {
            int captured = index;
            btn.clicked += () => SelectStage(captured);
        }

        return btn;
    }

    private void SelectStage(int index)
    {
        if (mapData == null || index < 0 || index >= mapData.nodes.Count) return;

        selectedNodeIndex = index;
        var info = mapData.nodes[index].stageInfo;

        // 미리보기 이미지
        var previewVe = root.Q<VisualElement>("preview-image");
        var previewLabel = root.Q<Label>("preview-label");
        if (info != null && info.PreviewImage != null)
        {
            UISprites.Apply(previewVe, info.PreviewImage);
            if (previewLabel != null) previewLabel.style.display = DisplayStyle.None;
        }
        else if (images != null && images.previewImage != null)
        {
            UISprites.Apply(previewVe, images.previewImage);
        }

        // 설명 텍스트
        var explainText = root.Q<Label>("explain-text");
        if (explainText != null)
        {
            explainText.text = info != null
                ? $"{info.StageName}\n{info.Description}\n목표 점수: {info.TargetScore}"
                : "스테이지 정보 없음";
        }

        // 플레이 버튼 표시
        var startBtn = root.Q<Button>("start-button");
        if (startBtn != null) startBtn.style.display = DisplayStyle.Flex;

        // 선택 하이라이트
        for (int i = 0; i < nodeButtons.Count; i++)
        {
            if (i == index) nodeButtons[i].AddToClassList("stage-node-btn--selected");
            else nodeButtons[i].RemoveFromClassList("stage-node-btn--selected");
        }
    }

    private void ScrollToNode(int index)
    {
        if (stageMapScroll == null || mapData == null || index >= mapData.nodes.Count) return;

        float3 pt = SplineUtility.EvaluatePosition(mapSpline.Spline, mapData.nodes[index].splineT);
        float targetY = (1f - pt.y) * MapHeight;

        // 레이아웃이 확정된 뒤 스크롤 오프셋 설정
        root.schedule.Execute(() =>
        {
            float viewH = stageMapScroll.layout.height;
            float offset = Mathf.Max(0f, targetY - viewH * 0.5f);
            stageMapScroll.scrollOffset = new Vector2(0f, offset);
        }).ExecuteLater(150);
    }

    private void ApplyStaticSprites()
    {
        if (images == null) return;
        UISprites.Apply(root.Q<VisualElement>("stage-screen"), images.background);
        UISprites.Apply(root.Q<VisualElement>("stage-title"), images.title);
        UISprites.Apply(root.Q<VisualElement>("stage-map"), images.mapBackground);
        UISprites.Apply(root.Q<VisualElement>("explain-box"), images.explainBox);
        UISprites.Apply(root.Q<Button>("start-button"), images.startButton);
        UISprites.Apply(root.Q<VisualElement>("preview-image"), images.previewImage);
    }

    private void StartSelectedStage()
    {
        string scene = null;
        if (mapData != null && selectedNodeIndex >= 0 && selectedNodeIndex < mapData.nodes.Count)
        {
            var info = mapData.nodes[selectedNodeIndex].stageInfo;
            if (info != null && !string.IsNullOrEmpty(info.SceneName))
                scene = info.SceneName;
        }
        // SO에 지정이 없으면 스테이지 순서대로 Level1, Level2... 로 이동한다.
        if (string.IsNullOrEmpty(scene))
            scene = $"Level{selectedNodeIndex + 1}";
        // 인게임(BlockTower/GameManager)이 결과를 어느 스테이지에 기록할지 알 수 있도록 전달.
        GameManager.Instance.CurrentStageIndex = selectedNodeIndex;
        LoadScene(scene);
    }
    private void GoToLobby() => LoadScene(lobbySceneName);

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("StageUIController: 이동할 씬 이름이 비어 있습니다.");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }

    private void Update()
    {
        if (root == null) return;
        if (WasEscapePressedThisFrame()) { setting.Toggle(); return; }
        if (!setting.IsOpen && WasRightClickThisFrame()) GoToLobby();
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
