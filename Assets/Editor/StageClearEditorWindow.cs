using System.IO;
using UnityEditor;
using UnityEngine;

public class StageClearEditorWindow : EditorWindow
{
    int _targetStage = 1;

    [MenuItem("Tools/Game/Stage Clear Tool")]
    static void Open()
    {
        var w = GetWindow<StageClearEditorWindow>("스테이지 클리어 도구");
        w.minSize = new Vector2(280, 260);
    }

    void OnGUI()
    {
        int total = GameManager.Instance.StageCount;

        EditorGUILayout.LabelField("스테이지 클리어 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        // 현재 클리어 상태
        EditorGUILayout.LabelField("현재 상태", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        var prevColor = GUI.color;
        for (int i = 0; i < total; i++)
        {
            bool cleared = GameManager.Instance.IsCleared(i);
            GUI.color = cleared ? new Color(0.4f, 1f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
            EditorGUILayout.LabelField($"Stage {i + 1}", cleared ? "● 클리어" : "○ 미클리어");
        }
        GUI.color = prevColor;
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(12);

        _targetStage = EditorGUILayout.IntSlider("이 단계까지 클리어", _targetStage, 1, total);

        EditorGUILayout.Space(6);

        if (GUILayout.Button($"Stage {_targetStage}까지 클리어 처리", GUILayout.Height(32)))
        {
            ApplyClear(_targetStage - 1); // 0-based 변환
            Repaint();
        }

        EditorGUILayout.Space(8);

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("전체 초기화"))
        {
            if (EditorUtility.DisplayDialog("초기화 확인", "모든 진행 데이터를 초기화합니까?", "초기화", "취소"))
            {
                GameManager.Instance.ResetAll();
                Repaint();
            }
        }
        GUI.backgroundColor = prevBg;
    }

    static void ApplyClear(int maxIndex)
    {
        for (int i = 0; i <= maxIndex; i++)
            GameManager.Instance.RecordStageResult(i, 1f, isClear: true);

        Debug.Log($"[StageClearTool] Stage 1 ~ {maxIndex + 1} 클리어 처리 완료");
    }
}
