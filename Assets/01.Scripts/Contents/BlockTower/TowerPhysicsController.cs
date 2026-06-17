using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using JSAM;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TowerPhysicsController : MonoBehaviour
{
    [SerializeField] BlockTower _tower;

    [Header("Physics")]
    [SerializeField] float blockFriction = 1f;
    [SerializeField] float toppleTorque = 4f;
    [SerializeField] float toppleMargin = 0.05f;

    [Header("Detached Blocks")]
    [SerializeField] float detachedReattachStableTime = 0.15f;
    [SerializeField] float detachedReattachVelocity   = 0.55f;
    [SerializeField] float detachedMinAirTime         = 0.35f;
    [SerializeField] float detachedPenaltyDelay       = 2f;
    [Tooltip("활성화 시 분리 블록이 데드라인 아래로 떨어지면 즉시 게임오버 (빙판 스테이지용)")]
    [SerializeField] bool  deadlineFallGameOver = false;
    [Tooltip("플로어 Y에서 몇 유닛 아래까지 떨어지면 게임오버로 처리할지")]
    [SerializeField] float deadlineFallDepth    = 3f;
    [SerializeField, Range(0.85f, 1f)] float blockBodyScale    = 0.94f;
    [SerializeField, Range(0.85f, 1f)] float blockColliderScale = 0.92f;
    [SerializeField] GameObject detachedLandingEffectPrefab;
    [SerializeField, Min(0.1f)] float detachedLandingEffectScale = 1f;

    Rigidbody _rb;
    CameraController _camera;
    readonly List<DetachedComponent> _detachedComponents = new();

    TowerGridModel                   _grid;
    Dictionary<Vector2Int, CellView> _cellViews;
    ScoreController                  _score;
    BombIceEffectController          _bombIce;
    TowerCellVisualizer              _visualizer;

    public float BlockBodyScale => blockBodyScale;

    void Awake()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
        _camera     = GetComponent<CameraController>();
        _grid       = _tower.Grid;
        _cellViews  = _tower.CellViews;
        _score      = GetComponent<ScoreController>();
        _bombIce    = GetComponent<BombIceEffectController>();
        _visualizer = GetComponent<TowerCellVisualizer>();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
        ResolveDetachedLandingEffectPrefab();
    }

    void ResolveDetachedLandingEffectPrefab()
    {
        if (detachedLandingEffectPrefab == null)
            detachedLandingEffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FX/Prefabs/FX002_01.prefab");
    }
#endif

    // ── 리지드바디 ───────────────────────────────────────────────────────────

    public void ConfigureTowerRigidbody()
    {
        var towerRoot = _tower.TowerRoot;
        if (!Application.isPlaying || towerRoot == null) return;

        if (!towerRoot.TryGetComponent(out _rb))
            _rb = towerRoot.gameObject.AddComponent<Rigidbody>();
        _rb.useGravity  = true;
        _rb.isKinematic = false;
        _rb.interpolation          = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        _rb.linearDamping  = 0.3f;
        _rb.angularDamping = 2f;
        _rb.constraints = RigidbodyConstraints.FreezePositionZ
                        | RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationY;
    }

    public void ClearRigidbody() => _rb = null;

    // ── 마찰 재질 / 충돌체 크기 ───────────────────────────────────────────

    public float LocalColliderSize() =>
        blockBodyScale > 0.001f ? blockColliderScale / blockBodyScale : blockColliderScale;

    public PhysicsMaterial CreateFrictionMaterial()
    {
        var mat = new PhysicsMaterial("BlockFriction");
        mat.dynamicFriction = blockFriction;
        mat.staticFriction  = blockFriction;
        mat.bounciness      = 0f;
        return mat;
    }

    // ── 타워 물리 상태 갱신 ──────────────────────────────────────────────

    public void UpdateTowerPhysicsState()
    {
        if (!Application.isPlaying || _rb == null || _grid.Count == 0) return;

        var com = CalculateCenterOfMass();
        _rb.centerOfMass = com;
        _rb.WakeUp();
        ApplyToppleTorqueIfUnsupported(com);
    }

    Vector3 CalculateCenterOfMass()
    {
        float totalWeight = 0f;
        Vector2 weightedSum = Vector2.zero;
        foreach (var (cell, state) in _grid.AllCells)
        {
            if (state.kind == CellKind.Ice) continue;
            var localPos = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            weightedSum += localPos * state.number;
            totalWeight += state.number;
        }

        var com = totalWeight > 0f
            ? weightedSum / totalWeight
            : new Vector2(_tower.columns * 0.5f, _tower.rows * 0.5f);
        return new Vector3(com.x, com.y, 0f);
    }

    void ApplyToppleTorqueIfUnsupported(Vector3 com)
    {
        if (toppleTorque <= 0f) return;
        if (!TryGetLowestSupportRange(out float supportMinX, out float supportMaxX)) return;

        float sign = 0f;
        if (com.x < supportMinX - toppleMargin)      sign =  1f;
        else if (com.x > supportMaxX + toppleMargin) sign = -1f;
        if (Mathf.Approximately(sign, 0f)) return;

        _rb.AddTorque(Vector3.forward * sign * toppleTorque, ForceMode.Impulse);
    }

    bool TryGetLowestSupportRange(out float minX, out float maxX)
    {
        minX = float.MaxValue;
        maxX = float.MinValue;
        int minY = int.MaxValue;

        foreach (var pair in _grid.AllCells)
        {
            if (pair.Value.kind == CellKind.Ice) continue;
            minY = Mathf.Min(minY, pair.Key.y);
        }

        if (minY == int.MaxValue) return false;

        foreach (var pair in _grid.AllCells)
        {
            if (pair.Value.kind == CellKind.Ice) continue;
            if (pair.Key.y != minY) continue;
            minX = Mathf.Min(minX, pair.Key.x);
            maxX = Mathf.Max(maxX, pair.Key.x + 1f);
        }

        return minX <= maxX;
    }

    // ── 분리 감지 ────────────────────────────────────────────────────────

    public void CheckForDetachment()
    {
        if (_grid.Count == 0) return;

        DetachOriginalTowerBlocksSupportedOnlyByTop();

        var components = _grid.FindConnectedComponents();
        if (components.Count <= 1) return;

        int mainIdx = FindMainTowerComponentIndex(components);
        for (int i = 0; i < components.Count; i++)
        {
            if (i == mainIdx) continue;
            DetachComponent(components[i]);
        }
    }

    void DetachOriginalTowerBlocksSupportedOnlyByTop()
    {
        var components = _grid.FindOriginalTowerComponents();
        if (components.Count <= 1) return;

        int mainIdx = FindMainTowerComponentIndex(components);
        for (int i = 0; i < components.Count; i++)
        {
            if (i == mainIdx) continue;
            if (_grid.TouchesPlacedTopBlock(components[i]))
                DetachComponent(components[i]);
        }
    }

    int FindMainTowerComponentIndex(List<List<Vector2Int>> components)
    {
        int bestIdx = 0;
        for (int i = 1; i < components.Count; i++)
            if (IsBetterMainTowerComponent(components[i], components[bestIdx]))
                bestIdx = i;
        return bestIdx;
    }

    bool IsBetterMainTowerComponent(List<Vector2Int> candidate, List<Vector2Int> current)
    {
        bool cGrounded = _grid.TouchesGround(candidate);
        bool curGrounded = _grid.TouchesGround(current);
        if (cGrounded != curGrounded) return cGrounded;

        int cMinY = _grid.MinComponentY(candidate);
        int curMinY = _grid.MinComponentY(current);
        if (cMinY != curMinY) return cMinY < curMinY;

        return candidate.Count > current.Count;
    }

    void DetachComponent(List<Vector2Int> component)
    {
        var centroid = Vector3.zero;
        int valid = 0;
        foreach (var cell in component)
        {
            if (_cellViews.TryGetValue(cell, out var v))
            {
                centroid += v.go.transform.position;
                valid++;
            }
        }

        if (valid == 0) return;
        centroid /= valid;

        var orphanGO = new GameObject("DetachedBlocks");
        _tower.TrackGeneratedObject(orphanGO);
        orphanGO.transform.SetParent(_tower.transform);
        orphanGO.transform.position = centroid;

        var orphanRb = orphanGO.AddComponent<Rigidbody>();
        orphanRb.useGravity             = true;
        orphanRb.interpolation          = RigidbodyInterpolation.Interpolate;
        orphanRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        orphanRb.linearVelocity         = _rb.linearVelocity;
        orphanRb.angularVelocity        = _rb.angularVelocity;
        orphanRb.linearDamping          = 0.3f;
        orphanRb.angularDamping         = 1f;
        orphanRb.constraints = RigidbodyConstraints.FreezePositionZ
                             | RigidbodyConstraints.FreezeRotationX
                             | RigidbodyConstraints.FreezeRotationY;

        float totalWeight   = 0f;
        Vector2 weightedSum = Vector2.zero;

        foreach (var cell in component)
        {
            if (!_grid.TryGetCell(cell, out var state)) continue;
            if (!_cellViews.TryGetValue(cell, out var view)) continue;

            view.go.transform.SetParent(orphanGO.transform, worldPositionStays: true);

            var localPos = (Vector2)view.go.transform.localPosition;
            weightedSum += localPos * state.number;
            totalWeight += state.number;

            _grid.RemoveCell(cell);
            _cellViews.Remove(cell);
        }

        if (totalWeight > 0f)
            orphanRb.centerOfMass = new Vector3(weightedSum.x / totalWeight, weightedSum.y / totalWeight, 0f);

        if (Application.isPlaying)
        {
            var detached = new DetachedComponent
            {
                root        = orphanGO,
                rb          = orphanRb,
                detachedAt  = Time.time,
                scorePenalty = Mathf.RoundToInt(totalWeight)
            };
            var landingEffect = orphanGO.AddComponent<DetachedLandingEffect>();
            landingEffect.Initialize(this, detached.detachedAt);
            _detachedComponents.Add(detached);
            StartCoroutine(TryReattachDetachedComponent(detached));
        }
    }

    // ── 분리 블록 재흡수 코루틴 ──────────────────────────────────────────

    IEnumerator TryReattachDetachedComponent(DetachedComponent detached)
    {
        float stable = 0f;

        while (!detached.resolved && detached.root != null && detached.rb != null && !_tower.IsGameOver)
        {
            if (deadlineFallGameOver && IsBelowDeadline(detached))
            {
                detached.resolved = true;
                _detachedComponents.Remove(detached);
                if (detached.root != null) Destroy(detached.root);
                _score?.TriggerGameOver();
                yield break;
            }

            stable = IsDetachedStable(detached.rb) ? stable + Time.deltaTime : 0f;

            bool canTry = Time.time - detached.detachedAt >= detachedMinAirTime &&
                          stable >= detachedReattachStableTime;

            if (canTry && (_bombIce?.ApplyIceColumnDamageToDetached(detached) ?? false))
            {
                detached.resolved = true;
                _detachedComponents.Remove(detached);
                yield break;
            }

            if (canTry && TryAbsorbDetachedComponent(detached.root, detached.rb))
            {
                detached.resolved = true;
                _detachedComponents.Remove(detached);
                yield break;
            }

            if (canTry && TriggerDetachedBombLanding(detached))
            {
                detached.resolved = true;
                _detachedComponents.Remove(detached);
                yield break;
            }

            if (Time.time - detached.detachedAt >= detachedPenaltyDelay)
            {
                ApplyDetachedPenalty(detached);
                yield break;
            }

            yield return null;
        }
    }

    // ── 분리 블록 목록 즉시 갱신 (Update 호출) ───────────────────────────

    public void RefreshDetachedComponents()
    {
        for (int i = _detachedComponents.Count - 1; i >= 0; i--)
        {
            var detached = _detachedComponents[i];
            if (detached.root == null || detached.rb == null)
            {
                _detachedComponents.RemoveAt(i);
                continue;
            }

            if (detached.resolved)
            {
                _detachedComponents.RemoveAt(i);
                continue;
            }

            if (deadlineFallGameOver && IsBelowDeadline(detached))
            {
                detached.resolved = true;
                _detachedComponents.RemoveAt(i);
                if (detached.root != null) Destroy(detached.root);
                _score?.TriggerGameOver();
                continue;
            }

            bool canTry = CanTryReattach(detached);
            if (canTry && (_bombIce?.ApplyIceColumnDamageToDetached(detached) ?? false))
            {
                detached.resolved = true;
                _detachedComponents.RemoveAt(i);
                continue;
            }

            if (canTry && TryAbsorbDetachedComponent(detached.root, detached.rb))
            {
                detached.resolved = true;
                _detachedComponents.RemoveAt(i);
                continue;
            }

            if (canTry && TriggerDetachedBombLanding(detached))
            {
                detached.resolved = true;
                _detachedComponents.RemoveAt(i);
                continue;
            }

            if (Time.time - detached.detachedAt >= detachedPenaltyDelay)
                ApplyDetachedPenalty(detached);
        }
    }

    void ApplyDetachedPenalty(DetachedComponent detached)
    {
        if (detached.resolved) return;
        detached.resolved = true;
        var scorePos = detached.root != null ? detached.root.transform.position : _tower.transform.position;
        _detachedComponents.Remove(detached);
        if (detached.root != null)
            Destroy(detached.root);
        _score?.AddScore(-Mathf.Max(0, detached.scorePenalty), scorePos);
    }

    bool IsBelowDeadline(DetachedComponent detached) =>
        detached.root != null &&
        detached.root.transform.position.y < _tower.FloorY - deadlineFallDepth;

    bool TriggerDetachedBombLanding(DetachedComponent detached)
    {
        if (detached?.root == null) return false;

        bool triggered = false;
        foreach (Transform child in detached.root.transform)
        {
            if (child == null) continue;
            var blockCell = child.GetComponent<BlockCell>();
            if (blockCell == null || blockCell.Kind != CellKind.Bomb) continue;
            if (_tower.TryWorldToGridCell(child.position, out var cell))
            {
                _bombIce?.TriggerBombAt(cell);
                triggered = true;
            }
        }

        if (!triggered) return false;
        Destroy(detached.root);
        return true;
    }

    public HashSet<Vector2Int> CollectStableDetachedCells()
    {
        var cells = new HashSet<Vector2Int>();
        foreach (var detached in _detachedComponents)
        {
            if (detached.root == null || detached.rb == null) continue;
            if (!IsDetachedStable(detached.rb)) continue;
            foreach (Transform child in detached.root.transform)
            {
                if (_tower.TryWorldToGridCell(child.position, out var cell))
                    cells.Add(cell);
            }
        }
        return cells;
    }

    bool IsDetachedStable(Rigidbody rb) =>
        rb.linearVelocity.sqrMagnitude  <= detachedReattachVelocity * detachedReattachVelocity &&
        rb.angularVelocity.sqrMagnitude <= detachedReattachVelocity * detachedReattachVelocity;

    bool CanTryReattach(DetachedComponent detached) =>
        detached.root != null &&
        detached.rb   != null &&
        Time.time - detached.detachedAt >= detachedMinAirTime &&
        IsDetachedStable(detached.rb);

    bool TryAbsorbDetachedComponent(GameObject detachedRoot, Rigidbody detachedRb)
    {
        var children = new List<Transform>();
        foreach (Transform child in detachedRoot.transform)
            children.Add(child);
        if (children.Count == 0) return false;

        var attach    = new List<(Transform child, Vector2Int cell)>(children.Count);
        var duplicates = new List<Transform>();
        var used       = new HashSet<Vector2Int>();

        foreach (var child in children)
        {
            if (!_tower.TryWorldToGridCell(child.position, out var cell)) return false;
            if (_grid.TryGetCell(cell, out var existing) && existing.kind == CellKind.Ice)
                return false;

            if (_grid.HasCell(cell) || !used.Add(cell))
            {
                duplicates.Add(child);
                continue;
            }

            attach.Add((child, cell));
        }

        if (attach.Count == 0 && duplicates.Count == 0) return false;
        if (!HasDetachedFaceContact(used)) return false;

        detachedRb.isKinematic = true;
        foreach (var dup in duplicates)
            Destroy(dup.gameObject);

        var towerRoot = _tower.TowerRoot;
        foreach (var (child, cell) in attach)
        {
            child.SetParent(towerRoot, worldPositionStays: false);
            child.localPosition = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
            child.localRotation = Quaternion.identity;

            var sr        = child.GetComponent<SpriteRenderer>();
            var label     = child.GetComponentInChildren<TextMeshPro>();
            var blockCell = child.GetComponent<BlockCell>();

            var state = new CellState
            {
                number          = Mathf.Max(1, blockCell?.Weight is float w ? Mathf.RoundToInt(w) : 1),
                isOriginalTower = blockCell != null && blockCell.IsOriginalTower,
                kind            = blockCell != null ? blockCell.Kind : CellKind.Normal,
                concealedByBomb = _bombIce?.IsConcealedByBomb(cell) ?? false
            };
            var view = new CellView
            {
                go      = child.gameObject,
                sr      = sr,
                outline = child.Find("FocusOutline")?.GetComponent<SpriteRenderer>(),
                label   = label
            };

            _grid.AddCell(cell, state);
            _cellViews[cell] = view;
            _bombIce?.ApplyIceColumnLandingDamage(cell, state);
            if (view.go == null) continue;
            _visualizer?.ApplyCellVisual(cell);
        }

        Destroy(detachedRoot);
        AudioManager.PlaySound(_AudioLibrarySounds.Drop);
        UpdateTowerPhysicsState();
        _tower.UpdateExtractionTowerRowsFromCells();
        if (!_tower.IsHolding)
        {
            _camera?.ShowExtractionView(immediate: true);
            _tower.FocusDefaultExtractionCell();
        }

        return true;
    }

    bool HasDetachedFaceContact(HashSet<Vector2Int> detachedCells)
    {
        foreach (var cell in detachedCells)
            foreach (var neighbor in Util.Neighbors(cell))
            {
                if (detachedCells.Contains(neighbor)) continue;
                if (_grid.IsMergeableCell(neighbor)) return true;
            }
        return false;
    }

    // ── 분리 블록 전체 제거 ──────────────────────────────────────────────

    public void ClearDetachedBlocks()
    {
        var roots = new HashSet<GameObject>();
        foreach (var detached in _detachedComponents)
        {
            detached.resolved = true;
            if (detached.root != null)
                roots.Add(detached.root);
        }

        _detachedComponents.Clear();

        var staleRoots = new List<Transform>();
        foreach (Transform child in _tower.transform)
            if (child.name == "DetachedBlocks")
                staleRoots.Add(child);

        foreach (var child in staleRoots)
            roots.Add(child.gameObject);

        foreach (var root in roots)
            if (root != null)
                _tower.DestroyTracked(root);
    }

    // ── 착지 이펙트 ──────────────────────────────────────────────────────

    public bool TrySpawnDetachedLandingEffect(
        Collision collision,
        Collider  landingSurface,
        BlockCell landedBlock,
        float     detachedAt)
    {
        if (detachedLandingEffectPrefab == null || collision == null || landedBlock == null ||
            Time.time - detachedAt < 0.1f || collision.relativeVelocity.sqrMagnitude < 0.0225f)
            return false;

        var generatedFloor  = _tower.GeneratedFloor;
        var floorTransform  = _tower.FloorTransformRef;

        bool hitBlock = landingSurface != null &&
                        landingSurface.GetComponentInParent<BlockCell>() != null;
        bool hitFloor = landingSurface != null &&
                        ((generatedFloor != null && landingSurface.transform.IsChildOf(generatedFloor.transform)) ||
                         (floorTransform != null && landingSurface.transform.IsChildOf(floorTransform))           ||
                         landingSurface.name.Contains("Floor"));
        if (!hitBlock && !hitFloor) return false;

        var impactPos = landedBlock.transform.position + new Vector3(0f, 0.5f, -0.2f);
        var effect    = Instantiate(detachedLandingEffectPrefab, impactPos, Quaternion.identity);
        effect.transform.localScale *= detachedLandingEffectScale;
        foreach (var sr in effect.GetComponentsInChildren<SpriteRenderer>())
            sr.sortingOrder = 50;
        Destroy(effect, LandingEffectLifetime(effect));
        return true;
    }

    static float LandingEffectLifetime(GameObject effect)
    {
        var animator = effect != null ? effect.GetComponentInChildren<Animator>() : null;
        var clips    = animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips : null;
        float lifetime = 0f;
        if (clips != null)
            foreach (var clip in clips)
                if (clip != null)
                    lifetime = Mathf.Max(lifetime, clip.length);
        return Mathf.Max(0.05f, lifetime);
    }

}
