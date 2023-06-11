using EngineKit.Graphics;
using Serilog;

namespace OpenSpace.Renderers;

internal class PrepareImageBasedLightingPass : IRenderPass
{
    private readonly IGraphicsContext _graphicsContext;
    private readonly PrepareBrdfIntegrationLookupTablePass _prepareBrdfIntegrationLookupTablePass;
    private readonly PreparePrefilteredEnvironmentMapPass _preparePrefilteredEnvironmentMapPass;
    private readonly PrepareIrradianceMapPass _prepareIrradianceMapPass;

    public PrepareImageBasedLightingPass(ILogger logger, IGraphicsContext graphicsContext)
    {
        _graphicsContext = graphicsContext;
        _prepareBrdfIntegrationLookupTablePass = new PrepareBrdfIntegrationLookupTablePass(logger, graphicsContext);
        _preparePrefilteredEnvironmentMapPass = new PreparePrefilteredEnvironmentMapPass(logger, graphicsContext);
        _prepareIrradianceMapPass = new PrepareIrradianceMapPass(logger, graphicsContext);
    }

    public ITexture? BrdfIntegrationLutTexture => _prepareBrdfIntegrationLookupTablePass.BrdfIntegrationLutTexture;

    public ITexture? PrefilteredCubeTexture => _preparePrefilteredEnvironmentMapPass.PrefilteredCubeTexture;

    public uint PrefilteredCubeTextureMipLevels => _preparePrefilteredEnvironmentMapPass.PrefilteredCubeTextureMipLevels;

    public ITexture? IrradianceCubeTexture => _prepareIrradianceMapPass.IrradianceCubeTexture;

    public ITexture? EnvironmentCubeTexture { get; private set; }

    public bool Load(ImageBasedLightingPassOptions imageBasedLightingPassOptions)
    {
        EnvironmentCubeTexture = LoadSkybox(imageBasedLightingPassOptions.SkyboxName);
        if (EnvironmentCubeTexture == null)
        {
            return false;
        }
        
        if (!_prepareBrdfIntegrationLookupTablePass.Load(imageBasedLightingPassOptions.BrdfIntegrationLutDimension))
        {
            return false;
        }

        if (!_preparePrefilteredEnvironmentMapPass.Load(
                imageBasedLightingPassOptions.PrefilterSize,
                EnvironmentCubeTexture,
                imageBasedLightingPassOptions.SkyboxName))
        {
            return false;
        }

        if (!_prepareIrradianceMapPass.Load(
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
        _prepareBrdfIntegrationLookupTablePass?.Dispose();
        _preparePrefilteredEnvironmentMapPass?.Dispose();
        _prepareIrradianceMapPass?.Dispose();
        EnvironmentCubeTexture?.Dispose();
    }

    public void Render()
    {
        _prepareBrdfIntegrationLookupTablePass.Render();
        _preparePrefilteredEnvironmentMapPass.Render();
        _prepareIrradianceMapPass.Render();
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