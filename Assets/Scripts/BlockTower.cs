using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[ExecuteAlways]
public class BlockTower : MonoBehaviour
{
    [Header("Grid")]
    public int columns = 4;
    public int rows    = 10;

    [Header("Placement Zone")]
    public Vector2Int placementMin = new(0, 0);
    public Vector2Int placementMax = new(3, 14);

    [Header("Physics")]
    public float blockFriction = 1f;

    [Header("Scene References (optional)")]
    [SerializeField] Transform   towerRootTransform;
    [SerializeField] Transform   floorTransform;
    [SerializeField] TextMeshPro scoreLabel;

    Transform      _towerRoot;
    Rigidbody      _rb;
    Sprite         _blockSprite;
    GameObject     _generatedFloor;
    GameObject     _generatedScoreLabel;
    GameObject     _leftBoundary;
    GameObject     _rightBoundary;
    GameOverScreen _gameOverScreen;
    int            _score;
    bool           _isGameOver;

    // ── 셀 데이터 ─────────────────────────────────────────────────────────
    class CellData
    {
        public int            number;
        public GameObject     go;
        public SpriteRenderer sr;
        public TextMeshPro    label;
    }

    readonly Dictionary<Vector2Int, CellData> _cells    = new();
    readonly List<Vector2Int>                  _selected = new();

    // ── 들기 상태 ─────────────────────────────────────────────────────────
    bool             _isHolding;
    GameObject       _heldRoot;
    List<Vector2Int> _heldRelPos = new();
    List<CellData>   _heldData   = new();
    Vector2          _heldCenter;

    // ─────────────────────────────────────────────────────────────────────

    void OnEnable() => Rebuild();

#if UNITY_EDITOR
    void OnValidate() =>
        UnityEditor.EditorApplication.delayCall += () => { if (this && gameObject.scene.IsValid()) Rebuild(); };
#endif

    void Rebuild()
    {
        if (_heldRoot != null) { DestroyLocal(_heldRoot); _heldRoot = null; }
        _heldRelPos.Clear();
        _heldData.Clear();
        ClearGenerated();
        _cells.Clear();
        _selected.Clear();
        _isHolding  = false;
        _isGameOver = false;
        _blockSprite = null;
        _score = 0;
        BuildTower();
    }

    void ClearGenerated()
    {
        if (_towerRoot == null && towerRootTransform == null)
        {
            var found = transform.Find("TowerRoot");
            if (found != null) _towerRoot = found;
        }
        if (_towerRoot != null)
        {
            if (towerRootTransform == null)
                DestroyLocal(_towerRoot.gameObject);
            else
            {
                var children = new List<Transform>();
                foreach (Transform t in _towerRoot) children.Add(t);
                foreach (var t in children) DestroyLocal(t.gameObject);
            }
            _towerRoot = null;
            _rb = null;
        }

        if (_generatedFloor == null) { var f = transform.Find("Floor"); if (f) _generatedFloor = f.gameObject; }
        if (_generatedFloor != null) { DestroyLocal(_generatedFloor); _generatedFloor = null; }

        if (_generatedScoreLabel == null) { var f = transform.Find("ScoreLabel"); if (f) _generatedScoreLabel = f.gameObject; }
        if (_generatedScoreLabel != null && scoreLabel == null) { DestroyLocal(_generatedScoreLabel); _generatedScoreLabel = null; scoreLabel = null; }

        if (_leftBoundary  == null) { var f = transform.Find("BoundaryLeft");  if (f) _leftBoundary  = f.gameObject; }
        if (_rightBoundary == null) { var f = transform.Find("BoundaryRight"); if (f) _rightBoundary = f.gameObject; }
        if (_leftBoundary  != null) { DestroyLocal(_leftBoundary);  _leftBoundary  = null; }
        if (_rightBoundary != null) { DestroyLocal(_rightBoundary); _rightBoundary = null; }

        if (_gameOverScreen != null) { DestroyLocal(_gameOverScreen.gameObject); _gameOverScreen = null; }
    }

    void DestroyLocal(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // ── 스코어 ───────────────────────────────────────────────────────────

    void AddScore(int delta)
    {
        _score += delta;
        UpdateScoreDisplay();
    }

    void UpdateScoreDisplay()
    {
        if (scoreLabel != null)
            scoreLabel.text = $"SCORE\n{_score}";
    }

    // ─────────────────────────────────────────────────────────────────────

    [Header("Camera Scroll")]
    public float scrollSpeed = 1.5f;

    float _floorY;

    void Update()
    {
        if (!Application.isPlaying) return;
        if (_isGameOver) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (_isHolding)
        {
            UpdateHeldPosition();
            if (mouse.leftButton.wasPressedThisFrame)  TryPlaceBlocks();
            if (mouse.rightButton.wasPressedThisFrame) CancelHold();
        }
        else
        {
            if (mouse.leftButton.wasPressedThisFrame)  HandleClick();
            if (mouse.rightButton.wasPressedThisFrame) ClearSelection();
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                float minY = _floorY + cam.orthographicSize;
                float newY = Mathf.Max(cam.transform.position.y + Mathf.Sign(scroll) * scrollSpeed, minY);
                cam.transform.position = new Vector3(cam.transform.position.x, newY, cam.transform.position.z);
            }
        }
    }

    // ── 게임오버 ─────────────────────────────────────────────────────────

    void TriggerGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        if (_heldRoot != null) { Destroy(_heldRoot); _heldRoot = null; _isHolding = false; }
        _gameOverScreen?.Show(_score, Rebuild);
    }

    // ── 유틸: 마우스 → 월드 좌표 ─────────────────────────────────────────

    static Vector3 MouseWorldPos()
    {
        var cam    = Camera.main;
        var mouse  = Mouse.current.position.ReadValue();
        var screen = new Vector3(mouse.x, mouse.y, -cam.transform.position.z);
        var pos    = cam.ScreenToWorldPoint(screen);
        pos.z = 0f;
        return pos;
    }

    // ── 일반 클릭 ─────────────────────────────────────────────────────────

    void HandleClick()
    {
        var worldPos = MouseWorldPos();
        var local = _towerRoot.InverseTransformPoint(worldPos);
        var cell  = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));

        if (!_cells.ContainsKey(cell)) return;

        if (_selected.Contains(cell))
            TryDeselect(cell);
        else if (_selected.Count < 4 && (_selected.Count == 0 || IsAdjacentToSelected(cell)))
        {
            SelectCell(cell);
            if (_selected.Count == 4) LiftBlocks();
        }
    }

    // ── 선택 / 해제 ───────────────────────────────────────────────────────

    void SelectCell(Vector2Int cell)
    {
        if (!_cells.TryGetValue(cell, out var data)) return;
        data.sr.color = Color.Lerp(NumberColor(data.number), Color.white, 0.5f);
        _selected.Add(cell);
    }

    void TryDeselect(Vector2Int cell)
    {
        var remaining = new List<Vector2Int>(_selected);
        remaining.Remove(cell);
        if (remaining.Count <= 1 || IsConnected(remaining))
            DeselectCell(cell);
    }

    void DeselectCell(Vector2Int cell)
    {
        if (_cells.TryGetValue(cell, out var data))
            data.sr.color = NumberColor(data.number);
        _selected.Remove(cell);
    }

    void ClearSelection()
    {
        foreach (var c in new List<Vector2Int>(_selected))
            DeselectCell(c);
    }

    // ── 블럭 들어올리기 ───────────────────────────────────────────────────

    void LiftBlocks()
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in _selected)
        {
            minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y);
            maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y);
        }

        _heldCenter = new Vector2((maxX - minX + 1) * 0.5f, (maxY - minY + 1) * 0.5f);

        _heldRelPos.Clear();
        _heldData.Clear();
        foreach (var c in _selected)
            _heldRelPos.Add(new Vector2Int(c.x - minX, c.y - minY));

        foreach (var cell in _selected)
        {
            if (!_cells.TryGetValue(cell, out var data)) continue;
            data.number--;
            if (data.number <= 0)
            {
                Destroy(data.go);
                _cells.Remove(cell);
            }
            else
            {
                data.sr.color   = NumberColor(data.number);
                data.label.text = data.number.ToString();
            }
        }

        _selected.Clear();

        _heldRoot = new GameObject("HeldBlocks");
        _heldRoot.transform.SetParent(transform);

        var heldColor = new Color(NumberColor(1).r, NumberColor(1).g, NumberColor(1).b, 0.6f);

        for (int i = 0; i < _heldRelPos.Count; i++)
        {
            var rel = _heldRelPos[i];

            var go = new GameObject($"Held_{i}");
            go.transform.SetParent(_heldRoot.transform, false);
            go.transform.localPosition = new Vector3(
                rel.x + 0.5f - _heldCenter.x,
                rel.y + 0.5f - _heldCenter.y,
                0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = CreateBlockSprite();
            sr.color        = heldColor;
            sr.sortingOrder = 0;

            var box = go.AddComponent<BoxCollider>();
            box.size           = Vector3.one;
            box.sharedMaterial = CreateFrictionMaterial();
            box.enabled        = false;

            var bc = go.AddComponent<BlockCell>();
            bc.Weight = 1;

            var label = SpawnLabel(1, go.transform);
            _heldData.Add(new CellData { number = 1, go = go, sr = sr, label = label });
        }

        CheckForDetachment();
        if (_cells.Count > 0)
            _rb.centerOfMass = CalculateCenterOfMass();

        _isHolding = true;
        AddScore(1);
    }

    // ── 커서 추적 ─────────────────────────────────────────────────────────

    void UpdateHeldPosition()
    {
        _heldRoot.transform.position = MouseWorldPos();
    }

    // ── 블럭 배치 ─────────────────────────────────────────────────────────

    void TryPlaceBlocks()
    {
        var worldPos = MouseWorldPos();
        var local   = _towerRoot.InverseTransformPoint(worldPos);
        int baseCol = Mathf.RoundToInt(local.x - _heldCenter.x);
        int baseRow = Mathf.RoundToInt(local.y - _heldCenter.y);

        var targets = new List<Vector2Int>(_heldRelPos.Count);
        foreach (var rel in _heldRelPos)
        {
            var target = new Vector2Int(baseCol + rel.x, baseRow + rel.y);
            if (_cells.ContainsKey(target)) return;
            if (target.x < placementMin.x || target.x > placementMax.x ||
                target.y < placementMin.y || target.y > placementMax.y) return;
            targets.Add(target);
        }

        bool adjacent = false;
        foreach (var t in targets)
        {
            foreach (var n in Neighbors(t))
            {
                if (_cells.ContainsKey(n)) { adjacent = true; break; }
            }
            if (adjacent) break;
        }
        if (!adjacent && _cells.Count > 0) return;

        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            var data   = _heldData[i];

            data.go.transform.SetParent(_towerRoot, false);
            data.go.transform.localPosition = new Vector3(target.x + 0.5f, target.y + 0.5f, 0f);

            var box = data.go.GetComponent<BoxCollider>();
            if (box != null) box.enabled = true;

            data.sr.color = NumberColor(data.number);

            var bc = data.go.GetComponent<BlockCell>();
            if (bc != null) bc.Weight = data.number;

            _cells[target] = data;
        }

        Destroy(_heldRoot);
        _heldRoot = null;
        _heldRelPos.Clear();
        _heldData.Clear();
        _isHolding = false;

        _rb.centerOfMass = CalculateCenterOfMass();
        AddScore(1);
    }

    // ── 들기 취소 ─────────────────────────────────────────────────────────

    void CancelHold()
    {
        Destroy(_heldRoot);
        _heldRoot = null;
        _heldRelPos.Clear();
        _heldData.Clear();
        _isHolding = false;

        if (_cells.Count > 0)
            _rb.centerOfMass = CalculateCenterOfMass();
    }

    // ── 연결 요소 분리 ────────────────────────────────────────────────────

    void CheckForDetachment()
    {
        if (_cells.Count == 0) return;

        var components = FindConnectedComponents();
        if (components.Count <= 1) return;

        int mainIdx = 0;
        for (int i = 1; i < components.Count; i++)
            if (components[i].Count > components[mainIdx].Count)
                mainIdx = i;

        for (int i = 0; i < components.Count; i++)
        {
            if (i == mainIdx) continue;
            DetachComponent(components[i]);
        }
    }

    List<List<Vector2Int>> FindConnectedComponents()
    {
        var unvisited  = new HashSet<Vector2Int>(_cells.Keys);
        var components = new List<List<Vector2Int>>();

        while (unvisited.Count > 0)
        {
            var en = unvisited.GetEnumerator();
            en.MoveNext();
            var start = en.Current;
            en.Dispose();

            var component = new List<Vector2Int>();
            var queue     = new Queue<Vector2Int>();
            queue.Enqueue(start);
            unvisited.Remove(start);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                component.Add(c);
                foreach (var n in Neighbors(c))
                    if (unvisited.Remove(n))
                        queue.Enqueue(n);
            }
            components.Add(component);
        }
        return components;
    }

    void DetachComponent(List<Vector2Int> component)
    {
        var centroid = Vector3.zero;
        int valid    = 0;
        foreach (var cell in component)
        {
            if (_cells.TryGetValue(cell, out var d))
            { centroid += d.go.transform.position; valid++; }
        }
        if (valid == 0) return;
        centroid /= valid;

        var orphanGO = new GameObject("DetachedBlocks");
        orphanGO.transform.SetParent(transform);
        orphanGO.transform.position = centroid;

        var orphanRb = orphanGO.AddComponent<Rigidbody>();
        orphanRb.useGravity             = true;
        orphanRb.interpolation          = RigidbodyInterpolation.Interpolate;
        orphanRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        orphanRb.linearVelocity         = _rb.linearVelocity;
        orphanRb.angularVelocity        = _rb.angularVelocity;
        orphanRb.linearDamping          = 0.3f;
        orphanRb.angularDamping         = 1f;
        orphanRb.constraints            = RigidbodyConstraints.FreezePositionZ
                                        | RigidbodyConstraints.FreezeRotationX
                                        | RigidbodyConstraints.FreezeRotationY;

        float   totalWeight = 0f;
        Vector2 weightedSum = Vector2.zero;

        foreach (var cell in component)
        {
            if (!_cells.TryGetValue(cell, out var data)) continue;

            data.go.transform.SetParent(orphanGO.transform, worldPositionStays: true);

            var localPos = (Vector2)data.go.transform.localPosition;
            weightedSum += localPos * data.number;
            totalWeight += data.number;

            _cells.Remove(cell);
        }

        if (totalWeight > 0f)
            orphanRb.centerOfMass = new Vector3(weightedSum.x / totalWeight, weightedSum.y / totalWeight, 0f);

        Destroy(orphanGO, 1f);
        AddScore(-Mathf.RoundToInt(totalWeight));
    }

    // ── 메인 타워 무게 중심 ───────────────────────────────────────────────

    Vector3 CalculateCenterOfMass()
    {
        float   totalWeight = 0f;
        Vector2 weightedSum = Vector2.zero;
        foreach (var (cell, data) in _cells)
        {
            var localPos = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            weightedSum += localPos * data.number;
            totalWeight += data.number;
        }
        var com = totalWeight > 0f
            ? weightedSum / totalWeight
            : new Vector2(columns * 0.5f, rows * 0.5f);
        return new Vector3(com.x, com.y, 0f);
    }

    // ── 유틸리티 ─────────────────────────────────────────────────────────

    bool IsAdjacentToSelected(Vector2Int cell)
    {
        foreach (var s in _selected)
            if (Mathf.Abs(cell.x - s.x) + Mathf.Abs(cell.y - s.y) == 1)
                return true;
        return false;
    }

    bool IsConnected(List<Vector2Int> cells)
    {
        if (cells.Count <= 1) return true;
        var set     = new HashSet<Vector2Int>(cells);
        var visited = new HashSet<Vector2Int> { cells[0] };
        var queue   = new Queue<Vector2Int>();
        queue.Enqueue(cells[0]);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var n in Neighbors(c))
                if (set.Contains(n) && visited.Add(n))
                    queue.Enqueue(n);
        }
        return visited.Count == cells.Count;
    }

    static IEnumerable<Vector2Int> Neighbors(Vector2Int c)
    {
        yield return new Vector2Int(c.x + 1, c.y);
        yield return new Vector2Int(c.x - 1, c.y);
        yield return new Vector2Int(c.x, c.y + 1);
        yield return new Vector2Int(c.x, c.y - 1);
    }

    // ── 타워 초기화 ───────────────────────────────────────────────────────

    void BuildTower()
    {
        GameObject towerRootGO;
        if (towerRootTransform != null)
        {
            towerRootGO = towerRootTransform.gameObject;
        }
        else
        {
            towerRootGO = new GameObject("TowerRoot");
            towerRootGO.transform.SetParent(transform);
        }
        towerRootGO.transform.position = new Vector3(-columns * 0.5f, -rows * 0.5f, 0f);
        _towerRoot = towerRootGO.transform;

        if (Application.isPlaying)
        {
            if (!towerRootGO.TryGetComponent(out _rb))
                _rb = towerRootGO.AddComponent<Rigidbody>();
            _rb.useGravity             = true;
            _rb.isKinematic            = false;
            _rb.interpolation          = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.linearDamping          = 0.3f;
            _rb.angularDamping         = 2f;
            _rb.constraints            = RigidbodyConstraints.FreezePositionZ
                                       | RigidbodyConstraints.FreezeRotationX
                                       | RigidbodyConstraints.FreezeRotationY;
        }

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                int number = Random.Range(2, 7);
                var cell   = new Vector2Int(col, row);

                var go = new GameObject($"Cell_{col}_{row}");
                go.transform.SetParent(towerRootGO.transform, false);
                go.transform.localPosition = new Vector3(col + 0.5f, row + 0.5f, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite       = CreateBlockSprite();
                sr.color        = NumberColor(number);
                sr.sortingOrder = 0;

                var box = go.AddComponent<BoxCollider>();
                box.size           = Vector3.one;
                box.sharedMaterial = CreateFrictionMaterial();

                var bc = go.AddComponent<BlockCell>();
                bc.Weight = number;

                var label = SpawnLabel(number, go.transform);
                _cells[cell] = new CellData { number = number, go = go, sr = sr, label = label };
            }
        }

        if (Application.isPlaying)
            _rb.centerOfMass = CalculateCenterOfMass();

        CreateFloor();
        CreateScoreLabel();

        CreateBoundaries();

        if (Application.isPlaying)
            CreateGameOverScreen();

        FitCamera();
    }

    // ── 바닥 ─────────────────────────────────────────────────────────────

    void CreateFloor()
    {
        float floorY     = _floorY = -rows * 0.5f - 1.5f;
        float floorWidth = columns + 4f;

        GameObject floorGO;
        if (floorTransform != null)
        {
            floorGO = floorTransform.gameObject;
        }
        else
        {
            floorGO = new GameObject("Floor");
            floorGO.transform.SetParent(transform);
            _generatedFloor = floorGO;
        }
        floorGO.transform.position   = new Vector3(0f, floorY, 0f);
        floorGO.transform.localScale = new Vector3(floorWidth, 1f, 1f);

        if (!floorGO.TryGetComponent<SpriteRenderer>(out var sr))
            sr = floorGO.AddComponent<SpriteRenderer>();
        sr.sprite = CreateBlockSprite();
        sr.color  = new Color(0.2f, 0.2f, 0.2f, 1f);

        if (Application.isPlaying)
        {
            if (!floorGO.TryGetComponent<BoxCollider>(out var col))
                col = floorGO.AddComponent<BoxCollider>();
            col.size           = Vector3.one;
            col.sharedMaterial = CreateFrictionMaterial();
        }
    }

    // ── 경계선 ────────────────────────────────────────────────────────────

    void CreateBoundaries()
    {
        float lineHeight  = 50f;
        float lineHalfH   = lineHeight * 0.5f;
        float lineWidth   = 0.15f;
        float offsetX     = columns * 0.5f + 2f;
        float centerY     = _floorY + lineHalfH;
        var   lineColor   = new Color(1f, 0.25f, 0.25f, 0.7f);

        _leftBoundary  = SpawnBoundary("BoundaryLeft",  new Vector3(-offsetX, centerY, 0f), lineWidth, lineHeight, lineColor);
        _rightBoundary = SpawnBoundary("BoundaryRight", new Vector3( offsetX, centerY, 0f), lineWidth, lineHeight, lineColor);
    }

    GameObject SpawnBoundary(string name, Vector3 worldPos, float width, float height, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.position   = worldPos;
        go.transform.localScale = new Vector3(width, height, 1f);

        var sr    = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateBlockSprite();
        sr.color  = color;
        sr.sortingOrder = 1;

        if (Application.isPlaying)
        {
            var rb         = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var col       = go.AddComponent<BoxCollider>();
            col.size      = Vector3.one;
            col.isTrigger = true;

            var bl = go.AddComponent<BoundaryLine>();
            bl.OnBlockTouched = TriggerGameOver;
        }

        return go;
    }

    // ── 게임오버 화면 ────────────────────────────────────────────────────

    void CreateGameOverScreen()
    {
        var go = new GameObject("GameOverScreen");
        go.transform.SetParent(transform);
        _gameOverScreen = go.AddComponent<GameOverScreen>();
    }

    // ── 스코어 라벨 ──────────────────────────────────────────────────────

    void CreateScoreLabel()
    {
        if (scoreLabel != null) { UpdateScoreDisplay(); return; }

        var go = new GameObject("ScoreLabel");
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(
            -columns * 0.5f - 2.5f,
             rows    * 0.5f + 0.5f,
             0f);

        var tmp  = go.AddComponent<TextMeshPro>();
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(4f, 2f);

        tmp.fontSize         = 2.5f;
        tmp.alignment        = TextAlignmentOptions.TopLeft;
        tmp.color            = Color.white;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Overflow;

        var r = go.GetComponent<Renderer>();
        if (r != null) r.sortingOrder = 3;

        scoreLabel           = tmp;
        _generatedScoreLabel = go;

        UpdateScoreDisplay();
    }

    // ── 레이블 ────────────────────────────────────────────────────────────

    TextMeshPro SpawnLabel(int number, Transform parent)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;

        var tmp  = go.AddComponent<TextMeshPro>();
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = Vector2.one;

        tmp.text             = number.ToString();
        tmp.fontSize         = 4f;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = Color.white;
        tmp.fontStyle        = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode     = TextOverflowModes.Overflow;

        var r = go.GetComponent<Renderer>();
        if (r != null) r.sortingOrder = 2;

        return tmp;
    }

    // ── 카메라 ────────────────────────────────────────────────────────────

    void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float halfW  = columns * 0.5f + 3.5f;
        float aspect = (float)Screen.width / Screen.height;

        cam.orthographicSize   = halfW / aspect;
        float startY           = _floorY + cam.orthographicSize;
        cam.transform.position = new Vector3(0f, startY, cam.transform.position.z);
    }

    // ── 스프라이트 ────────────────────────────────────────────────────────

    PhysicsMaterial CreateFrictionMaterial()
    {
        var mat             = new PhysicsMaterial("BlockFriction");
        mat.dynamicFriction = blockFriction;
        mat.staticFriction  = blockFriction;
        mat.bounciness      = 0f;
        return mat;
    }

    Sprite CreateBlockSprite()
    {
        if (_blockSprite != null) return _blockSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % size, y = i / size;
            bool border = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
            pixels[i] = border ? new Color(0f, 0f, 0f, 0.3f) : Color.white;
        }
        tex.SetPixels(pixels);
        tex.Apply();

        _blockSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                                     new Vector2(0.5f, 0.5f), size);
        return _blockSprite;
    }

    // ── 기즈모 ────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        // TowerRoot 기준 오프셋 계산
        var origin = new Vector3(-columns * 0.5f, -rows * 0.5f, 0f);

        float x0 = origin.x + placementMin.x;
        float y0 = origin.y + placementMin.y;
        float x1 = origin.x + placementMax.x + 1f;
        float y1 = origin.y + placementMax.y + 1f;

        // 반투명 채우기
        Gizmos.color = new Color(1f, 0.92f, 0.02f, 0.08f);
        Gizmos.DrawCube(
            new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f),
            new Vector3(x1 - x0, y1 - y0, 0.01f));

        // 테두리
        Gizmos.color = new Color(1f, 0.92f, 0.02f, 0.9f);
        Gizmos.DrawLine(new Vector3(x0, y0), new Vector3(x1, y0));
        Gizmos.DrawLine(new Vector3(x1, y0), new Vector3(x1, y1));
        Gizmos.DrawLine(new Vector3(x1, y1), new Vector3(x0, y1));
        Gizmos.DrawLine(new Vector3(x0, y1), new Vector3(x0, y0));
    }

    static Color NumberColor(int n) => n switch
    {
        2 => new Color(0.93f, 0.27f, 0.27f),
        3 => new Color(0.26f, 0.65f, 0.96f),
        4 => new Color(0.22f, 0.80f, 0.45f),
        5 => new Color(0.98f, 0.73f, 0.15f),
        6 => new Color(0.72f, 0.38f, 0.92f),
        _ => Color.gray
    };
}
