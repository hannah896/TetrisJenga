using UnityEngine;

[CreateAssetMenu(fileName = "Block Number Sprite Set", menuName = "Tetris Jenga/Block Number Sprite Set")]
public class BlockNumberSpriteSetAsset : ScriptableObject
{
    public enum BombObscureKind
    {
        Center,
        Edge,
        Corner
    }

    [Header("Number Blocks")]
    [SerializeField] Sprite number1Sprite;
    [SerializeField] Sprite number2Sprite;
    [SerializeField] Sprite number3Sprite;
    [SerializeField] Sprite number4Sprite;
    [SerializeField] Sprite number5Sprite;
    [SerializeField] Sprite number6Sprite;

    [Header("Special Blocks")]
    [SerializeField] Sprite bombSprite;
    [SerializeField] Sprite iceSprite;

    [Header("Bomb Obscure")]
    [SerializeField] Sprite bombObscureCenterSprite;
    [SerializeField] Sprite bombObscureEdgeSprite;
    [SerializeField] Sprite bombObscureCornerSprite;

    public Sprite GetSprite(int number)
    {
        return number switch
        {
            1 => number1Sprite,
            2 => number2Sprite,
            3 => number3Sprite,
            4 => number4Sprite,
            5 => number5Sprite,
            6 => number6Sprite,
            _ => null
        };
    }

    public Sprite BombSprite => bombSprite;
    public Sprite IceSprite => iceSprite;

    public Sprite GetBombObscureSprite(BombObscureKind kind)
    {
        return kind switch
        {
            BombObscureKind.Center => bombObscureCenterSprite,
            BombObscureKind.Edge => bombObscureEdgeSprite,
            BombObscureKind.Corner => bombObscureCornerSprite,
            _ => null
        };
    }
}
