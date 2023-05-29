namespace OpenSpace.Renderers;

public readonly struct ImageBasedLightingPassOptions
{
    public ImageBasedLightingPassOptions(
        int irradianceSize,
        int prefilterSize,
        int brdfIntegrationLutDimension,
        string skyboxName)
    {
        SkyboxName = skyboxName;
        IrradianceSize = irradianceSize;
        PrefilterSize = prefilterSize;
        BrdfIntegrationLutDimension = brdfIntegrationLutDimension;
    }

    public readonly int IrradianceSize;

    public readonly int PrefilterSize;
    
    public readonly int BrdfIntegrationLutDimension;

    public readonly string SkyboxName;
}