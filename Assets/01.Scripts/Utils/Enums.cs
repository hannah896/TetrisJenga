
public enum TetrominoPreset
{
    I,
    J,
    L,
    O,
    S,
    T,
    Z
}

// 블록 셀 종류 (구 BlockCell.CellKind)
public enum CellKind
{
    Normal,
    Bomb,
    Ice
}

// 폭탄 가림 타일 종류 (구 BlockNumberSpriteSetAsset.BombObscureKind)
public enum BombObscureKind
{
    Center,
    Edge,
    Corner
}
