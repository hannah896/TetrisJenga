using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// 점수 계산, 표시, 플로팅 텍스트, 클리어/게임오버 판정, 보너스 프리셋 큐를 총관리.
/// BlockTower 게임 로직 정리(PerformGameEndCleanup)는 _tower 공개 API로 위임한다.
/// </summary>
public class ScoreController : MonoBehaviour
{
    [SerializeField] BlockTower _tower;

    [Header("Score")]
    [SerializeField] int targetScore         = 30;
    [SerializeField] int goldFishDeadlineScore = 20;
    [SerializeField] TextMeshPro scoreLabel;

    int  _score;
    bool _isGameOver;

    Coroutine                   _canvasFloatingScoreRoutine;
    TMPro.TextMeshProUGUI       _canvasFloatingScoreText;

    TetrominoPreset _bonusTargetPreset;
    TetrominoPreset _nextBonusTargetPreset;
    TetrominoPreset _thirdBonusTargetPreset;
    bool _bonusQueueInitialized;
    readonly List<TetrominoPreset> _bonusPresetBag = new();
    int  _bonusPresetBagIndex;

    public System.Action<int, int>                                           OnScoreChanged;
    public System.Action<TetrominoPreset, TetrominoPreset, TetrominoPreset> OnBonusRolled;
    public System.Action<int, int>                                           OnGameOver;
    public System.Action<int, int>                                           OnClear;
    public System.Action<int>                                                OnFloatingScore;

    public int  Score               => _score;
    public int  TargetScore         => targetScore;
    public bool IsGameOver          => _isGameOver;
    public TetrominoPreset BonusTargetPreset      => _bonusTargetPreset;
    public TetrominoPreset NextBonusTargetPreset  => _nextBonusTargetPreset;
    public TetrominoPreset ThirdBonusTargetPreset => _thirdBonusTargetPreset;

    private void OnValidate()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
    }

    void Awake()
    {
        if (_tower == null) _tower = GetComponent<BlockTower>();
    }

    // ── 리셋 ─────────────────────────────────────────────────────────────
    public void ResetForRebuild()
    {
        _score                 = 0;
        _isGameOver            = false;
        _bonusQueueInitialized = false;
        ResetBonusPresetBag();
        UpdateScoreDisplay();
    }

    public void SetScoreTo(int score)
    {
        _score = score;
        UpdateScoreDisplay();
    }

    // ── 점수 ─────────────────────────────────────────────────────────────
    public void AddScore(int delta)
    {
        if (_isGameOver) return;
        _score += delta;
        
        if (delta < 0)
        {
            Debug.LogWarning(
                $"Negative Score : {delta}\n{Environment.StackTrace}"
            );
        }
        
        // 0이하로 떨어지지않게 방어 코드
        _score = Math.Max(_score, 0);
        UpdateScoreDisplay();
        if (delta > 0)
            CheckClearCondition();
    }

    public void AddScore(int delta, Vector3 worldPosition)
    {
        AddScore(delta);
        SpawnFloatingScoreText(delta);
    }

    public void AwardGoldFishDeadlineScore(Vector3 worldPosition)
    {
        AddScore(goldFishDeadlineScore, worldPosition);
    }

    public void UpdateScoreDisplay()
    {
        if (scoreLabel != null) scoreLabel.text = $"SCORE\n{_score}";
        OnScoreChanged?.Invoke(_score, targetScore);
    }

    void CheckClearCondition()
    {
        if (_isGameOver) return;
        if (targetScore > 0 && _score >= targetScore)
            TriggerClear();
    }

    // ── 게임 종료 ─────────────────────────────────────────────────────────
    public void TriggerGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        _tower?.PerformGameEndCleanup();
        if (GameManager.Instance != null)
            GameManager.Instance.RecordStageResult(GameManager.Instance.CurrentStageIndex, _score, isClear: false);
        OnGameOver?.Invoke(_score, targetScore);
    }

    public void TriggerClear()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        _tower?.PerformGameEndCleanup();
        if (GameManager.Instance != null)
            GameManager.Instance.RecordStageResult(GameManager.Instance.CurrentStageIndex, _score, isClear: true);
        OnClear?.Invoke(_score, targetScore);
    }

    // ── 플로팅 점수 텍스트 ────────────────────────────────────────────────
    void SpawnFloatingScoreText(int delta)
    {
        if (delta == 0 || !Application.isPlaying) return;
        OnFloatingScore?.Invoke(delta);

        var go = new GameObject("FloatingScoreText");
        if (scoreLabel != null)
        {
            go.transform.SetParent(scoreLabel.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        }
        else
        {
            go.transform.SetParent(transform);
            go.transform.position = transform.position + new Vector3(0f, 2f, 0f);
        }

        var tmp  = go.AddComponent<TextMeshPro>();
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta        = new Vector2(3f, 1.2f);
        tmp.text              = delta > 0 ? $"+{delta}" : delta.ToString();
        tmp.fontSize          = 10f;
        tmp.alignment         = TextAlignmentOptions.Center;
        tmp.color             = delta > 0 ? Color.white : Color.red;
        tmp.fontStyle         = FontStyles.Bold;
        tmp.textWrappingMode  = TextWrappingModes.NoWrap;
        tmp.overflowMode      = TextOverflowModes.Overflow;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.sortingOrder = 30;
        StartCoroutine(AnimateFloatingScoreText(go, tmp));
    }

    IEnumerator AnimateFloatingScoreText(GameObject go, TextMeshPro tmp)
    {
        float duration  = 0.85f;
        float elapsed   = 0f;
        var   startPos  = go.transform.localPosition;
        var   startColor = tmp.color;

        while (elapsed < duration && go != null && tmp != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            go.transform.localPosition = startPos + new Vector3(0f, t * 0.9f, 0f);
            tmp.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    IEnumerator AnimateCanvasFloatingScoreText(int delta)
    {
        float duration = 0.85f;
        float elapsed  = 0f;

        _canvasFloatingScoreText.gameObject.name = "Floating Score";
        _canvasFloatingScoreText.text            = delta > 0 ? $"+{delta}" : delta.ToString();
        _canvasFloatingScoreText.color           = delta > 0 ? Color.white : Color.red;
        _canvasFloatingScoreText.alpha           = 1f;
        _canvasFloatingScoreText.gameObject.SetActive(true);

        var rect     = _canvasFloatingScoreText.rectTransform;
        var startPos = rect.anchoredPosition;
        while (elapsed < duration && _canvasFloatingScoreText != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition         = startPos + new Vector2(0f, t * 30f);
            _canvasFloatingScoreText.alpha = 1f - t;
            yield return null;
        }

        if (_canvasFloatingScoreText != null)
        {
            rect.anchoredPosition         = startPos;
            _canvasFloatingScoreText.text  = string.Empty;
            _canvasFloatingScoreText.alpha = 1f;
            _canvasFloatingScoreText.gameObject.SetActive(false);
        }

        _canvasFloatingScoreRoutine = null;
    }

    // ── 보너스 프리셋 큐 ──────────────────────────────────────────────────
    public void RollBonusTarget()
    {
        if (!_bonusQueueInitialized)
        {
            _bonusTargetPreset      = DrawBonusPreset();
            _nextBonusTargetPreset  = DrawBonusPreset();
            _thirdBonusTargetPreset = DrawBonusPreset();
            _bonusQueueInitialized  = true;
        }
        else
        {
            _bonusTargetPreset      = _nextBonusTargetPreset;
            _nextBonusTargetPreset  = _thirdBonusTargetPreset;
            _thirdBonusTargetPreset = DrawBonusPreset();
        }

        OnBonusRolled?.Invoke(_bonusTargetPreset, _nextBonusTargetPreset, _thirdBonusTargetPreset);
    }

    public void ResetBonusPresetBag()
    {
        _bonusPresetBag.Clear();
        _bonusPresetBagIndex = 0;
    }

    TetrominoPreset DrawBonusPreset()
    {
        if (_bonusPresetBagIndex >= _bonusPresetBag.Count)
            RefillBonusPresetBag();
        return _bonusPresetBag[_bonusPresetBagIndex++];
    }

    void RefillBonusPresetBag()
    {
        _bonusPresetBag.Clear();
        _bonusPresetBag.Add(TetrominoPreset.I);
        _bonusPresetBag.Add(TetrominoPreset.Z);
        _bonusPresetBag.Add(TetrominoPreset.S);
        _bonusPresetBag.Add(TetrominoPreset.O);
        _bonusPresetBag.Add(TetrominoPreset.L);
        _bonusPresetBag.Add(TetrominoPreset.T);
        _bonusPresetBag.Add(TetrominoPreset.J);

        for (int i = _bonusPresetBag.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (_bonusPresetBag[i], _bonusPresetBag[swap]) = (_bonusPresetBag[swap], _bonusPresetBag[i]);
        }

        _bonusPresetBagIndex = 0;
    }
}
