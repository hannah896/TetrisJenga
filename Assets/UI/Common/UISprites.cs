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
    }

    public static void Apply(VisualElement element, Texture2D texture)
    {
        if (element == null || texture == null)
        {
            return;
        }

        element.style.backgroundImage = new StyleBackground(texture);
    }
}
