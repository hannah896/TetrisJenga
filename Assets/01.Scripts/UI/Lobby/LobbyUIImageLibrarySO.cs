using UnityEngine;

/// <summary>
/// LobbyScene(로비 + 모드선택)에서 사용하는 스프라이트 모음.
/// 인스펙터의 각 슬롯에 스프라이트를 끼우면 해당 UI 요소 배경으로 적용된다.
/// 비어 있는 슬롯은 USS 기본 색으로 표시된다.
/// </summary>
[CreateAssetMenu(fileName = "LobbyUIImageLibrary", menuName = "UI/Image Library/Lobby")]
public class LobbyUIImageLibrarySO : ScriptableObject
{
    [Header("로비 화면")]
    [Tooltip("로비 전체 배경")]
    public Sprite lobbyBackground;
    [Tooltip("Abyss Stack 타이틀 로고 (있으면 텍스트 대신 이미지로)")]
    public Sprite titleLogo;
    [Tooltip("플레이 버튼")]
    public Sprite playButton;
    [Tooltip("Exit 버튼")]
    public Sprite exitButton;

    [Header("모드 선택 화면")]
    [Tooltip("모드 선택 전체 배경")]
    public Sprite mainMenuBackground;
    [Tooltip("Main Menu 타이틀 박스")]
    public Sprite mainTitle;
    [Tooltip("Endless Mode 버튼")]
    public Sprite endlessButton;
    [Tooltip("Story 버튼")]
    public Sprite storyButton;
    [Tooltip("Multi(봇전) 버튼")]
    public Sprite multiButton;

    [Header("모드 선택 - 프리뷰 패널")]
    [Tooltip("모드 미리보기 이미지")]
    public Sprite previewImage;
    [Tooltip("모드 설명 박스 배경")]
    public Sprite modeExplainBox;
}
