using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BlockEffectController : MonoBehaviour
{
    [SerializeField] BlockTower          _tower;
    [SerializeField] HeldBlockController _held;

    [Header("Extraction Effect")]
    [SerializeField] Sprite[] extractionEffectSprites;
    [SerializeField, Min(1f)] float extractionEffectFps   = 12f;
    [SerializeField, Min(1f)] float extractionEffectScale = 6f;

    [Header("Hard Drop Effect")]
    [SerializeField] Sprite hardDropBackSprite;
    [SerializeField, Min(0.05f)] float hardDropFadeDuration = 0.5f;

    [Header("Tower Shake Effect")]
    [SerializeField] Sprite towerShakeSprite;
    [SerializeField, Min(0f)] float towerShakeImageScale = 1f;

    const float ExtractionEffectMinimumScale        = 6f;
    const float TowerShakeAngularVelocityThreshold  = 0.45f;
    const float TowerShakeVisibleDuration           = 0.15f;

    Rigidbody    _rb;
    GameObject   _towerShakeEffectRoot;
    SpriteRenderer _leftTowerShakeEffect;
    SpriteRenderer _rightTowerShakeEffect;
    bool  _hasTowerShakeAnchor;
    float _leftTowerShakeAnchorX;
    float _rightTowerShakeAnchorX;
    float _towerShakeVisibleUntil;

    // ── 초기화 ──────────────────────────────────────────────────────────

    void Awake()
    {
        _tower = GetComponent<BlockTower>();
        _held  = GetComponent<HeldBlockController>();
    }

    void Start()
    {
        if (_tower?.TowerRoot != null)
            _rb = _tower.TowerRoot.GetComponent<Rigidbody>();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
        if (_held  == null) _held  = GetComponent<HeldBlockController>();
        ResolveExtractionEffectSprites();
        ResolveHardDropBackSprite();
        ResolveTowerShakeSprite();
    }

    void ResolveExtractionEffectSprites()
    {
        if (extractionEffectSprites != null && extractionEffectSprites.Length > 0) return;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/00.Resources/FX/New Folder/extraction.anim");
        if (clip == null) return;

        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        var frames = new List<Sprite>();
        foreach (var binding in bindings)
        {
            foreach (var frame in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                if (frame.value is Sprite sprite && !frames.Contains(sprite))
                    frames.Add(sprite);
        }
        if (frames.Count > 0)
            extractionEffectSprites = frames.ToArray();
    }

    void ResolveHardDropBackSprite()
    {
        if (hardDropBackSprite == null)
            hardDropBackSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/00.Resources/Sprite/effect/Hard drop Back.png");
    }

    void ResolveTowerShakeSprite()
    {
        if (towerShakeSprite == null)
            towerShakeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/00.Resources/FX/New Folder/shake.png");
    }
#endif

    // ── 매 프레임: 타워 흔들림 ──────────────────────────────────────────

    void LateUpdate()
    {
        if (!Application.isPlaying) return;

        if (_rb == null && _tower?.TowerRoot != null)
            _rb = _tower.TowerRoot.GetComponent<Rigidbody>();

        UpdateTowerShakeFeedback();
    }

    // ── 추출 / 배치 이펙트 ──────────────────────────────────────────────

    public void SpawnExtractionEffects(IEnumerable<Vector2Int> cells)
    {
        foreach (var cell in cells)
            SpawnExtractionEffect(cell);
    }

    void SpawnExtractionEffect(Vector2Int cell)
    {
        if (extractionEffectSprites == null || extractionEffectSprites.Length == 0) return;
        if (_tower?.TowerRoot == null) return;

        var go = new GameObject("ExtractionEffect");
        go.transform.SetParent(_tower.TowerRoot, false);
        go.transform.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, -0.1f);
        float scale = Mathf.Max(ExtractionEffectMinimumScale, extractionEffectScale);
        go.transform.localScale = Vector3.one * scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = extractionEffectSprites[0];
        sr.sortingOrder = 10;

        var effect = go.AddComponent<SpriteFrameEffect>();
        effect.Initialize(sr, extractionEffectSprites, extractionEffectFps, destroyOnComplete: true);
        _tower.TrackGeneratedObject(go);
    }

    // ── 하드드롭 이펙트 ─────────────────────────────────────────────────

    public void SpawnHardDropEffect(Vector2Int startBase, Vector2Int dropBase)
    {
        if (hardDropBackSprite == null || _tower?.TowerRoot == null || _held == null) return;

        int dropHeight = startBase.y - dropBase.y;
        if (dropHeight <= 0) return;

        int minX = int.MaxValue, maxX = int.MinValue;
        foreach (var rel in _held.RelPos)
        {
            if (rel.x < minX) minX = rel.x;
            if (rel.x > maxX) maxX = rel.x;
        }
        float width = (minX <= maxX) ? (maxX - minX + 1) : 1f;

        var go = new GameObject("HardDropEffect");
        go.transform.SetParent(_tower.TowerRoot, false);

        float localX = dropBase.x + _held.Center.x;
        float localY = dropBase.y + _held.Center.y + dropHeight * 0.5f;
        go.transform.localPosition = new Vector3(localX, localY, -0.05f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = hardDropBackSprite;
        sr.drawMode     = SpriteDrawMode.Sliced;
        sr.size         = new Vector2(width, dropHeight);
        sr.sortingOrder = 5;

        _tower.TrackGeneratedObject(go);
        StartCoroutine(FadeOutHardDropEffect(go, sr));
    }

    IEnumerator FadeOutHardDropEffect(GameObject go, SpriteRenderer sr)
    {
        float elapsed = 0f;
        while (elapsed < hardDropFadeDuration)
        {
            if (go == null || sr == null) yield break;
            elapsed += Time.deltaTime;
            sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, elapsed / hardDropFadeDuration));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ── 타워 흔들림 이펙트 ──────────────────────────────────────────────

    void UpdateTowerShakeFeedback()
    {
        if (_rb == null || _tower?.TowerRoot == null) return;

        float angVel = Mathf.Abs(_rb.angularVelocity.z);
        if (angVel > TowerShakeAngularVelocityThreshold)
        {
            EnsureTowerShakeEffects();
            CaptureTowerShakeAnchors();
            SetTowerShakeEffectsActive(true);
            _towerShakeVisibleUntil = Time.time + TowerShakeVisibleDuration;
        }

        if (Time.time >= _towerShakeVisibleUntil)
            SetTowerShakeEffectsActive(false);
    }

    void EnsureTowerShakeEffects()
    {
        if (_towerShakeEffectRoot != null) return;

        _towerShakeEffectRoot = new GameObject("TowerShakeEffects");
        _towerShakeEffectRoot.transform.SetParent(_tower.TowerRoot, false);
        _tower.TrackGeneratedObject(_towerShakeEffectRoot);

        _leftTowerShakeEffect  = CreateTowerShakeEffect("LeftShake",  flipX: false);
        _rightTowerShakeEffect = CreateTowerShakeEffect("RightShake", flipX: true);
        SetTowerShakeEffectsActive(false);
    }

    SpriteRenderer CreateTowerShakeEffect(string goName, bool flipX)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(_towerShakeEffectRoot.transform, false);
        _tower.TrackGeneratedObject(go);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = towerShakeSprite;
        sr.flipX        = flipX;
        sr.sortingOrder = 20;
        sr.enabled      = false;

        float s = Mathf.Max(0.1f, towerShakeImageScale);
        go.transform.localScale = new Vector3(s, s, 1f);

        return sr;
    }

    void CaptureTowerShakeAnchors()
    {
        if (_hasTowerShakeAnchor || _tower?.TowerRoot == null) return;

        var grid = _tower.Grid;
        if (grid == null) return;

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue;
        foreach (var pair in grid.AllCells)
        {
            minX = Mathf.Min(minX, pair.Key.x);
            maxX = Mathf.Max(maxX, pair.Key.x);
            minY = Mathf.Min(minY, pair.Key.y);
        }

        if (minX > maxX) return;

        float anchorY = minY + 0.5f;
        _leftTowerShakeAnchorX  = minX - 0.5f;
        _rightTowerShakeAnchorX = maxX + 1.5f;

        if (_leftTowerShakeEffect != null)
            _leftTowerShakeEffect.transform.localPosition  = new Vector3(_leftTowerShakeAnchorX,  anchorY, -0.1f);
        if (_rightTowerShakeEffect != null)
            _rightTowerShakeEffect.transform.localPosition = new Vector3(_rightTowerShakeAnchorX, anchorY, -0.1f);

        _hasTowerShakeAnchor = true;
    }

    void SetTowerShakeEffectsActive(bool active)
    {
        if (_leftTowerShakeEffect != null)  _leftTowerShakeEffect.enabled  = active;
        if (_rightTowerShakeEffect != null) _rightTowerShakeEffect.enabled = active;
    }

    public void DestroyTowerShakeEffects()
    {
        if (_towerShakeEffectRoot != null)
        {
            Destroy(_towerShakeEffectRoot);
            _towerShakeEffectRoot  = null;
        }
        _leftTowerShakeEffect    = null;
        _rightTowerShakeEffect   = null;
        _hasTowerShakeAnchor     = false;
    }
}
