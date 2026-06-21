using UnityEngine;

public sealed class LevelBackgroundController : MonoBehaviour
{
    [SerializeField] UnderwaterPostProcessingController _background;
    [SerializeField] bool applyLevelVisualAtRuntime;

    [SerializeField] LevelVisualSO _level1Visual;
    [SerializeField] LevelVisualSO _level2Visual;
    [SerializeField] LevelVisualSO _level3Visual;
    [SerializeField] LevelVisualSO _level4Visual;
    [SerializeField] LevelVisualSO _level5Visual;
    [SerializeField] LevelVisualSO _level6Visual;
    [SerializeField] LevelVisualSO _endlessVisual;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_background == null)
            _background = FindFirstObjectByType<UnderwaterPostProcessingController>();
    }
#endif

    void Start()
    {
        if (_background == null)
            _background = FindFirstObjectByType<UnderwaterPostProcessingController>();

        ApplyCurrentLevel();
    }

    void ApplyCurrentLevel()
    {
        if (_background == null || !applyLevelVisualAtRuntime) return;

        int idx = GameManager.Instance?.CurrentStageIndex ?? -1;
        var visual = idx switch
        {
            0 => _level1Visual,
            1 => _level2Visual,
            2 => _level3Visual,
            3 => _level4Visual,
            4 => _level5Visual,
            5 => _level6Visual,
            _ => _endlessVisual,
        };

        _background.ApplyLevelVisual(visual);
    }
}
