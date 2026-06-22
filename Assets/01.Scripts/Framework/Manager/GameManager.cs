using System;
using System.Collections.Generic;
using System.IO;
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

/// <summary>
/// 게임 상태(현재 스테이지) 및 스테이지 클리어 데이터/점수 저장을 책임진다.
/// BlockTower(게임 규칙)에서 클리어/게임오버 시 결과만 넘겨받아 기록한다.
/// 인게임 BlockTower는 DI 밖의 씬 MonoBehaviour라 정적 싱글톤으로 접근한다.
/// </summary>
public class GameManager
{
    private const int DefaultStageCount = 6;
    public const int EndlessStageIndex = 6;

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

    private string Path =>
        System.IO.Path.Combine(Application.persistentDataPath, "StageData.json");

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
    /// 클리어 시 StageProgress(맵 잠금해제용)도 함께 갱신한다.
    /// </summary>
    public void RecordStageResult(int stageNumber, float score, bool isClear)
    {
        SetStageData(stageNumber, score, isClear);
        SaveData();

        if (isClear)
            StageProgress.SetCleared(stageNumber);
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

    private void SaveData()
    {
        var wrapper = new SaveDataWrapper
        {
            StageDatas = stageSaveData
        };

        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(Path, json);
    }

    private void LoadData()
    {
        if (!File.Exists(Path))
            return;

        string json = File.ReadAllText(Path);
        var wrapper = JsonUtility.FromJson<SaveDataWrapper>(json);
        if (wrapper?.StageDatas == null)
            return;

        // 저장된 길이가 다를 수 있으니 현재 stageCount에 맞춰 병합한다.
        for (int i = 0; i < stageCount && i < wrapper.StageDatas.Length; i++)
        {
            stageSaveData[i] = wrapper.StageDatas[i];
            stageSaveData[i].Scores ??= new List<float>();
        }
    }
}
