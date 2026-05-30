using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BlockNumberSpriteSet : MonoBehaviour
{
    [SerializeField] Sprite number1Sprite;
    [SerializeField] Sprite number2Sprite;
    [SerializeField] Sprite number3Sprite;
    [SerializeField] Sprite number4Sprite;
    [SerializeField] Sprite number5Sprite;
    [SerializeField] Sprite number6Sprite;

    void Awake()
    {
        SyncFromScriptDefaultReferences();
    }

    void OnValidate()
    {
        SyncFromScriptDefaultReferences();
    }

    public Sprite GetSprite(int number)
    {
        SyncFromScriptDefaultReferences();

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

    void SyncFromScriptDefaultReferences()
    {
#if UNITY_EDITOR
        var script = MonoScript.FromMonoBehaviour(this);
        if (script == null)
            return;

        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(script)) as MonoImporter;
        if (importer == null)
            return;

        bool changed = false;
        changed |= AssignSprite(ref number1Sprite, importer.GetDefaultReference(nameof(number1Sprite)));
        changed |= AssignSprite(ref number2Sprite, importer.GetDefaultReference(nameof(number2Sprite)));
        changed |= AssignSprite(ref number3Sprite, importer.GetDefaultReference(nameof(number3Sprite)));
        changed |= AssignSprite(ref number4Sprite, importer.GetDefaultReference(nameof(number4Sprite)));
        changed |= AssignSprite(ref number5Sprite, importer.GetDefaultReference(nameof(number5Sprite)));
        changed |= AssignSprite(ref number6Sprite, importer.GetDefaultReference(nameof(number6Sprite)));

        if (changed && !Application.isPlaying)
            EditorUtility.SetDirty(this);
#endif
    }

#if UNITY_EDITOR
    static bool AssignSprite(ref Sprite target, Object source)
    {
        var sprite = source as Sprite;
        if (target == sprite)
            return false;

        target = sprite;
        return true;
    }
#endif
}
