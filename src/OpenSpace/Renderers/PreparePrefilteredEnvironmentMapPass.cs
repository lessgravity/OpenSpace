using System;
using EngineKit.Mathematics;
using EngineKit.Graphics;
using Serilog;

namespace OpenSpace.Renderers;

internal class PreparePrefilteredEnvironmentMapPass : IRenderPass
{
    private readonly ILogger _logger;
    private readonly IGraphicsContext _graphicsContext;
    private ITexture? _environmentCubeTexture;
    private ISampler? _environmentCubeTextureSampler;
    private TextureCreateDescriptor _prefilteredCubeTextureCreateDescriptor;

    private IComputePipeline? _prefilterComputePipeline;

    public PreparePrefilteredEnvironmentMapPass(ILogger logger, IGraphicsContext graphicsContext)
    {
        _logger = logger.ForContext<PreparePrefilteredEnvironmentMapPass>();
        _graphicsContext = graphicsContext;
    }

    public ITexture? PrefilteredCubeTexture { get; private set; }
    
    public uint PrefilteredCubeTextureMipLevels { get; private set; }

    public void Dispose()
    {
        PrefilteredCubeTexture?.Dispose();
        _prefilterComputePipeline?.Dispose();
        _environmentCubeTextureSampler?.Dispose();
    }

    public bool Load(int prefilterSize, ITexture environmentMapTexture, string skyboxName)
    {
        _environmentCubeTexture = environmentMapTexture;
        _environmentCubeTextureSampler = _graphicsContext
            .CreateSamplerBuilder()
            .WithAddressMode(TextureAddressMode.MirroredRepeat)
            .WithInterpolationFilter(TextureInterpolationFilter.Linear)
            .WithMipmapFilter(TextureMipmapFilter.LinearMipmapLinear)
            .Build("Sampler-EnvironmentCubeTexture");

        const Format format = Format.R16G16B16A16Float;
        _prefilteredCubeTextureCreateDescriptor = new TextureCreateDescriptor
        {
            ImageType = ImageType.TextureCube,
            Format = format,
            Label = $"TextureCube-{prefilterSize}x{prefilterSize}-{format}-{skyboxName}-Prefiltered",
            Size = new Int3(prefilterSize, prefilterSize, 1),
            MipLevels = (uint)(1 + MathF.Ceiling(MathF.Log2(prefilterSize))),
            TextureSampleCount = TextureSampleCount.OneSample
        };
        PrefilteredCubeTexture = _graphicsContext.CreateTexture(_prefilteredCubeTextureCreateDescriptor);
        PrefilteredCubeTextureMipLevels = _prefilteredCubeTextureCreateDescriptor.MipLevels;
        
        var prefilterComputePipelineResult = _graphicsContext.CreateComputePipelineBuilder()
            .WithShaderFromFile("Shaders/Skybox.Prefilter.cs.glsl")
            .Build("Skybox-Prefilter-Pass");
        if (prefilterComputePipelineResult.IsFailure)
        {
            _logger.Error("{Category}: Unable to compile compute pipeline. {Details}", "Renderer",
                prefilterComputePipelineResult.Error);
            return false;
        }

        _prefilterComputePipeline = prefilterComputePipelineResult.Value;
        
        return true;
    }

    public void Render()
    {
        if (_prefilterComputePipeline == null ||
            _environmentCubeTexture == null ||
            _environmentCubeTextureSampler == null ||
            PrefilteredCubeTexture == null)
        {
            return;
        }
        
        _graphicsContext.BindComputePipeline(_prefilterComputePipeline);

        _prefilterComputePipeline.BindSampledTexture(_environmentCubeTextureSampler, _environmentCubeTexture, 0);

        var numGroups = (uint)(_prefilteredCubeTextureCreateDescriptor.Size.X / 32f);
        _prefilterComputePipeline.BindImage(
            PrefilteredCubeTexture,
            0,
            0,
            0,
            MemoryAccess.WriteOnly,
            PrefilteredCubeTexture.TextureCreateDescriptor.Format);
        _prefilterComputePipeline.Uniform(0, 0);
        _prefilterComputePipeline.Dispatch(numGroups, numGroups, 6);

        var mipLevels = _prefilteredCubeTextureCreateDescriptor.MipLevels;
        var deltaRoughness = 1.0f / MathF.Max(mipLevels - 1, 1.0f);
        for (int level = 1, size = _prefilteredCubeTextureCreateDescriptor.Size.X / 2; level <= mipLevels; ++level, size /= 2)
        {
            numGroups = (uint)MathF.Max(1, size / 32f);

            _prefilterComputePipeline.BindImage(
                PrefilteredCubeTexture,
                0,
                level,
                0,
                MemoryAccess.WriteOnly,
                PrefilteredCubeTexture.TextureCreateDescriptor.Format);
            _prefilterComputePipeline.Uniform(0, level * deltaRoughness);
            _prefilterComputePipeline.Dispatch(numGroups, numGroups, 6);
        }
    }
}