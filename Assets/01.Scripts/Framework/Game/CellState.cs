using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CellState
{
    public int number;
    public bool isOriginalTower;
    public CellKind kind;
    public bool concealedByBomb;
}

// Unity 뷰
public class CellView
{
    public GameObject go;
    public SpriteRenderer sr;
    public SpriteRenderer numberSpriteRenderer;
    public SpriteRenderer outline;
    public TextMeshPro label;
    public List<SpriteRenderer> previewBlurRenderers;
}