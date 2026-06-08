using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 스테이지 맵의 거품 경로를 그리는 커스텀 VisualElement.
/// StageUIController가 스플라인 샘플 결과를 SetPath()로 넘겨준다.
/// </summary>
public class StageMapElement : VisualElement
{
    private List<Vector2> _densePath = new();
    private int _activeEndIndex = -1;

    public StageMapElement()
    {
        style.position = Position.Absolute;
        style.left = 0;
        style.top = 0;
        style.right = 0;
        style.bottom = 0;
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    /// <summary>
    /// 경로 데이터 설정.
    /// densePath: 스플라인에서 촘촘하게 샘플링한 UI 좌표 목록.
    /// activeEndIndex: densePath 중 활성화(클리어된) 구간의 마지막 인덱스. -1이면 경로 미표시.
    /// </summary>
    public void SetPath(List<Vector2> densePath, int activeEndIndex)
    {
        _densePath = densePath;
        _activeEndIndex = activeEndIndex;
        MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext ctx)
    {
        if (_densePath == null || _densePath.Count < 2) return;

        var painter = ctx.painter2D;
        const float dotRadius = 7f;
        const float dotSpacing = 22f;

        float traveled = 0f;
        float nextDot = 0f;

        for (int i = 1; i < _densePath.Count; i++)
        {
            var a = _densePath[i - 1];
            var b = _densePath[i];
            float segLen = Vector2.Distance(a, b);
            bool isActive = _activeEndIndex >= 0 && i <= _activeEndIndex;

            while (traveled + segLen >= nextDot)
            {
                float t = segLen > 0f ? (nextDot - traveled) / segLen : 0f;
                var dot = Vector2.Lerp(a, b, Mathf.Clamp01(t));

                painter.fillColor = isActive
                    ? new Color(1f, 0.83f, 0.22f, 0.95f)
                    : new Color(0.55f, 0.55f, 0.55f, 0.45f);

                painter.BeginPath();
                painter.Arc(dot, dotRadius, 0f, 360f);
                painter.Fill();

                nextDot += dotSpacing;
            }

            traveled += segLen;
        }
    }
}
