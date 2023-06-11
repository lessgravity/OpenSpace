using System;
using System.IO;
using EngineKit.Mathematics;
using EngineKit;
using EngineKit.Graphics;
using Serilog;

namespace OpenSpace.Renderers;

internal class GBufferPass : IDisposable
{
    private readonly ILogger _logger;
    private readonly IGraphicsContext _graphicsContext;
    private readonly IApplicationContext _applicationContext;

    public ITexture? GBufferAlbedoTexture;
    public TextureView? GBufferAlbedoTextureView;
    public ITexture? GBufferNormalTexture;
    public TextureView? GBufferNormalTextureView;
    public ITexture? GBufferMaterialTexture;
    public TextureView? GBufferMaterialTextureView;
    public ITexture? GBufferMotionTexture;
    public TextureView? GBufferMotionTextureView;
    public ITexture? GBufferEmissiveTexture;
    public TextureView? GBufferEmissiveTextureView;
    public ITexture? DepthBufferTexture;
    public TextureView? DepthBufferTextureView;
    private FramebufferDescriptor _gBufferPassDescriptor;
    private IGraphicsPipeline? _gBufferPassGraphicsPipeline;

    public GBufferPass(
        ILogger logger,
        IGraphicsContext graphicsContext,
        IApplicationContext applicationContext)
    {
        _logger = logger;
        _graphicsContext = graphicsContext;
        _applicationContext = applicationContext;
    }

    public void Dispose()
    {
        _gBufferPassGraphicsPipeline?.Dispose();
    }

    public void CreateResolutionDependentFramebuffers()
    {
        GBufferAlbedoTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R8G8B8A8Srgb, "GBuffer-Albedo-ColorAttachment");
        GBufferNormalTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R16G16B16A16Float, "GBuffer-Normal-ColorAttachment");
        GBufferMaterialTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R16G16B16A16Float, "GBuffer-Material-ColorAttachment");
        GBufferMotionTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R16G16B16A16Float, "GBuffer-Motion-ColorAttachment");
        GBufferEmissiveTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R16G16B16A16Float, "GBuffer-Emissive-ColorAttachment");
        DepthBufferTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.D32Float, "GBuffer-DepthAttachment");
        _gBufferPassDescriptor = new FramebufferDescriptorBuilder()
            .WithColorAttachment(GBufferAlbedoTexture, false, Color4.Black)
            .WithColorAttachment(GBufferNormalTexture, true, Color4.Black)
            .WithColorAttachment(GBufferMaterialTexture, true, Color4.Black)
            .WithColorAttachment(GBufferMotionTexture, true, Color4.Black)
            .WithColorAttachment(GBufferEmissiveTexture, true, Color4.Black)
            .WithDepthAttachment(DepthBufferTexture, true)
            .WithViewport(_applicationContext.ScaledFramebufferSize.X, _applicationContext.ScaledFramebufferSize.Y)
            .Build("GBuffer");

        GBufferAlbedoTextureView = GBufferAlbedoTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
        GBufferNormalTextureView = GBufferNormalTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
        GBufferMaterialTextureView = GBufferMaterialTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
        GBufferMotionTextureView = GBufferMotionTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
        GBufferEmissiveTextureView = GBufferEmissiveTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
        DepthBufferTextureView = DepthBufferTexture.CreateTextureView(SwizzleMapping.CreateForDepthTextures());
    }

    public void DestroyResolutionDependentFramebuffers()
    {
        GBufferAlbedoTexture?.Dispose();
        GBufferAlbedoTextureView?.Dispose();
        GBufferNormalTexture?.Dispose();
        GBufferNormalTextureView?.Dispose();
        GBufferMaterialTexture?.Dispose();
        GBufferMaterialTextureView?.Dispose();
        GBufferMotionTexture?.Dispose();
        GBufferMotionTextureView?.Dispose();
        GBufferEmissiveTexture?.Dispose();
        GBufferEmissiveTextureView?.Dispose();
        DepthBufferTexture?.Dispose();
        _graphicsContext.RemoveFramebuffer(_gBufferPassDescriptor);        
    }

    public bool ReloadPipeline(string sourcePath, string destinationPath)
    {
        const string sceneDeferredVertexShader = "Shaders/SceneDeferred.vs.glsl";
        const string sceneDeferredFragmentShader = "Shaders/SceneDeferred.fs.glsl";
        File.Copy(Path.Combine(sourcePath, sceneDeferredVertexShader),
            Path.Combine(destinationPath, sceneDeferredVertexShader), true);
        File.Copy(Path.Combine(sourcePath, sceneDeferredFragmentShader),
            Path.Combine(destinationPath, sceneDeferredFragmentShader), true);

        var gBufferGraphicsPipelineResult = _graphicsContext.CreateGraphicsPipelineBuilder()
            .WithShadersFromFiles(sceneDeferredVertexShader, sceneDeferredFragmentShader)
            .WithVertexInput(new VertexInputDescriptorBuilder()
                .AddAttribute(0, DataType.Float, 3, 0)
                .AddAttribute(0, DataType.Float, 3, 12)
                .AddAttribute(0, DataType.Float, 2, 24)
                .AddAttribute(0, DataType.Float, 4, 32)
                .Build(nameof(VertexPositionNormalUvTangent)))
            .WithTopology(PrimitiveTopology.Triangles)
            .WithFaceWinding(FaceWinding.CounterClockwise)
            .EnableCulling(CullMode.Back)
            .EnableDepthTest()
            .Build(nameof(GBufferPass));

        if (gBufferGraphicsPipelineResult.IsFailure)
        {
            _logger.Error("{Category}: Unable to build graphics pipeline {PipelineName}. {Details}",
                nameof(GBufferPass), gBufferGraphicsPipelineResult.Error);
            return false;
        }

        _gBufferPassGraphicsPipeline?.Dispose();
        _gBufferPassGraphicsPipeline = gBufferGraphicsPipelineResult.Value;
        _logger.Debug("{Category}: Pipeline built", _gBufferPassGraphicsPipeline.Label);

        return true;
    }

    public void Render(
        ref IMeshPool meshPool,
        ref IMaterialPool materialPool,
        ref IUniformBuffer cameraInformationBuffer,
        ref IShaderStorageBuffer instanceBuffer,
        ref IIndirectBuffer indirectBuffer,
        int meshInstancesCount)
    {
        _graphicsContext.BindGraphicsPipeline(_gBufferPassGraphicsPipeline!);

        _graphicsContext.BeginRenderToFramebuffer(_gBufferPassDescriptor);
        _gBufferPassGraphicsPipeline.BindVertexBuffer(meshPool.VertexBuffer, 0, 0);
        _gBufferPassGraphicsPipeline.BindIndexBuffer(meshPool.IndexBuffer);
        _gBufferPassGraphicsPipeline.BindUniformBuffer(cameraInformationBuffer, 0);
        _gBufferPassGraphicsPipeline.BindShaderStorageBuffer(instanceBuffer, 1);
        _gBufferPassGraphicsPipeline.BindShaderStorageBuffer(materialPool.MaterialBuffer, 2);
        _gBufferPassGraphicsPipeline.MultiDrawElementsIndirect(indirectBuffer, meshInstancesCount);
        _graphicsContext.EndRender();
    }
}