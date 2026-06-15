using UnityEngine;

/// <summary>
/// 인게임 HUD(RuntimeHud.uxml + BlockTower)에서 사용하는 스프라이트 모음.
/// 인스펙터의 각 슬롯에 스프라이트를 끼우면 해당 HUD 요소 배경으로 적용된다.
/// 비어 있는 슬롯은 UXML 인라인 기본 색이 그대로 유지된다(그래픽 작업 전 fallback).
/// </summary>
[CreateAssetMenu(fileName = "RuntimeHudImageLibrary", menuName = "UI/Image Library/RuntimeHud")]
public class RuntimeHudImageLibrarySO : ScriptableObject
{
    [Header("점수 HUD")]
    [Tooltip("현재 점수 패널 배경 (HudScorePanel)")]
    public Sprite scorePanel;
    [Tooltip("목표 점수 패널 배경 (HudTargetScorePanel)")]
    public Sprite targetScorePanel;

    [Header("보너스 프리뷰")]
    [Tooltip("보너스 프리뷰 전체 패널 배경 (BonusPreview)")]
    public Sprite bonusPreviewPanel;
    [Tooltip("보너스 프리뷰 셀 영역 배경 (BonusPreview1/2/3Cells 공통)")]
    public Sprite bonusPreviewItem;
    [Tooltip("보너스 키 뱃지 배경 (BonusPreview1/2/3Key 공통)")]
    public Sprite bonusKeyBadge;

    [Header("기타 HUD 패널")]
    [Tooltip("블록 무게 가이드 패널 배경 (BlockWeightGuide)")]
    public Sprite weightGuidePanel;
    [Tooltip("서브 카메라 프리뷰 패널 배경 (SubCameraPreviewPanel)")]
    public Sprite subCameraPreviewPanel;
}
