using System.Collections.Generic;
using UnityEngine;

public static class TetrominoShapeUtil
{
    #region  Get Method
    public static Vector2Int[] GetOffsets(TetrominoPreset preset)
    {
        return preset switch
        {
            TetrominoPreset.I => new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(2, 0), new Vector2Int(3, 0)
            },
            TetrominoPreset.J => new[]
            {
                new Vector2Int(0, 1), new Vector2Int(0, 0),
                new Vector2Int(1, 0), new Vector2Int(2, 0)
            },
            TetrominoPreset.L => new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(2, 0), new Vector2Int(2, 1)
            },
            TetrominoPreset.O => new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 1)
            },
            TetrominoPreset.S => new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(1, 1), new Vector2Int(2, 1)
            },
            TetrominoPreset.T => new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(2, 0), new Vector2Int(1, 1)
            },
            TetrominoPreset.Z => new[]
            {
                new Vector2Int(0, 1), new Vector2Int(1, 1),
                new Vector2Int(1, 0), new Vector2Int(2, 0)
            },
            _ => new[] { Vector2Int.zero }
        };
    }

    public static Vector2 GetBaseRotationPivot(TetrominoPreset preset)
    {
        return preset switch
        {
            TetrominoPreset.I => new Vector2(1.5f, 0.5f),
            TetrominoPreset.O => new Vector2(1f, 1f),
            TetrominoPreset.S or TetrominoPreset.Z => new Vector2(1f, 1f),
            _ => new Vector2(1f, 0f)
        };
    }

    public static Vector2 GetRotationPivot(TetrominoPreset preset, int rotation)
    {
        var pivot = GetBaseRotationPivot(preset);

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        foreach (var offset in GetOffsets(preset))
        {
            var rotatedOffset = RotateCellAroundPivot(offset, pivot, rotation);
            var cell = RoundToCell(rotatedOffset);
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
        }

        return pivot - new Vector2(minX, minY);
    }

    public static bool TryGetMatchingPreset(List<Vector2Int> shape, out TetrominoPreset preset, out int rotation)
    {
        var shapeKey = NormalizedShapeKey(shape);
        for (int i = 0; i <= (int)TetrominoPreset.Z; i++)
        {
            var candidate = (TetrominoPreset)i;
            for (int candidateRotation = 0; candidateRotation < 4; candidateRotation++)
            {
                var candidateKey = NormalizedShapeKey(GetCells(Vector2Int.zero, candidate, candidateRotation));
                if (shapeKey != candidateKey) continue;

                preset = candidate;
                rotation = candidateRotation;
                return true;
            }
        }

        preset = TetrominoPreset.I;
        rotation = 0;
        return false;
    }
    
    public static List<Vector2Int> GetCells(Vector2Int anchor, TetrominoPreset preset, int rotation = 0)
    {
        if (preset == TetrominoPreset.O)
            rotation = 0;

        var offsets = GetOffsets(preset);
        var pivot = GetBaseRotationPivot(preset);

        var cells = new List<Vector2Int>(offsets.Length);
        foreach (var offset in offsets)
        {
            var rel = RotateCellAroundPivot(offset, pivot, rotation) - pivot;
            cells.Add(anchor + FloorToCell(rel));
        }

        return cells;
    }
    #endregion

    #region  Cell Method
    public static Vector2 RotateCellAroundPivot(Vector2Int cell, Vector2 pivot, int quarterTurns)
    {
        var rotated = new Vector2(cell.x, cell.y);
        int turns = ((quarterTurns % 4) + 4) % 4;
        for (int i = 0; i < turns; i++)
            rotated = new Vector2(pivot.x + rotated.y - pivot.y, pivot.y - rotated.x + pivot.x);

        return rotated;
    }
    
    public static Vector2Int RotateCell(Vector2Int cell, int quarterTurns)
    {
        return quarterTurns switch
        {
            1 => new Vector2Int(-cell.y, cell.x),
            2 => new Vector2Int(-cell.x, -cell.y),
            3 => new Vector2Int(cell.y, -cell.x),
            _ => cell
        };
    }
    
    public static Vector2Int RoundToCell(Vector2 value)
    {
        return new Vector2Int(
            Mathf.FloorToInt(value.x + 0.5f),
            Mathf.FloorToInt(value.y + 0.5f));
    }
    
    public static Vector2Int FloorToCell(Vector2 value)
    {
        return new Vector2Int(
            Mathf.FloorToInt(value.x),
            Mathf.FloorToInt(value.y));
    }
    #endregion

    #region  Shape Method
    public static bool ShapeMatchesPreset(List<Vector2Int> shape, TetrominoPreset preset)
    {
        return CanonicalShape(shape) == CanonicalShape(GetCells(Vector2Int.zero, preset));
    }
    
    public static string NormalizedShapeKey(List<Vector2Int> cells)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        foreach (var cell in cells)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
        }

        var normalized = new List<Vector2Int>(cells.Count);
        foreach (var cell in cells)
            normalized.Add(new Vector2Int(cell.x - minX, cell.y - minY));

        normalized.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        var key = "";
        foreach (var cell in normalized)
            key += $"{cell.x},{cell.y};";
        return key;
    }
    public static string CanonicalShape(List<Vector2Int> cells)
    {
        string best = null;
        for (int rotation = 0; rotation < 4; rotation++)
        {
            var rotated = new List<Vector2Int>(cells.Count);
            foreach (var cell in cells)
                rotated.Add(RotateCell(cell, rotation));

            var key = NormalizedShapeKey(rotated);
            if (best == null || string.CompareOrdinal(key, best) < 0)
                best = key;
        }

        return best;
    }
    #endregion
}