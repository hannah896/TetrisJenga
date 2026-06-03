using System.Collections.Generic;
using UnityEngine;

namespace Framework.Lobby
{
    /// <summary>
    /// 로비에서 사용하는 로드된 에셋을 보관하는 컨테이너.
    /// VContainer를 통해 주입받아 사용한다.
    /// </summary>
    public class LobbyResources
    {
        private readonly Dictionary<string, GameObject> _prefabs = new();
        private readonly Dictionary<string, Sprite> _sprites = new();
        private readonly Dictionary<string, AudioClip> _audioClips = new();

        public void RegisterPrefab(string key, GameObject prefab) => _prefabs[key] = prefab;
        public void RegisterSprite(string key, Sprite sprite) => _sprites[key] = sprite;
        public void RegisterAudioClip(string key, AudioClip clip) => _audioClips[key] = clip;

        public GameObject GetPrefab(string key) => _prefabs.GetValueOrDefault(key);
        public Sprite GetSprite(string key) => _sprites.GetValueOrDefault(key);
        public AudioClip GetAudioClip(string key) => _audioClips.GetValueOrDefault(key);

        public bool IsLoaded => _prefabs.Count > 0 || _sprites.Count > 0 || _audioClips.Count > 0;
    }
}
