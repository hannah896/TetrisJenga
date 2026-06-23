using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
struct StageSaveData
{
    public List<float> Scores;
    public bool isClear;
}

[Serializable]
class SaveDataWrapper
{
    public StageSaveData[] StageDatas;
}

[Serializable]
class SaveFileWrapper
{
    public string Data;
    public string Hash;
}

/// <summary>
/// 게임 상태(현재 스테이지) 및 스테이지 클리어 데이터/점수 저장을 책임진다.
/// BlockTower(게임 규칙)에서 클리어/게임오버 시 결과만 넘겨받아 기록한다.
/// 인게임 BlockTower는 DI 밖의 씬 MonoBehaviour라 정적 싱글톤으로 접근한다.
/// </summary>
public class GameManager
{
    private const int DefaultStageCount = 6;
    public const int EndlessStageIndex = 6;

    // HMAC 서명용 키 — 바이너리에 내장되어 외부에서 위조 불가
    private const string HmacKey = "TetrisJenga_2026_StageData_Key";

    private static GameManager _instance;
    public static GameManager Instance => _instance ??= new GameManager();

    private readonly int stageCount;
    private StageSaveData[] stageSaveData;

    /// <summary>스테이지 선택 시 설정되어 인게임 씬에서 참조하는 현재 스테이지 인덱스(0-based).</summary>
    public int CurrentStageIndex { get; set; }

    /// <summary>DialogueScene 종료 후 이동할 Level 씬 이름.</summary>
    public string PendingLevelScene { get; set; }

    /// <summary>로비 진입 시 모드 선택 화면을 바로 표시할지 여부.</summary>
    public bool GoToModeSelect { get; set; }

    public int StageCount => stageCount;

    private string SavePath =>
        Path.Combine(Application.persistentDataPath, "StageData.json");
    private string BackupPath =>
        Path.Combine(Application.persistentDataPath, "StageData.backup.json");

    private GameManager()
    {
        stageCount = DefaultStageCount;
        Init();
        LoadData();
    }

    private void Init()
    {
        stageSaveData = new StageSaveData[stageCount + 1]; // +1 for endless (index 6)
        for (int i = 0; i < stageSaveData.Length; i++)
            stageSaveData[i].Scores = new List<float>();
    }

    /// <summary>
    /// 스테이지 결과(점수 + 클리어 여부)를 기록하고 영속화한다.
    /// </summary>
    public void RecordStageResult(int stageNumber, float score, bool isClear)
    {
        SetStageData(stageNumber, score, isClear);
        SaveData();
    }

    /// <summary>
    /// 클리어 데이터 입력 (메모리 갱신만; 영속화는 RecordStageResult/SaveData에서).
    /// </summary>
    public void SetStageData(int stageNumber, float score, bool isClear)
    {
        if (stageSaveData == null)
            Init();
        if (stageNumber < 0 || stageNumber >= stageSaveData.Length)
        {
            Debug.LogWarning($"GameManager: 잘못된 스테이지 인덱스 {stageNumber}");
            return;
        }

        // struct 배열이므로 요소에 직접 접근해 수정한다(복사본 수정 버그 방지).
        stageSaveData[stageNumber].Scores ??= new List<float>();
        stageSaveData[stageNumber].Scores.Add(score);
        // 한 번이라도 클리어했으면 유지.
        stageSaveData[stageNumber].isClear |= isClear;
    }

    public bool IsCleared(int stageNumber)
    {
        if (stageSaveData == null || stageNumber < 0 || stageNumber >= stageSaveData.Length)
            return false;
        return stageSaveData[stageNumber].isClear;
    }

    public float GetBestScore(int stageNumber)
    {
        if (stageSaveData == null || stageNumber < 0 || stageNumber >= stageSaveData.Length)
            return 0f;
        var scores = stageSaveData[stageNumber].Scores;
        if (scores == null || scores.Count == 0)
            return 0f;

        float best = scores[0];
        for (int i = 1; i < scores.Count; i++)
            if (scores[i] > best)
                best = scores[i];
        return best;
    }

    /// <summary>현재 진행 가능한 스테이지의 0-based 인덱스. 모두 클리어했으면 마지막 인덱스를 반환.</summary>
    public int GetHighestUnlockedIndex(int totalCount)
    {
        for (int i = 0; i < totalCount; i++)
        {
            if (!IsCleared(i)) return i;
        }
        return Mathf.Max(0, totalCount - 1);
    }

    public bool IsAllCleared(int totalCount)
    {
        if (totalCount <= 0) return false;
        for (int i = 0; i < totalCount; i++)
            if (!IsCleared(i)) return false;
        return true;
    }

    /// <summary>저장 파일(주+백업)을 삭제하고 메모리를 초기화한다.</summary>
    public void ResetAll()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        if (File.Exists(BackupPath)) File.Delete(BackupPath);
        Init();
    }

    // ── 저장/로드 ──────────────────────────────────────────────

    private void SaveData()
    {
        string dataJson = JsonUtility.ToJson(new SaveDataWrapper { StageDatas = stageSaveData }, true);
        string fileJson = JsonUtility.ToJson(new SaveFileWrapper
        {
            Data = dataJson,
            Hash = ComputeHmac(dataJson)
        }, true);

        File.WriteAllText(SavePath, fileJson);
        File.WriteAllText(BackupPath, fileJson);
    }

    private void LoadData()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            if (TryParse(json, out var data))
            {
                ApplyLoaded(data);
                MigrateFromPlayerPrefs();
                return;
            }
            Debug.LogWarning("[GameManager] 주 저장 파일 손상 → 백업 복구 시도");
        }

        if (File.Exists(BackupPath))
        {
            string json = File.ReadAllText(BackupPath);
            if (TryParse(json, out var data))
            {
                ApplyLoaded(data);
                SaveData(); // 주 파일 복원
                Debug.Log("[GameManager] 백업에서 복구 완료");
                MigrateFromPlayerPrefs();
                return;
            }
            Debug.LogWarning("[GameManager] 백업 파일도 손상 → 초기 상태로 시작");
        }

        MigrateFromPlayerPrefs();
    }

    private bool TryParse(string fileJson, out SaveDataWrapper result)
    {
        result = null;
        try
        {
            var file = JsonUtility.FromJson<SaveFileWrapper>(fileJson);
            if (file == null || string.IsNullOrEmpty(file.Data) || string.IsNullOrEmpty(file.Hash))
                return false;

            if (file.Hash != ComputeHmac(file.Data))
            {
                Debug.LogWarning("[GameManager] HMAC 불일치 — 파일 변조 또는 구버전 데이터");
                return false;
            }

            result = JsonUtility.FromJson<SaveDataWrapper>(file.Data);
            return result?.StageDatas != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameManager] 파싱 실패: {e.Message}");
            return false;
        }
    }

    private void ApplyLoaded(SaveDataWrapper wrapper)
    {
        for (int i = 0; i < stageCount && i < wrapper.StageDatas.Length; i++)
        {
            stageSaveData[i] = wrapper.StageDatas[i];
            stageSaveData[i].Scores ??= new List<float>();
        }
    }

    private void MigrateFromPlayerPrefs()
    {
        const string Prefix = "stage_cleared_";
        bool migrated = false;
        for (int i = 0; i < stageCount; i++)
        {
            if (PlayerPrefs.GetInt(Prefix + i, 0) == 1)
            {
                stageSaveData[i].isClear = true;
                PlayerPrefs.DeleteKey(Prefix + i);
                migrated = true;
            }
        }
        if (migrated)
        {
            PlayerPrefs.Save();
            SaveData();
        }
    }

    // ── HMAC-SHA256 ─────────────────────────────────────────────

    private static string ComputeHmac(string data)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(HmacKey);
        byte[] dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToBase64String(hmac.ComputeHash(dataBytes));
    }
}
