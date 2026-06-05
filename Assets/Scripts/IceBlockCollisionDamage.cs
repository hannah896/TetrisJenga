using System.Collections.Generic;
using UnityEngine;

public sealed class IceBlockCollisionDamage : MonoBehaviour
{
    BlockTower _owner;
    readonly HashSet<int> _damagedBlocks = new();

    public void Initialize(BlockTower owner)
    {
        _owner = owner;
    }

    void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        TryDamage(collision);
    }

    void TryDamage(Collision collision)
    {
        if (_owner == null || collision.collider == null)
            return;

        var body = collision.rigidbody;
        var blockCell = collision.collider.GetComponentInParent<BlockCell>();
        if (blockCell != null && blockCell.Kind == BlockCell.CellKind.Ice)
            return;

        int id = body != null ? body.GetInstanceID() : collision.collider.GetInstanceID();
        if (_damagedBlocks.Contains(id))
            return;

        if (body != null && body.isKinematic)
            return;

        var iceCell = GetComponent<BlockCell>();
        if (_owner.ApplyIceDamage(iceCell != null ? iceCell : null, transform.position))
            _damagedBlocks.Add(id);
    }
}
