using System.IO;
using UnityEditor;
using UnityEngine;

public static class GameManagerTools
{
    [MenuItem("Tools/Game/Clear Save Data")]
    private static void ClearSaveData()
    {
        string path = Path.Combine(
            Application.persistentDataPath,
            "StageData.json"
        );

        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[GameManagerTools] JSON 삭제 완료: {path}");
        }
        else
        {
            Debug.Log("[GameManagerTools] 삭제할 JSON 파일이 없습니다.");
        }

        // StageProgress는 PlayerPrefs 기반 → 스테이지 수만큼 키를 정확히 지운다
        StageProgress.ResetAll(GameManager.Instance.StageCount);
        Debug.Log("[GameManagerTools] StageProgress PlayerPrefs 초기화 완료");
    }
}