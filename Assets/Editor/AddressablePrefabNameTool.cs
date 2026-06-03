using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace TetrisJenga.EditorTools
{
    [InitializeOnLoad]
    public static class AddressablePrefabNameTool
    {
        private const string MenuPath = "Tools/Addressables/Use Prefab Name As Address";
        private static bool isUpdating;

        static AddressablePrefabNameTool()
        {
            AddressableAssetSettings.OnModificationGlobal -= OnAddressablesModified;
            AddressableAssetSettings.OnModificationGlobal += OnAddressablesModified;
        }

        [MenuItem(MenuPath)]
        public static void UsePrefabNameAsAddress()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("Addressable settings could not be found.");
                return;
            }

            var changedCount = 0;
            foreach (var group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (var entry in group.entries)
                {
                    if (TrySetPrefabNameAddress(entry))
                    {
                        changedCount++;
                    }
                }
            }

            if (changedCount > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"Addressable prefab addresses updated: {changedCount}");
        }

        private static void OnAddressablesModified(
            AddressableAssetSettings settings,
            AddressableAssetSettings.ModificationEvent modificationEvent,
            object eventData)
        {
            if (isUpdating || settings == null)
            {
                return;
            }

            if (modificationEvent != AddressableAssetSettings.ModificationEvent.EntryCreated &&
                modificationEvent != AddressableAssetSettings.ModificationEvent.EntryAdded &&
                modificationEvent != AddressableAssetSettings.ModificationEvent.EntryMoved)
            {
                return;
            }

            var entries = GetEntries(eventData);
            if (entries.Count == 0)
            {
                return;
            }

            isUpdating = true;
            try
            {
                var changed = false;
                foreach (var entry in entries)
                {
                    changed |= TrySetPrefabNameAddress(entry);
                }

                if (changed)
                {
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                isUpdating = false;
            }
        }

        private static List<AddressableAssetEntry> GetEntries(object eventData)
        {
            var entries = new List<AddressableAssetEntry>();

            if (eventData is AddressableAssetEntry singleEntry)
            {
                entries.Add(singleEntry);
            }
            else if (eventData is IEnumerable<AddressableAssetEntry> entryCollection)
            {
                entries.AddRange(entryCollection);
            }
            else if (eventData is IEnumerable<object> objectCollection)
            {
                foreach (var item in objectCollection)
                {
                    if (item is AddressableAssetEntry entry)
                    {
                        entries.Add(entry);
                    }
                }
            }

            return entries;
        }

        private static bool TrySetPrefabNameAddress(AddressableAssetEntry entry)
        {
            if (entry == null || entry.IsFolder)
            {
                return false;
            }

            var path = AssetDatabase.GUIDToAssetPath(entry.guid);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                return false;
            }

            var prefabName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (entry.address == prefabName)
            {
                return false;
            }

            entry.SetAddress(prefabName, false);
            return true;
        }
    }
}
