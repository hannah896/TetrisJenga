using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class HeldBlockController : MonoBehaviour
{
    readonly List<Vector2Int> relPos = new();
    readonly List<(CellState state, CellView view)> data = new();

    readonly List<HeldSourceCell> sourceCells = new();

    [SerializeField, Range(0f, 0.5f)] float placementFailDuration = 0.18f;
    [SerializeField, Range(0f, 0.5f)] float placementFailShakeDistance = 0.12f;
    [SerializeField, Range(1, 8)] int placementFailShakeCount = 3;

    public IReadOnlyList<Vector2Int> RelPosReadOnly => relPos;
    public List<Vector2Int> RelPos => relPos;
    internal List<(CellState state, CellView view)> Data => data;
    internal List<HeldSourceCell> SourceCells => sourceCells;

    public bool IsHolding { get; set; }
    public GameObject Root { get; set; }
    public Vector2 Center { get; set; }
    public Vector2Int BaseCell { get; set; }
    public bool UsingKeyboardPlacement { get; set; }
    public int StartScore { get; set; }
    public bool MatchesBonus { get; set; }
    public float FailEndTime { get; private set; } = -1f;

    float placementFailStartTime = -1f;

    public void ResetState()
    {
        relPos.Clear();
        data.Clear();
        sourceCells.Clear();
        Root = null;
        Center = Vector2.zero;
        BaseCell = Vector2Int.zero;
        UsingKeyboardPlacement = false;
        StartScore = 0;
        MatchesBonus = false;
        IsHolding = false;
        placementFailStartTime = -1f;
        FailEndTime = -1f;
    }

    public GameObject CreateRoot(Transform parent, UnityAction<GameObject> markGenerated)
    {
        Root = new GameObject("HeldBlocks");
        markGenerated?.Invoke(Root);
        Root.transform.SetParent(parent);
        return Root;
    }

    public void DestroyRoot(UnityAction<GameObject> destroy)
    {
        if (Root != null)
            destroy?.Invoke(Root);
        Root = null;
    }

    public void ClearHeldLists()
    {
        relPos.Clear();
        data.Clear();
        sourceCells.Clear();
    }

    public void SetRelativeCells(IEnumerable<Vector2Int> cells)
    {
        relPos.Clear();
        relPos.AddRange(cells);
        RecalculateCenter();
    }

    public void RecalculateCenter()
    {
        if (relPos.Count == 0)
        {
            Center = Vector2.zero;
            return;
        }

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (var rel in relPos)
        {
            minX = Mathf.Min(minX, rel.x);
            minY = Mathf.Min(minY, rel.y);
            maxX = Mathf.Max(maxX, rel.x);
            maxY = Mathf.Max(maxY, rel.y);
        }

        Center = new Vector2((maxX - minX + 1) * 0.5f, (maxY - minY + 1) * 0.5f);
    }

    public void SetCenterFromBounds(int minX, int minY, int maxX, int maxY)
    {
        Center = new Vector2((maxX - minX + 1) * 0.5f, (maxY - minY + 1) * 0.5f);
    }

    public void UpdateChildLocalPositions()
    {
        for (int i = 0; i < relPos.Count && i < data.Count; i++)
        {
            var rel = relPos[i];
            data[i].view.go.transform.localPosition = new Vector3(
                rel.x + 0.5f - Center.x,
                rel.y + 0.5f - Center.y,
                0f);
        }
    }



    public Vector2Int ClampBase(Vector2Int baseCell, Vector2Int placementMin, Vector2Int placementMax, int placementFloorY, int placementCeilingY)
    {
        if (relPos.Count == 0)
            return baseCell;

        int minRelX = int.MaxValue, minRelY = int.MaxValue;
        int maxRelX = int.MinValue, maxRelY = int.MinValue;

        foreach (var rel in relPos)
        {
            minRelX = Mathf.Min(minRelX, rel.x);
            minRelY = Mathf.Min(minRelY, rel.y);
            maxRelX = Mathf.Max(maxRelX, rel.x);
            maxRelY = Mathf.Max(maxRelY, rel.y);
        }

        int minBaseX = placementMin.x - minRelX;
        int maxBaseX = placementMax.x - maxRelX;
        int minBaseY = placementFloorY - minRelY;
        int maxBaseY = placementCeilingY - maxRelY;

        return new Vector2Int(
            Mathf.Clamp(baseCell.x, minBaseX, maxBaseX),
            Mathf.Clamp(baseCell.y, minBaseY, maxBaseY));
    }

    public int LowestRelativeY()
    {
        int minRelY = 0;
        bool found = false;
        foreach (var rel in relPos)
        {
            minRelY = found ? Mathf.Min(minRelY, rel.y) : rel.y;
            found = true;
        }

        return minRelY;
    }

    public void SetPreviewColor(Color color, float previewBlurAlpha)
    {
        foreach (var (_, view) in data)
        {
            if (view.sr != null)
                view.sr.color = color;

            if (view.previewBlurRenderers == null) continue;

            var blurColor = new Color(color.r, color.g, color.b, previewBlurAlpha);
            foreach (var blur in view.previewBlurRenderers)
            {
                if (blur == null) continue;
                blur.enabled = false;
                blur.color = blurColor;
            }
        }
    }

    public void PlayPlacementFailFeedback()
    {
        placementFailStartTime = Time.time;
        FailEndTime = Time.time + placementFailDuration;
#if UNITY_ANDROID || UNITY_IOS
        if (Application.isPlaying)
            Handheld.Vibrate();
#endif
    }

    public void MoveHeldBase(Vector2Int dir, UnityAction onVerticalMove = null)
    {
        BaseCell += dir;
        if (dir.y != 0)
            onVerticalMove?.Invoke();
    }

    public void RotateHeldBlocks(bool clockwise)
    {
        if (relPos.Count == 0) return;

        if (TetrominoShapeUtil.TryGetMatchingPreset(relPos, out var preset, out var presetRotation))
        {
            if (preset == TetrominoPreset.O)
                return;

            var pivot = TetrominoShapeUtil.GetRotationPivot(preset, presetRotation);
            var pivotWorld = BaseCell + pivot;
            int minX = int.MaxValue, minY = int.MaxValue;
            var rotated = new List<Vector2Int>(relPos.Count);

            foreach (var rel in relPos)
            {
                var next = clockwise
                    ? new Vector2(pivot.x + rel.y - pivot.y, pivot.y - rel.x + pivot.x)
                    : new Vector2(pivot.x - rel.y + pivot.y, pivot.y + rel.x - pivot.x);
                var nextCell = TetrominoShapeUtil.RoundToCell(next);
                rotated.Add(nextCell);
                minX = Mathf.Min(minX, nextCell.x);
                minY = Mathf.Min(minY, nextCell.y);
            }

            var normalizeOffset = new Vector2Int(minX, minY);
            for (int i = 0; i < rotated.Count; i++)
                relPos[i] = rotated[i] - normalizeOffset;

            if (UsingKeyboardPlacement)
            {
                var normalizedPivot = pivot - normalizeOffset;
                BaseCell = TetrominoShapeUtil.RoundToCell(pivotWorld - normalizedPivot);
            }

            RecalculateCenter();
            UpdateChildLocalPositions();
            return;
        }

        int maxX = 0, maxY = 0;
        foreach (var rel in relPos)
        {
            maxX = Mathf.Max(maxX, rel.x);
            maxY = Mathf.Max(maxY, rel.y);
        }

        for (int i = 0; i < relPos.Count; i++)
        {
            var rel = relPos[i];
            relPos[i] = clockwise
                ? new Vector2Int(maxY - rel.y, rel.x)
                : new Vector2Int(rel.y, maxX - rel.x);
        }

        RecalculateCenter();
        UpdateChildLocalPositions();
    }

    public Vector3 FailShakeOffset(bool isFailing)
    {
        if (!isFailing || placementFailDuration <= 0f) return Vector3.zero;

        float progress = Mathf.Clamp01((Time.time - placementFailStartTime) / placementFailDuration);
        float fade = 1f - progress;
        float wave = Mathf.Sin(progress * Mathf.PI * 2f * placementFailShakeCount);
        return new Vector3(wave * placementFailShakeDistance * fade, 0f, 0f);
    }
}
