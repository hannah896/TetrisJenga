using UnityEditor;
using UnityEngine;

public static class GameManagerTools
{
    [MenuItem("Tools/Game/Clear Save Data")]
    private static void ClearSaveData()
    {
        GameManager.Instance.ResetAll();
        Debug.Log("[GameManagerTools] 저장 데이터 초기화 완료");
    }
}
