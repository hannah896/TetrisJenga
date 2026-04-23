using UnityEngine;

public class BoundaryLine : MonoBehaviour
{
    public System.Action OnBlockTouched;

    void OnTriggerStay(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb != null && !rb.isKinematic)
            OnBlockTouched?.Invoke();
    }
}
