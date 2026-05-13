using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(UIDocument))]
public class StageMapUIController : MonoBehaviour
{
    [SerializeField] private UIImageLibrarySO stageImageLibrary;
    [SerializeField] private int stageCount = 10;
    [SerializeField] private float mapZoom = 0.72f;
    [SerializeField] private bool fitBackgroundWidthToCamera = true;

    private readonly List<Vector2> sampledSpline = new List<Vector2>();
    private readonly List<Button> stageButtons = new List<Button>();

    private VisualElement viewport;
    private VisualElement mapContent;
    private VisualElement splineLayer;
    private VisualElement bubbleLayer;
    private VisualElement submarine;
    private Label selectedStageLabel;

    private Vector2 mapPan;
    private Vector2 submarinePosition;
    private Vector2 dragStartPan;
    private Vector2 dragStartPointer;
    private int selectedStage = 1;
    private int draggingPointerId;
    private bool isDragging;
    private bool hasAppliedInitialFit;
    private MotionHandle submarineMoveHandle;

    private const float StageButtonSize = 96f;
    private const float SubmarineSize = 116f;
    private const float MinZoom = 0.45f;
    private const float MaxZoom = 2f;
    private const float DefaultMapWidth = 1376f;
    private const float DefaultMapHeight = 3096f;
    private const float CameraEdgePadding = 420f;
    private const float BubbleSpawnInterval = 0.045f;
    private const float BubbleLifeTime = 0.72f;
    private const float SmallBubbleMinSize = 6f;
    private const float SmallBubbleMaxSize = 13f;
    private const float MediumBubbleMinSize = 14f;
    private const float MediumBubbleMaxSize = 22f;
    private const float LargeBubbleMinSize = 24f;
    private const float LargeBubbleMaxSize = 34f;
    private const float ExtraSmallBubbleChance = 0.55f;
    private const float MediumBubbleChance = 0.48f;
    private const float LargeBubbleChance = 0.18f;
    private const float SplineGlowSpacing = 18f;
    private const float SplineCoreSpacing = 34f;

    private static readonly Vector2[] SplineControls =
    {
        new Vector2(520f, 2500f),
        new Vector2(744f, 2320f),
        new Vector2(540f, 2070f),
        new Vector2(850f, 1840f),
        new Vector2(650f, 1600f),
        new Vector2(360f, 1390f),
        new Vector2(520f, 1170f),
        new Vector2(820f, 950f),
        new Vector2(650f, 720f),
        new Vector2(400f, 550f),
        new Vector2(320f, 420f)
    };

    private float MapWidth => DefaultMapWidth;
    private float MapHeight => DefaultMapHeight;

    private void Awake()
    {
        BuildUI();
        SampleSpline();
        DrawSplineDots();
        CreateStageButtons();
        CreateSubmarine();
        SelectStage(1, true, true);
    }

    private void Update()
    {
        int stageDirection = GetStageNavigationInput();
        if (stageDirection == 0)
        {
            return;
        }

        MoveSelectedStage(stageDirection);
    }

    private void BuildUI()
    {
        UIDocument document = GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1f;
        root.style.overflow = Overflow.Hidden;
        root.style.backgroundColor = new Color(0.27f, 0.48f, 0.68f);

        viewport = new VisualElement { name = "stage-map-viewport" };
        viewport.style.position = Position.Absolute;
        viewport.style.left = 0f;
        viewport.style.top = 0f;
        viewport.style.right = 0f;
        viewport.style.bottom = 0f;
        viewport.style.overflow = Overflow.Hidden;
        root.Add(viewport);

        mapContent = new VisualElement { name = "stage-map-content" };
        mapContent.style.position = Position.Absolute;
        mapContent.style.left = 0f;
        mapContent.style.top = 0f;
        mapContent.style.width = MapWidth;
        mapContent.style.height = MapHeight;
        mapContent.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
        Sprite stageBackground = GetStageSprite(UIImageId.StageBackground);
        if (stageBackground != null)
        {
            mapContent.style.backgroundImage = new StyleBackground(stageBackground);
#pragma warning disable 0618
            mapContent.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
#pragma warning restore 0618
        }
        viewport.Add(mapContent);

        splineLayer = new VisualElement { name = "stage-spline-layer" };
        splineLayer.style.position = Position.Absolute;
        splineLayer.style.left = 0f;
        splineLayer.style.top = 0f;
        splineLayer.style.width = MapWidth;
        splineLayer.style.height = MapHeight;
        mapContent.Add(splineLayer);

        bubbleLayer = new VisualElement { name = "stage-bubble-layer" };
        bubbleLayer.pickingMode = PickingMode.Ignore;
        bubbleLayer.style.position = Position.Absolute;
        bubbleLayer.style.left = 0f;
        bubbleLayer.style.top = 0f;
        bubbleLayer.style.width = MapWidth;
        bubbleLayer.style.height = MapHeight;
        mapContent.Add(bubbleLayer);

        VisualElement hud = CreateHud();
        root.Add(hud);

        RegisterCameraControls();
        viewport.RegisterCallback<GeometryChangedEvent>(_ => ApplyInitialCameraFit());
    }

    private VisualElement CreateHud()
    {
        VisualElement hud = new VisualElement();
        hud.style.position = Position.Absolute;
        hud.style.left = 22f;
        hud.style.right = 22f;
        hud.style.bottom = 18f;
        hud.style.height = 74f;
        hud.style.flexDirection = FlexDirection.Row;
        hud.style.alignItems = Align.Center;
        hud.style.paddingLeft = 16f;
        hud.style.paddingRight = 16f;
        hud.style.backgroundColor = new Color(1f, 0.98f, 0.9f, 0.9f);
        hud.style.borderTopWidth = 4f;
        hud.style.borderBottomWidth = 4f;
        hud.style.borderLeftWidth = 4f;
        hud.style.borderRightWidth = 4f;

        selectedStageLabel = new Label("Stage 1");
        selectedStageLabel.style.minWidth = 190f;
        selectedStageLabel.style.fontSize = 28f;
        selectedStageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        hud.Add(selectedStageLabel);

        Button centerButton = CreateHudButton("Center");
        centerButton.clicked += CenterOnSelectedStage;
        hud.Add(centerButton);

        Button zoomOutButton = CreateHudButton("-");
        zoomOutButton.style.minWidth = 48f;
        zoomOutButton.clicked += () => Zoom(-0.1f, GetViewportCenter());
        hud.Add(zoomOutButton);

        Button zoomInButton = CreateHudButton("+");
        zoomInButton.style.minWidth = 48f;
        zoomInButton.clicked += () => Zoom(0.1f, GetViewportCenter());
        hud.Add(zoomInButton);

        Button playButton = CreateHudButton("PLAY");
        playButton.style.marginLeft = StyleKeyword.Auto;
        playButton.style.width = 150f;
        playButton.clicked += () => Debug.Log($"Stage selected: {selectedStage}");
        hud.Add(playButton);

        return hud;
    }

    private static Button CreateHudButton(string text)
    {
        Button button = new Button { text = text };
        button.style.height = 44f;
        button.style.minWidth = 96f;
        button.style.marginLeft = 4f;
        button.style.marginRight = 4f;
        button.style.backgroundColor = Color.white;
        button.style.borderTopWidth = 3f;
        button.style.borderBottomWidth = 3f;
        button.style.borderLeftWidth = 3f;
        button.style.borderRightWidth = 3f;
        button.style.fontSize = 20f;
        return button;
    }

    private void SampleSpline()
    {
        sampledSpline.Clear();

        for (int i = 0; i < SplineControls.Length - 1; i++)
        {
            Vector2 p0 = SplineControls[Mathf.Max(i - 1, 0)];
            Vector2 p1 = SplineControls[i];
            Vector2 p2 = SplineControls[i + 1];
            Vector2 p3 = SplineControls[Mathf.Min(i + 2, SplineControls.Length - 1)];

            for (int step = 0; step < 24; step++)
            {
                sampledSpline.Add(EvaluateCatmullRom(p0, p1, p2, p3, step / 24f));
            }
        }

        sampledSpline.Add(SplineControls[SplineControls.Length - 1]);
    }

    private void DrawSplineDots()
    {
        splineLayer.Clear();
        DrawSplineGlow();
        DrawSplineCore();
    }

    private void DrawSplineGlow()
    {
        float distanceSinceGlow = 0f;
        Vector2 previous = sampledSpline[0];

        for (int i = 0; i < sampledSpline.Count; i++)
        {
            Vector2 point = sampledSpline[i];
            distanceSinceGlow += Vector2.Distance(previous, point);

            if (i == 0 || distanceSinceGlow >= SplineGlowSpacing)
            {
                AddSplineCircle(point, 54f, new Color(0.18f, 0.74f, 1f, 0.14f), Color.clear, 0f);
                AddSplineCircle(point, 32f, new Color(0.58f, 0.94f, 1f, 0.22f), Color.clear, 0f);
                distanceSinceGlow = 0f;
            }

            previous = point;
        }
    }

    private void DrawSplineCore()
    {
        float distanceSinceDot = 0f;
        int sparkleIndex = 0;
        Vector2 previous = sampledSpline[0];

        for (int i = 0; i < sampledSpline.Count; i++)
        {
            Vector2 point = sampledSpline[i];
            distanceSinceDot += Vector2.Distance(previous, point);

            if (i == 0 || distanceSinceDot >= SplineCoreSpacing)
            {
                float size = distanceSinceDot < 45f ? 16f : 24f;
                AddSplineCircle(point, size, new Color(1f, 0.93f, 0.38f, 0.96f), new Color(1f, 1f, 0.86f, 0.82f), 2f);

                if (sparkleIndex % 3 == 0)
                {
                    AddSplineSparkle(point, sparkleIndex);
                }

                sparkleIndex++;
                distanceSinceDot = 0f;
            }

            previous = point;
        }
    }

    private void AddSplineSparkle(Vector2 point, int index)
    {
        float angle = index * 2.399963f;
        float radius = 18f + (index % 3) * 5f;
        float size = index % 2 == 0 ? 9f : 7f;
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        AddSplineCircle(point + offset, size, new Color(1f, 1f, 0.9f, 0.82f), Color.clear, 0f);
    }

    private void AddSplineCircle(Vector2 point, float size, Color fillColor, Color borderColor, float borderWidth)
    {
        VisualElement circle = new VisualElement();
        circle.pickingMode = PickingMode.Ignore;
        circle.style.position = Position.Absolute;
        circle.style.left = point.x - size * 0.5f;
        circle.style.top = point.y - size * 0.5f;
        circle.style.width = size;
        circle.style.height = size;
        circle.style.borderTopLeftRadius = size * 0.5f;
        circle.style.borderTopRightRadius = size * 0.5f;
        circle.style.borderBottomLeftRadius = size * 0.5f;
        circle.style.borderBottomRightRadius = size * 0.5f;
        circle.style.backgroundColor = fillColor;
        circle.style.borderTopWidth = borderWidth;
        circle.style.borderBottomWidth = borderWidth;
        circle.style.borderLeftWidth = borderWidth;
        circle.style.borderRightWidth = borderWidth;
        circle.style.borderTopColor = borderColor;
        circle.style.borderBottomColor = borderColor;
        circle.style.borderLeftColor = borderColor;
        circle.style.borderRightColor = borderColor;
        splineLayer.Add(circle);
    }

    private void CreateStageButtons()
    {
        stageButtons.Clear();

        for (int i = 0; i < stageCount; i++)
        {
            int stage = i + 1;
            Button button = new Button { text = stage.ToString() };
            button.name = $"stage-{stage}-button";
            button.style.position = Position.Absolute;
            button.style.width = StageButtonSize;
            button.style.height = StageButtonSize;
            button.style.borderTopLeftRadius = StageButtonSize * 0.5f;
            button.style.borderTopRightRadius = StageButtonSize * 0.5f;
            button.style.borderBottomLeftRadius = StageButtonSize * 0.5f;
            button.style.borderBottomRightRadius = StageButtonSize * 0.5f;
            button.style.backgroundColor = Color.clear;
            button.style.borderTopWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.fontSize = stage >= 10 ? 28f : 34f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.color = new Color(0.16f, 0.09f, 0.05f);
            Sprite stageButtonSprite = GetStageSprite(UIImageId.StageButton);
            if (stageButtonSprite != null)
            {
                button.style.backgroundImage = new StyleBackground(stageButtonSprite);
#pragma warning disable 0618
                button.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
#pragma warning restore 0618
            }

            Vector2 position = GetStagePosition(stage);
            button.style.left = position.x - StageButtonSize * 0.5f;
            button.style.top = position.y - StageButtonSize * 0.5f;
            button.clicked += () => SelectStage(stage, true, false);

            mapContent.Add(button);
            stageButtons.Add(button);
        }
    }

    private void CreateSubmarine()
    {
        submarine = new VisualElement { name = "submarine" };
        submarine.pickingMode = PickingMode.Ignore;
        submarine.style.position = Position.Absolute;
        submarine.style.width = SubmarineSize;
        submarine.style.height = SubmarineSize;
        Sprite submarineSprite = GetStageSprite(UIImageId.Submarine);
        if (submarineSprite != null)
        {
            submarine.style.backgroundImage = new StyleBackground(submarineSprite);
#pragma warning disable 0618
            submarine.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
#pragma warning restore 0618
        }

        mapContent.Add(submarine);
        SetSubmarinePosition(GetStagePosition(1));
    }

    private Sprite GetStageSprite(UIImageId imageId)
    {
        if (stageImageLibrary != null && stageImageLibrary.TryGetSprite(imageId, out Sprite sprite))
        {
            return sprite;
        }

        return null;
    }

    private void SelectStage(int stage, bool centerCamera, bool instantSubmarine)
    {
        selectedStage = Mathf.Clamp(stage, 1, stageCount);
        selectedStageLabel.text = $"Stage {selectedStage}";

        for (int i = 0; i < stageButtons.Count; i++)
        {
            bool selected = i + 1 == selectedStage;
            stageButtons[i].style.unityBackgroundImageTintColor = selected ? Color.white : new Color(0.88f, 0.88f, 0.88f);
        }

        Vector2 targetPosition = GetStagePosition(selectedStage) + new Vector2(0f, -82f);
        if (instantSubmarine)
        {
            CancelSubmarineMotion();
            SetSubmarinePosition(targetPosition);
        }
        else
        {
            MoveSubmarineTo(targetPosition);
        }

        if (centerCamera)
        {
            CenterOnSelectedStage();
        }
    }

    private void MoveSelectedStage(int direction)
    {
        int targetStage = Mathf.Clamp(selectedStage + direction, 1, stageCount);
        if (targetStage == selectedStage)
        {
            return;
        }

        SelectStage(targetStage, true, false);
    }

    private void SetSubmarinePosition(Vector2 position)
    {
        submarinePosition = position;
        if (submarine == null)
        {
            return;
        }

        submarine.style.left = position.x - SubmarineSize * 0.5f;
        submarine.style.top = position.y - SubmarineSize * 0.5f;
    }

    private void MoveSubmarineTo(Vector2 targetPosition)
    {
        CancelSubmarineMotion();

        Vector2 from = submarinePosition;
        Vector2 to = targetPosition;
        float distance = Vector2.Distance(from, to);
        float duration = Mathf.Clamp(distance / 760f, 0.35f, 1.35f);
        float bubbleElapsed = BubbleSpawnInterval;
        Vector2 direction = distance > 0.001f ? (to - from).normalized : Vector2.right;

        submarineMoveHandle = LMotion.Create(from, to, duration)
            .WithEase(Ease.OutExpo)
            .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
            .WithOnComplete(() =>
            {
                SetSubmarinePosition(to);
                submarineMoveHandle = MotionHandle.None;
            })
            .WithOnCancel(() => submarineMoveHandle = MotionHandle.None)
            .Bind(position =>
            {
                SetSubmarinePosition(position);

                bubbleElapsed += Time.unscaledDeltaTime;
                if (bubbleElapsed >= BubbleSpawnInterval)
                {
                    SpawnBubble(position, direction);
                    bubbleElapsed = 0f;
                }
            })
            .AddTo(gameObject);
    }

    private void CancelSubmarineMotion()
    {
        if (submarineMoveHandle.IsActive())
        {
            submarineMoveHandle.Cancel();
            submarineMoveHandle = MotionHandle.None;
        }
    }

    private void SpawnBubble(Vector2 submarineCenter, Vector2 moveDirection)
    {
        if (bubbleLayer == null)
        {
            return;
        }

        CreateBubble(submarineCenter, moveDirection, Random.Range(SmallBubbleMinSize, SmallBubbleMaxSize));

        if (Random.value < ExtraSmallBubbleChance)
        {
            CreateBubble(submarineCenter, moveDirection, Random.Range(SmallBubbleMinSize, SmallBubbleMaxSize));
        }

        if (Random.value < MediumBubbleChance)
        {
            CreateBubble(submarineCenter, moveDirection, Random.Range(MediumBubbleMinSize, MediumBubbleMaxSize));
        }

        if (Random.value < LargeBubbleChance)
        {
            CreateBubble(submarineCenter, moveDirection, Random.Range(LargeBubbleMinSize, LargeBubbleMaxSize));
        }
    }

    private void CreateBubble(Vector2 submarineCenter, Vector2 moveDirection, float size)
    {
        Vector2 sideOffset = new Vector2(-moveDirection.y, moveDirection.x) * Random.Range(-18f, 18f);
        Vector2 tailOffset = -moveDirection * Random.Range(SubmarineSize * 0.25f, SubmarineSize * 0.42f);
        Vector2 start = submarineCenter + tailOffset + sideOffset + new Vector2(Random.Range(-8f, 8f), Random.Range(-8f, 8f));

        VisualElement bubble = new VisualElement();
        bubble.pickingMode = PickingMode.Ignore;
        bubble.style.position = Position.Absolute;
        bubble.style.left = start.x - size * 0.5f;
        bubble.style.top = start.y - size * 0.5f;
        bubble.style.width = size;
        bubble.style.height = size;
        bubble.style.borderTopLeftRadius = size * 0.5f;
        bubble.style.borderTopRightRadius = size * 0.5f;
        bubble.style.borderBottomLeftRadius = size * 0.5f;
        bubble.style.borderBottomRightRadius = size * 0.5f;
        bubble.style.backgroundColor = new Color(0.78f, 0.94f, 1f, 0.36f);
        bubble.style.borderTopWidth = 2f;
        bubble.style.borderBottomWidth = 2f;
        bubble.style.borderLeftWidth = 2f;
        bubble.style.borderRightWidth = 2f;
        bubble.style.borderTopColor = new Color(1f, 1f, 1f, 0.62f);
        bubble.style.borderBottomColor = new Color(1f, 1f, 1f, 0.62f);
        bubble.style.borderLeftColor = new Color(1f, 1f, 1f, 0.62f);
        bubble.style.borderRightColor = new Color(1f, 1f, 1f, 0.62f);
        bubble.style.opacity = 0.85f;

        bubbleLayer.Add(bubble);
        StartCoroutine(AnimateBubble(bubble, start, size, moveDirection));
    }

    private System.Collections.IEnumerator AnimateBubble(VisualElement bubble, Vector2 start, float startSize, Vector2 moveDirection)
    {
        float elapsed = 0f;
        Vector2 drift = -moveDirection * Random.Range(18f, 42f) + new Vector2(Random.Range(-24f, 24f), Random.Range(-78f, -42f));
        float endSize = startSize * Random.Range(1.25f, 1.75f);

        while (elapsed < BubbleLifeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / BubbleLifeTime);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            Vector2 position = start + drift * eased;
            float size = Mathf.Lerp(startSize, endSize, eased);

            bubble.style.left = position.x - size * 0.5f;
            bubble.style.top = position.y - size * 0.5f;
            bubble.style.width = size;
            bubble.style.height = size;
            bubble.style.borderTopLeftRadius = size * 0.5f;
            bubble.style.borderTopRightRadius = size * 0.5f;
            bubble.style.borderBottomLeftRadius = size * 0.5f;
            bubble.style.borderBottomRightRadius = size * 0.5f;
            bubble.style.opacity = Mathf.Lerp(0.85f, 0f, t);

            yield return null;
        }

        bubble.RemoveFromHierarchy();
    }

    private Vector2 GetStagePosition(int stage)
    {
        float t = stageCount <= 1 ? 0f : (stage - 1f) / (stageCount - 1f);
        return GetPointAtPercent(t);
    }

    private Vector2 GetPointAtPercent(float percent)
    {
        float totalLength = 0f;
        for (int i = 1; i < sampledSpline.Count; i++)
        {
            totalLength += Vector2.Distance(sampledSpline[i - 1], sampledSpline[i]);
        }

        float targetLength = totalLength * Mathf.Clamp01(percent);
        float walkedLength = 0f;

        for (int i = 1; i < sampledSpline.Count; i++)
        {
            Vector2 from = sampledSpline[i - 1];
            Vector2 to = sampledSpline[i];
            float segmentLength = Vector2.Distance(from, to);

            if (walkedLength + segmentLength >= targetLength)
            {
                float segmentPercent = Mathf.InverseLerp(walkedLength, walkedLength + segmentLength, targetLength);
                return Vector2.Lerp(from, to, segmentPercent);
            }

            walkedLength += segmentLength;
        }

        return sampledSpline[sampledSpline.Count - 1];
    }

    private static Vector2 EvaluateCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void RegisterCameraControls()
    {
        viewport.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (IsInsideButton(evt.target as VisualElement))
            {
                return;
            }

            isDragging = true;
            draggingPointerId = evt.pointerId;
            dragStartPointer = new Vector2(evt.position.x, evt.position.y);
            dragStartPan = mapPan;
            viewport.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });

        viewport.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!isDragging || evt.pointerId != draggingPointerId)
            {
                return;
            }

            Vector2 pointer = new Vector2(evt.position.x, evt.position.y);
            mapPan = dragStartPan + pointer - dragStartPointer;
            ClampPan();
            ApplyCamera();
            evt.StopPropagation();
        });

        viewport.RegisterCallback<PointerUpEvent>(evt => EndDrag(evt.pointerId));
        viewport.RegisterCallback<PointerCancelEvent>(evt => EndDrag(evt.pointerId));
        viewport.RegisterCallback<WheelEvent>(evt =>
        {
            MoveSelectedStage(evt.delta.y > 0f ? -1 : 1);
            evt.StopPropagation();
        });
    }

    private static bool IsInsideButton(VisualElement element)
    {
        while (element != null)
        {
            if (element is Button)
            {
                return true;
            }

            element = element.parent;
        }

        return false;
    }

    private void EndDrag(int pointerId)
    {
        if (!isDragging || pointerId != draggingPointerId)
        {
            return;
        }

        isDragging = false;
        if (viewport.HasPointerCapture(pointerId))
        {
            viewport.ReleasePointer(pointerId);
        }
    }

    private void Zoom(float amount, Vector2 viewportPivot)
    {
        float oldZoom = mapZoom;
        float minZoom = fitBackgroundWidthToCamera ? GetFitWidthZoom() : MinZoom;
        float newZoom = Mathf.Clamp(mapZoom + amount, minZoom, MaxZoom);
        if (Mathf.Approximately(oldZoom, newZoom))
        {
            return;
        }

        Vector2 mapPointAtPivot = (viewportPivot - mapPan) / oldZoom;
        mapZoom = newZoom;
        mapPan = viewportPivot - mapPointAtPivot * mapZoom;
        ClampPan();
        ApplyCamera();
    }

    private void ApplyInitialCameraFit()
    {
        if (fitBackgroundWidthToCamera)
        {
            mapZoom = GetFitWidthZoom();
        }

        if (!hasAppliedInitialFit)
        {
            hasAppliedInitialFit = true;
            CenterOnSelectedStage();
            return;
        }

        ClampPan();
        ApplyCamera();
    }

    private float GetFitWidthZoom()
    {
        Rect bounds = viewport.contentRect;
        if (bounds.width <= 0f)
        {
            return mapZoom;
        }

        return Mathf.Clamp(bounds.width / MapWidth, MinZoom, MaxZoom);
    }

    private void CenterOnSelectedStage()
    {
        Vector2 stagePosition = GetStagePosition(selectedStage);
        mapPan = GetViewportCenter() - stagePosition * mapZoom;
        ClampPan();
        ApplyCamera();
    }

    private Vector2 GetViewportCenter()
    {
        Rect bounds = viewport.contentRect;
        return new Vector2(bounds.width * 0.5f, bounds.height * 0.5f);
    }

    private void ClampPan()
    {
        Rect bounds = viewport.contentRect;
        float scaledWidth = MapWidth * mapZoom;
        float scaledHeight = MapHeight * mapZoom;

        mapPan.x = scaledWidth <= bounds.width + 0.5f
            ? (bounds.width - scaledWidth) * 0.5f
            : Mathf.Clamp(mapPan.x, bounds.width - scaledWidth - CameraEdgePadding, CameraEdgePadding);

        mapPan.y = scaledHeight <= bounds.height + 0.5f
            ? (bounds.height - scaledHeight) * 0.5f
            : Mathf.Clamp(mapPan.y, bounds.height - scaledHeight - CameraEdgePadding, CameraEdgePadding);
    }

    private void ApplyCamera()
    {
        mapContent.style.translate = new Translate(mapPan.x, mapPan.y, 0f);
        mapContent.style.scale = new Scale(new Vector3(mapZoom, mapZoom, 1f));
    }

    private static int GetStageNavigationInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0;
        }

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
        {
            return 1;
        }

        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
        {
            return -1;
        }

        return 0;
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            return 1;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            return -1;
        }

        return 0;
#else
        return 0;
#endif
    }
}
