using UnityEngine;

// 블럭 GO에 붙는 경량 컴포넌트.
// DetachComponent에서 무게 중심 계산 시 참조한다.
public class BlockCell : MonoBehaviour
{
    public float Weight;
    public bool IsOriginalTower;
}
