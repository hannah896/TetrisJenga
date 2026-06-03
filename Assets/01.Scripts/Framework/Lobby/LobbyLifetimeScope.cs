using Framework.Lobby;
using VContainer;
using VContainer.Unity;

public class LobbyLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // 로드된 에셋 컨테이너 - 싱글톤으로 등록
        builder.Register<LobbyResources>(Lifetime.Singleton);

        // 씬 시작 시 StartAsync 자동 실행, 씬 종료 시 Dispose로 핸들 해제
        builder.RegisterEntryPoint<LobbyResourcesLoader>(Lifetime.Singleton)
               .AsSelf();

        // UI: 씬에 배치된 컨트롤러를 수집하고, Addressables로 UI 스프라이트 SO를 로드해 주입
        builder.RegisterComponentInHierarchy<LobbyUIController>();
        builder.RegisterEntryPoint<LobbyUIResourcesLoader>(Lifetime.Singleton);
    }
}
