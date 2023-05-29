using System;
using lessGravity.Mathematics;
using OpenSpace.Engine.Graphics;
using Serilog;

namespace OpenSpace.Renderers;

internal class CreateIrradianceMapPass : IDisposable
{
    private readonly ILogger _logger;
    private readonly IGraphicsContext _graphicsContext;
    private Vector2 _dimension;
    private ITexture? _environmentCubeTexture;
    private IComputePipeline? _computeIrradianceComputePipeline;

    public CreateIrradianceMapPass(ILogger logger, IGraphicsContext graphicsContext)
    {
        _logger = logger.ForContext<CreateIrradianceMapPass>();
        _graphicsContext = graphicsContext;
    }

    public ITexture? IrradianceCubeTexture { get; private set; }

    public void Dispose()
    {
        IrradianceCubeTexture?.Dispose();
        _computeIrradianceComputePipeline?.Dispose();
    }

    public bool Load(int irradianceSize, ITexture environmentCubeTexture, string skyboxName)
    {
        _dimension = new Vector2(irradianceSize);
        _environmentCubeTexture = environmentCubeTexture;

        var format = Format.R16G16B16A16Float;
        var irradianceTextureCreateDescriptor = new TextureCreateDescriptor
        {
            ImageType = ImageType.TextureCube,
            Format = format,
            Label = $"TextureCube-{_dimension.X}x{_dimension.X}-{format}-{skyboxName}-Irradiance",
            Size = new Int3((int)_dimension.X, (int)_dimension.Y, 1),
            MipLevels = (uint)(1 + MathF.Ceiling(MathF.Log2(_dimension.X))),
            TextureSampleCount = TextureSampleCount.OneSample
        };
        IrradianceCubeTexture = _graphicsContext.CreateTexture(irradianceTextureCreateDescriptor);
        
        var computeIrradianceComputePipelineResult = _graphicsContext.CreateComputePipelineBuilder()
            .WithShaderFromFile("Shaders/Skybox.Irradiance.cs.glsl")
            .Build("Skybox-Irradiance-Pass");
        if (computeIrradianceComputePipelineResult.IsFailure)
        {
            _logger.Error("{Category}: Unable to compile compute pipeline. {Details}", "Renderer",
                computeIrradianceComputePipelineResult.Error);
            return false;
        }

        _computeIrradianceComputePipeline = computeIrradianceComputePipelineResult.Value;
        
        return true;
    }

    public void Render()
    {
        if (_computeIrradianceComputePipeline == null || _environmentCubeTexture == null || IrradianceCubeTexture == null)
        {
            return;
        }

        var groupX = (uint)(_dimension.X / 32);
        var groupY = (uint)(_dimension.Y / 32);
        var groupZ = 6u;

        _graphicsContext.BindComputePipeline(_computeIrradianceComputePipeline);

        _computeIrradianceComputePipeline.BindTexture(_environmentCubeTexture, 0);
        _computeIrradianceComputePipeline.BindImage(
            IrradianceCubeTexture,
            0,
            0,
            0,
            MemoryAccess.WriteOnly,
            IrradianceCubeTexture.TextureCreateDescriptor.Format);
        _computeIrradianceComputePipeline.Dispatch(groupX, groupY, groupZ);
        //_graphicsContext.InsertMemoryBarrier(BarrierMask.ShaderImageAccess);

        //IrradianceCubeTexture.GenerateMipmaps();
    }
}