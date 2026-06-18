using UnityEngine;

[CreateAssetMenu(fileName = "LevelVisual", menuName = "Level/Level Visual")]
public sealed class LevelVisualSO : ScriptableObject
{
    [Header("태양광")]
    public Color sunLightColor          = new(0.62f, 0.95f, 1f,  1f);
    [Min(0f)] public float sunLightIntensity  = 2.2f;
    public Vector3 sunLightEulerAngles  = new(58f, -22f, 0f);
    [Min(0f)] public float sunLightPulseAmount = 0.18f;
    [Min(0f)] public float sunLightPulseSpeed  = 0.65f;

    [Header("틴트 라이트 (Center Beam)")]
    public Color tintLightColor         = new(0.34f, 0.86f, 1f,  1f);
    [Min(0f)] public float tintLightIntensity = 3.2f;
    public Color beamColor              = new(0.82f, 0.96f, 1f,  1f);
    [Range(0f, 1f)] public float beamOpacity = 0.72f;

    [Header("거리 대비")]
    public float nearContrast = 30f;
    public float farContrast  = -100f;
}
