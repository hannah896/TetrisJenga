using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JSAM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// LobbyScene을 거치지 않고 씬을 직접 시작할 때 AudioManager가 없으면
/// Addressables "default" 라벨에서 소환하고 지정된 BGM을 재생한다.
/// </summary>
public class AudioManagerBootstrapper : MonoBehaviour
{
    [SerializeField] _AudioLibraryMusic bgm = _AudioLibraryMusic.EndlessBGM;
    [SerializeField] string defaultLabel = "default";

    async void Start()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.Initialized)
        {
            PlayBGM();
            return;
        }

        await BootstrapAudioManager();
        PlayBGM();
    }

    async UniTask BootstrapAudioManager()
    {
        var locHandle = Addressables.LoadResourceLocationsAsync(defaultLabel);
        var locations = locHandle.WaitForCompletion();

        if (locHandle.Status != AsyncOperationStatus.Succeeded || locations == null)
        {
            Debug.LogWarning("[AudioManagerBootstrapper] default 라벨 로드 실패");
            Addressables.Release(locHandle);
            return;
        }

        var instanceHandles = new List<AsyncOperationHandle<GameObject>>();
        foreach (var loc in locations)
        {
            var assetHandle = Addressables.LoadAssetAsync<GameObject>(loc);
            var prefab = assetHandle.WaitForCompletion();
            if (prefab != null)
                instanceHandles.Add(Addressables.InstantiateAsync(loc.PrimaryKey));
        }

        Addressables.Release(locHandle);

        await UniTask.WaitUntil(
            () => AudioManager.Instance != null && AudioManager.Instance.Initialized,
            cancellationToken: destroyCancellationToken);
    }

    void PlayBGM()
    {
        AudioManager.StopAllMusic();
        AudioManager.PlayMusic(bgm);
    }
}
