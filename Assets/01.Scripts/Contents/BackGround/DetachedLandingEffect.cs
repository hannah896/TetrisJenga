using System.Collections.Generic;
using UnityEngine;

sealed class DetachedLandingEffect : MonoBehaviour
{
    TowerPhysicsController _owner;
    float _detachedAt;
    readonly HashSet<int> _spawnedColliders = new();

    public void Initialize(TowerPhysicsController owner, float detachedAt)
    {
        _owner = owner;
        _detachedAt = detachedAt;
    }

    void OnCollisionEnter(Collision collision)
    {
        SpawnForNewBlockContacts(collision);
    }

    void SpawnForNewBlockContacts(Collision collision)
    {
        if (_owner == null || collision == null)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            Collider blockCollider;
            Collider landingSurface;
            if (IsDetachedBlockCollider(contact.thisCollider))
            {
                blockCollider = contact.thisCollider;
                landingSurface = contact.otherCollider;
            }
            else if (IsDetachedBlockCollider(contact.otherCollider))
            {
                blockCollider = contact.otherCollider;
                landingSurface = contact.thisCollider;
            }
            else
            {
                continue;
            }

            var landedBlock = blockCollider.GetComponentInParent<BlockCell>();
            if (landedBlock == null)
                continue;

            int blockId = landedBlock.GetInstanceID();
            if (_spawnedColliders.Contains(blockId))
                continue;

            if (_owner.TrySpawnDetachedLandingEffect(
                    collision,
                    landingSurface,
                    landedBlock,
                    _detachedAt))
                _spawnedColliders.Add(blockId);
        }
    }

    bool IsDetachedBlockCollider(Collider candidate)
    {
        return candidate != null &&
               candidate.transform.IsChildOf(transform) &&
               candidate.GetComponentInParent<BlockCell>() != null;
    }
}

