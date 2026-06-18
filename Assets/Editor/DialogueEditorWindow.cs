using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DialogueEditorWindow : EditorWindow
{
    private Dialogue _target;
    private int _selectedCharIndex = -1;
    private string _sentence = "";
    private string _newCharName = "";
    private Sprite _newCharSprite;
    private Vector2 _scrollChars;
    private Vector2 _scrollLines;

    [MenuItem("Tools/Story/Dialogue Editor")]
    static void Open()
    {
        var w = GetWindow<DialogueEditorWindow>("다이얼로그 편집기");
        w.minSize = new Vector2(500, 620);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("다이얼로그 편집기", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        _target = (Dialogue)EditorGUILayout.ObjectField("Dialogue SO", _target, typeof(Dialogue), false);

        if (_target == null)
        {
            EditorGUILayout.HelpBox("Dialogue SO를 선택하거나 Project 뷰에서 드래그하세요.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(10);
        DrawCharacterSection();
        EditorGUILayout.Space(10);
        DrawSentenceSection();
        EditorGUILayout.Space(10);
        DrawLinesSection();
    }

    void DrawCharacterSection()
    {
        EditorGUILayout.LabelField("캐릭터", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _newCharName = EditorGUILayout.TextField("이름", _newCharName);
            _newCharSprite = (Sprite)EditorGUILayout.ObjectField(_newCharSprite, typeof(Sprite), false, GUILayout.Width(64), GUILayout.Height(20));

            using (new EditorGUI.DisabledGroupScope(string.IsNullOrWhiteSpace(_newCharName)))
            {
                if (GUILayout.Button("+ 추가", GUILayout.Width(54)))
                {
                    Undo.RecordObject(_target, "Add Character");
                    _target.Characters.Add(new Character { Name = _newCharName.Trim(), Sprite = _newCharSprite });
                    EditorUtility.SetDirty(_target);
                    _newCharName = "";
                    _newCharSprite = null;
                    GUI.FocusControl(null);
                }
            }
        }

        EditorGUILayout.Space(6);

        if (_target.Characters.Count == 0)
        {
            EditorGUILayout.HelpBox("캐릭터를 추가해주세요.", MessageType.None);
            return;
        }

        _scrollChars = EditorGUILayout.BeginScrollView(_scrollChars, GUILayout.Height(110));
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < _target.Characters.Count; i++)
            {
                DrawCharacterToggle(i);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawCharacterToggle(int index)
    {
        var ch = _target.Characters[index];
        bool isSelected = _selectedCharIndex == index;

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = isSelected ? new Color(0.3f, 0.65f, 1f) : new Color(0.85f, 0.85f, 0.85f);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(84)))
        {
            var tex = ch.Sprite != null ? AssetPreview.GetAssetPreview(ch.Sprite) : null;
            if (tex != null)
                GUILayout.Label(tex, GUILayout.Width(72), GUILayout.Height(72));
            else
                GUILayout.Box("No Img", GUILayout.Width(72), GUILayout.Height(72));

            if (GUILayout.Button(ch.Name, GUILayout.Width(80)))
                _selectedCharIndex = isSelected ? -1 : index;
        }

        GUI.backgroundColor = prevBg;
    }

    void DrawSentenceSection()
    {
        EditorGUILayout.LabelField("대사 입력", EditorStyles.boldLabel);

        string selectedLabel = _selectedCharIndex >= 0 && _selectedCharIndex < _target.Characters.Count
            ? $"선택된 캐릭터: {_target.Characters[_selectedCharIndex].Name}"
            : "선택된 캐릭터: 없음 (위에서 캐릭터를 클릭하세요)";

        EditorGUILayout.LabelField(selectedLabel, EditorStyles.miniLabel);

        _sentence = EditorGUILayout.TextArea(_sentence, GUILayout.Height(56));

        bool canAdd = _selectedCharIndex >= 0
            && _selectedCharIndex < _target.Characters.Count
            && !string.IsNullOrWhiteSpace(_sentence);

        using (new EditorGUI.DisabledGroupScope(!canAdd))
        {
            if (GUILayout.Button("대사 추가", GUILayout.Height(30)))
            {
                Undo.RecordObject(_target, "Add Dialogue Line");
                _target.Lines.Add(new DialogueLine(_selectedCharIndex, _sentence.Trim()));
                EditorUtility.SetDirty(_target);
                _sentence = "";
                GUI.FocusControl(null);
            }
        }
    }

    void DrawLinesSection()
    {
        EditorGUILayout.LabelField($"대사 목록 ({_target.Lines.Count})", EditorStyles.boldLabel);

        if (_target.Lines.Count == 0)
        {
            EditorGUILayout.HelpBox("대사가 없습니다.", MessageType.None);
            return;
        }

        _scrollLines = EditorGUILayout.BeginScrollView(_scrollLines);

        int toRemove = -1;
        int swapWith = -1;

        for (int i = 0; i < _target.Lines.Count; i++)
        {
            var line = _target.Lines[i];
            string charName = line.CharacterIndex >= 0 && line.CharacterIndex < _target.Characters.Count
                ? _target.Characters[line.CharacterIndex].Name
                : "?";

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{i + 1}", GUILayout.Width(22));
                EditorGUILayout.LabelField(charName, EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField(line.Sentence, EditorStyles.wordWrappedLabel);

                using (new EditorGUI.DisabledGroupScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(22))) swapWith = i - 1;

                using (new EditorGUI.DisabledGroupScope(i == _target.Lines.Count - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(22))) swapWith = i + 1 + 10000;

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
                if (GUILayout.Button("×", GUILayout.Width(22))) toRemove = i;
                GUI.backgroundColor = prevBg;
            }
        }

        EditorGUILayout.EndScrollView();

        if (toRemove >= 0)
        {
            Undo.RecordObject(_target, "Remove Dialogue Line");
            _target.Lines.RemoveAt(toRemove);
            EditorUtility.SetDirty(_target);
        }
        else if (swapWith >= 0)
        {
            int a = swapWith >= 10000 ? swapWith - 10001 : swapWith;
            int b = swapWith >= 10000 ? swapWith - 10000 : swapWith + 1;
            Undo.RecordObject(_target, "Move Dialogue Line");
            (_target.Lines[a], _target.Lines[b]) = (_target.Lines[b], _target.Lines[a]);
            EditorUtility.SetDirty(_target);
        }
    }
}
