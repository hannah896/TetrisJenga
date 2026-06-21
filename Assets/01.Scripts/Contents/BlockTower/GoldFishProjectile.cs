using System.Collections.Generic;
using JSAM;
using UnityEngine;

public class GoldFishProjectile : MonoBehaviour
{
    [SerializeField] float speed = 5f;
    [SerializeField] Vector2 initialDirection = new(1f, 1f);
    [SerializeField] float stuckSeconds = 0.6f;
    [SerializeField] float stallRecoverySeconds = 0.15f;

    [Header("Visual")]
    [SerializeField] Vector2 visualCellSize = Vector2.one;
    [SerializeField, Range(0.01f, 3f)] float visualScaleMultiplier = 1f / 3f;

    [SerializeField] ScoreController scoreController;

    Rigidbody _rb;
    float _stuckTimer;
    float _stallTimer;
    bool _finished;
    Vector3 _lastTravelDirection = Vector3.right;
    readonly Dictionary<Collider, Vector3> _cellContactNormals = new();

    void OnEnable()
    {
        Util.SetNoPostLayer(gameObject);
    }

    void OnValidate()
    {
        Util.SetNoPostLayer(gameObject);
        FitSpriteToCell();
        scoreController = FindFirstObjectByType<ScoreController>();
    }

    void Awake()
    {
        Util.SetNoPostLayer(gameObject);
        FitSpriteToCell();

        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody>();

        _rb.useGravity = false;
        _rb.isKinematic = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.constraints = RigidbodyConstraints.FreezePositionZ
                        | RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationY;

        var dir = initialDirection.sqrMagnitude > 0.001f ? initialDirection.normalized : Vector2.one.normalized;
        _lastTravelDirection = new Vector3(dir.x, dir.y, 0f).normalized;
        _rb.linearVelocity = _lastTravelDirection * speed;
    }

    void FitSpriteToCell()
    {
        if (!TryGetComponent<SpriteRenderer>(out var sr) || sr.sprite == null)
            return;

        var spriteSize = sr.sprite.bounds.size;
        if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f)
            return;

        float targetWidth = Mathf.Max(0.0001f, visualCellSize.x * visualScaleMultiplier);
        float targetHeight = Mathf.Max(0.0001f, visualCellSize.y * visualScaleMultiplier);
        float scaleX = targetWidth / spriteSize.x;
        float scaleY = targetHeight / spriteSize.y;
        transform.localScale = new Vector3(scaleX, scaleY, 1f);

        if (TryGetComponent<BoxCollider>(out var box))
            box.size = new Vector3(targetWidth / scaleX, targetHeight / scaleY, box.size.z);
    }

    void FixedUpdate()
    {
        if (_finished || _rb == null)
            return;

        var velocity = _rb.linearVelocity;
        velocity.z = 0f;
        if (velocity.sqrMagnitude > 0.001f)
        {
            _lastTravelDirection = velocity.normalized;
            _rb.linearVelocity = velocity.normalized * speed;
            _stallTimer = 0f;
        }
        else
        {
            _stallTimer += Time.fixedDeltaTime;
            if (_stallTimer >= stallRecoverySeconds)
            {
                _rb.linearVelocity = _lastTravelDirection * speed;
                _stallTimer = 0f;
            }
        }

        _stuckTimer = IsPressedBetweenCellColliders()
            ? _stuckTimer + Time.fixedDeltaTime
            : 0f;

        if (_stuckTimer >= stuckSeconds)
        {
            if (!TryEscapeStuck())
                Destroy(gameObject);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_finished || _rb == null || collision.contactCount == 0)
            return;

        var normal = GetAlignedNormal(collision.GetContact(0).normal);
        if (normal == Vector3.zero)
            return;

        RecordCellContact(collision, normal);

        var incoming = _rb.linearVelocity;
        incoming.z = 0f;
        if (incoming.sqrMagnitude < 0.001f)
            incoming = _lastTravelDirection;

        var reflected = Vector3.Reflect(incoming.normalized, normal);
        reflected.z = 0f;
        if (reflected.sqrMagnitude > 0.001f)
        {
            _lastTravelDirection = reflected.normalized;
            _rb.linearVelocity = reflected.normalized * speed;
            // 겹침 해소: 노멀 방향으로 살짝 밀어냄
            transform.position += normal * 0.05f;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (_finished || collision.contactCount == 0)
            return;

        var normal = GetAlignedNormal(collision.GetContact(0).normal);
        RecordCellContact(collision, normal);
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.collider != null)
            _cellContactNormals.Remove(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        if (_finished || other.GetComponent<BoundaryLine>() == null)
            return;

        _finished = true;
        AudioPlayback.PlaySound(_AudioLibrarySounds.EscapeFish);
        scoreController?.AwardGoldFishDeadlineScore(transform.position);
        Destroy(gameObject);
    }

    void RecordCellContact(Collision collision, Vector3 normal)
    {
        if (collision.collider == null ||
            collision.collider.GetComponentInParent<BlockCell>() == null ||
            normal.sqrMagnitude <= 0.001f)
        {
            return;
        }

        _cellContactNormals[collision.collider] = normal.normalized;
    }

    bool IsPressedBetweenCellColliders()
    {
        if (_cellContactNormals.Count < 2)
            return false;

        var normals = new List<Vector3>(_cellContactNormals.Values);
        for (int i = 0; i < normals.Count; i++)
        {
            for (int j = i + 1; j < normals.Count; j++)
            {
                if (IsVerticalCrushPair(normals[i], normals[j]))
                    return true;
            }
        }

        return false;
    }

    bool TryEscapeStuck()
    {
        var escapeDir = Vector3.zero;
        foreach (var n in _cellContactNormals.Values)
            escapeDir += n;
        escapeDir.z = 0f;

        if (escapeDir.sqrMagnitude < 0.001f)
            return false;

        escapeDir = escapeDir.normalized;
        transform.position += escapeDir * 0.2f;
        _rb.linearVelocity = escapeDir * speed;
        _lastTravelDirection = escapeDir;
        _stuckTimer = 0f;
        _cellContactNormals.Clear();
        return true;
    }

    static Vector3 GetAlignedNormal(Vector3 raw)
    {
        raw.z = 0f;
        if (raw.sqrMagnitude <= 0.001f)
            return Vector3.zero;

        return Mathf.Abs(raw.x) >= Mathf.Abs(raw.y)
            ? new Vector3(Mathf.Sign(raw.x), 0f, 0f)
            : new Vector3(0f, Mathf.Sign(raw.y), 0f);
    }

    static bool IsVerticalCrushPair(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(a.y) >= 0.65f &&
               Mathf.Abs(b.y) >= 0.65f &&
               Vector3.Dot(a, b) <= -0.65f;
    }
}
