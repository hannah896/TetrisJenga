using System.Collections.Generic;
using UnityEngine;

public class TowerGridModel
{
    readonly Dictionary<Vector2Int, CellState> _cells    = new();
    readonly Dictionary<Vector2Int, CellState> _iceCells = new();

    // Check
    public bool TryGetCell(Vector2Int cell, out CellState state) => _cells.TryGetValue(cell, out state);
    public bool TryGetIceCell(Vector2Int cell, out CellState state) => _iceCells.TryGetValue(cell, out state);
    public bool HasCell(Vector2Int cell) => _cells.ContainsKey(cell);
    public int Count => _cells.Count;
    public IEnumerable<KeyValuePair<Vector2Int, CellState>> AllCells => _cells;
    public IEnumerable<KeyValuePair<Vector2Int, CellState>> AllIceCells => _iceCells;

    // Distinguish
    public bool IsExtractableCell(Vector2Int cell) =>
        _cells.TryGetValue(cell, out var s) && s.isOriginalTower && s.kind != CellKind.Ice;

    public bool IsMergeableCell(Vector2Int cell) =>
        _cells.TryGetValue(cell, out var s) && s.kind != CellKind.Ice;

    // change
    public void AddCell(Vector2Int cell, CellState state)    => _cells[cell]    = state;
    public void AddIceCell(Vector2Int cell, CellState state) => _iceCells[cell] = state;
    public void RemoveCell(Vector2Int cell)    => _cells.Remove(cell);
    public void RemoveIceCell(Vector2Int cell) => _iceCells.Remove(cell);
    public void Clear() { _cells.Clear(); _iceCells.Clear(); }

    // search
    public List<List<Vector2Int>> FindConnectedComponents()
    {
        var unvisited = new HashSet<Vector2Int>(_cells.Keys);
        var components = new List<List<Vector2Int>>();

        while (unvisited.Count > 0)
        {
            var en = unvisited.GetEnumerator();
            en.MoveNext();
            var start = en.Current;
            en.Dispose();

            var component = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            unvisited.Remove(start);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                component.Add(c);
                foreach (var n in Neighbors(c))
                    if (unvisited.Remove(n))
                        queue.Enqueue(n);
            }

            components.Add(component);
        }

        return components;
    }
    public List<List<Vector2Int>> FindOriginalTowerComponents()
    {
        var unvisited = new HashSet<Vector2Int>();
        foreach (var (cell, data) in _cells)
            if (data.isOriginalTower)
                unvisited.Add(cell);

        var components = new List<List<Vector2Int>>();
        while (unvisited.Count > 0)
        {
            var en = unvisited.GetEnumerator();
            en.MoveNext();
            var start = en.Current;
            en.Dispose();

            var component = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            unvisited.Remove(start);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                component.Add(c);
                foreach (var n in Neighbors(c))
                {
                    if (!unvisited.Contains(n)) continue;
                    if (!_cells.TryGetValue(n, out var data) || !data.isOriginalTower) continue;
                    unvisited.Remove(n);
                    queue.Enqueue(n);
                }
            }

            components.Add(component);
        }

        return components;
    }
    public bool IsConnected(List<Vector2Int> cells)
    {
        if (cells.Count <= 1) return true;
        var set = new HashSet<Vector2Int>(cells);
        var visited = new HashSet<Vector2Int> { cells[0] };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(cells[0]);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var n in Neighbors(c))
                if (set.Contains(n) && visited.Add(n))
                    queue.Enqueue(n);
        }

        return visited.Count == cells.Count;
    }
    public bool TouchesPlacedTopBlock(List<Vector2Int> component)
    {
        foreach (var cell in component)
        {
            foreach (var neighbor in Neighbors(cell))
            {
                if (!_cells.TryGetValue(neighbor, out var state)) continue;
                if (!state.isOriginalTower)
                    return true;
            }
        }

        return false;
    }
    public bool TouchesGround(List<Vector2Int> component)
    {
        foreach (var cell in component)
            if (cell.y == 0)
                return true;
        return false;
    }
    public int MinComponentY(List<Vector2Int> component)
    {
        int minY = int.MaxValue;
        foreach (var cell in component)
            minY = Mathf.Min(minY, cell.y);
        return minY;
    }

    public bool TryFindIceBelowInColumn(Vector2Int landedCell, out Vector2Int iceCell)
    {
        iceCell = default;
        bool found = false;
        int bestY = int.MinValue;
        foreach (var pair in _iceCells)
        {
            if (pair.Key.x == landedCell.x &&
                pair.Key.y < landedCell.y &&
                pair.Key.y > bestY)
            {
                bestY = pair.Key.y;
                iceCell = pair.Key;
                found = true;
            }
        }

        return found;
    }

    // range
    public bool TryGetOccupiedGridBounds(out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = minY = int.MaxValue; maxX = maxY = int.MinValue;
        if (_cells.Count == 0) return false;
        foreach (var c in _cells.Keys)
        {
            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
        }
        return minX <= maxX && minY <= maxY;
    }
    
    public int HighestOccupiedRow()
    {
        int maxY = -1;
        foreach (var cell in _cells.Keys) maxY = Mathf.Max(maxY, cell.y);
        return maxY;
    }

    static IEnumerable<Vector2Int> Neighbors(Vector2Int c)
    {
        yield return new Vector2Int(c.x + 1, c.y);
        yield return new Vector2Int(c.x - 1, c.y);
        yield return new Vector2Int(c.x, c.y + 1);
        yield return new Vector2Int(c.x, c.y - 1);
    }
}

