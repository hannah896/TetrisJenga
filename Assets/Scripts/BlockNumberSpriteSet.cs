using UnityEngine;

public class BlockNumberSpriteSet : MonoBehaviour
{
    [SerializeField] Sprite number1Sprite;
    [SerializeField] Sprite number2Sprite;
    [SerializeField] Sprite number3Sprite;
    [SerializeField] Sprite number4Sprite;
    [SerializeField] Sprite number5Sprite;
    [SerializeField] Sprite number6Sprite;

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
}
