using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JSAM;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer.Unity;

namespace Framework.Lobby
{
    /// <summary>
    /// VContainer IAsyncStartable을 통해 씬 시작 시 로비 어드레서블 에셋을 로드하고 GameObject는 생성한다.
    /// LifetimeScope에 등록 후 자동 실행된다.
    ///
    /// default 라벨 자산(AudioManager, Main Camera 등)은 다른 로더(UI 등)보다 반드시 먼저 생성돼야 하므로
    /// WaitForCompletion으로 '동기' 소환한다. (await로 yield하면 동기 로드인 LobbyUIResourcesLoader가
    /// 그 사이에 먼저 완성되어 카메라/오디오가 뒤로 밀린다. 또한 PlayMode에서 Addressables ToUniTask await가
    /// 멈추는 이슈도 WaitForCompletion으로 회피된다.)
    /// </summary>
    public class LobbyResourcesLoader : IAsyncStartable, IDisposable
    {
        // Addressables에서 로비 핵심 에셋(AudioManager, Main Camera 등)을 묶는 라벨
        private const string DefaultLabel = "default";
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
            // default 라벨로 묶인 핵심 자산(AudioManager, Main Camera 등)을 '동기'로 가장 먼저 소환한다.
            LoadByLabel(DefaultLabel);
            // 소환된 AudioManager가 초기화된 뒤 로비 BGM 재생
            await PlayLobbyBgmAsync(cancellation);
            Debug.Log("[LobbyResourcesLoader] 로비 리소스 로드 완료");
        }

        private async UniTask PlayLobbyBgmAsync(System.Threading.CancellationToken cancellation)
        {
            // 동기 소환된 AudioManager가 Awake/Start로 초기화될 때까지 대기한다.
            // (InstantiateAsync 직후에는 GameObject만 존재하고 InternalInstance가 아직 준비되지 않을 수 있다)
            // AudioManager.Instance 게터는 못 찾을 때마다 에러를 찍으므로, 부수효과 없는 FindAnyObjectByType로 폴링하고
            // 일정 시간 내 나타나지 않으면(=default 라벨 등록 누락 등) 무한 대기 대신 경고 후 건너뛴다.
            const float timeoutSeconds = 5f;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            AudioManager manager = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                manager = UnityEngine.Object.FindAnyObjectByType<AudioManager>();
                if (manager != null && manager.Initialized)
                    break;
                await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
            }

            if (manager == null || !manager.Initialized)
            {
                Debug.LogWarning("[LobbyResourcesLoader] AudioManager를 찾지 못해 BGM 재생을 건너뜁니다. (default 라벨에 AudioManager 프리팹이 등록/저장됐는지 확인)");
                return;
            }

            // AudioLibrary가 생성한 enum으로 재생 (AudioManager.preloadedLibraries에 _AudioLibrary가 등록돼 있어야 함)
            AudioManager.PlayMusic(_AudioLibraryMusic.LobbyBGM, isMainMusic: true);
        }

        private void LoadByLabel(string label)
        {
            // 라벨에 속한 모든 위치(key) 목록을 동기로 가져온다
            var locationsHandle = Addressables.LoadResourceLocationsAsync(label);
            var locations = locationsHandle.WaitForCompletion();

            if (locationsHandle.Status != AsyncOperationStatus.Succeeded || locations == null)
            {
                Debug.LogWarning($"[LobbyResourcesLoader] 라벨 '{label}' 위치 로드 실패");
                Addressables.Release(locationsHandle);
                return;
            }

            _handles.Add(locationsHandle);

            foreach (var location in locations)
            {
                LoadSingleAsset(location.PrimaryKey);
            }
        }

        private void LoadSingleAsset(string key)
        {
            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(key);
            var asset = handle.WaitForCompletion();

            if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
            {
                Debug.LogWarning($"[LobbyResourcesLoader] 에셋 로드 실패: {key}");
                return;
            }

            _handles.Add(handle);
            RegisterAsset(key, asset);
        }

        private void InstantiatePrefab(string key)
        {
            var handle = Addressables.InstantiateAsync(key, _spawnRoot.transform);
            handle.WaitForCompletion();

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning($"[LobbyResourcesLoader] 프리팹 생성 실패: {key}");
                return;
            }

            _instanceHandles.Add(handle);
        }

        private void RegisterAsset(string key, UnityEngine.Object asset)
        {
            switch (asset)
            {
                case GameObject prefab:
                    _lobbyResources.RegisterPrefab(key, prefab);
                    InstantiatePrefab(key);
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
