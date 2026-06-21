using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Dialogue", menuName = "Scriptable Objects/Dialogue")]
public class Dialogue : ScriptableObject
{
    public Sprite Background;
    public List<Character> Characters = new();
    public List<DialogueLine> Lines = new();
}

[Serializable]
public class DialogueLine
{
    public int CharacterIndex;
    public string Sentence;
    [Tooltip("라인 표시 시 재생할 효과음. 비워두면 재생 안 함.")]
    public AudioClip SoundEffect;

    public DialogueLine(int characterIndex, string sentence)
    {
        CharacterIndex = characterIndex;
        Sentence = sentence;
    }
}

[Serializable]
public class Character
{
    public string Name;
    public Sprite Sprite;
}
