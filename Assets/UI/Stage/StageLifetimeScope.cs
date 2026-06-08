using UnityEngine;
using VContainer;
using VContainer.Unity;

public class StageLifetimeScope : LifetimeScope
{
    [Tooltip("StageUIController가 루트에 있는 UI 프리팹. 씬에 미리 배치하지 않고 런타임에 생성된다.")]
    [SerializeField] private StageUIController uiPrefab;

    protected override void Configure(IContainerBuilder builder)
    {
        // 씬에 미리 배치된 컨트롤러를 수집하는 대신, 프리팹에서 런타임 생성한다.
        builder.RegisterComponentInNewPrefab(uiPrefab, Lifetime.Scoped);
        builder.RegisterEntryPoint<StageUIResourcesLoader>(Lifetime.Singleton);
    }
}
