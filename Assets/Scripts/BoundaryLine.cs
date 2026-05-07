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
        var rb = other.attachedRigidbody;
        if (rb != null && rb.GetComponent<BlockCell>() != null)
        {
            OnBlockTouched?.Invoke();
            return;
        }

        if (other.GetComponentInParent<BlockCell>() != null)
            OnBlockTouched?.Invoke();
    }
}
