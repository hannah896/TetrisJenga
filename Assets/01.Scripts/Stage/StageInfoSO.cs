using UnityEngine;

[CreateAssetMenu(fileName = "StageInfo", menuName = "Stage/Stage Info")]
public class StageInfoSO : ScriptableObject
{
    [SerializeField, Min(1)] private int stageNumber = 1;
    [SerializeField] private string stageName = "Stage 1";
    [SerializeField] private Sprite previewImage;
    [SerializeField, TextArea(3, 8)] private string description;
    [SerializeField, Min(0)] private int targetScore;

    public int StageNumber => stageNumber;
    public string StageName => stageName;
    public Sprite PreviewImage => previewImage;
    public string Description => description;
    public int TargetScore => targetScore;
}
