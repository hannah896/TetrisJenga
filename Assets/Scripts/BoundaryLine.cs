using UnityEngine;

public class BoundaryLine : MonoBehaviour
{
    public System.Action OnBlockTouched;

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
        if (rb != null && !rb.isKinematic)
            OnBlockTouched?.Invoke();
    }
}
