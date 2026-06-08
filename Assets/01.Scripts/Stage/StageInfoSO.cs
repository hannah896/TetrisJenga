using UnityEngine;

[CreateAssetMenu(fileName = "StageInfo", menuName = "Stage/Stage Info")]
public class StageInfoSO : ScriptableObject
{
    [SerializeField, Min(1)] private int stageNumber = 1;
    [SerializeField] private string stageName = "Stage 1";
    [SerializeField] private Sprite previewImage;
    [SerializeField, TextArea(3, 8)] private string description;
    [SerializeField, Min(0)] private int targetScore;
    [SerializeField, Tooltip("플레이할 씬 이름. 비우면 스테이지 순서대로 Level1, Level2... 가 자동 사용된다.")]
    private string sceneName;

    public int StageNumber => stageNumber;
    public string StageName => stageName;
    public Sprite PreviewImage => previewImage;
    public string Description => description;
    public int TargetScore => targetScore;
    public string SceneName => sceneName;
}
