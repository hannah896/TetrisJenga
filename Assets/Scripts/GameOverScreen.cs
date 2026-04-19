using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class GameOverScreen : MonoBehaviour
{
    TextMeshProUGUI _scoreText;
    System.Action   _onRestart;

    void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        gameObject.AddComponent<GraphicRaycaster>();

        var bg    = new GameObject("BG");
        bg.transform.SetParent(transform, false);
        var img   = bg.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.75f);
        var bgR   = bg.GetComponent<RectTransform>();
        bgR.anchorMin = Vector2.zero;
        bgR.anchorMax = Vector2.one;
        bgR.sizeDelta = Vector2.zero;

        CreateLabel("GAME OVER",       new Vector2(0,  90), 64, Color.white);
        _scoreText = CreateLabel("SCORE: 0", new Vector2(0,  10), 42, new Color(1f, 0.85f, 0.1f));
        CreateLabel("Click to Restart", new Vector2(0, -70), 26, new Color(0.75f, 0.75f, 0.75f));

        gameObject.SetActive(false);
    }

    TextMeshProUGUI CreateLabel(string text, Vector2 pos, float size, Color color)
    {
        var go  = new GameObject(text);
        go.transform.SetParent(transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin        = new Vector2(0.5f, 0.5f);
        r.anchorMax        = new Vector2(0.5f, 0.5f);
        r.pivot            = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta        = new Vector2(700, 90);
        return tmp;
    }

    public void Show(int score, System.Action onRestart)
    {
        _onRestart = onRestart;
        if (_scoreText != null) _scoreText.text = $"SCORE: {score}";
        gameObject.SetActive(true);
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            _onRestart?.Invoke();
    }
}
