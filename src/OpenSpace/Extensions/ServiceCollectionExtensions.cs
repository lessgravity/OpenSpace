using EngineKit.Mathematics;
using Microsoft.Extensions.DependencyInjection;
using OpenSpace.Ecs;
using EngineKit;
using EngineKit.Extensions;
using EngineKit.Input;
using OpenSpace.Physics;
using OpenSpace.Renderers;
using OpenSpace.Windows;

namespace OpenSpace.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddEngine();
        services.AddSingleton<IApplication, SpaceGameApplication>();

        services.AddSingleton<IRenderer, Renderer>();
        services.AddSingleton<ICamera>(provider => new Camera(provider.GetRequiredService<IApplicationContext>(),
            provider.GetRequiredService<IInputProvider>(), new Vector3(0, 0, 10), Vector3.UnitY));

        services.AddSingleton<IEntityWorld, EntityWorld>();
        services.AddSingleton<IPhysicsWorld, JoltPhysicsWorld>();
        
        services.AddSingleton<UiWindow, AssetUiWindow>();
        services.AddSingleton<UiWindow, PropertiesUiWindow>();
        services.AddSingleton<UiWindow, SceneUiWindow>();
        services.AddSingleton<UiWindow, HierarchyUiWindow>();

        services.AddSingleton<IMessageBus, MessageBus>();

        services.AddSingleton<IStatistics, Statistics>();
    }
}