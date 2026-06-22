using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public sealed class PostProcessingExclusionCamera : MonoBehaviour
{
    [SerializeField] Camera sourceCamera;
    [SerializeField] Transform targetRoot;
    [SerializeField] string exclusionLayerName = "BlockTowerNoPost";
    [SerializeField] string overlayCameraName = "BlockTower No Post Camera";
    [SerializeField] bool includeInactiveRenderers = true;
    [SerializeField] bool renderExcludedObjectsOrthographic = true;
    [SerializeField] string[] extraExcludedRootNames =
    {
        "PlacementZone",
        "TowerStackDivider",
        "BoundaryLeft",
        "BoundaryRight",
        "BoundaryBottom",
        "Floor",
        "PresetOutlinePreview",
        "HeldOutlinePreview"
    };
    [SerializeField] string[] camerasThatShouldSeeExcludedLayer =
    {
        "SubViewCamera"
    };

    Camera _overlayCamera;
    readonly Dictionary<Camera, Camera> _additionalOverlays = new();
    readonly Dictionary<Camera, int> _originalAdditionalMasks = new();
    int _layer = -1;
    int _lastMainMask;
    bool _hasLastMainMask;

    void OnEnable()
    {
        ResolveReferences();
    }

    void OnDisable()
    {
        RestoreSourceCameraMask();

        if (_overlayCamera != null)
            _overlayCamera.enabled = false;

        foreach (var overlay in _additionalOverlays.Values)
        {
            if (overlay != null)
                overlay.enabled = false;
        }

        foreach (var pair in _originalAdditionalMasks)
        {
            if (pair.Key != null)
                pair.Key.cullingMask = pair.Value;
        }

        _originalAdditionalMasks.Clear();
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    void LateUpdate()
    {
        ResolveReferences();
        ApplySetup();
    }

    void ResolveReferences()
    {
        if (sourceCamera == null)
            sourceCamera = Camera.main;

        if (targetRoot == null)
            targetRoot = transform;

        _layer = LayerMask.NameToLayer(exclusionLayerName);
    }

    void ApplySetup()
    {
        if (sourceCamera == null || targetRoot == null || _layer < 0)
            return;

        EnsureOverlayCamera();
        AssignRendererLayers();
        ConfigureCameraMasks();
        ConfigureAdditionalCameraMasks();
        CopyCameraSettings();
    }

    void EnsureOverlayCamera()
    {
        if (_overlayCamera != null)
            return;

        var existing = GameObject.Find(overlayCameraName);
        if (existing != null)
            _overlayCamera = existing.GetComponent<Camera>();

        if (_overlayCamera == null)
        {
            var go = new GameObject(overlayCameraName);
            _overlayCamera = go.AddComponent<Camera>();
        }

        _overlayCamera.enabled = true;

        var listener = _overlayCamera.GetComponent<AudioListener>();
        if (listener != null)
            DestroyLocal(listener);
    }

    void AssignRendererLayers()
    {
        int defaultLayer = LayerMask.NameToLayer("Default");
        var renderers = targetRoot.GetComponentsInChildren<Renderer>(includeInactiveRenderers);
        foreach (var renderer in renderers)
            ApplyRendererLayer(renderer, defaultLayer);

        foreach (string rootName in extraExcludedRootNames)
            AssignNamedRootLayer(rootName);
    }

    void AssignNamedRootLayer(string rootName)
    {
        if (string.IsNullOrWhiteSpace(rootName))
            return;

        var root = GameObject.Find(rootName);
        if (root == null)
            return;

        SetLayerRecursive(root.transform, _layer);

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(includeInactiveRenderers))
        {
            if (renderer != null)
                renderer.gameObject.layer = _layer;
        }
    }

    static void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null)
            return;

        root.gameObject.layer = layer;

        foreach (Transform child in root)
            SetLayerRecursive(child, layer);
    }

    void ApplyRendererLayer(Renderer renderer, int defaultLayer)
    {
        if (renderer == null)
            return;

        if (ShouldExcludeRenderer(renderer))
        {
            renderer.gameObject.layer = _layer;
        }
        else if (renderer.gameObject.layer == _layer && defaultLayer >= 0)
        {
            renderer.gameObject.layer = defaultLayer;
        }
    }

    static bool ShouldExcludeRenderer(Renderer renderer)
    {
        return renderer.GetComponentInParent<BlockCell>() != null
            || renderer.GetComponentInParent<NoPostProcessingRenderer>() != null
            || renderer.GetComponentInParent<GoldFishProjectile>() != null
            || renderer.gameObject.name.StartsWith("PresetOutline_", System.StringComparison.Ordinal)
            || renderer.gameObject.name.StartsWith("HeldOutline_", System.StringComparison.Ordinal)
            || HasAncestorNamed(renderer.transform, "PresetOutlinePreview")
            || HasAncestorNamed(renderer.transform, "HeldOutlinePreview");
    }

    static bool HasAncestorNamed(Transform transform, string objectName)
    {
        while (transform != null)
        {
            if (transform.name == objectName)
                return true;

            transform = transform.parent;
        }

        return false;
    }

    void ConfigureCameraMasks()
    {
        int layerMask = 1 << _layer;

        if (!_hasLastMainMask)
        {
            _lastMainMask = sourceCamera.cullingMask;
            _hasLastMainMask = true;
        }

        sourceCamera.cullingMask &= ~layerMask;
        _overlayCamera.cullingMask = layerMask;
    }

    void ConfigureAdditionalCameraMasks()
    {
        foreach (string cameraName in camerasThatShouldSeeExcludedLayer)
        {
            if (string.IsNullOrWhiteSpace(cameraName))
                continue;

            var cameraObject = GameObject.Find(cameraName);
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
            if (camera == null || camera == sourceCamera || camera == _overlayCamera)
                continue;

            ConfigureAdditionalCamera(camera);
        }
    }

    void ConfigureAdditionalCamera(Camera camera)
    {
        int layerMask = 1 << _layer;
        if (!_originalAdditionalMasks.ContainsKey(camera))
            _originalAdditionalMasks.Add(camera, camera.cullingMask);

        camera.cullingMask = _originalAdditionalMasks[camera] & ~layerMask;
        var overlay = EnsureAdditionalOverlay(camera);
        if (overlay == null)
            return;

        CopyCameraSettings(camera, overlay);
        overlay.cullingMask = layerMask;

        var mainData = sourceCamera.GetUniversalAdditionalCameraData();
        var cameraData = camera.GetUniversalAdditionalCameraData();
        var overlayData = overlay.GetUniversalAdditionalCameraData();

        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = mainData.renderPostProcessing;
        cameraData.volumeLayerMask = mainData.volumeLayerMask;
        cameraData.volumeTrigger = mainData.volumeTrigger;
        cameraData.antialiasing = mainData.antialiasing;
        cameraData.antialiasingQuality = mainData.antialiasingQuality;
        cameraData.stopNaN = mainData.stopNaN;
        cameraData.dithering = mainData.dithering;

        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderPostProcessing = false;
        var stack = cameraData.cameraStack;
        if (!stack.Contains(overlay))
            stack.Add(overlay);
    }

    Camera EnsureAdditionalOverlay(Camera camera)
    {
        if (_additionalOverlays.TryGetValue(camera, out var overlay) && overlay != null)
            return overlay;

        string overlayName = $"{camera.name} No Post Camera";
        var existing = GameObject.Find(overlayName);
        overlay = existing != null ? existing.GetComponent<Camera>() : null;
        if (overlay == null)
        {
            var go = new GameObject(overlayName);
            overlay = go.AddComponent<Camera>();
        }

        var listener = overlay.GetComponent<AudioListener>();
        if (listener != null)
            DestroyLocal(listener);

        overlay.enabled = true;
        _additionalOverlays[camera] = overlay;
        return overlay;
    }

    void CopyCameraSettings()
    {
        CopyCameraSettings(sourceCamera, _overlayCamera);

        ConfigureUrpCameraStack();
    }

    void CopyCameraSettings(Camera source, Camera destination)
    {
        int cullingMask = destination.cullingMask;
        destination.CopyFrom(source);
        destination.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        if (renderExcludedObjectsOrthographic)
        {
            destination.orthographic = true;
            destination.orthographicSize = VisibleHalfHeightAtTargetPlane(source);
        }
        destination.clearFlags = CameraClearFlags.Nothing;
        destination.backgroundColor = Color.clear;
        destination.cullingMask = cullingMask;
        destination.depth = source.depth + 1f;
        destination.targetTexture = null;
        destination.enabled = source.enabled;
    }

    float VisibleHalfHeightAtTargetPlane(Camera source)
    {
        if (source.orthographic)
            return source.orthographicSize;

        Vector3 planePoint = targetRoot != null ? targetRoot.position : Vector3.zero;
        var tower = targetRoot != null ? targetRoot.GetComponent<BlockTower>() : null;
        if (tower != null && tower.TowerRoot != null)
            planePoint = tower.TowerRoot.position;
        float distance = Mathf.Abs(Vector3.Dot(planePoint - source.transform.position, source.transform.forward));
        if (distance < 0.01f)
            distance = Mathf.Abs(planePoint.z - source.transform.position.z);

        return Mathf.Max(0.01f, distance * Mathf.Tan(source.fieldOfView * Mathf.Deg2Rad * 0.5f));
    }

    void RestoreSourceCameraMask()
    {
        if (sourceCamera != null && _hasLastMainMask)
            sourceCamera.cullingMask = _lastMainMask;

        _hasLastMainMask = false;
    }

    bool _loggedOnce;

    void ConfigureUrpCameraStack()
    {
        var sourceData  = sourceCamera.GetUniversalAdditionalCameraData();
        var overlayData = _overlayCamera.GetUniversalAdditionalCameraData();

        sourceData.renderType            = CameraRenderType.Base;
        overlayData.renderType           = CameraRenderType.Overlay;
        overlayData.renderPostProcessing = false;

        var stack = sourceData.cameraStack;
        if (!stack.Contains(_overlayCamera))
            stack.Add(_overlayCamera);

        if (!_loggedOnce)
        {
            _loggedOnce = true;
            Debug.Log($"[PostCam] layer={_layer}, overlayCamera={_overlayCamera?.name}, stackCount={stack.Count}, overlayInStack={stack.Contains(_overlayCamera)}, overlayType={overlayData.renderType}");
        }
    }

    static void DestroyLocal(Object obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}
