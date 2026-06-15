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
    [Tooltip("'프리셋 가이드 창 여부' 라벨 박스 배경")]
    public Sprite presetGuideLabel;
    [Tooltip("토글 ON 상태 체크 박스")]
    public Sprite toggleOn;
    [Tooltip("토글 OFF 상태 체크 박스")]
    public Sprite toggleOff;

    [Header("BGM 슬라이더")]
    [Tooltip("'BGM' 라벨 박스 배경")]
    public Sprite bgmLabel;
    [Tooltip("BGM 슬라이더 트랙(배경)")]
    public Sprite bgmSliderTrack;
    [Tooltip("BGM 슬라이더 핸들(드래그 손잡이)")]
    public Sprite bgmSliderHandle;

    [Header("SFX 슬라이더")]
    [Tooltip("'SFX' 라벨 박스 배경")]
    public Sprite sfxLabel;
    [Tooltip("SFX 슬라이더 트랙(배경)")]
    public Sprite sfxSliderTrack;
    [Tooltip("SFX 슬라이더 핸들(드래그 손잡이)")]
    public Sprite sfxSliderHandle;

    [Header("버튼")]
    [Tooltip("Restart 버튼")]
    public Sprite restartButton;
    [Tooltip("Back 버튼")]
    public Sprite backButton;
}
