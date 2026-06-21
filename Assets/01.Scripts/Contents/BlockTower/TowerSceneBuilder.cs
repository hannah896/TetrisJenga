using UnityEngine;

[RequireComponent(typeof(BlockTower))]
public class TowerSceneBuilder : MonoBehaviour
{
    [SerializeField] BlockTower _tower;
    PlacementZoneController _placement;
    TowerCellVisualizer     _visualizer;
    TowerPhysicsController  _physics;

    GameObject _leftBoundary;
    GameObject _rightBoundary;
    GameObject _bottomBoundary;
    GameObject _towerStackDivider;

    public GameObject TowerStackDividerGO => _towerStackDivider;

    void Awake()
    {
        if (_tower == null)    _tower     = GetComponent<BlockTower>();
        if (_placement == null) _placement = GetComponent<PlacementZoneController>();
        if (_visualizer == null) _visualizer = GetComponent<TowerCellVisualizer>();
        if (_physics == null) _physics = GetComponent<TowerPhysicsController>();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_tower == null)    _tower     = GetComponent<BlockTower>();
        if (_placement == null) _placement = GetComponent<PlacementZoneController>();
        if (_visualizer == null) _visualizer = GetComponent<TowerCellVisualizer>();
        if (_physics == null) _physics = GetComponent<TowerPhysicsController>();
    }
#endif

    public void CreateFloor()
    {
        float floorY     = -_tower.rows * 0.5f - 1.5f;
        float floorWidth = _tower.columns + 4f;

        GameObject floorGO;
        bool created = false;
        if (_tower.FloorTransformRef != null)
        {
            floorGO = _tower.FloorTransformRef.gameObject;
        }
        else
        {
            if (_tower.GeneratedFloor == null)
            {
                var existing = Util.FindSceneObjectByName(transform, gameObject.scene, "Floor");
                if (existing != null)
                    _tower.SetGeneratedFloor(existing.gameObject);
            }

            if (_tower.GeneratedFloor != null)
            {
                floorGO = _tower.GeneratedFloor;
            }
            else
            {
                _tower.SetFloorY(floorY);
                return;
            }
        }

        if (created)
        {
            floorGO.transform.position   = new Vector3(0f, floorY, 0f);
            floorGO.transform.localScale = new Vector3(floorWidth, 1f, 1f);
        }
        else
        {
            _tower.SetFloorY(floorGO.transform.position.y);
        }

        if (!floorGO.TryGetComponent<SpriteRenderer>(out var sr))
            sr = floorGO.AddComponent<SpriteRenderer>();
        if (sr.sprite == null)
        {
            sr.sprite = _visualizer?.CreateBlockSprite();
            sr.color  = new Color(0.2f, 0.2f, 0.2f, 1f);
        }

        if (created)
            FitFloorSpriteToCurrentCellSize(floorGO, sr);
    }

    void FitFloorSpriteToCurrentCellSize(GameObject floorGO, SpriteRenderer sr)
    {
        if (floorGO == null || sr == null || sr.sprite == null) return;

        var spriteSize = sr.sprite.bounds.size;
        if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f) return;

        var t = floorGO.transform;
        bool hasCollider = floorGO.TryGetComponent<BoxCollider>(out var col);
        float targetWidth = hasCollider
            ? Mathf.Abs(col.size.x * t.localScale.x)
            : Mathf.Abs(t.localScale.x);
        float targetHeight = hasCollider
            ? Mathf.Abs(col.size.y * t.localScale.y)
            : Mathf.Abs(t.localScale.y);
        targetWidth = Mathf.Max(0.0001f, targetWidth);
        targetHeight = Mathf.Max(0.0001f, targetHeight);
        float signX = Mathf.Sign(t.localScale.x);
        float signY = Mathf.Sign(t.localScale.y);
        if (Mathf.Approximately(signX, 0f)) signX = 1f;
        if (Mathf.Approximately(signY, 0f)) signY = 1f;

        float scaleX = signX * targetWidth / spriteSize.x;
        float scaleY = signY * targetHeight / spriteSize.y;
        t.localScale = new Vector3(scaleX, scaleY, t.localScale.z);

        if (!hasCollider) return;
        col.size = new Vector3(targetWidth / Mathf.Abs(scaleX), targetHeight / Mathf.Abs(scaleY), col.size.z);
    }

    public void CreateBoundaries()
    {
        _placement?.Configure(_tower, _tower.TowerRoot, _towerStackDivider?.transform, _visualizer?.CreateBlockSprite());
        _placement?.SyncPlacementZoneFromObject();

        float lineHeight = 50f;
        float lineHalfH  = lineHeight * 0.5f;
        float lineWidth  = 0.15f;
        float offsetX    = _tower.columns * 0.5f + 2f;
        float centerY    = _tower.FloorY + lineHalfH;
        var   lineColor  = new Color(1f, 0.25f, 0.25f, 0.7f);

        if (_leftBoundary == null)
        {
            var existing = Util.FindSceneObjectByName(transform, gameObject.scene, "BoundaryLeft");
            if (existing != null) _leftBoundary = existing.gameObject;
        }

        if (_rightBoundary == null)
        {
            var existing = Util.FindSceneObjectByName(transform, gameObject.scene, "BoundaryRight");
            if (existing != null) _rightBoundary = existing.gameObject;
        }

        if (_bottomBoundary == null)
        {
            var existing = Util.FindSceneObjectByName(transform, gameObject.scene, "BoundaryBottom");
            if (existing != null) _bottomBoundary = existing.gameObject;
        }

        if (_leftBoundary == null)
            _leftBoundary  = SpawnBoundary("BoundaryLeft",  new Vector3(-offsetX, centerY, 0f), lineWidth, lineHeight, lineColor);
        if (_rightBoundary == null)
            _rightBoundary = SpawnBoundary("BoundaryRight", new Vector3( offsetX, centerY, 0f), lineWidth, lineHeight, lineColor);
        if (_bottomBoundary == null)
            _bottomBoundary = SpawnBoundary("BoundaryBottom", new Vector3(0f, _tower.FloorY, 0f), _tower.columns + 4f, lineWidth, lineColor);

        ConfigureBoundary(_leftBoundary,  lineColor);
        ConfigureBoundary(_rightBoundary, lineColor);
        ConfigureBoundary(_bottomBoundary, lineColor);
        UpdateTowerStackDivider();
    }

    public void UpdateTowerStackDivider()
    {
        if (_tower.TowerRoot == null) return;

        _placement?.Configure(_tower, _tower.TowerRoot, _towerStackDivider?.transform, _visualizer?.CreateBlockSprite());
        _placement?.SyncPlacementZoneFromObject();

        if (_towerStackDivider == null)
        {
            var existing = Util.FindChildObject(transform, "TowerStackDivider");
            if (existing != null)
                _towerStackDivider = existing.gameObject;
        }

        bool created = false;
        if (_towerStackDivider == null)
        {
            _towerStackDivider = new GameObject("TowerStackDivider");
            if (Application.isPlaying)
                _tower.TrackGeneratedObject(_towerStackDivider);
            created = true;
        }

        float minX  = _placement != null ? _placement.placementMin.x       : 0f;
        float maxX  = _placement != null ? _placement.placementMax.x + 1f  : _tower.columns;
        float width = Mathf.Max(0.1f, maxX - minX);
        float y     = _tower.ExtractionMaxRow + 1f;

        var dividerRenderer = _towerStackDivider.GetComponent<SpriteRenderer>();
        if (dividerRenderer == null)
            dividerRenderer = _towerStackDivider.AddComponent<SpriteRenderer>();

        if (dividerRenderer != null)
        {
            if (dividerRenderer.sprite == null)
            {
                dividerRenderer.sprite = _visualizer?.CreateBlockSprite();
                dividerRenderer.color  = new Color(1f, 0f, 0f, 0.85f);
            }
            else if (created)
            {
                dividerRenderer.color = new Color(1f, 0f, 0f, 0.85f);
            }

            if (created)
                dividerRenderer.sortingOrder = 2;
        }

        SnapDividerToTowerGrid(minX, maxX, y, width, dividerRenderer);

        _placement?.SetTowerStackDivider(_towerStackDivider.transform);
        _placement?.AlignPlacementZoneToDivider(y);
    }

    void SnapDividerToTowerGrid(float minX, float maxX, float y, float width, SpriteRenderer dividerRenderer)
    {
        if (_towerStackDivider == null || _tower.TowerRoot == null)
            return;

        var spriteSize = dividerRenderer != null && dividerRenderer.sprite != null
            ? dividerRenderer.sprite.bounds.size
            : Vector3.one;
        float spriteWidth = Mathf.Max(0.0001f, spriteSize.x);
        float spriteHeight = Mathf.Max(0.0001f, spriteSize.y);

        var divider = _towerStackDivider.transform;
        divider.SetParent(_tower.TowerRoot, worldPositionStays: false);
        divider.localPosition = new Vector3((minX + maxX) * 0.5f, y, 0.02f);
        divider.localRotation = Quaternion.identity;
        divider.localScale = new Vector3(width / spriteWidth, 0.12f / spriteHeight, divider.localScale.z);
    }

    public void CreateScoreLabel()
    {
        _tower.Score?.UpdateScoreDisplay();
    }

    public void CreateGameOverScreen()
    {
        _tower.HideResultScreens();
    }

    void ConfigureBoundary(GameObject boundary, Color fallbackColor)
    {
        if (boundary == null) return;

        bool authored = !_tower.IsTrackedObject(boundary);

        if (!boundary.TryGetComponent<SpriteRenderer>(out var sr))
            sr = boundary.AddComponent<SpriteRenderer>();
        if (sr.sprite == null)
        {
            sr.sprite = _visualizer?.CreateBlockSprite();
            sr.color  = fallbackColor;
        }

        if (!authored)
            sr.sortingOrder = 1;

        if (!Application.isPlaying) return;

        if (!boundary.TryGetComponent<Rigidbody>(out var rb))
            rb = boundary.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        if (!boundary.TryGetComponent<BoxCollider>(out var col))
        {
            col = boundary.AddComponent<BoxCollider>();
            col.size = Vector3.one;
        }
        else if (!authored)
        {
            col.size = Vector3.one;
        }

        col.isTrigger = true;

        if (!boundary.TryGetComponent<BoundaryLine>(out var bl))
            bl = boundary.AddComponent<BoundaryLine>();
        bl.OnBlockTouched -= _tower.TriggerGameOver;
        bl.OnBlockTouched += _tower.TriggerGameOver;
        bl.OnDetachedBlocksTouched -= _physics.HandleDetachedDeadlineContact;
        bl.OnDetachedBlocksTouched += _physics.HandleDetachedDeadlineContact;
    }

    GameObject SpawnBoundary(string name, Vector3 worldPos, float width, float height, Color color)
    {
        var go = new GameObject(name);
        if (Application.isPlaying)
            _tower.TrackGeneratedObject(go);
        go.transform.SetParent(null);
        go.transform.position   = worldPos;
        go.transform.localScale = new Vector3(width, height, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _visualizer?.CreateBlockSprite();
        sr.color  = color;
        sr.sortingOrder = 1;

        if (Application.isPlaying)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var col = go.AddComponent<BoxCollider>();
            col.size      = Vector3.one;
            col.isTrigger = true;

            var bl = go.AddComponent<BoundaryLine>();
            bl.OnBlockTouched = _tower.TriggerGameOver;
            bl.OnDetachedBlocksTouched = _physics.HandleDetachedDeadlineContact;
        }

        return go;
    }
}
