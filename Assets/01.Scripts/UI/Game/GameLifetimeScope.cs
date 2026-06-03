using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<GameplayUIController>();
        builder.RegisterEntryPoint<GameUIResourcesLoader>(Lifetime.Singleton);
    }
}
