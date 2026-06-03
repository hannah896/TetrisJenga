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

    [Header("스크롤 맵")]
    [Tooltip("스크롤 맵 내부 배경 이미지 (화면 2~3배 높이 이미지 권장)")]
    public Sprite mapBackground;

    [Header("우측 패널")]
    [Tooltip("스테이지 미리보기 이미지 (선택 전 기본)")]
    public Sprite previewImage;

    [Header("설명 / 버튼")]
    [Tooltip("난이도·부가설명 박스 배경")]
    public Sprite explainBox;
    [Tooltip("플레이 스타트 버튼")]
    public Sprite startButton;
}
