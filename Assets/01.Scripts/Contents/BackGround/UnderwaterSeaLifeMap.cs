using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class UnderwaterSeaLifeMap : MonoBehaviour
{
    [SerializeField, Tooltip("Complete editor-authored layout. When assigned, this replaces the fallback eight-object layout.")]
    GameObject layoutPrefab;
    [SerializeField] GameObject[] decorationPrefabs = new GameObject[0];
    [SerializeField] Vector3[] localPositions = new Vector3[0];
    [SerializeField] float[] localScales = new float[0];
    [SerializeField] bool[] flipX = new bool[0];

    const string ContainerName = "Animated Sea Life";

    void Awake()
    {
        EnsureDecorations();
    }

    void OnEnable()
    {
        EnsureDecorations();
    }

    void OnValidate()
    {
        if (isActiveAndEnabled)
            EnsureDecorations();
    }

    void EnsureDecorations()
    {
        if (transform.Find(ContainerName) != null)
            return;

        if (layoutPrefab != null)
        {
            var layout = Instantiate(layoutPrefab, transform);
            layout.name = ContainerName;
            layout.transform.localPosition = Vector3.zero;
            layout.transform.localRotation = Quaternion.identity;
            layout.transform.localScale = Vector3.one;
            return;
        }

        var container = new GameObject(ContainerName).transform;
        container.SetParent(transform, false);

        for (int i = 0; i < decorationPrefabs.Length; i++)
        {
            if (decorationPrefabs[i] == null)
                continue;

            var decoration = Instantiate(decorationPrefabs[i], container);
            decoration.name = decorationPrefabs[i].name;
            decoration.transform.localPosition = i < localPositions.Length ? localPositions[i] : Vector3.zero;
            float scale = i < localScales.Length ? Mathf.Max(0.01f, localScales[i]) : 0.1f;
            bool mirrored = i < flipX.Length && flipX[i];
            decoration.transform.localScale = new Vector3(mirrored ? -scale : scale, scale, 1f);
        }
    }
}
