using UnityEngine;

[ExecuteAlways]
public sealed class BombEditorSnap : MonoBehaviour
{
    [SerializeField] float offset = 0.5f;

    void OnValidate()
    {
        Snap();
    }

    void Update()
    {
        if (!Application.isPlaying)
            Snap();
    }

    void Snap()
    {
        var position = transform.position;
        var snapped = new Vector3(
            Mathf.Floor(position.x) + offset,
            Mathf.Floor(position.y) + offset,
            position.z);

        if ((position - snapped).sqrMagnitude <= 0.000001f)
            return;

        transform.position = snapped;
    }
}
