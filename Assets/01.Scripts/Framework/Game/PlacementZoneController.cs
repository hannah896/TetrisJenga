using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns placement zone bounds, editor/runtime zone visuals, and placement exclusions.
/// </summary>
public class PlacementZoneController : MonoBehaviour
{
    [SerializeField] Transform towerRoot;
    [SerializeField] Transform towerStackDivider;

    [Header("Placement Zone")]
    [SerializeField] bool usePlacementZoneObject = true;
    [SerializeField] Transform placementZoneTransform;

    public Vector2Int placementMin = new(-1, 0);
    public Vector2Int placementMax = new(4, 14);

    readonly List<RectInt> placementExclusions = new();
    readonly List<GameObject> placementZoneFillObjects = new();
    readonly HashSet<GameObject> generatedObjects = new();

    BlockTower owner;
    Sprite fallbackSprite;
    string placementZoneVisualSignature;
    float placementZoneTopLimitLocal = float.NaN;
    bool freezePlacementZoneVisuals;

    public Transform PlacementZoneTransform => placementZoneTransform;
    public bool UsePlacementZoneObject => usePlacementZoneObject;
    public int PlacementFloorY => placementMin.y;
    public int PlacementCeilingY => placementMax.y;

    public void Configure(BlockTower blockTower, Transform root, Transform divider, Sprite blockSprite)
    {
        owner = blockTower != null ? blockTower : owner;
        towerRoot = root != null ? root : towerRoot;
        towerStackDivider = divider != null ? divider : towerStackDivider;
        fallbackSprite = blockSprite != null ? blockSprite : fallbackSprite;
    }

    public void SetTowerRoot(Transform root) => towerRoot = root;
    public void SetTowerStackDivider(Transform divider) => towerStackDivider = divider;

    public void SetFrozen(bool frozen)
    {
        freezePlacementZoneVisuals = frozen;
        if (frozen)
            SetPlacementZoneFillsActive(false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!usePlacementZoneObject || !gameObject.scene.IsValid())
            return;

        freezePlacementZoneVisuals = false;
        EnsureOwner();
        EnsureTowerRoot();

        if (placementZoneTransform == null)
        {
            var existing = Util.FindChildObject(transform, "PlacementZone");
            if (existing != null)
                placementZoneTransform = existing;
        }

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!this || Application.isPlaying || !gameObject.scene.IsValid()) return;
            if (towerRoot != null && placementZoneTransform != null)
                SyncPlacementZoneFromObject();
        };
    }
#endif

    public void UpdateEditorPlacementZonePreview()
    {
        if (!usePlacementZoneObject || !gameObject.scene.IsValid())
            return;

        freezePlacementZoneVisuals = false;
        EnsureOwner();
        EnsureTowerRoot();

        if (placementZoneTransform == null)
        {
            var existing = Util.FindChildObject(transform, "PlacementZone");
            if (existing != null)
                placementZoneTransform = existing;
        }

        if (towerRoot != null && placementZoneTransform != null)
            SyncPlacementZoneFromObject();
    }

    public void EnsurePlacementZoneObjectVisible()
    {
        if (!usePlacementZoneObject || Application.isPlaying)
            return;

        EnsureOwner();
        EnsureTowerRoot();

        if (placementZoneTransform == null)
        {
            var existing = FindBlockTowerChild("PlacementZone");
            if (existing != null)
                placementZoneTransform = existing;
        }

        if (placementZoneTransform != null)
        {
            ParentToBlockTowerPreserveWorld(placementZoneTransform);
            return;
        }

        var zone = new GameObject("PlacementZone");
        MarkGeneratedObject(zone);
        float minX = placementMin.x;
        float maxX = placementMax.x + 1f;
        float minY = placementMin.y;
        float maxY = placementMax.y + 1f;
        var zoneLocalCenter = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, -0.04f);
        zone.transform.SetParent(transform, worldPositionStays: true);
        zone.transform.position = TowerGridLocalToWorld(zoneLocalCenter);
        zone.transform.localScale = new Vector3(Mathf.Max(0.1f, maxX - minX), Mathf.Max(0.1f, maxY - minY), 1f);

        var sr = zone.AddComponent<SpriteRenderer>();
        sr.sprite = GetFallbackSprite();
        sr.color = new Color(1f, 0.92f, 0.02f, 0.16f);
        sr.sortingOrder = -5;

        placementZoneTransform = zone.transform;
    }

    public void SyncPlacementZoneFromObject(bool updateVisuals = true)
    {
        placementExclusions.Clear();
        if (!usePlacementZoneObject)
            return;

        EnsureOwner();
        EnsureTowerRoot();

        if (placementZoneTransform == null)
        {
            var existing = FindBlockTowerChild("PlacementZone");
            if (existing != null)
                placementZoneTransform = existing;
        }

        if (placementZoneTransform == null || towerRoot == null)
            return;

        ParentToBlockTowerPreserveWorld(placementZoneTransform);
        RectInt zoneRect = default;
        if (TryTransformToGridRect(placementZoneTransform, out zoneRect))
        {
            zoneRect = MatchRectToDividerX(zoneRect);
            placementMin = new Vector2Int(zoneRect.xMin, zoneRect.yMin);
            placementMax = new Vector2Int(zoneRect.xMax - 1, zoneRect.yMax - 1);
        }

        ParentPlacementExclusionsToBlockTower();
        foreach (var exclusionTransform in FindPlacementExclusionTransforms())
        {
            if (TryTransformToGridRect(exclusionTransform, out var exclusion))
                placementExclusions.Add(exclusion);
        }

        if (updateVisuals && zoneRect.width > 0 && zoneRect.height > 0)
            UpdatePlacementZoneVisuals(zoneRect);
    }

    public bool IsPlacementExcluded(Vector2Int cell)
    {
        foreach (var rect in placementExclusions)
            if (rect.Contains(cell))
                return true;
        return false;
    }

    public void AlignPlacementZoneToDivider(float dividerLocalY)
    {
        AlignPlacementZoneWidthToDivider();
        AlignPlacementZoneBottomToDivider(dividerLocalY);
        SyncPlacementZoneFromObject();
    }

    public void AlignPlacementZoneBottomToDivider(float dividerLocalY)
    {
        if (!usePlacementZoneObject || placementZoneTransform == null || towerRoot == null)
            return;

        if (!TryGetTowerLocalBounds(placementZoneTransform, out _, out _, out var minY, out var maxY))
            return;

        if (float.IsNaN(placementZoneTopLimitLocal) || !Application.isPlaying)
            placementZoneTopLimitLocal = maxY;

        float targetTop = Mathf.Max(placementZoneTopLimitLocal, dividerLocalY + 0.1f);
        float currentHeight = Mathf.Max(0.001f, maxY - minY);
        float targetHeight = Mathf.Max(0.1f, targetTop - dividerLocalY);
        if (Mathf.Abs(targetHeight - currentHeight) > 0.001f)
        {
            var scale = placementZoneTransform.localScale;
            scale.y *= targetHeight / currentHeight;
            placementZoneTransform.localScale = scale;
        }

        if (!TryGetTowerLocalBounds(placementZoneTransform, out _, out _, out minY, out _))
            return;

        float deltaY = dividerLocalY - minY;
        if (Mathf.Abs(deltaY) > 0.001f)
            placementZoneTransform.position += towerRoot.TransformVector(new Vector3(0f, deltaY, 0f));

        SyncPlacementZoneFromObject();
    }

    void AlignPlacementZoneWidthToDivider()
    {
        if (!usePlacementZoneObject || placementZoneTransform == null || towerStackDivider == null || towerRoot == null)
            return;

        if (!TryGetTowerLocalBounds(towerStackDivider, out var dividerMinX, out var dividerMaxX, out _, out _))
            return;
        if (!TryGetTowerLocalBounds(placementZoneTransform, out var zoneMinX, out var zoneMaxX, out _, out _))
            return;

        float dividerWidth = Mathf.Max(0.1f, dividerMaxX - dividerMinX);
        float zoneWidth = Mathf.Max(0.001f, zoneMaxX - zoneMinX);
        if (Mathf.Abs(dividerWidth - zoneWidth) > 0.001f)
        {
            var scale = placementZoneTransform.localScale;
            scale.x *= dividerWidth / zoneWidth;
            placementZoneTransform.localScale = scale;
        }

        ForcePlacementZoneRectToDividerX();
        ClampPlacementZoneTransformInsideDividerX();
    }

    void ForcePlacementZoneRectToDividerX()
    {
        if (placementZoneTransform == null || towerStackDivider == null || towerRoot == null)
            return;

        if (!TryGetTowerLocalBounds(towerStackDivider, out var dividerMinX, out var dividerMaxX, out _, out _))
            return;
        if (!TryGetTowerLocalBounds(placementZoneTransform, out var zoneMinX, out var zoneMaxX, out _, out _))
            return;

        float dividerWidth = Mathf.Max(0.1f, dividerMaxX - dividerMinX);
        float zoneWidth = Mathf.Max(0.001f, zoneMaxX - zoneMinX);
        var scale = placementZoneTransform.localScale;
        scale.x *= dividerWidth / zoneWidth;
        placementZoneTransform.localScale = scale;

        if (!TryGetTowerLocalBounds(placementZoneTransform, out zoneMinX, out zoneMaxX, out _, out _))
            return;

        float dividerCenterX = (dividerMinX + dividerMaxX) * 0.5f;
        float zoneCenterX = (zoneMinX + zoneMaxX) * 0.5f;
        placementZoneTransform.position += towerRoot.TransformVector(new Vector3(dividerCenterX - zoneCenterX, 0f, 0f));
    }

    void ClampPlacementZoneTransformInsideDividerX()
    {
        if (placementZoneTransform == null || towerStackDivider == null || towerRoot == null)
            return;

        if (!TryGetTowerLocalBounds(towerStackDivider, out var dividerMinX, out var dividerMaxX, out _, out _))
            return;
        if (!TryGetTowerLocalBounds(placementZoneTransform, out var zoneMinX, out var zoneMaxX, out _, out _))
            return;

        float deltaX = 0f;
        if (zoneMinX < dividerMinX)
            deltaX = dividerMinX - zoneMinX;
        if (zoneMaxX + deltaX > dividerMaxX)
            deltaX += dividerMaxX - (zoneMaxX + deltaX);

        if (Mathf.Abs(deltaX) > 0.001f)
            placementZoneTransform.position += towerRoot.TransformVector(new Vector3(deltaX, 0f, 0f));
    }

    void UpdatePlacementZoneVisuals(RectInt zoneRect)
    {
        if (freezePlacementZoneVisuals)
        {
            SetPlacementZoneFillsActive(false);
            return;
        }

        if (placementZoneTransform == null)
            return;

        var sourceRenderer = placementZoneTransform.GetComponent<SpriteRenderer>();
        if (sourceRenderer == null)
            return;

        zoneRect = MatchRectToDividerX(zoneRect);
        if (zoneRect.width <= 0 || zoneRect.height <= 0)
        {
            sourceRenderer.enabled = false;
            ClearPlacementZoneFillObjects();
            return;
        }

        string signature = PlacementZoneVisualSignature(zoneRect, sourceRenderer);
        if (placementZoneVisualSignature == signature)
            return;

        ClearPlacementZoneFillObjects();
        placementZoneVisualSignature = signature;

        sourceRenderer.enabled = false;
        var remaining = new List<RectInt> { zoneRect };
        foreach (var exclusion in placementExclusions)
            remaining = SubtractRectList(remaining, ClipRect(exclusion, zoneRect));

        int index = 0;
        foreach (var rect in remaining)
        {
            if (rect.width <= 0 || rect.height <= 0) continue;
            var go = new GameObject($"__PlacementZoneFill_{index++}");
            MarkGeneratedObject(go);
            go.transform.SetParent(placementZoneTransform, false);
            go.transform.position = TowerGridLocalToWorld(new Vector3(
                rect.xMin + rect.width * 0.5f,
                rect.yMin + rect.height * 0.5f,
                -0.04f));

            var parentScale = placementZoneTransform.lossyScale;
            go.transform.localScale = new Vector3(
                SafeDivideScale(rect.width, parentScale.x),
                SafeDivideScale(rect.height, parentScale.y),
                SafeDivideScale(1f, parentScale.z));

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sourceRenderer.sprite != null ? sourceRenderer.sprite : GetFallbackSprite();
            renderer.color = sourceRenderer.color;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder;
            placementZoneFillObjects.Add(go);
        }
    }

    void ParentPlacementExclusionsToBlockTower()
    {
        if (placementZoneTransform == null)
            return;

        var exclusions = new List<Transform>();
        foreach (Transform child in placementZoneTransform)
        {
            if (child == null) continue;
            if (!child.name.Contains("Exclude")) continue;
            exclusions.Add(child);
        }

        foreach (var exclusion in exclusions)
            ParentToBlockTowerPreserveWorld(exclusion);
    }

    List<Transform> FindPlacementExclusionTransforms()
    {
        var exclusions = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child == null || !child.gameObject.activeInHierarchy) continue;
            if (!child.name.Contains("Exclude")) continue;
            exclusions.Add(child);
        }

        return exclusions;
    }

    bool TryTransformToGridRect(Transform source, out RectInt rect)
    {
        rect = default;
        if (!TryGetTowerLocalBounds(source, out var minX, out var maxX, out var minY, out var maxY))
            return false;

        int xMin = Mathf.RoundToInt(minX);
        int yMin = Mathf.RoundToInt(minY);
        int xMax = Mathf.RoundToInt(maxX);
        int yMax = Mathf.RoundToInt(maxY);
        if (xMax <= xMin || yMax <= yMin)
            return false;

        rect = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        return true;
    }

    RectInt MatchRectToDividerX(RectInt rect)
    {
        if (rect.width <= 0 || towerStackDivider == null || towerRoot == null)
            return rect;

        if (!TryGetTowerLocalBounds(towerStackDivider, out var dividerMinX, out var dividerMaxX, out _, out _))
            return rect;

        int minX = Mathf.CeilToInt(dividerMinX - 0.001f);
        int maxX = Mathf.FloorToInt(dividerMaxX + 0.001f);
        if (maxX <= minX)
            return new RectInt(minX, rect.yMin, 0, rect.height);

        return new RectInt(minX, rect.yMin, maxX - minX, rect.height);
    }

    bool TryGetTowerLocalBounds(Transform source, out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = minY = float.PositiveInfinity;
        maxX = maxY = float.NegativeInfinity;
        if (source == null || towerRoot == null)
            return false;

        Vector3[] corners;
        var spriteRenderer = source.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            var bounds = spriteRenderer.sprite.bounds;
            var min = bounds.min;
            var max = bounds.max;
            corners = new[]
            {
                source.TransformPoint(new Vector3(min.x, min.y, 0f)),
                source.TransformPoint(new Vector3(min.x, max.y, 0f)),
                source.TransformPoint(new Vector3(max.x, min.y, 0f)),
                source.TransformPoint(new Vector3(max.x, max.y, 0f))
            };
        }
        else if (source.TryGetComponent<Renderer>(out var renderer))
        {
            var bounds = renderer.bounds;
            var min = bounds.min;
            var max = bounds.max;
            corners = new[]
            {
                new Vector3(min.x, min.y, source.position.z),
                new Vector3(min.x, max.y, source.position.z),
                new Vector3(max.x, min.y, source.position.z),
                new Vector3(max.x, max.y, source.position.z)
            };
        }
        else
        {
            var halfRight = source.right * (source.lossyScale.x * 0.5f);
            var halfUp = source.up * (source.lossyScale.y * 0.5f);
            corners = new[]
            {
                source.position - halfRight - halfUp,
                source.position - halfRight + halfUp,
                source.position + halfRight - halfUp,
                source.position + halfRight + halfUp
            };
        }

        foreach (var corner in corners)
        {
            var local = towerRoot.InverseTransformPoint(corner);
            minX = Mathf.Min(minX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxX = Mathf.Max(maxX, local.x);
            maxY = Mathf.Max(maxY, local.y);
        }

        return maxX > minX && maxY > minY;
    }

    Transform FindBlockTowerChild(string objectName) =>
        Util.FindChildObject(transform, objectName);

    void ParentToBlockTowerPreserveWorld(Transform child)
    {
        if (child != null && child.parent != transform)
            child.SetParent(transform, worldPositionStays: true);
    }

    void EnsureOwner()
    {
        if (owner == null)
            owner = GetComponent<BlockTower>();
    }

    void EnsureTowerRoot()
    {
        if (towerRoot == null)
            towerRoot = transform.Find("TowerRoot");
    }

    Vector3 TowerGridLocalToWorld(Vector3 local)
    {
        if (towerRoot != null)
            return towerRoot.TransformPoint(local);

        int columns = owner != null ? owner.columns : 4;
        int rows = owner != null ? owner.rows : 10;
        return new Vector3(-columns * 0.5f, -rows * 0.5f, 0f) + local;
    }

    Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Point
        };

        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % size;
            int y = i / size;
            bool border = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
            pixels[i] = border ? new Color(0f, 0f, 0f, 0.3f) : Color.white;
        }

        tex.SetPixels(pixels);
        tex.Apply();

        fallbackSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        fallbackSprite.hideFlags = HideFlags.DontSave;
        return fallbackSprite;
    }

    void MarkGeneratedObject(GameObject go)
    {
        if (go != null)
            generatedObjects.Add(go);
#if UNITY_EDITOR
        if (go != null && !Application.isPlaying)
            go.hideFlags = HideFlags.DontSaveInEditor;
#endif
    }

    void SetPlacementZoneFillsActive(bool active)
    {
        foreach (var go in placementZoneFillObjects)
            if (go != null)
                go.SetActive(active);

        if (placementZoneTransform == null) return;
        foreach (Transform child in placementZoneTransform)
            if (child != null && child.name.StartsWith("__PlacementZoneFill_"))
                child.gameObject.SetActive(active);
        if (towerRoot != null)
        {
            foreach (Transform child in towerRoot)
                if (child != null && child.name.StartsWith("__PlacementZoneFill_"))
                    child.gameObject.SetActive(active);
        }
    }

    void ClearPlacementZoneFillObjects()
    {
        for (int i = placementZoneFillObjects.Count - 1; i >= 0; i--)
            DestroyPlacementZoneFillObject(placementZoneFillObjects[i]);
        placementZoneFillObjects.Clear();

        if (placementZoneTransform == null) return;
        var stale = new List<GameObject>();
        foreach (Transform child in placementZoneTransform)
            if (child != null && child.name.StartsWith("__PlacementZoneFill_"))
                stale.Add(child.gameObject);
        foreach (var go in stale)
            DestroyPlacementZoneFillObject(go);
        placementZoneVisualSignature = null;
    }

    void DestroyPlacementZoneFillObject(GameObject go)
    {
        if (go == null) return;
        generatedObjects.Remove(go);
        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    string PlacementZoneVisualSignature(RectInt zoneRect, SpriteRenderer sourceRenderer)
    {
        var signature =
            $"{zoneRect.xMin},{zoneRect.yMin},{zoneRect.width},{zoneRect.height}|{sourceRenderer.color}|{sourceRenderer.sortingOrder}";
        foreach (var exclusion in placementExclusions)
            signature += $"|{exclusion.xMin},{exclusion.yMin},{exclusion.width},{exclusion.height}";
        return signature;
    }

    RectInt ClipRect(RectInt rect, RectInt bounds)
    {
        int xMin = Mathf.Max(rect.xMin, bounds.xMin);
        int yMin = Mathf.Max(rect.yMin, bounds.yMin);
        int xMax = Mathf.Min(rect.xMax, bounds.xMax);
        int yMax = Mathf.Min(rect.yMax, bounds.yMax);
        return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
    }

    IEnumerable<RectInt> SubtractRect(RectInt source, RectInt cut)
    {
        var overlap = ClipRect(cut, source);
        if (overlap.width <= 0 || overlap.height <= 0)
        {
            yield return source;
            yield break;
        }

        if (overlap.yMax < source.yMax)
            yield return new RectInt(source.xMin, overlap.yMax, source.width, source.yMax - overlap.yMax);
        if (overlap.yMin > source.yMin)
            yield return new RectInt(source.xMin, source.yMin, source.width, overlap.yMin - source.yMin);
        if (overlap.xMin > source.xMin)
            yield return new RectInt(source.xMin, overlap.yMin, overlap.xMin - source.xMin, overlap.height);
        if (overlap.xMax < source.xMax)
            yield return new RectInt(overlap.xMax, overlap.yMin, source.xMax - overlap.xMax, overlap.height);
    }

    List<RectInt> SubtractRectList(List<RectInt> sources, RectInt cut)
    {
        if (cut.width <= 0 || cut.height <= 0)
            return sources;

        var result = new List<RectInt>();
        foreach (var source in sources)
            result.AddRange(SubtractRect(source, cut));
        return result;
    }

    float SafeDivideScale(float value, float scale)
    {
        return Mathf.Abs(scale) > 0.0001f ? value / scale : value;
    }
}
