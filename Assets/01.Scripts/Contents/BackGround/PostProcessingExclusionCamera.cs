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
    [SerializeField] string[] extraExcludedRootNames =
    {
        "PlacementZone",
        "TowerStackDivider",
        "BoundaryLeft",
        "BoundaryRight",
        "Floor",
        "PresetOutlinePreview"
    };
    [SerializeField] string[] camerasThatShouldSeeExcludedLayer =
    {
        "SubViewCamera"
    };

    Camera _overlayCamera;
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
            || HasAncestorNamed(renderer.transform, "PresetOutlinePreview");
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
        int layerMask = 1 << _layer;

        foreach (string cameraName in camerasThatShouldSeeExcludedLayer)
        {
            if (string.IsNullOrWhiteSpace(cameraName))
                continue;

            var cameraObject = GameObject.Find(cameraName);
            var camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
            if (camera == null || camera == sourceCamera || camera == _overlayCamera)
                continue;

            camera.cullingMask |= layerMask;
            CopyPostProcessingSettings(sourceCamera, camera);
        }
    }

    void CopyCameraSettings()
    {
        _overlayCamera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
        _overlayCamera.clearFlags = CameraClearFlags.Nothing;
        _overlayCamera.backgroundColor = Color.clear;
        _overlayCamera.orthographic = sourceCamera.orthographic;
        _overlayCamera.orthographicSize = sourceCamera.orthographicSize;
        _overlayCamera.fieldOfView = sourceCamera.fieldOfView;
        _overlayCamera.nearClipPlane = sourceCamera.nearClipPlane;
        _overlayCamera.farClipPlane = sourceCamera.farClipPlane;
        _overlayCamera.depth = sourceCamera.depth + 1f;
        _overlayCamera.allowHDR = sourceCamera.allowHDR;
        _overlayCamera.allowMSAA = sourceCamera.allowMSAA;
        _overlayCamera.targetTexture = null;

        ConfigureUrpCameraStack();
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

    static void CopyPostProcessingSettings(Camera source, Camera destination)
    {
        if (source == null || destination == null)
            return;

        destination.allowHDR  = source.allowHDR;
        destination.allowMSAA = source.allowMSAA;

        var sourceData      = source.GetUniversalAdditionalCameraData();
        var destinationData = destination.GetUniversalAdditionalCameraData();

        destinationData.antialiasing        = sourceData.antialiasing;
        destinationData.antialiasingQuality = sourceData.antialiasingQuality;
        destinationData.stopNaN             = sourceData.stopNaN;
        destinationData.dithering           = sourceData.dithering;
        destinationData.volumeLayerMask     = sourceData.volumeLayerMask;
        destinationData.volumeTrigger       = sourceData.volumeTrigger;
        destinationData.renderPostProcessing = true;
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
