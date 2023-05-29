using OpenSpace.Engine.Graphics;
using Serilog;

namespace OpenSpace.Renderers;

internal class PrepareImageBasedLightingPass : IRenderPass
{
    private readonly IGraphicsContext _graphicsContext;
    private readonly CreateBrdfIntegrationLookupTablePass _createBrdfIntegrationLookupTablePass;
    private readonly CreatePrefilteredEnvironmentMapPass _createPrefilteredEnvironmentMapPass;
    private readonly CreateIrradianceMapPass _createIrradianceMapPass;

    public PrepareImageBasedLightingPass(ILogger logger, IGraphicsContext graphicsContext)
    {
        _graphicsContext = graphicsContext;
        _createBrdfIntegrationLookupTablePass = new CreateBrdfIntegrationLookupTablePass(logger, graphicsContext);
        _createPrefilteredEnvironmentMapPass = new CreatePrefilteredEnvironmentMapPass(logger, graphicsContext);
        _createIrradianceMapPass = new CreateIrradianceMapPass(logger, graphicsContext);
    }

    public ITexture? BrdfIntegrationLutTexture => _createBrdfIntegrationLookupTablePass.BrdfIntegrationLutTexture;

    public ITexture? PrefilteredCubeTexture => _createPrefilteredEnvironmentMapPass.PrefilteredCubeTexture;

    public ITexture? IrradianceCubeTexture => _createIrradianceMapPass.IrradianceCubeTexture;

    public ITexture? EnvironmentCubeTexture { get; private set; }

    public bool Load(ImageBasedLightingPassOptions imageBasedLightingPassOptions)
    {
        EnvironmentCubeTexture = LoadSkybox(imageBasedLightingPassOptions.SkyboxName);
        if (EnvironmentCubeTexture == null)
        {
            return false;
        }
        
        if (!_createBrdfIntegrationLookupTablePass.Load(imageBasedLightingPassOptions.BrdfIntegrationLutDimension))
        {
            return false;
        }

        if (!_createPrefilteredEnvironmentMapPass.Load(
                imageBasedLightingPassOptions.PrefilterSize,
                EnvironmentCubeTexture,
                imageBasedLightingPassOptions.SkyboxName))
        {
            return false;
        }

        if (!_createIrradianceMapPass.Load(
                imageBasedLightingPassOptions.IrradianceSize / 4,
                EnvironmentCubeTexture,
                imageBasedLightingPassOptions.SkyboxName))
        {
            return false;
        }
        
        return true;
    }

    public void Dispose()
    {
        _createBrdfIntegrationLookupTablePass?.Dispose();
        _createPrefilteredEnvironmentMapPass?.Dispose();
        _createIrradianceMapPass?.Dispose();
        EnvironmentCubeTexture?.Dispose();
    }

    public void Render()
    {
        _createBrdfIntegrationLookupTablePass.Render();
        _createPrefilteredEnvironmentMapPass.Render();
        _createIrradianceMapPass.Render();
    }
    
    private ITexture? LoadSkybox(string skyboxName)
    {
        var skyboxFileNames = new[]
        {
            $"Data/Sky/TC_{skyboxName}_Xp.png",
            $"Data/Sky/TC_{skyboxName}_Xn.png",
            $"Data/Sky/TC_{skyboxName}_Yp.png",
            $"Data/Sky/TC_{skyboxName}_Yn.png",            
            $"Data/Sky/TC_{skyboxName}_Zp.png",
            $"Data/Sky/TC_{skyboxName}_Zn.png"
        };

        return _graphicsContext.CreateTextureCubeFromFiles(skyboxName, skyboxFileNames, false, false);
    }    
}