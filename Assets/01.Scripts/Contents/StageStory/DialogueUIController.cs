using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class DialogueUIController : MonoBehaviour
{
    [Tooltip("Stage1~6 순서대로 할당. GameManager.CurrentStageIndex로 자동 선택됨.")]
    [SerializeField] Dialogue[] stageDialogues;

    UIDocument _doc;
    VisualElement _portraitArea;
    VisualElement _portraitImage;
    Label _nameLabel;
    VisualElement _dialogueBox;
    Label _dialogueText;
    Button _nextButton;
    Button _skipButton;

    Dialogue _dialogue;
    int _currentLineIndex;

    public event Action OnDialogueFinished;

    void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        var root = _doc.rootVisualElement;
        _portraitArea = root.Q("portrait-area");
        _portraitImage = root.Q("portrait-image");
        _nameLabel = root.Q<Label>("name-label");
        _dialogueBox = root.Q("dialogue-box");
        _dialogueText = root.Q<Label>("dialogue-text");
        _nextButton = root.Q<Button>("next-button");
        _skipButton = root.Q<Button>("skip-button");

        _nextButton.clicked += OnNextClicked;
        _skipButton.clicked += OnSkipClicked;
    }

    void Start()
    {
        var idx = GameManager.Instance.CurrentStageIndex;
        Dialogue toPlay = null;
        if (stageDialogues != null && idx >= 0 && idx < stageDialogues.Length)
            toPlay = stageDialogues[idx];

        if (toPlay != null)
            StartDialogue(toPlay);
    }

    void OnDisable()
    {
        if (_nextButton != null) _nextButton.clicked -= OnNextClicked;
        if (_skipButton != null) _skipButton.clicked -= OnSkipClicked;
    }

    public void StartDialogue(Dialogue dlg)
    {
        _dialogue = dlg;
        _currentLineIndex = 0;
        ShowCurrentLine();
    }

    void ShowCurrentLine()
    {
        if (_dialogue == null || _currentLineIndex >= _dialogue.Lines.Count)
        {
            Finish();
            return;
        }

        var line = _dialogue.Lines[_currentLineIndex];
        var hasPortrait = false;
        var isNarration = true;

        if (line.CharacterIndex >= 0 && line.CharacterIndex < _dialogue.Characters.Count)
        {
            var character = _dialogue.Characters[line.CharacterIndex];
            hasPortrait = character.Sprite != null;
            isNarration = !hasPortrait;

            _nameLabel.text = character.Name;
            _nameLabel.style.display = hasPortrait ? DisplayStyle.Flex : DisplayStyle.None;

            if (hasPortrait)
                _portraitImage.style.backgroundImage = new StyleBackground(character.Sprite);
            else
                _portraitImage.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        }

        _portraitArea.style.display = hasPortrait ? DisplayStyle.Flex : DisplayStyle.None;
        _dialogueBox.EnableInClassList("dialogue-box--no-portrait", !hasPortrait);
        _dialogueText.EnableInClassList("dialogue-text--narration", isNarration);

        _dialogueText.text = line.Sentence;
    }

    void OnNextClicked()
    {
        _currentLineIndex++;
        ShowCurrentLine();
    }

    void OnSkipClicked()
    {
        Finish();
    }

    void Finish()
    {
        OnDialogueFinished?.Invoke();

        var nextScene = GameManager.Instance.PendingLevelScene;
        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
    }
}
