using UnityEngine;

/// <summary>
/// SampleScene(게임 플레이 HUD + 결과 화면)에서 사용하는 스프라이트 모음.
/// 인스펙터의 각 슬롯에 스프라이트를 끼우면 해당 UI 요소 배경으로 적용된다.
/// 비어 있는 슬롯은 USS 기본 색으로 표시된다.
/// </summary>
[CreateAssetMenu(fileName = "GameplayUIImageLibrary", menuName = "UI/Image Library/Gameplay")]
public class GameplayUIImageLibrarySO : ScriptableObject
{
    [Header("공통")]
    [Tooltip("중앙 게임 보드 프레임 (RenderTexture 테두리)")]
    public Sprite gameViewFrame;

    [Header("싱글 플레이 HUD")]
    [Tooltip("좌측 세로 프리셋 가이드 패널")]
    public Sprite presetGuidePanel;
    [Tooltip("좌하단 프리셋 박스")]
    public Sprite presetGuideBox;
    [Tooltip("NEXT 큐 한 칸 배경")]
    public Sprite nextBox;
    [Tooltip("점수판 배경 (현재/목표)")]
    public Sprite scorePanel;
    [Tooltip("획득 점수(+n) 박스 배경")]
    public Sprite scoreAddBox;

    [Header("멀티 플레이 HUD")]
    [Tooltip("내 플레이어 정보 패널")]
    public Sprite playerInfoPanel;
    [Tooltip("상대(CPU) 정보 패널")]
    public Sprite opponentInfoPanel;

    [Header("결과 화면 (클리어 / 게임오버)")]
    [Tooltip("결과 팝업 패널 배경")]
    public Sprite resultPanel;
    [Tooltip("결과 타이틀 박스 (클리어!/게임오버)")]
    public Sprite resultTitleBox;
    [Tooltip("결과 점수 박스")]
    public Sprite resultScoreBox;
    [Tooltip("결과 버튼 (Stage/Retry/Next 공통)")]
    public Sprite resultButton;
}
