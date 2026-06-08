using Framework.Lobby;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class LobbyLifetimeScope : LifetimeScope
{
    [Tooltip("LobbyUIController가 루트에 있는 UI 프리팹. 씬에 미리 배치하지 않고 런타임에 생성된다.")]
    [SerializeField] private LobbyUIController uiPrefab;

    protected override void Configure(IContainerBuilder builder)
    {
        // 로드된 에셋 컨테이너 - 싱글톤으로 등록
        builder.Register<LobbyResources>(Lifetime.Singleton);

        // 씬 시작 시 StartAsync 자동 실행, 씬 종료 시 Dispose로 핸들 해제
        builder.RegisterEntryPoint<LobbyResourcesLoader>(Lifetime.Singleton)
               .AsSelf();

        // UI: 씬에 미리 배치하는 대신 프리팹에서 런타임 생성하고, Addressables로 UI 스프라이트 SO를 로드해 주입
        builder.RegisterComponentInNewPrefab(uiPrefab, Lifetime.Scoped);
        builder.RegisterEntryPoint<LobbyUIResourcesLoader>(Lifetime.Singleton);
    }
}
