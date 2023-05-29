using Microsoft.Extensions.DependencyInjection;
using OpenSpace.Engine.Graphics;
using OpenSpace.Engine.Graphics.MeshLoaders;
using OpenSpace.Engine.Graphics.Shaders;
using OpenSpace.Engine.Input;
using OpenSpace.Engine.UI;
using SixLabors.ImageSharp;

namespace OpenSpace.Engine.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddEngine(this IServiceCollection services)
    {
        Configuration.Default.StreamProcessingBufferSize = 16384;
        Configuration.Default.PreferContiguousImageBuffers = true;
        
        services.AddSingleton<IApplicationContext, ApplicationContext>();
        services.AddSingleton<IMetrics, Metrics>();
        services.AddSingleton<ILimits, Limits>();
        services.AddSingleton<IInputProvider, InputProvider>();

        services.AddSingleton<IFramebufferCache, FramebufferCache>();
        services.AddSingleton<IShaderProgramFactory, ShaderProgramFactory>();
        services.AddSingleton<IShaderParser, ShaderParser>();
        services.AddSingleton<IShaderIncludeHandler, FileShaderIncludeHandler>();
        services.AddSingleton<IShaderIncludeHandler, VirtualFileShaderIncludeHandler>();

        services.AddSingleton<IGraphicsContext, GraphicsContext>();
        services.AddSingleton<IApplicationContext, ApplicationContext>();
        services.AddSingleton<IInputProvider, InputProvider>();
        services.AddSingleton<IMeshLoader, SharpGltfMeshLoader>();
        services.AddSingleton<IUIRenderer, UIRenderer>();
        
        services.AddSingleton<IModelLibrary, ModelLibrary>();
        services.AddSingleton<IMaterialLibrary, MaterialLibrary>();
        services.AddSingleton<ISamplerLibrary, SamplerLibrary>();

        services.AddSingleton<IImageLoader, SixLaborsImageLoader>();
    }
}