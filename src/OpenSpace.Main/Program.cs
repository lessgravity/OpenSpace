using System.Threading.Tasks;
using EngineKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSpace.Extensions;
using Serilog;

namespace OpenSpace.Main;

internal static class Program
{
    public static async Task Main()
    {
        await using var serviceProvider = CreateServiceProvider();
        
        var application = serviceProvider.GetRequiredService<IApplication>();
        application.Run();
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton(Log.Logger);
        services.Configure<WindowSettings>(configuration.GetSection(nameof(WindowSettings)));
        services.Configure<ContextSettings>(configuration.GetSection(nameof(ContextSettings)));

        services.AddApplication();
        return services.BuildServiceProvider();
    }
}
