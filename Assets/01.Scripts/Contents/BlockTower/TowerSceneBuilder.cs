using UnityEngine;

[RequireComponent(typeof(BlockTower))]
public class TowerSceneBuilder : MonoBehaviour
{
    [SerializeField] BlockTower _tower;
    PlacementZoneController _placement;

    void Awake()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
        _placement = GetComponent<PlacementZoneController>();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
        if (_placement == null) _placement = GetComponent<PlacementZoneController>();
    }
#endif

    public void CreateFloor()
    {
        float floorY = -_tower.rows * 0.5f - 1.5f;
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
            floorGO.transform.position = new Vector3(0f, floorY, 0f);
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
            sr.sprite = _tower.CreateBlockSprite();
            sr.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        }
    }

    public void CreateBoundaries()
    {
        _placement?.Configure(_tower, _tower.TowerRoot, _tower.TowerStackDividerGO?.transform, _tower.CreateBlockSprite());
        _placement?.SyncPlacementZoneFromObject();
        float lineHeight = 50f;
        float lineHalfH = lineHeight * 0.5f;
        float lineWidth = 0.15f;
        float offsetX = _tower.columns * 0.5f + 2f;
        float centerY = _tower.FloorY + lineHalfH;
        var lineColor = new Color(1f, 0.25f, 0.25f, 0.7f);

        if (_tower.LeftBoundary == null)
        {
            var existing = Util.FindSceneObjectByName(transform, gameObject.scene, "BoundaryLeft");
            if (existing != null) _tower.SetLeftBoundary(existing.gameObject);
        }

        if (_tower.RightBoundary == null)
        {
            var existing = Util.FindSceneObjectByName(transform, gameObject.scene, "BoundaryRight");
            if (existing != null) _tower.SetRightBoundary(existing.gameObject);
        }

        if (_tower.LeftBoundary == null)
            _tower.SetLeftBoundary(SpawnBoundary("BoundaryLeft", new Vector3(-offsetX, centerY, 0f), lineWidth, lineHeight, lineColor));
        if (_tower.RightBoundary == null)
            _tower.SetRightBoundary(SpawnBoundary("BoundaryRight", new Vector3(offsetX, centerY, 0f), lineWidth, lineHeight, lineColor));

        ConfigureBoundary(_tower.LeftBoundary, lineColor);
        ConfigureBoundary(_tower.RightBoundary, lineColor);
        UpdateTowerStackDivider();
    }

    public void UpdateTowerStackDivider()
    {
        if (_tower.TowerRoot == null) return;

        _placement?.Configure(_tower, _tower.TowerRoot, _tower.TowerStackDividerGO?.transform, _tower.CreateBlockSprite());
        _placement?.SyncPlacementZoneFromObject();

        var towerStackDivider = _tower.TowerStackDividerGO;
        if (towerStackDivider == null)
        {
            var existing = Util.FindChildObject(transform, "TowerStackDivider");
            if (existing != null)
            {
                towerStackDivider = existing.gameObject;
                _tower.SetTowerStackDividerGO(towerStackDivider);
            }
        }

        bool created = false;
        if (towerStackDivider == null)
        {
            towerStackDivider = new GameObject("TowerStackDivider");
            if (Application.isPlaying)
                _tower.TrackGeneratedObject(towerStackDivider);
            towerStackDivider.transform.SetParent(_tower.transform, worldPositionStays: true);
            created = true;
            _tower.SetTowerStackDividerGO(towerStackDivider);
        }
        else
        {
            towerStackDivider.transform.SetParent(_tower.transform, worldPositionStays: true);
        }

        float minX = _placement != null ? _placement.placementMin.x : 0f;
        float maxX = _placement != null ? _placement.placementMax.x + 1f : _tower.columns;
        float width = Mathf.Max(0.1f, maxX - minX);
        float y = _tower.ExtractionMaxRow + 1f;

        bool authoredDivider = !created && !_tower.IsTrackedObject(towerStackDivider);
        var dividerLocalPosition = new Vector3((minX + maxX) * 0.5f, y, 0.02f);
        if (authoredDivider)
        {
            var authoredLocal = _tower.TowerRoot.InverseTransformPoint(towerStackDivider.transform.position);
            authoredLocal.y = y;
            towerStackDivider.transform.position = _tower.TowerRoot.TransformPoint(authoredLocal);
        }
        else
        {
            towerStackDivider.transform.SetParent(_tower.transform, worldPositionStays: true);
            towerStackDivider.transform.position = _tower.TowerRoot.TransformPoint(dividerLocalPosition);
        }

        if (!authoredDivider)
            towerStackDivider.transform.localScale = new Vector3(width, 0.12f, 1f);

        var dividerRenderer = towerStackDivider.GetComponent<SpriteRenderer>();
        if (dividerRenderer == null)
            dividerRenderer = towerStackDivider.AddComponent<SpriteRenderer>();

        if (dividerRenderer != null)
        {
            if (dividerRenderer.sprite == null)
            {
                dividerRenderer.sprite = _tower.CreateBlockSprite();
                dividerRenderer.color = new Color(1f, 0f, 0f, 0.85f);
            }
            else if (created)
            {
                dividerRenderer.color = new Color(1f, 0f, 0f, 0.85f);
            }

            if (created)
                dividerRenderer.sortingOrder = 2;
        }

        _placement?.SetTowerStackDivider(towerStackDivider.transform);
        _placement?.AlignPlacementZoneToDivider(y);
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
            sr.sprite = _tower.CreateBlockSprite();
            sr.color = fallbackColor;
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
        bl.OnBlockTouched = _tower.TriggerGameOver;
    }

    GameObject SpawnBoundary(string name, Vector3 worldPos, float width, float height, Color color)
    {
        var go = new GameObject(name);
        if (Application.isPlaying)
            _tower.TrackGeneratedObject(go);
        go.transform.SetParent(_tower.transform);
        go.transform.position = worldPos;
        go.transform.localScale = new Vector3(width, height, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _tower.CreateBlockSprite();
        sr.color = color;
        sr.sortingOrder = 1;

        if (Application.isPlaying)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var col = go.AddComponent<BoxCollider>();
            col.size = Vector3.one;
            col.isTrigger = true;

            var bl = go.AddComponent<BoundaryLine>();
            bl.OnBlockTouched = _tower.TriggerGameOver;
        }

        return go;
    }
}
