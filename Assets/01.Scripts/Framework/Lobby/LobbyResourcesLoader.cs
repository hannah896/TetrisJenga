using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer.Unity;

namespace Framework.Lobby
{
    /// <summary>
    /// VContainer IAsyncStartable을 통해 씬 시작 시 로비 어드레서블 에셋을 로드하고 GameObject는 생성한다.
    /// LifetimeScope에 등록 후 자동 실행된다.
    /// </summary>
    public class LobbyResourcesLoader : IAsyncStartable, IDisposable
    {
        // Addressables에서 로비 에셋을 묶는 라벨
        private const string LobbyLabel = "Lobby";
        private const string SpawnRootName = "Lobby Addressables";

        private readonly LobbyResources _lobbyResources;
        private readonly List<AsyncOperationHandle> _handles = new();
        private readonly List<AsyncOperationHandle<GameObject>> _instanceHandles = new();
        private GameObject _spawnRoot;

        public LobbyResourcesLoader(LobbyResources lobbyResources)
        {
            _lobbyResources = lobbyResources;
        }

        public async UniTask StartAsync(System.Threading.CancellationToken cancellation)
        {
            _spawnRoot = new GameObject(SpawnRootName);
            await LoadByLabelAsync(LobbyLabel, cancellation);
            Debug.Log("[LobbyResourcesLoader] 로비 리소스 로드 완료");
        }

        private async UniTask LoadByLabelAsync(string label, System.Threading.CancellationToken cancellation)
        {
            // 라벨에 속한 모든 위치(key) 목록을 가져온다
            var locationsHandle = Addressables.LoadResourceLocationsAsync(label);
            await locationsHandle.ToUniTask(cancellationToken: cancellation);

            if (locationsHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning($"[LobbyResourcesLoader] 라벨 '{label}' 위치 로드 실패");
                Addressables.Release(locationsHandle);
                return;
            }

            var locations = locationsHandle.Result;
            _handles.Add(locationsHandle);

            var tasks = new List<UniTask>(locations.Count);

            foreach (var location in locations)
            {
                tasks.Add(LoadSingleAssetAsync(location.PrimaryKey, cancellation));
            }

            await UniTask.WhenAll(tasks);
        }

        private async UniTask LoadSingleAssetAsync(string key, System.Threading.CancellationToken cancellation)
        {
            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(key);
            await handle.ToUniTask(cancellationToken: cancellation);

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning($"[LobbyResourcesLoader] 에셋 로드 실패: {key}");
                return;
            }

            _handles.Add(handle);
            await RegisterAssetAsync(key, handle.Result, cancellation);
        }

        private async UniTask InstantiatePrefabAsync(string key, System.Threading.CancellationToken cancellation)
        {
            var handle = Addressables.InstantiateAsync(key, _spawnRoot.transform);
            await handle.ToUniTask(cancellationToken: cancellation);

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning($"[LobbyResourcesLoader] 프리팹 생성 실패: {key}");
                return;
            }

            _instanceHandles.Add(handle);
        }

        private async UniTask RegisterAssetAsync(string key, UnityEngine.Object asset, System.Threading.CancellationToken cancellation)
        {
            switch (asset)
            {
                case GameObject prefab:
                    _lobbyResources.RegisterPrefab(key, prefab);
                    await InstantiatePrefabAsync(key, cancellation);
                    break;
                case Sprite sprite:
                    _lobbyResources.RegisterSprite(key, sprite);
                    break;
                case AudioClip clip:
                    _lobbyResources.RegisterAudioClip(key, clip);
                    break;
                default:
                    Debug.LogWarning($"[LobbyResourcesLoader] 지원하지 않는 에셋 타입: {asset.GetType().Name} (key: {key})");
                    break;
            }
        }

        // LifetimeScope 해제 시 어드레서블 핸들을 정리한다
        public void Dispose()
        {
            foreach (var handle in _instanceHandles)
            {
                if (handle.IsValid())
                    Addressables.ReleaseInstance(handle);
            }
            _instanceHandles.Clear();

            foreach (var handle in _handles)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            _handles.Clear();

            if (_spawnRoot != null)
                UnityEngine.Object.Destroy(_spawnRoot);
        }
    }
}
