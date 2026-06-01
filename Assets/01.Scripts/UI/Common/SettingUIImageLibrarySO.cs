using UnityEngine;

/// <summary>
/// 설정 팝업(모든 씬 공통)에 사용하는 스프라이트 모음.
/// 그래픽 팀원이 인스펙터에서 각 슬롯에 스프라이트를 끼우면 바로 반영된다.
/// 비어 있는 슬롯은 USS 기본 색으로 표시된다.
/// </summary>
[CreateAssetMenu(fileName = "SettingUIImageLibrary", menuName = "UI/Image Library/Setting")]
public class SettingUIImageLibrarySO : ScriptableObject
{
    [Header("배경")]
    [Tooltip("설정 팝업 패널 배경")]
    public Sprite panel;

    [Header("프리셋 가이드 토글")]
    [Tooltip("토글 ON 상태 체크 박스")]
    public Sprite toggleOn;
    [Tooltip("토글 OFF 상태 체크 박스")]
    public Sprite toggleOff;

    [Header("슬라이더")]
    [Tooltip("슬라이더 트랙(배경)")]
    public Sprite sliderTrack;
    [Tooltip("슬라이더 핸들(드래그 손잡이)")]
    public Sprite sliderHandle;

    [Header("버튼")]
    [Tooltip("Restart 버튼")]
    public Sprite restartButton;
    [Tooltip("Back 버튼")]
    public Sprite backButton;
}
