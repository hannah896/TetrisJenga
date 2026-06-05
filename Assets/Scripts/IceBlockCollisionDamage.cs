using System.Collections.Generic;
using UnityEngine;

public sealed class IceBlockCollisionDamage : MonoBehaviour
{
    const float InitialContactGraceSeconds = 0.25f;
    const float InitialContactDamageSpeed = 0.15f;

    BlockTower _owner;
    readonly HashSet<int> _damagedBlocks = new();
    readonly HashSet<int> _initialContacts = new();
    float _ignoreInitialContactsUntil;

    public void Initialize(BlockTower owner)
    {
        _owner = owner;
        _ignoreInitialContactsUntil = Application.isPlaying
            ? Time.time + InitialContactGraceSeconds
            : 0f;
        _initialContacts.Clear();
        _damagedBlocks.Clear();
    }

    void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        TryDamage(collision);
    }

    void OnCollisionExit(Collision collision)
    {
        int id = GetContactId(collision);
        if (id == 0)
            return;

        _initialContacts.Remove(id);
    }

    void TryDamage(Collision collision)
    {
        if (_owner == null || collision.collider == null)
            return;

        var body = collision.rigidbody;
        var blockCell = collision.collider.GetComponentInParent<BlockCell>();
        if (blockCell != null && blockCell.Kind == BlockCell.CellKind.Ice)
            return;

        int id = GetContactId(collision);
        if (id == 0)
            return;

        if (Time.time <= _ignoreInitialContactsUntil)
        {
            _initialContacts.Add(id);
            return;
        }

        if (_initialContacts.Contains(id))
        {
            if (collision.relativeVelocity.sqrMagnitude < InitialContactDamageSpeed * InitialContactDamageSpeed)
                return;

            _initialContacts.Remove(id);
        }

        if (_damagedBlocks.Contains(id))
            return;

        if (body != null && body.isKinematic)
            return;

        var iceCell = GetComponent<BlockCell>();
        if (_owner.ApplyIceDamage(iceCell != null ? iceCell : null, transform.position))
            _damagedBlocks.Add(id);
    }

    static int GetContactId(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return 0;

        var body = collision.rigidbody;
        return body != null ? body.GetInstanceID() : collision.collider.GetInstanceID();
    }
}
