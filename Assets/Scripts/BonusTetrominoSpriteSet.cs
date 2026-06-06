using UnityEngine;

[CreateAssetMenu(fileName = "BonusTetrominoSpriteSet", menuName = "TetrisJenga/Bonus Tetromino Sprite Set")]
public class BonusTetrominoSpriteSet : ScriptableObject
{
    [SerializeField] Sprite iSprite;
    [SerializeField] Sprite jSprite;
    [SerializeField] Sprite lSprite;
    [SerializeField] Sprite oSprite;
    [SerializeField] Sprite sSprite;
    [SerializeField] Sprite tSprite;
    [SerializeField] Sprite zSprite;

    public Sprite I => iSprite;
    public Sprite J => jSprite;
    public Sprite L => lSprite;
    public Sprite O => oSprite;
    public Sprite S => sSprite;
    public Sprite T => tSprite;
    public Sprite Z => zSprite;
}
