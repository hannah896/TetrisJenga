using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverScreen : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI currentScoreText;
    [SerializeField] TextMeshProUGUI targetScoreText;
    [SerializeField] TextMeshProUGUI restartHintText;
    [SerializeField] Button restartButton;

    System.Action _onRestart;

    void Awake()
    {
        BindLabels();
    }

    void BindLabels()
    {
        titleText ??= FindText("Title");
        currentScoreText ??= FindText("CurrentScore");
        targetScoreText ??= FindText("TargetScore");
        restartHintText ??= FindText("RestartHint");
        restartButton ??= FindButton("RestartButton");
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(Restart);
            restartButton.onClick.AddListener(Restart);
        }
    }

    TextMeshProUGUI FindText(string objectName)
    {
        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var text in texts)
            if (text != null && text.name == objectName)
                return text;
        return null;
    }

    Button FindButton(string objectName)
    {
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
            if (button != null && button.name == objectName)
                return button;
        return null;
    }

    public void ShowGameOver(int score, int targetScore, System.Action onRestart)
    {
        Show("GAME OVER", score, targetScore, onRestart);
    }

    public void ShowClear(int score, int targetScore, System.Action onRestart)
    {
        Show("CLEAR", score, targetScore, onRestart);
    }

    void Show(string title, int score, int targetScore, System.Action onRestart)
    {
        BindLabels();
        _onRestart = onRestart;
        if (titleText != null) titleText.text = title;
        if (currentScoreText != null) currentScoreText.text = $"SCORE: {score}";
        if (targetScoreText != null)
        {
            targetScoreText.gameObject.SetActive(targetScore > 0);
            if (targetScore > 0)
                targetScoreText.text = $"TARGET: {targetScore}";
        }
        if (restartHintText != null) restartHintText.text = "Click to Restart";
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _onRestart = null;
    }

    void Restart()
    {
        _onRestart?.Invoke();
    }
}
