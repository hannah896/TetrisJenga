using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UIImageLibrary", menuName = "UI/Image Library")]
public class UIImageLibrarySO : ScriptableObject
{
    [SerializeField] private List<UIImageEntry> images = new List<UIImageEntry>();

    private Dictionary<UIImageId, Sprite> imageIdMap;

    public IReadOnlyList<UIImageEntry> Images => images;

    public bool TryGetSprite(UIImageId imageId, out Sprite sprite)
    {
        EnsureImageMap();
        return imageIdMap.TryGetValue(imageId, out sprite);
    }

    public Sprite GetSprite(UIImageId imageId)
    {
        EnsureImageMap();

        if (imageIdMap.TryGetValue(imageId, out Sprite sprite))
        {
            return sprite;
        }

        Debug.LogWarning($"UIImageLibrarySO could not find sprite id '{imageId}'.", this);
        return null;
    }

    private void EnsureImageMap()
    {
        if (imageIdMap != null)
        {
            return;
        }

        imageIdMap = new Dictionary<UIImageId, Sprite>();

        foreach (UIImageEntry image in images)
        {
            if (image.Id != UIImageId.None)
            {
                imageIdMap[image.Id] = image.Sprite;
            }
        }
    }

    private void OnValidate()
    {
        imageIdMap = null;
    }
}

[Serializable]
public enum UIImageId
{
    None = 0,
    LobbyBackground = 10,
    TitleAbyss = 20,
    TitleStack = 30,
    PlayButton = 40,
    SettingButton = 50,
    ExitButton = 60,
    SinglePlayButton = 70,
    MultiPlayButton = 80,
    SettingBackground = 90,
    StageBackground = 100,
    StageButton = 110,
    Submarine = 120
}

[Serializable]
public struct UIImageEntry
{
    [SerializeField] private UIImageId id;
    [SerializeField] private Sprite sprite;

    public UIImageId Id => id;
    public Sprite Sprite => sprite;
}
