using System;
using System.Collections;
using JSAM;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class DialogueUIController : MonoBehaviour
{
    [Tooltip("Stage1~6 순서대로 할당. GameManager.CurrentStageIndex로 자동 선택됨.")]
    [SerializeField] Dialogue[] stageDialogues;
    [SerializeField, Range(0.01f, 0.1f)] float typeInterval = 0.04f;

    UIDocument _doc;
    VisualElement _background;
    VisualElement _portraitArea;
    VisualElement _portraitImage;
    Label _nameLabel;
    VisualElement _dialogueBox;
    Label _dialogueText;
    Button _nextButton;
    Button _skipButton;

    Dialogue _dialogue;
    int _currentLineIndex;
    bool _isTyping;
    string _fullText;
    Coroutine _typingCoroutine;

    public event Action OnDialogueFinished;

    void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    void OnEnable()
    {
        var root = _doc.rootVisualElement;
        _background   = root.Q("dialogue-bg");
        _portraitArea = root.Q("portrait-area");
        _portraitImage = root.Q("portrait-image");
        _nameLabel    = root.Q<Label>("name-label");
        _dialogueBox  = root.Q("dialogue-box");
        _dialogueText = root.Q<Label>("dialogue-text");
        _nextButton   = root.Q<Button>("next-button");
        _skipButton   = root.Q<Button>("skip-button");

        _skipButton.clicked += OnSkipClicked;

        // 화면 어디를 눌러도 Advance 처리 (skip-button 제외)
        root.RegisterCallback<ClickEvent>(OnScreenClicked);
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
        if (_skipButton != null) _skipButton.clicked -= OnSkipClicked;
        _doc?.rootVisualElement?.UnregisterCallback<ClickEvent>(OnScreenClicked);
    }

    public void StartDialogue(Dialogue dlg)
    {
        _dialogue = dlg;
        _currentLineIndex = 0;

        if (_background != null)
        {
            _background.style.backgroundImage = dlg.Background != null
                ? new StyleBackground(dlg.Background)
                : new StyleBackground(StyleKeyword.None);
        }

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

            _portraitImage.style.backgroundImage = hasPortrait
                ? new StyleBackground(character.Sprite)
                : new StyleBackground(StyleKeyword.None);
        }

        _portraitArea.style.display = hasPortrait ? DisplayStyle.Flex : DisplayStyle.None;
        _dialogueBox.EnableInClassList("dialogue-box--no-portrait", !hasPortrait);
        _dialogueText.EnableInClassList("dialogue-text--narration", isNarration);

        if (line.HasSoundEffect)
            AudioPlayback.PlaySound(line.SoundEffect);

        BeginTyping(line.Sentence);
    }

    void BeginTyping(string text)
    {
        _fullText = text;
        if (_typingCoroutine != null)
            StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypeRoutine(text));
    }

    IEnumerator TypeRoutine(string text)
    {
        _isTyping = true;
        _dialogueText.text = "";
        foreach (char c in text)
        {
            _dialogueText.text += c;
            yield return new WaitForSeconds(typeInterval);
        }
        _isTyping = false;
        _typingCoroutine = null;
    }

    void CompleteTyping()
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }
        _isTyping = false;
        _dialogueText.text = _fullText;
    }

    // 화면 클릭: 타이핑 중이면 즉시 완성, 완성 상태면 다음 라인
    void OnScreenClicked(ClickEvent e)
    {
        // skip-button 영역 클릭은 무시
        var target = e.target as VisualElement;
        while (target != null)
        {
            if (target == _skipButton) return;
            target = target.parent;
        }

        if (_isTyping)
        {
            CompleteTyping();
        }
        else
        {
            _currentLineIndex++;
            ShowCurrentLine();
        }
    }

    void OnSkipClicked()
    {
        CompleteTyping();
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
