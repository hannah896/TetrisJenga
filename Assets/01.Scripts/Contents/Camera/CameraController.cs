using System;
using UnityEngine;

/// <summary>
/// 카메라 책임 총관리. 메인 카메라 이동·시야각 조정과 서브 카메라(RenderTexture) 운용을 담당한다.
/// BlockTower의 게임 상태 프로퍼티를 읽어 카메라를 갱신하며, 게임 로직에는 개입하지 않는다.
/// </summary>
public class CameraController : MonoBehaviour
{
    [SerializeField] BlockTower _tower;

    [Header("Camera")]
    [SerializeField] bool autoFocusCameraOnLift      = true;
    [SerializeField] bool autoReturnCameraAfterPlace  = true;
    [SerializeField] float scrollSpeed               = 1.5f;
    [SerializeField] float cameraFocusSpeed          = 8f;
    [SerializeField] float cameraTopPadding          = 2f;
    [SerializeField, Range(0f, 1f)] float cameraReturnBodyAnchor = 0.55f;
    [SerializeField] float extractionViewPadding     = 1.25f;
    [SerializeField] float placementViewTopOffset    = 1f;

    [Header("Secondary Camera")]
    [SerializeField] Camera secondaryViewCamera;
    [SerializeField] UnityEngine.UI.RawImage secondaryViewImage;
    [SerializeField] Vector2 secondaryViewPanelSize         = new(540f, 360f);
    [SerializeField] Vector2 secondaryViewPanelPosition     = new(40f, 0f);
    [SerializeField] float secondaryViewOrthographicSize    = 3f;
    [SerializeField] int   secondaryViewTextureSize         = 768;
    [SerializeField, HideInInspector] bool secondaryViewDefaultsMigrated;

    bool          _hasCameraTarget;
    float         _cameraTargetY;
    float         _cameraTargetSize;
    RenderTexture _secondaryViewTexture;

    public System.Action<RenderTexture> OnSecondaryViewTextureChanged;

    private void OnValidate()
    {
        if (_tower == null)
            _tower = GetComponent<BlockTower>();
        if (secondaryViewCamera == null)
            secondaryViewCamera = transform.Find("SubViewCamera").GetComponent<Camera>();
    }

    void Awake()
    {
        if (_tower == null)
            _tower = GetComponent<BlockTower>();
        if (_tower != null)
        {
            _tower.OnTowerReady    += HandleTowerReady;
            _tower.OnBlocksLifted  += FocusHeldCenter;
            _tower.OnBlocksPlaced  += ShowAfterPlace;
            _tower.OnHoldCancelled += HandleHoldCancelled;
        }
    }

    void OnEnable()
    {
        MigrateSecondaryViewDefaults();
    }

    void OnDisable()
    {
        if (_tower != null)
        {
            _tower.OnTowerReady    -= HandleTowerReady;
            _tower.OnBlocksLifted  -= FocusHeldCenter;
            _tower.OnBlocksPlaced  -= ShowAfterPlace;
            _tower.OnHoldCancelled -= HandleHoldCancelled;
        }
        if (secondaryViewCamera != null && secondaryViewCamera.targetTexture == _secondaryViewTexture)
            secondaryViewCamera.targetTexture = null;
        if (secondaryViewImage != null && secondaryViewImage.texture == _secondaryViewTexture)
            secondaryViewImage.texture = null;
        if (_secondaryViewTexture != null)
        {
            _secondaryViewTexture.Release();
            if (Application.isPlaying) Destroy(_secondaryViewTexture);
            else DestroyImmediate(_secondaryViewTexture);
            _secondaryViewTexture = null;
        }
    }

    void HandleTowerReady()
    {
        EnsureSecondaryViewObjects();
        UpdateSecondaryViewCamera();
        FitCamera();
    }

    void HandleHoldCancelled() => ShowExtractionView(immediate: true);

    // ── InputHandler가 호출하는 퍼블릭 API ────────────────────────────────
    public void ScrollCamera(float delta)
    {
        var cam = Camera.main;
        if (cam == null) return;
        _hasCameraTarget = false;
        float minY = (_tower != null ? _tower.FloorY : 0f) + CurrentCameraHalfHeight(cam);
        float newY = Mathf.Max(cam.transform.position.y + Mathf.Sign(delta) * scrollSpeed, minY);
        cam.transform.position = new Vector3(cam.transform.position.x, newY, cam.transform.position.z);
    }

    public void UpdateCameraTarget()
    {
        if (!_hasCameraTarget) return;
        var cam = Camera.main;
        if (cam == null) return;
        float t     = 1f - Mathf.Exp(-cameraFocusSpeed * Time.deltaTime);
        float nextY = Mathf.Lerp(cam.transform.position.y, _cameraTargetY, t);
        float nextS = _cameraTargetSize > 0f
            ? Mathf.Lerp(CurrentCameraHalfHeight(cam), _cameraTargetSize, t)
            : CurrentCameraHalfHeight(cam);
        ApplyCameraView(cam, cam.transform.position.x, nextY, nextS);
        if (Mathf.Abs(nextY - _cameraTargetY) < 0.01f && (_cameraTargetSize <= 0f || Mathf.Abs(nextS - _cameraTargetSize) < 0.01f))
            _hasCameraTarget = false;
    }

    public void UpdateSecondaryViewCamera()
    {
        if (_tower == null || !Application.isPlaying || _tower.TowerRoot == null) return;
        EnsureSecondaryViewObjects();
        if (secondaryViewCamera == null) return;
        float aspect  = SecondaryViewAspect();
        bool  hasView = _tower.IsHolding
            ? TryCalculateOccupiedRowsCameraView(extractionViewPadding, aspect, out var tY, out var tS)
            : TryCalculatePlacementCameraView(aspect, out tY, out tS);
        if (!hasView) return;
        var main = Camera.main;
        if (!_tower.IsHolding && secondaryViewOrthographicSize > 0.01f) tS = secondaryViewOrthographicSize;
        secondaryViewCamera.orthographic = false;
        if (main != null) secondaryViewCamera.fieldOfView = main.fieldOfView;
        secondaryViewCamera.transform.position = new Vector3(0f, tY, CameraZForHalfHeight(secondaryViewCamera, tS));
    }

    // ── BlockTower가 호출하는 퍼블릭 API ─────────────────────────────────
    public void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = false;
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        if (TryCalculateOccupiedRowsCameraView(extractionViewPadding, aspect, out var cY, out var hH))
        {
            _cameraTargetY    = cY;
            _cameraTargetSize = hH;
            ApplyCameraView(cam, 0f, cY, hH, adjustFieldOfView: true);
            _hasCameraTarget = false;
            return;
        }
        FitCameraToGridRows(0, (_tower != null ? _tower.rows : 9), extractionViewPadding, immediate: true);
    }

    public void ShowExtractionView(bool immediate)
    {
        if (_tower == null) return;
        FitCameraToGridRows(_tower.ExtractionMinRow, _tower.ExtractionMaxRow, extractionViewPadding, immediate);
    }

    public void ShowPlacementView(bool immediate)
    {
        if (_tower == null) return;
        FocusGridY(_tower.HighestOccupiedRow() + 1, immediate, placementViewTopOffset);
    }

    public void ShowAfterPlace()
    {
        if (_tower == null) return;
        if (autoReturnCameraAfterPlace)
            ShowExtractionView(immediate: true);
        else if (autoFocusCameraOnLift)
            ShowPlacementView(immediate: false);
    }

    public void FocusHeldCenter()
    {
        if (!autoFocusCameraOnLift || _tower == null) return;
        var cam = Camera.main;
        if (cam == null || _tower.TowerRoot == null) return;
        float halfH       = CurrentCameraHalfHeight(cam);
        _cameraTargetY    = Mathf.Max(_tower.FloorY + halfH, _tower.TowerRoot.position.y + _tower.HeldBaseCell.y + _tower.HeldCenter.y);
        _cameraTargetSize = halfH;
        _hasCameraTarget  = true;
    }

    public void EnsureSecondaryViewObjects()
    {
        MigrateSecondaryViewDefaults();
        if (secondaryViewCamera == null)
        {
            var existing = GameObject.Find("SubViewCamera");
            if (existing != null) secondaryViewCamera = existing.GetComponent<Camera>();
        }
        if (secondaryViewCamera == null)
        {
            var go = new GameObject("SubViewCamera");
            go.transform.SetParent(transform, false);
            secondaryViewCamera = go.AddComponent<Camera>();
        }
        ConfigureSecondaryViewCamera();
        EnsureSecondaryViewTexture();
    }

    // ── 내부 카메라 계산 ──────────────────────────────────────────────────
    void FitCameraToGridRows(int minGridY, int maxGridY, float padding, bool immediate)
    {
        var cam = Camera.main;
        if (cam == null || _tower == null || _tower.TowerRoot == null) return;
        cam.orthographic = false;
        float aspect  = (float)Screen.width / Mathf.Max(1, Screen.height);
        float bottom  = _tower.TowerRoot.position.y + minGridY - padding;
        float top     = _tower.TowerRoot.position.y + maxGridY + 1f + padding;
        _cameraTargetY    = (bottom + top) * 0.5f;
        _cameraTargetSize = Mathf.Max((_tower.columns * 0.5f + 3.5f) / aspect, (top - bottom) * 0.5f);
        if (immediate) { ApplyCameraView(cam, 0f, _cameraTargetY, _cameraTargetSize, adjustFieldOfView: true); _hasCameraTarget = false; return; }
        _hasCameraTarget = true;
    }

    void FocusGridY(int gridY, bool immediate, float padding)
    {
        var cam = Camera.main;
        if (cam == null) return;
        float rootY  = _tower != null && _tower.TowerRoot != null ? _tower.TowerRoot.position.y : 0f;
        float worldY = rootY + gridY + 0.5f;
        float halfH  = CurrentCameraHalfHeight(cam);
        _cameraTargetY    = Mathf.Max((_tower != null ? _tower.FloorY : 0f) + halfH, worldY + padding);
        _cameraTargetSize = halfH;
        if (immediate) { ApplyCameraView(cam, cam.transform.position.x, _cameraTargetY, _cameraTargetSize); _hasCameraTarget = false; return; }
        _hasCameraTarget = true;
    }

    void ApplyCameraView(Camera cam, float x, float y, float halfH, bool adjustFieldOfView = false)
    {
        if (cam == null) return;
        cam.orthographic = false;
        if (adjustFieldOfView) cam.fieldOfView = FieldOfViewForHalfHeight(cam, halfH);
        cam.transform.position = new Vector3(x, y, CameraZForHalfHeight(cam, halfH));
    }

    float FieldOfViewForHalfHeight(Camera cam, float halfH)
    {
        if (cam == null) return 60f;
        float dist = Mathf.Abs(CameraPlaneZ() - cam.transform.position.z);
        if (dist <= 0.001f) return cam.fieldOfView;
        return Mathf.Clamp(2f * Mathf.Atan(Mathf.Max(0.01f, halfH) / dist) * Mathf.Rad2Deg, 20f, 100f);
    }

    float CurrentCameraHalfHeight(Camera cam)
    {
        if (cam == null) return 6f;
        if (cam.orthographic) return cam.orthographicSize;
        float dist = Mathf.Abs(CameraPlaneZ() - cam.transform.position.z);
        return Mathf.Max(0.1f, dist * Mathf.Tan(Mathf.Max(1f, cam.fieldOfView) * Mathf.Deg2Rad * 0.5f));
    }

    float CameraZForHalfHeight(Camera cam, float halfH)
    {
        float fovRad = Mathf.Max(1f, cam != null ? cam.fieldOfView : 60f) * Mathf.Deg2Rad;
        return CameraPlaneZ() - Mathf.Max(0.1f, halfH) / Mathf.Tan(fovRad * 0.5f);
    }

    float CameraPlaneZ() => _tower != null && _tower.TowerRoot != null ? _tower.TowerRoot.position.z : 0f;

    // ── 서브 카메라 계산 ──────────────────────────────────────────────────
    void MigrateSecondaryViewDefaults()
    {
        if (secondaryViewDefaultsMigrated) return;
        if (Mathf.Approximately(secondaryViewPanelSize.x, 360f) && Mathf.Approximately(secondaryViewPanelSize.y, 240f))
            secondaryViewPanelSize = new Vector2(540f, 360f);
        if (secondaryViewOrthographicSize <= 0.01f) secondaryViewOrthographicSize = 3f;
        secondaryViewDefaultsMigrated = true;
    }

    void ConfigureSecondaryViewCamera()
    {
        if (secondaryViewCamera == null) return;
        var main = Camera.main;
        if (main != null && main != secondaryViewCamera)
        {
            secondaryViewCamera.clearFlags      = main.clearFlags;
            secondaryViewCamera.backgroundColor = main.backgroundColor;
            secondaryViewCamera.cullingMask     = main.cullingMask;
            secondaryViewCamera.orthographic    = false;
            secondaryViewCamera.nearClipPlane   = main.nearClipPlane;
            secondaryViewCamera.farClipPlane    = main.farClipPlane;
            secondaryViewCamera.fieldOfView     = main.fieldOfView;
            secondaryViewCamera.transform.rotation = main.transform.rotation;
            secondaryViewCamera.transform.position = main.transform.position;
        }
        else secondaryViewCamera.orthographic = false;
        secondaryViewCamera.depth   = -10f;
        secondaryViewCamera.enabled = Application.isPlaying;
        var listener = secondaryViewCamera.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = false;
    }

    void EnsureSecondaryViewTexture()
    {
        if (!Application.isPlaying || secondaryViewCamera == null) return;
        int w = Mathf.Max(64, secondaryViewTextureSize);
        int h = Mathf.Max(64, Mathf.RoundToInt(w * Mathf.Max(0.1f, secondaryViewPanelSize.y) / Mathf.Max(0.1f, secondaryViewPanelSize.x)));
        if (_secondaryViewTexture == null || _secondaryViewTexture.width != w || _secondaryViewTexture.height != h)
        {
            if (_secondaryViewTexture != null) _secondaryViewTexture.Release();
            _secondaryViewTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "SubViewCameraTexture" };
            _secondaryViewTexture.Create();
        }
        secondaryViewCamera.targetTexture = _secondaryViewTexture;
        OnSecondaryViewTextureChanged?.Invoke(_secondaryViewTexture);
    }

    float SecondaryViewAspect()
    {
        if (secondaryViewImage != null)
        {
            var r = secondaryViewImage.rectTransform.rect;
            if (r.width > 1f && r.height > 1f) return r.width / r.height;
        }
        return Mathf.Max(0.1f, secondaryViewPanelSize.x) / Mathf.Max(0.1f, secondaryViewPanelSize.y);
    }

    bool TryCalculateOccupiedRowsCameraView(float padding, float aspect, out float centerY, out float size)
    {
        centerY = size = 0f;
        if (_tower == null || !_tower.TryGetOccupiedGridBounds(out var mnX, out var mxX, out var mnY, out var mxY)) return false;
        return TryCalculateGridBoundsCameraView(mnX, mxX, mnY, mxY, padding, aspect, out centerY, out size);
    }

    bool TryCalculateGridBoundsCameraView(int mnX, int mxX, int mnY, int mxY, float pad, float aspect, out float centerY, out float size)
    {
        centerY = size = 0f;
        if (_tower == null || _tower.TowerRoot == null) return false;
        float halfW  = Mathf.Max(_tower.columns * 0.5f + 1f, (_tower.TowerRoot.position.x + mxX + 1f + pad - (_tower.TowerRoot.position.x + mnX - pad)) * 0.5f);
        float bottom = _tower.TowerRoot.position.y + mnY - pad;
        float top    = _tower.TowerRoot.position.y + mxY + 1f + pad;
        centerY = (bottom + top) * 0.5f;
        size    = Mathf.Max(halfW / Mathf.Max(0.1f, aspect), (top - bottom) * 0.5f);
        return size > 0.01f;
    }

    bool TryCalculatePlacementCameraView(float aspect, out float centerY, out float size)
    {
        centerY = size = 0f;
        if (_tower == null || _tower.TowerRoot == null) return false;
        int   gridY   = _tower.HasLastPlacementCenter ? Mathf.RoundToInt(_tower.LastPlacementCenter.y) : _tower.HighestOccupiedRow() + 1;
        var   main    = Camera.main;
        float curSize = main != null ? CurrentCameraHalfHeight(main) : 6f;
        float worldY  = _tower.TowerRoot.position.y + gridY + 0.5f + placementViewTopOffset;
        centerY = Mathf.Max(_tower.FloorY + curSize, worldY);
        size    = Mathf.Max(curSize, (_tower.columns * 0.5f + 3.5f) / Mathf.Max(0.1f, aspect));
        return true;
    }
}
