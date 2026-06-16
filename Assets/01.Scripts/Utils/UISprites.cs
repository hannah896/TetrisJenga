using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 씬별 UI 스프라이트 SO가 들고 있는 Sprite를 VisualElement 배경으로 적용하는 공통 헬퍼.
/// 스프라이트가 비어 있으면(그래픽 작업 전) 아무것도 하지 않으므로 USS 기본 색이 그대로 유지된다.
/// </summary>
public static class UISprites
{
    public static void Apply(VisualElement element, Sprite sprite)
    {
        if (element == null || sprite == null)
        {
            return;
        }

        element.style.backgroundImage = new StyleBackground(sprite);
        // 이미지가 들어가면 USS의 background-color(회색/검정 박스)는 더 이상 필요 없다.
        // 투명으로 덮어써야 scale-to-fit 레터박스 영역에 색 박스가 남지 않는다.
        element.style.backgroundColor = Color.clear;
    }

    public static void Apply(VisualElement element, Texture2D texture)
    {
        if (element == null || texture == null)
        {
            return;
        }

        element.style.backgroundImage = new StyleBackground(texture);
        element.style.backgroundColor = Color.clear;
    }
}
