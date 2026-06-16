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
            Debug.Log($"저장 데이터 삭제 완료\n{path}");
        }
        else
        {
            Debug.Log("삭제할 저장 데이터가 없습니다.");
        }

        // 혹시 StageProgress가 PlayerPrefs를 쓴다면 같이 삭제
        // PlayerPrefs.DeleteAll();
        // PlayerPrefs.Save();
    }
}