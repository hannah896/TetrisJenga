using UnityEngine;
using UnityEngine.Events;

public class BoundaryLine : MonoBehaviour
{
    public UnityAction OnBlockTouched;
    public UnityAction<Rigidbody> OnDetachedBlocksTouched;

    void OnTriggerEnter(Collider other)
    {
        TryTriggerGameOver(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryTriggerGameOver(other);
    }

    void TryTriggerGameOver(Collider other)
    {
        if (other.GetComponentInParent<GoldFishProjectile>() != null)
            return;

        var rb = other.attachedRigidbody;
        if (rb == null || rb.isKinematic)
            return;

        if (rb.gameObject.name == "DetachedBlocks")
        {
            OnDetachedBlocksTouched?.Invoke(rb);
            return;
        }

        OnBlockTouched?.Invoke();
    }
}
