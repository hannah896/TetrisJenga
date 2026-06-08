using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageMapNodeData
{
    /// <summary>
    /// 스플라인 상의 위치. 0 = 시작점(맵 하단), 1 = 끝점(맵 상단).
    /// </summary>
    [Range(0f, 1f)]
    public float splineT;
    public StageInfoSO stageInfo;
}

/// <summary>
/// 스테이지 맵의 노드 배치 데이터.
/// SplineContainer 좌표계: X/Y 모두 0~1 정규화 좌표.
/// (0,0) = 맵 좌하단, (1,1) = 맵 우상단.
/// </summary>
[CreateAssetMenu(fileName = "StageMapData", menuName = "Stage/Stage Map Data")]
public class StageMapSO : ScriptableObject
{
    public List<StageMapNodeData> nodes = new();

    [Header("노드 버튼 스프라이트")]
    public Sprite nodeNormal;
    public Sprite nodeLocked;
    public Sprite nodeCleared;
}
