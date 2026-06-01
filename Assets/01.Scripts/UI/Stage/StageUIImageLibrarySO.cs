using UnityEngine;

/// <summary>
/// StageScene(스테이지 선택/미리보기)에서 사용하는 스프라이트 모음.
/// 인스펙터의 각 슬롯에 스프라이트를 끼우면 해당 UI 요소 배경으로 적용된다.
/// 비어 있는 슬롯은 USS 기본 색으로 표시된다.
/// </summary>
[CreateAssetMenu(fileName = "StageUIImageLibrary", menuName = "UI/Image Library/Stage")]
public class StageUIImageLibrarySO : ScriptableObject
{
    [Header("기본")]
    [Tooltip("스테이지 화면 전체 배경")]
    public Sprite background;
    [Tooltip("Stage Scene 타이틀 박스")]
    public Sprite title;

    [Header("뷰 / 프리뷰")]
    [Tooltip("스테이지 뷰 영역 배경 (실제로는 RenderTexture로 대체될 수 있음)")]
    public Sprite stageView;
    [Tooltip("스테이지 미리보기 이미지")]
    public Sprite previewImage;

    [Header("설명 / 버튼")]
    [Tooltip("난이도·부가설명 박스 배경")]
    public Sprite explainBox;
    [Tooltip("플레이 스타트 버튼")]
    public Sprite startButton;
}
