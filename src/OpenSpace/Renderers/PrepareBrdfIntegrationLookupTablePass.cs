using EngineKit.Mathematics;
using EngineKit.Graphics;
using Serilog;

namespace OpenSpace.Renderers;

internal class PrepareBrdfIntegrationLookupTablePass : IRenderPass
{
    private readonly ILogger _logger;
    private readonly IGraphicsContext _graphicsContext;
    private IGraphicsPipeline? _brdfIntegrationLutGraphicsPipeline;
    private FramebufferDescriptor _framebufferDescriptor;
    
    public PrepareBrdfIntegrationLookupTablePass(ILogger logger, IGraphicsContext graphicsContext)
    {
        _logger = logger.ForContext<PrepareBrdfIntegrationLookupTablePass>();
        _graphicsContext = graphicsContext;
    }

    public ITexture? BrdfIntegrationLutTexture { get; private set; }

    public bool Load(int dimension)
    {
        _logger.Debug("{Category}: Generating BRDF Lookup Table", "Renderer");

        var brdfIntegrationLutTextureCreateDescriptor = new TextureCreateDescriptor
        {
            ImageType = ImageType.Texture2D,
            Format = Format.R16G16Float,
            Label = $"Texture-{dimension}x{dimension}-{Format.R16G16Float}-Lut-Brdf",
            Size = new Int3(dimension, dimension, 1),
            MipLevels = 1,
            TextureSampleCount = TextureSampleCount.OneSample
        };
        BrdfIntegrationLutTexture = _graphicsContext.CreateTexture(brdfIntegrationLutTextureCreateDescriptor);
        
        var brdfIntegrationLutGraphicsPipelineResult = _graphicsContext
            .CreateGraphicsPipelineBuilder()
            .WithShadersFromFiles("Shaders/FST.vs.glsl", "Shaders/Lut.Brdf.fs.glsl")
            .WithVertexInput(new VertexInputDescriptorBuilder()
                .AddAttribute(0, DataType.Float, 3, 0)
                .Build(nameof(VertexPosition)))
            .DisableDepthTest()
            .WithTopology(PrimitiveTopology.Triangles)
            .Build("Create-Brdf-Integration-Lut-Pass");
        if (brdfIntegrationLutGraphicsPipelineResult.IsFailure)
        {
            _logger.Error("{Category}: Unable to compile graphics pipeline. {Details}", "Renderer",
                brdfIntegrationLutGraphicsPipelineResult.Error);
            return false;
        }
        
        _framebufferDescriptor = new FramebufferDescriptorBuilder()
            .WithViewport(dimension, dimension)
            .WithColorAttachment(BrdfIntegrationLutTexture, true, Vector4.Zero)
            .Build("Framebuffer-Create-Brdf-Integration-Lut");        

        _brdfIntegrationLutGraphicsPipeline = brdfIntegrationLutGraphicsPipelineResult.Value;
        
        return true;
    }

    public void Render()
    {
        if (_brdfIntegrationLutGraphicsPipeline == null)
        {
            return;
        }
        _graphicsContext.BindGraphicsPipeline(_brdfIntegrationLutGraphicsPipeline);
        _graphicsContext.BeginRenderToFramebuffer(_framebufferDescriptor);
        _brdfIntegrationLutGraphicsPipeline.DrawArrays(3);
        _graphicsContext.EndRender();
    }

    public void Dispose()
    {
        _brdfIntegrationLutGraphicsPipeline?.Dispose();
        BrdfIntegrationLutTexture?.Dispose();
        _graphicsContext.RemoveFramebuffer(_framebufferDescriptor);
    }
}