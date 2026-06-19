using UnityEngine;

[RequireComponent(typeof(BlockTower))]
public class TowerShapeBackground : MonoBehaviour
{
    [SerializeField] BlockTower _tower;
    [SerializeField] Sprite _sprite;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
    }
#endif

    void OnEnable()
    {
        if (_tower != null) _tower.OnTowerReady += HandleTowerReady;
    }

    void OnDisable()
    {
        if (_tower != null) _tower.OnTowerReady -= HandleTowerReady;
    }

    void HandleTowerReady()
    {
        if (_sprite == null || _tower.TowerRoot == null) return;

        var go = new GameObject("TowerShapeBG");
        go.transform.SetParent(_tower.TowerRoot);
        go.transform.localPosition = new Vector3(0f, 0f, 0.2f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _sprite;
        sr.sortingOrder = -1;

        float sx = _sprite.bounds.size.x;
        float sy = _sprite.bounds.size.y;
        go.transform.localScale = new Vector3(_tower.columns / sx, _tower.rows / sy, 1f);
    }
}
