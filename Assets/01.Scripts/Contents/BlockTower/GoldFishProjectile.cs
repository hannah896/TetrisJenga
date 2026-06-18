using System.Collections.Generic;
using UnityEngine;

public class GoldFishProjectile : MonoBehaviour
{
    [SerializeField] float speed = 5f;
    [SerializeField] Vector2 initialDirection = new(1f, 1f);
    [SerializeField] float stuckSeconds = 0.6f;
    [SerializeField] float stallRecoverySeconds = 0.15f;
    
    [SerializeField] ScoreController  scoreController; 

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
        scoreController = FindObjectOfType<ScoreController>();
    }

    void Awake()
    {
        Util.SetNoPostLayer(gameObject);

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
            Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_finished || _rb == null || collision.contactCount == 0)
            return;

        var normal = collision.GetContact(0).normal;
        normal.z = 0f;
        if (normal.sqrMagnitude <= 0.001f)
            return;

        RecordCellContact(collision, normal);

        var reflected = Vector3.Reflect(_rb.linearVelocity.normalized, normal.normalized);
        reflected.z = 0f;
        if (reflected.sqrMagnitude > 0.001f)
        {
            _lastTravelDirection = reflected.normalized;
            _rb.linearVelocity = reflected.normalized * speed;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (_finished || collision.contactCount == 0)
            return;

        var normal = collision.GetContact(0).normal;
        normal.z = 0f;
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

    static bool IsVerticalCrushPair(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(a.y) >= 0.65f &&
               Mathf.Abs(b.y) >= 0.65f &&
               Vector3.Dot(a, b) <= -0.65f;
    }

}
