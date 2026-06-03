using UnityEngine;

/// <summary>
/// PlayerPrefs 기반 스테이지 진행 상태 관리.
/// index는 0-based (mapData.nodes 인덱스와 동일).
/// </summary>
public static class StageProgress
{
    private const string Prefix = "stage_cleared_";

    public static bool IsCleared(int index) =>
        PlayerPrefs.GetInt(Prefix + index, 0) == 1;

    public static void SetCleared(int index)
    {
        PlayerPrefs.SetInt(Prefix + index, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 현재 진행 가능한 스테이지의 0-based 인덱스.
    /// 모두 클리어했으면 마지막 인덱스를 반환.
    /// </summary>
    public static int GetHighestUnlockedIndex(int totalCount)
    {
        for (int i = 0; i < totalCount; i++)
        {
            if (!IsCleared(i)) return i;
        }
        return Mathf.Max(0, totalCount - 1);
    }

    /// <summary>개발 편의용 — 모든 진행 데이터 초기화.</summary>
    public static void ResetAll(int totalCount)
    {
        for (int i = 0; i < totalCount; i++)
            PlayerPrefs.DeleteKey(Prefix + i);
        PlayerPrefs.Save();
    }
}
