using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// BlockTower 분리 과정에서 추출한 순수 함수 모음 (상태 의존 없음).
/// 상태(_cells, _focusedCell 등)에 의존하는 로직은 여기 두지 않는다 → BlockTower/InputHandler/GameManager로.
/// </summary>
public static class Util
{
    #region Colors
    public static Color NumberColor(int n) => n switch
    {
        1 => new Color(0.95f, 0.43f, 0.68f),
        2 => new Color(0.93f, 0.27f, 0.27f),
        3 => new Color(0.26f, 0.65f, 0.96f),
        4 => new Color(0.22f, 0.80f, 0.45f),
        5 => new Color(0.98f, 0.73f, 0.15f),
        6 => new Color(0.72f, 0.38f, 0.92f),
        _ => Color.gray
    };

    public static Color PlacedNumberColor(int n)
    {
        float t = Mathf.InverseLerp(1f, 6f, Mathf.Clamp(n, 1, 6));
        return Color.Lerp(new Color32(160, 160, 160, 255), new Color32(50, 50, 50, 255), t);
    }

    public static Color IceBlockColor() => new Color32(0x71, 0xC0, 0xC0, 0xFF);

    public static Color TetrominoColor(TetrominoPreset preset) => preset switch
    {
        TetrominoPreset.I => new Color(0f, 0.85f, 0.90f),
        TetrominoPreset.J => new Color(0.05f, 0.13f, 0.95f),
        TetrominoPreset.L => new Color(0.95f, 0.60f, 0.05f),
        TetrominoPreset.O => new Color(0.90f, 0.95f, 0.02f),
        TetrominoPreset.S => new Color(0.10f, 0.95f, 0.15f),
        TetrominoPreset.T => new Color(0.65f, 0.12f, 0.95f),
        TetrominoPreset.Z => new Color(0.95f, 0.05f, 0.04f),
        _ => Color.white
    };
    #endregion

    # region Placement
    public static int PlacementFloorY(Vector2Int placementMin, int extractionMaxRow) => Mathf.Max(placementMin.y, extractionMaxRow + 1);

    public static int PlacementCeilingY(Vector2Int placementMin, Vector2Int placementMax, int extractionMaxRow)
        => PlacementFloorY(placementMin, extractionMaxRow) + Mathf.Max(0, placementMax.y - placementMin.y);

    #endregion

    public static Vector3 MouseWorldPos()
    {
        var cam = Camera.main;
        var mouse = Mouse.current.position.ReadValue();
        var screen = new Vector3(mouse.x, mouse.y, -cam.transform.position.z);
        var pos = cam.ScreenToWorldPoint(screen);
        pos.z = 0f;
        return pos;
    }
    
    public static string PresetKeyText(TetrominoPreset preset) => preset switch
    {
        TetrominoPreset.I => "Q",
        TetrominoPreset.J => "W",
        TetrominoPreset.L => "E",
        TetrominoPreset.O => "R",
        TetrominoPreset.S => "A",
        TetrominoPreset.T => "S",
        TetrominoPreset.Z => "D",
        _ => string.Empty
    };

    public static Sprite BonusPresetSprite(BonusTetrominoSpriteSet set, TetrominoPreset preset)
    {
        if (set == null)
            return null;

        return preset switch
        {
            TetrominoPreset.I => set.I,
            TetrominoPreset.J => set.J,
            TetrominoPreset.L => set.L,
            TetrominoPreset.O => set.O,
            TetrominoPreset.S => set.S,
            TetrominoPreset.T => set.T,
            TetrominoPreset.Z => set.Z,
            _ => null
        };
    }

    #region BonusPreview
    public static float ResolvedOrDefault(float value, float fallback)
        => float.IsNaN(value) || value <= 0.01f
            ? fallback : value;

    public static float BonusPreviewCellsFallbackWidth(VisualElement container)
        => container != null && (container.name == "BonusPreview2Cells" || container.name == "BonusPreview3Cells")
            ? 160f : 240f;

    public static float BonusPreviewCellsFallbackHeight(VisualElement container)
        => container != null && (container.name == "BonusPreview2Cells" || container.name == "BonusPreview3Cells")
            ? 80f : 120f;

    public static float BonusPreviewCellSize(VisualElement container, List<VisualElement> elements)
    {
        if (elements != null)
            foreach (var element in elements)
            {
                if (element == null) continue;
                float width = ResolvedOrDefault(element.resolvedStyle.width, 0f);
                if (width > 0.01f) return width;
            }

        return container != null
               && (container.name == "BonusPreview2Cells" || container.name == "BonusPreview3Cells")
            ? 40f : 60f;
    }
    #endregion
}
