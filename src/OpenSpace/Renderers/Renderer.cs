using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ImGuiNET;
using lessGravity.Mathematics;
using lessGravity.Native.OpenGL;
using OpenSpace.Engine;
using OpenSpace.Engine.Graphics;
using OpenSpace.Game;
using OpenSpace.Messages;
using Serilog;
using Num = System.Numerics;
using Vector2 = lessGravity.Mathematics.Vector2;
using Vector3 = lessGravity.Mathematics.Vector3;
using Vector4 = lessGravity.Mathematics.Vector4;

namespace OpenSpace.Renderers;

internal class Renderer : IRenderer
{
    private float _shadowSettingBias1 = 0.001f;
    private float _shadowSettingBias2 = 0.0015f;
    private float _shadowSettingRMax = 0.005f;
    private int _shadowSettingSamples = 8;
    
    private readonly ILogger _logger;
    private readonly IApplicationContext _applicationContext;
    private readonly IGraphicsContext _graphicsContext;
    private readonly IMeshLoader _meshLoader;
    private readonly ISamplerLibrary _samplerLibrary;

    private ITexture? _blueNoiseTexture;

    private ISampler? _nearestSampler;
    private ISampler? _linearMipmapLinearRepeatSampler;
    private ISampler? _linearMipmapLinearClampedSampler;
    
    private SwapchainDescriptor _swapchainDescriptor;

    private ITexture? _gBufferAlbedoTexture;
    private TextureView? _gBufferAlbedoTextureView;
    private ITexture? _gBufferNormalTexture;
    private TextureView? _gBufferNormalTextureView;
    private ITexture? _gBufferMaterialTexture;
    private TextureView? _gBufferMaterialTextureView;
    private ITexture? _gBufferMotionTexture;
    private TextureView? _gBufferMotionTextureView;
    private ITexture? _gBufferEmissiveTexture;
    private TextureView? _gBufferEmissiveTextureView;
    private ITexture? _depthBufferTexture;
    private TextureView? _depthBufferTextureView;
    private FramebufferDescriptor _gBufferPassDescriptor;
    private IGraphicsPipeline? _gBufferPassGraphicsPipeline;

    private ITexture? _finalTexture;
    private TextureView? _finalTextureView;
    private FramebufferDescriptor _finalPassDescriptor;
    private IGraphicsPipeline? _finalPassGraphicsPipeline;

    private ITexture? _lightsTexture;
    private TextureView? _lightsTextureView;
    private FramebufferDescriptor _lightsPassDescriptor;
    private IGraphicsPipeline? _lightsGlobalPassGraphicsPipeline;
    private IGraphicsPipeline? _lightsLocalPassGraphicsPipeline;

    private IGraphicsPipeline? _shadowPassGraphicsPipeline; 
    
    private IUniformBuffer? _cameraInformationBuffer;
    private GpuCameraInformation _cameraInformation;

    private IIndirectBuffer? _indirectBuffer;
    private IShaderStorageBuffer? _instanceBuffer;
    private readonly IList<MeshInstance> _meshInstances;

    private readonly IList<GlobalLight> _globalLights;
    private bool _updateGlobalLights;
    private IShaderStorageBuffer? _globalLightsBuffer;

    private readonly IList<GpuLocalLight> _localLights;
    private bool _updateLocalLights;
    private IShaderStorageBuffer? _localLightsBuffer;

    private GpuShadowSettings _shadowSettings;
    private IUniformBuffer? _shadowSettingsBuffer;

    private bool _isDisposed;
    private IMeshPool? _meshPool;
    private IMaterialPool? _materialPool;
    private bool _updateGeometryBuffers;

    private PooledMesh _pointLightVolumePooledMesh;
    private PooledMesh _spotLightVolumePooledMesh;
    
    // DEBUG START
    private int _selectedTextureToBeRendered = -1;
    
    // tonemapping - uchimura
    private float _uchimuraMaxDisplayBrightness = 1.0f;
    private float _uchimuraContrast = 1.0f;
    private float _uchimuraLinearSectionStart = 0.22f;
    private float _uchimuraLinearSectionLength = 0.4f;
    private float _uchimuraBlack = 1.33f;
    private float _uchimuraPedestal = 0.0f;
    private float _gamma = 2.2f;

    private IUniformBuffer? _uchimuraSettingsBuffer;
    private GpuUchimuraSettings _uchimuraSettings;
    private readonly float[] _tonemapSamples;
    
    // DEBUG END

    private ProgramMode _programMode;

    private PrepareImageBasedLightingPass _preparePrepareImageBasedLightingPass;

    public Renderer(
        ILogger logger,
        IApplicationContext applicationContext,
        IGraphicsContext graphicsContext,
        IMeshLoader meshLoader,
        ISamplerLibrary samplerLibrary,
        IMessageBus messageBus)
    {
        _logger = logger;
        _applicationContext = applicationContext;
        _graphicsContext = graphicsContext;
        _meshLoader = meshLoader;
        _samplerLibrary = samplerLibrary;

        _cameraInformation = new GpuCameraInformation();
        _meshInstances = new List<MeshInstance>(2048);

        _globalLights = new List<GlobalLight>();
        _localLights = new List<GpuLocalLight>();
        
        _shadowSettings = new GpuShadowSettings();
        _tonemapSamples = new float[100];
        
        messageBus.Subscribe<SwitchedToGameModeMessage>(SwitchedToGameMode);
        messageBus.Subscribe<SwitchedToEditorModeMessage>(SwitchedToEditorMode);

        _preparePrepareImageBasedLightingPass = new PrepareImageBasedLightingPass(logger, graphicsContext);
    }

    public int SelectedTextureToBeRendered => _selectedTextureToBeRendered;

    public TextureView? FinalTexture => _finalTextureView;

    public TextureView? DepthTexture => _depthBufferTextureView;

    public TextureView? GBufferAlbedoTexture => _gBufferAlbedoTextureView;

    public TextureView? GBufferNormalsTexture => _gBufferNormalTextureView;

    public TextureView? GBufferMaterialTexture => _gBufferMaterialTextureView;

    public TextureView? GBufferMotionTexture => _gBufferMotionTextureView;

    public TextureView? GBufferEmissiveTexture => _gBufferEmissiveTextureView;

    public TextureView? LightsTexture => _lightsTextureView;

    public ITexture? FirstGlobalLightShadowMap => _preparePrepareImageBasedLightingPass.EnvironmentCubeTexture;//_globalLights[0].ShadowMapTexture;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        DestroyFramebuffers();
        
        _blueNoiseTexture?.Dispose();

        _nearestSampler?.Dispose();
        _linearMipmapLinearRepeatSampler?.Dispose();
        _linearMipmapLinearClampedSampler?.Dispose();
        
        _gBufferPassGraphicsPipeline?.Dispose();
        _finalPassGraphicsPipeline?.Dispose();

        _shadowSettingsBuffer?.Dispose();
        _localLightsBuffer?.Dispose();
        _globalLightsBuffer?.Dispose();
        
        _cameraInformationBuffer?.Dispose();
        _indirectBuffer?.Dispose();
        _instanceBuffer?.Dispose();
        
        _materialPool?.Dispose();
        _meshPool?.Dispose();
        
        _preparePrepareImageBasedLightingPass?.Dispose();
        
        _shadowPassGraphicsPipeline?.Dispose();
        
        _uchimuraSettingsBuffer?.Dispose();
        
        _isDisposed = true;
    }

    public void AddMeshInstance(MeshInstance meshInstance)
    {
        _meshInstances.Add(meshInstance);
        _updateGeometryBuffers = true;
    }

    public void AddDirectionalLight(
        Vector3 direction,
        Vector3 color,
        float intensity,
        Vector2 dimensions,
        float near,
        float far,
        bool isShadowCaster,
        int shadowQuality)
    {
        var globalLight = new GlobalLight
        {
            Direction = direction, 
            Color = color,
            Dimensions = dimensions,
            Near = near,
            Far = far,
            IsShadowCaster = isShadowCaster,
            ShadowQuality = shadowQuality,
            Intensity = intensity
        };
        
        CreateShadowMap(globalLight, 1024, 1024);
        
        _globalLights.Add(globalLight);
        _updateGlobalLights = true;
    }

    public void AddPointLight(
        Vector3 position,
        Vector3 color,
        float intensity)
    {
        _localLights.Add(GpuLocalLight.CreatePointLight(position, color, intensity));
        _updateLocalLights = true;
    }

    public void AddSpotLight(
        Vector3 position,
        Vector3 direction,
        Vector3 color,
        float intensity,
        float range,
        float cutOffAngle,
        float outerCutOffAngle)
    {
        _localLights.Add(GpuLocalLight.CreateSpotLight(position, color, direction, intensity, range, cutOffAngle, outerCutOffAngle));
        _updateLocalLights = true;
    }

    public void ClearMeshInstances()
    {
        _meshInstances.Clear();
    }

    public bool Load()
    {
        if (!ReloadPipelines("../../../../OpenSpace.Assets"))
        {
            return false;
        }

        CreateFramebuffers();
        CreateSwapchain();

        _nearestSampler = _graphicsContext
            .CreateSamplerBuilder()
            .WithAddressMode(TextureAddressMode.ClampToBorder)
            .WithInterpolationFilter(TextureInterpolationFilter.Nearest)
            .WithMipmapFilter(TextureMipmapFilter.Nearest)
            .Build("Nearest-ClampToBorder");

        _linearMipmapLinearRepeatSampler = _graphicsContext
            .CreateSamplerBuilder()
            .WithAddressMode(TextureAddressMode.Repeat)
            .WithInterpolationFilter(TextureInterpolationFilter.Linear)
            .WithMipmapFilter(TextureMipmapFilter.LinearMipmapLinear)
            .Build("LinearMipmapLinear-Repeat");
        
        _linearMipmapLinearClampedSampler = _graphicsContext
            .CreateSamplerBuilder()
            .WithAddressMode(TextureAddressMode.ClampToEdge)
            .WithInterpolationFilter(TextureInterpolationFilter.Linear)
            .WithMipmapFilter(TextureMipmapFilter.LinearMipmapLinear)
            .Build("LinearMipmapLinear-Clamped");

        _meshPool = _graphicsContext.CreateMeshPool("GeometryMeshPool", 1024 * 1024 * 1024, 1024 * 1024 * 1024);
        _materialPool = _graphicsContext.CreateMaterialPool("MaterialsMeshPool", 128 * 1024, _samplerLibrary);
        
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var pointLightVolumeMeshPrimitives =
            _meshLoader.LoadMeshPrimitivesFromFile(Path.Combine(baseDirectory, "Data/Default/SM_Light_Point.gltf"));
        var spotLightVolumeMeshPrimitives =
            _meshLoader.LoadMeshPrimitivesFromFile(Path.Combine(baseDirectory, "Data/Default/SM_Light_Spot.gltf"));

        _spotLightVolumePooledMesh = GetOrAddMeshPrimitive(spotLightVolumeMeshPrimitives.First());
        _pointLightVolumePooledMesh = GetOrAddMeshPrimitive(pointLightVolumeMeshPrimitives.First());
        
        _cameraInformationBuffer = _graphicsContext.CreateUniformBuffer<GpuCameraInformation>("CameraInformation");
        _cameraInformationBuffer.AllocateStorage(Marshal.SizeOf<GpuCameraInformation>(), StorageAllocationFlags.Dynamic);

        _instanceBuffer = _graphicsContext.CreateShaderStorageBuffer<GpuMeshInstance>("SceneInstances");
        _instanceBuffer.AllocateStorage(4096 * Marshal.SizeOf<GpuMeshInstance>(), StorageAllocationFlags.Dynamic);

        _indirectBuffer = _graphicsContext.CreateIndirectBuffer("SceneIndirects");
        _indirectBuffer.AllocateStorage(4096 * Marshal.SizeOf<GpuIndirectElementData>(), StorageAllocationFlags.Dynamic);

        _localLightsBuffer = _graphicsContext.CreateShaderStorageBuffer<GpuLocalLight>("LocalLights");
        _localLightsBuffer.AllocateStorage(32 * Marshal.SizeOf<GpuLocalLight>(), StorageAllocationFlags.Dynamic);

        _globalLightsBuffer = _graphicsContext.CreateShaderStorageBuffer<GpuGlobalLight>("GlobalLights");
        _globalLightsBuffer.AllocateStorage(4 * Marshal.SizeOf<GpuGlobalLight>(), StorageAllocationFlags.Dynamic);

        _shadowSettingsBuffer = _graphicsContext.CreateUniformBuffer<GpuShadowSettings>("ShadowSettings");
        _shadowSettingsBuffer.AllocateStorage(Marshal.SizeOf<GpuShadowSettings>(), StorageAllocationFlags.Dynamic);

        _uchimuraSettingsBuffer = _graphicsContext.CreateUniformBuffer<GpuUchimuraSettings>("Tonemapping-Ochimura-Settings");
        _uchimuraSettingsBuffer.AllocateStorage(Marshal.SizeOf<GpuUchimuraSettings>(), StorageAllocationFlags.Dynamic);

        _shadowSettings.Bias1 = 0.02f;
        _shadowSettings.Bias2 = 0.0015f;
        _shadowSettings.RMax = 0.005f;
        _shadowSettings.AccumFactor = 1.0f;
        _shadowSettings.Samples = 4;
        _shadowSettings.RandomOffset = 10000;
        _shadowSettingsBuffer.Update(_shadowSettings);
        
        _uchimuraSettings.MaxDisplayBrightness = _uchimuraMaxDisplayBrightness;
        _uchimuraSettings.Contrast = _uchimuraContrast;
        _uchimuraSettings.LinearSectionStart = _uchimuraLinearSectionStart;
        _uchimuraSettings.LinearSectionLength = _uchimuraLinearSectionLength;
        _uchimuraSettings.Black = _uchimuraBlack;
        _uchimuraSettings.Pedestal = _uchimuraPedestal;
        _uchimuraSettings.Gamma = _gamma;
        _uchimuraSettingsBuffer.Update(_uchimuraSettings);

        _blueNoiseTexture = _graphicsContext.CreateTextureFromFile("Data/Default/T_Bluenoise256.png", Format.R8G8B8A8UNorm, true);

        var imageBasedLightingPassOptions = new ImageBasedLightingPassOptions(32, 128, 256, "Miramar");
        if (!_preparePrepareImageBasedLightingPass.Load(imageBasedLightingPassOptions))
        {
            return false;
        }
        
        _preparePrepareImageBasedLightingPass.Render();
        
        GL.Enable(GL.EnableType.TextureCubemapSeamless);
        
        return true;
    }

    public PooledMesh GetOrAddMeshPrimitive(MeshPrimitive meshPrimitive)
    {
        return _meshPool!.GetOrAdd(meshPrimitive);
    }

    public PooledMaterial GetOrAddMaterial(Material material)
    {
        return _materialPool!.GetOrAdd(material);
    }

    public void RenderWorld(ICamera camera)
    {
        if (_cameraInformationBuffer == null || _uchimuraSettingsBuffer == null)
        {
            return;
        }
        
        _cameraInformation.ProjectionMatrix = camera.ProjectionMatrix;
        _cameraInformation.ViewMatrix = camera.ViewMatrix;
        _cameraInformation.Viewport = new Vector4(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y, 
            0, 
            0);
        _cameraInformation.CameraPositionAndFieldOfView = new Vector4(camera.Position, MathHelper.ToRadians(camera.FieldOfView));
        _cameraInformation.CameraDirectionAndAspectRatio = new Vector4(camera.Direction, camera.AspectRatio);
        _cameraInformationBuffer.Update(_cameraInformation);

        UpdateGeometryBuffersIfNecessary();
        
        RenderPassGBuffer();

        RenderLights(camera);
        
        RenderPassFinal(camera);
        
        GL.PushDebugGroup("DrawToSwapChain");
        _graphicsContext.BeginRenderToSwapchain(_swapchainDescriptor);
        if (_programMode == ProgramMode.Game)
        {
            _graphicsContext.BlitFramebufferToSwapchain(
                _applicationContext.ScaledFramebufferSize.X,
                _applicationContext.ScaledFramebufferSize.Y,
                _applicationContext.FramebufferSize.X,
                _applicationContext.FramebufferSize.Y);
        }
    }

    public void FramebufferResized()
    {
        DestroyFramebuffers();
        CreateFramebuffers();
        CreateSwapchain();
    }

    public void RenderDebugUI(ICamera camera)
    {
        if (ImGui.Begin("Debug"))
        {
            ImGui.TextUnformatted($"Camera: {camera.Position}");
            ImGui.BeginGroup();
            {
                ImGui.TextUnformatted($"Which {_selectedTextureToBeRendered}");
                ImGui.Separator();
                ImGui.RadioButton("Final", ref _selectedTextureToBeRendered, -1);
                ImGui.Separator();
                ImGui.RadioButton("Depth", ref _selectedTextureToBeRendered, 0);
                ImGui.RadioButton("Albedo", ref _selectedTextureToBeRendered, 1);
                ImGui.RadioButton("Normals", ref _selectedTextureToBeRendered, 2);
                ImGui.RadioButton("Material", ref _selectedTextureToBeRendered, 3);
                ImGui.RadioButton("Motion", ref _selectedTextureToBeRendered, 4);
                ImGui.RadioButton("Emissive", ref _selectedTextureToBeRendered, 5);
                ImGui.Separator();
                ImGui.RadioButton("Light", ref _selectedTextureToBeRendered, 6);

                ImGui.EndGroup();
            }

            ImGui.BeginGroup();
            {
                ImGui.SliderFloat("Linear Bias", ref _shadowSettingBias1, 0.00001f, 0.0001f, "%.06f");
                ImGui.SliderFloat("Constant Bias", ref _shadowSettingBias2, 0.00001f, 0.001f, "%.05f");
                ImGui.SliderFloat("rMax", ref _shadowSettingRMax, 0.0001f, 0.005f, "%.05f");
                ImGui.SliderInt("Samples", ref _shadowSettingSamples, 1, 32);

                _shadowSettings.Bias1 = _shadowSettingBias1;
                _shadowSettings.Bias2 = _shadowSettingBias2;
                _shadowSettings.RMax = _shadowSettingRMax;
                _shadowSettings.AccumFactor = 1.0f;
                _shadowSettings.Samples = _shadowSettingSamples;
                _shadowSettings.RandomOffset = 0;
            }
            ImGui.EndGroup();

            if (ImGui.CollapsingHeader("Tone map - Uchimura"))
            {
                ImGui.SliderFloat("Max Display Brightness", ref _uchimuraMaxDisplayBrightness, 0.01f, 2.0f);
                ImGui.SliderFloat("Contrast", ref _uchimuraContrast, 0.1f, 2.0f);
                ImGui.SliderFloat("Linear Section Start", ref _uchimuraLinearSectionStart, 0.001f, 1.0f);
                ImGui.SliderFloat("Linear Section Length", ref _uchimuraLinearSectionLength, 0.0f, 0.999f);
                ImGui.SliderFloat("Black", ref _uchimuraBlack, 0.0f, 2.0f);
                ImGui.SliderFloat("Pedestal", ref _uchimuraPedestal, 0.0f, 1.0f);
                ImGui.SliderFloat("Gamma", ref _gamma, 0.1f, 2.4f);
                if (ImGui.Button("Reset Settings"))
                {
                    _uchimuraMaxDisplayBrightness = 1.0f;
                    _uchimuraContrast = 1.0f;
                    _uchimuraLinearSectionStart = 0.22f;
                    _uchimuraLinearSectionLength = 0.4f;
                    _uchimuraBlack = 1.33f;
                    _uchimuraPedestal = 0.0f;
                    _gamma = 2.2f;
                }

                _uchimuraSettings.MaxDisplayBrightness = _uchimuraMaxDisplayBrightness;
                _uchimuraSettings.Contrast = _uchimuraContrast;
                _uchimuraSettings.LinearSectionStart = _uchimuraLinearSectionStart;
                _uchimuraSettings.LinearSectionLength = _uchimuraLinearSectionLength;
                _uchimuraSettings.Black = _uchimuraBlack;
                _uchimuraSettings.Pedestal = _uchimuraPedestal;
                _uchimuraSettings.Gamma = _gamma;

                for (var i = 0; i < _tonemapSamples.Length; i++)
                {
                    _tonemapSamples[i] = Uchimura(
                        3 * (i / (float)_tonemapSamples.Length),
                        _uchimuraMaxDisplayBrightness,
                        _uchimuraContrast,
                        _uchimuraLinearSectionStart,
                        _uchimuraLinearSectionLength,
                        _uchimuraBlack,
                        _uchimuraPedestal);
                }

                ImGui.PlotLines(
                    "Uchimura", 
                    ref _tonemapSamples[0], 
                    _tonemapSamples.Length, 
                    0, string.Empty, 
                    0.0f, 
                    1.0f,
                    new Num.Vector2(ImGui.GetContentRegionAvail().X, 192));
            }

            ImGui.End();
        }
    }

    private void UpdateGeometryBuffersIfNecessary()
    {
        if (_instanceBuffer == null || _indirectBuffer == null)
        {
            return;
        }
        
        if (!_updateGeometryBuffers)
        {
            return;
        }
        
        var index = 0;
        foreach (var meshInstance in _meshInstances)
        {
            GpuMeshInstance instance;
            instance.World = meshInstance.WorldMatrix;
            instance.MaterialId = new Int4(meshInstance.Material.Index, 0, 0, 0);
            _instanceBuffer.Update(instance, index);

            GpuIndirectElementData indirectElementData;
            indirectElementData.FirstIndex = meshInstance.Mesh.IndexOffset;
            indirectElementData.IndexCount = meshInstance.Mesh.IndexCount;
            indirectElementData.BaseInstance = 0;
            indirectElementData.BaseVertex = meshInstance.Mesh.VertexOffset;
            indirectElementData.InstanceCount = 1;
            _indirectBuffer.Update(indirectElementData, index);
            
            index++;
        }

        _updateGeometryBuffers = false;
    }

    private void DestroyFramebuffers()
    {
        _gBufferAlbedoTexture?.Dispose();
        _gBufferAlbedoTextureView?.Dispose();
        _gBufferNormalTexture?.Dispose();
        _gBufferNormalTextureView?.Dispose();
        _gBufferMaterialTexture?.Dispose();
        _gBufferMaterialTextureView?.Dispose();
        _gBufferMotionTexture?.Dispose();
        _gBufferMotionTextureView?.Dispose();
        _gBufferEmissiveTexture?.Dispose();
        _gBufferEmissiveTextureView?.Dispose();
        _depthBufferTexture?.Dispose();
        _graphicsContext.RemoveFramebuffer(_gBufferPassDescriptor);
        
        _finalTexture?.Dispose();
        _graphicsContext.RemoveFramebuffer(_finalPassDescriptor);
        
        _lightsTexture?.Dispose();
        _graphicsContext.RemoveFramebuffer(_lightsPassDescriptor);
    }

    private void CreateFramebuffers()
    {
        _gBufferAlbedoTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R8G8B8A8Srgb, "GBuffer-Albedo-ColorAttachment");
        _gBufferNormalTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R16G16B16A16Float, "GBuffer-Normal-ColorAttachment");
        _gBufferMaterialTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R16G16B16A16Float, "GBuffer-Material-ColorAttachment");
        _gBufferMotionTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R16G16B16A16Float, "GBuffer-Motion-ColorAttachment");
        _gBufferEmissiveTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R16G16B16A16Float, "GBuffer-Emissive-ColorAttachment");
        _depthBufferTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.D32Float, "GBuffer-DepthAttachment");
        _gBufferPassDescriptor = new FramebufferDescriptorBuilder()
            .WithColorAttachment(_gBufferAlbedoTexture, false, Color4.Black)
            .WithColorAttachment(_gBufferNormalTexture, true, Color4.Black)
            .WithColorAttachment(_gBufferMaterialTexture, true, Color4.Black)
            .WithColorAttachment(_gBufferMotionTexture, true, Color4.Black)
            .WithColorAttachment(_gBufferEmissiveTexture, true, Color4.Black)
            .WithDepthAttachment(_depthBufferTexture, true)
            .WithViewport(_applicationContext.ScaledFramebufferSize.X, _applicationContext.ScaledFramebufferSize.Y)
            .Build("GBuffer");

        _gBufferAlbedoTextureView = _gBufferAlbedoTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
        _gBufferNormalTextureView = _gBufferNormalTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
        _gBufferMaterialTextureView = _gBufferMaterialTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
        _gBufferMotionTextureView = _gBufferMotionTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
        _gBufferEmissiveTextureView = _gBufferEmissiveTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
        _depthBufferTextureView = _depthBufferTexture.CreateTextureView(new SwizzleMapping(red: Swizzle.Red, green: Swizzle.Red, blue: Swizzle.Red, alpha: Swizzle.One));
        
        _lightsTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R16G16B16A16Float, "Lights-ColorAttachment");
        _lightsPassDescriptor = new FramebufferDescriptorBuilder()
            .WithColorAttachment(_lightsTexture, true, Color.Black.ToColor4())
            .WithViewport(_applicationContext.ScaledFramebufferSize.X, _applicationContext.ScaledFramebufferSize.Y)
            .Build("Lights");

        _lightsTextureView = _lightsTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));

        _finalTexture = _graphicsContext.CreateTexture2D(
            _applicationContext.ScaledFramebufferSize.X,
            _applicationContext.ScaledFramebufferSize.Y,
            Format.R8G8B8A8UNorm, "Final-ColorAttachment");
        _finalPassDescriptor = new FramebufferDescriptorBuilder()
            .WithColorAttachment(_finalTexture, true, Color4.Black)
            .WithViewport(_applicationContext.ScaledFramebufferSize.X, _applicationContext.ScaledFramebufferSize.Y)
            .Build("Final");

        _finalTextureView = _finalTexture.CreateTextureView(new SwizzleMapping(alpha: Swizzle.One));
    }

    private void CreateSwapchain()
    {
        _swapchainDescriptor = new SwapchainDescriptorBuilder()
            .WithViewport(_applicationContext.FramebufferSize.X, _applicationContext.FramebufferSize.Y)
            .ClearColor(Color4.Black)
            .EnableSrgb()
            .Build();
    }
    
#if DEBUG
    public 
#else
    private
#endif
        bool ReloadPipelines(
#if DEBUG
            string? sourcePath
#endif
        )
    {
        const string sceneDeferredVertexShader = "Shaders/SceneDeferred.vs.glsl";
        const string sceneDeferredFragmentShader = "Shaders/SceneDeferred.fs.glsl";

        const string globalShadowMapVertexShader = "Shaders/Shadow.vs.glsl";
        const string globalShadowMapFragmentShader = "Shaders/Shadow.fs.glsl";
        
        const string lightsGlobalVertexShader = "Shaders/FST.vs.glsl";
        const string lightsGlobalFragmentShader = "Shaders/LightsGlobal.fs.glsl";

        const string lightsLocalVertexShader = "Shaders/Lights.vs.glsl";
        const string lightsLocalFragmentShader = "Shaders/LightsLocal.fs.glsl";

        const string finalVertexShader = "Shaders/Final.vs.glsl";
        const string finalFragmentShader = "Shaders/Final.fs.glsl";
        
#if DEBUG
        if (!string.IsNullOrEmpty(sourcePath))
        {
            var destinationPath = AppDomain.CurrentDomain.BaseDirectory;
            File.Copy(Path.Combine(sourcePath, sceneDeferredVertexShader), Path.Combine(destinationPath, sceneDeferredVertexShader), true);
            File.Copy(Path.Combine(sourcePath, sceneDeferredFragmentShader), Path.Combine(destinationPath, sceneDeferredFragmentShader), true);
            
            File.Copy(Path.Combine(sourcePath, globalShadowMapVertexShader), Path.Combine(destinationPath, globalShadowMapVertexShader), true);
            File.Copy(Path.Combine(sourcePath, globalShadowMapFragmentShader), Path.Combine(destinationPath, globalShadowMapFragmentShader), true);
            
            File.Copy(Path.Combine(sourcePath, lightsGlobalVertexShader), Path.Combine(destinationPath, lightsGlobalVertexShader), true);
            File.Copy(Path.Combine(sourcePath, lightsGlobalFragmentShader), Path.Combine(destinationPath, lightsGlobalFragmentShader), true);
                    
            File.Copy(Path.Combine(sourcePath, lightsLocalVertexShader), Path.Combine(destinationPath, lightsLocalVertexShader), true);
            File.Copy(Path.Combine(sourcePath, lightsLocalFragmentShader), Path.Combine(destinationPath, lightsLocalFragmentShader), true);
            
            File.Copy(Path.Combine(sourcePath, finalVertexShader), Path.Combine(destinationPath, finalVertexShader), true);
            File.Copy(Path.Combine(sourcePath, finalFragmentShader), Path.Combine(destinationPath, finalFragmentShader), true);
        }
#endif

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
            .Build("GBufferPass");

        if (gBufferGraphicsPipelineResult.IsFailure)
        {
            _logger.Error("Renderer: Unable to build graphics pipeline {PipelineName}. {Details}",
                "GBufferPass", gBufferGraphicsPipelineResult.Error);
            return false;
        }
        _gBufferPassGraphicsPipeline?.Dispose();
        _gBufferPassGraphicsPipeline = gBufferGraphicsPipelineResult.Value;
        _logger.Debug("Renderer: Build pipeline {PipelineName}", _gBufferPassGraphicsPipeline.Label);

        var lightsGlobalPassGraphicsPipelineResult = _graphicsContext.CreateGraphicsPipelineBuilder()
            .WithShadersFromFiles(lightsGlobalVertexShader, lightsGlobalFragmentShader)
            .WithVertexInput(new VertexInputDescriptorBuilder()
                .AddAttribute(0, DataType.Float, 3, 0)
                .Build(nameof(VertexPosition)))
            .WithTopology(PrimitiveTopology.Triangles)
            .WithFaceWinding(FaceWinding.Clockwise)
            .DisableCulling()
            //.DisableDepthTest()
            //.EnableBlending(ColorBlendAttachmentDescriptor.Additive)
            .Build("LightsGlobalPass");
        if (lightsGlobalPassGraphicsPipelineResult.IsFailure)
        {
            _logger.Error("Renderer: Unable to build graphics pipeline {PipelineName}. {Details}",
                "LightsGlobalPass", gBufferGraphicsPipelineResult.Error);
            return false;
        }
        _lightsGlobalPassGraphicsPipeline?.Dispose();
        _lightsGlobalPassGraphicsPipeline = lightsGlobalPassGraphicsPipelineResult.Value;
        _logger.Debug("Renderer: Build pipeline {PipelineName}", _lightsGlobalPassGraphicsPipeline.Label);
        
        var lightsLocalPassGraphicsPipelineResult = _graphicsContext.CreateGraphicsPipelineBuilder()
            .WithShadersFromFiles(lightsLocalVertexShader, lightsLocalFragmentShader)
            .WithVertexInput(new VertexInputDescriptorBuilder()
                .AddAttribute(0, DataType.Float, 3, 0)
                .Build(nameof(VertexPosition)))
            .WithTopology(PrimitiveTopology.Triangles)
            .WithFaceWinding(FaceWinding.CounterClockwise)
            .EnableCulling(CullMode.Front)
            .DisableDepthTest()
            .Build("LightsLocalPass");
        if (lightsLocalPassGraphicsPipelineResult.IsFailure)
        {
            _logger.Error("Renderer: Unable to build graphics pipeline {PipelineName}. {Details}",
                "LightsLocalPass", gBufferGraphicsPipelineResult.Error);
            return false;
        }
        _lightsLocalPassGraphicsPipeline?.Dispose();
        _lightsLocalPassGraphicsPipeline = lightsLocalPassGraphicsPipelineResult.Value;
        _logger.Debug("Renderer: Build pipeline {PipelineName}", _lightsLocalPassGraphicsPipeline.Label);

        var finalPassGraphicsPipelineResult = _graphicsContext.CreateGraphicsPipelineBuilder()
            .WithShadersFromFiles(finalVertexShader, finalFragmentShader)
            .WithVertexInput(new VertexInputDescriptorBuilder()
                .AddAttribute(0, DataType.Float, 3, 0)
                .AddAttribute(0, DataType.Float, 2, 12)
                .Build(nameof(VertexPositionUv)))
            .WithTopology(PrimitiveTopology.Triangles)
            .WithFaceWinding(FaceWinding.CounterClockwise)
            .DisableDepthTest()            
            .Build("FinalPass");
        if (finalPassGraphicsPipelineResult.IsFailure)
        {
            _logger.Error("Renderer: Unable to build graphics pipeline {PipelineName}. {Details}",
                "FinalPass", gBufferGraphicsPipelineResult.Error);
            return false;
        }
        _finalPassGraphicsPipeline?.Dispose();
        _finalPassGraphicsPipeline = finalPassGraphicsPipelineResult.Value;
        _logger.Debug("Renderer: Build pipeline {PipelineName}", _finalPassGraphicsPipeline.Label);

        var shadowPassGraphicsPipelineResult = _graphicsContext.CreateGraphicsPipelineBuilder()
            .WithShadersFromFiles(globalShadowMapVertexShader, globalShadowMapFragmentShader)
            .WithVertexInput(new VertexInputDescriptorBuilder()
                .AddAttribute(0, DataType.Float, 3, 0)
                .Build(nameof(VertexPosition)))
            .WithTopology(PrimitiveTopology.Triangles)
            .WithFaceWinding(FaceWinding.CounterClockwise)
            .EnableDepthTest()
            .Build("GlobalLightShadowPass");
        if (shadowPassGraphicsPipelineResult.IsFailure)
        {
            _logger.Error("Renderer: Unable to build graphics pipeline {PipelineName}. {Details}",
                "GlobalLightShadowPass", shadowPassGraphicsPipelineResult.Error);
            return false;
        }
        _shadowPassGraphicsPipeline?.Dispose();
        _shadowPassGraphicsPipeline = shadowPassGraphicsPipelineResult.Value;
        _logger.Debug("Renderer: Build pipeline {PipelineName}", _shadowPassGraphicsPipeline.Label);

        return true;
    }
    
    private void RenderPassGBuffer()
    {
        if (_gBufferPassGraphicsPipeline == null || _meshPool == null || _materialPool == null ||
            _cameraInformationBuffer == null || _indirectBuffer == null)
        {
            return;
        }
        
        _graphicsContext.BindGraphicsPipeline(_gBufferPassGraphicsPipeline!);

        _graphicsContext.BeginRenderToFramebuffer(_gBufferPassDescriptor);
        _gBufferPassGraphicsPipeline.BindVertexBuffer(_meshPool.VertexBuffer, 0, 0);
        _gBufferPassGraphicsPipeline.BindIndexBuffer(_meshPool.IndexBuffer);
        _gBufferPassGraphicsPipeline.BindUniformBuffer(_cameraInformationBuffer, 0);
        _gBufferPassGraphicsPipeline.BindShaderStorageBuffer(_instanceBuffer, 1);
        _gBufferPassGraphicsPipeline.BindShaderStorageBuffer(_materialPool.MaterialBuffer, 2);
        _gBufferPassGraphicsPipeline.MultiDrawElementsIndirect(_indirectBuffer, _meshInstances.Count);
        _graphicsContext.EndRender();
    }

    private void RenderLights(ICamera camera)
    {
        if (_shadowPassGraphicsPipeline == null)
        {
            return;
        }
        
        GL.PushDebugGroup("Render-Shadow-Maps");
        _graphicsContext.BindGraphicsPipeline(_shadowPassGraphicsPipeline);
        RenderShadowMaps();
        GL.PopDebugGroup();
        
        _graphicsContext.BeginRenderToFramebuffer(_lightsPassDescriptor);

        RenderGlobalLights();
        
        //RenderLocalLights();

        _graphicsContext.EndRender();
    }
    
    private void CreateShadowMap(GlobalLight globalLight, int width, int height)
    {
        globalLight.ShadowMapTexture?.Dispose();
        globalLight.ShadowMapTexture = _graphicsContext.CreateTexture2D(width, height, Format.D16UNorm, $"Directional-Light-ShadowMap-{GetHashCode()}");
        //globalLight.ShadowMapTexture.MakeResident(_linearMipmapLinearRepeatSampler);

        if (globalLight.ShadowMapFramebufferDescriptor != null)
        {
            _graphicsContext.RemoveFramebuffer(globalLight.ShadowMapFramebufferDescriptor.Value);
        }

        globalLight.ShadowMapFramebufferDescriptor = new FramebufferDescriptorBuilder()
            .WithDepthAttachment(globalLight.ShadowMapTexture, true)
            .WithViewport(width, height, 0)
            .Build($"Directional-Light-Shadow-{GetHashCode()}");
    }

    private void RenderShadowMaps()
    {
        if (_globalLightsBuffer == null ||
            _shadowPassGraphicsPipeline == null ||
            _meshPool == null ||
            _indirectBuffer == null)
        {
            return;
        }

        var globalLightBuffer = _graphicsContext.CreateUniformBuffer<GpuGlobalLight>("GlobalLightBuffer");
        globalLightBuffer.AllocateStorage(Marshal.SizeOf<GpuGlobalLight>(), StorageAllocationFlags.Dynamic);

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < _globalLights.Count; i++)
        {
            var globalLight = _globalLights[i];
            if (!globalLight.IsShadowCaster || !globalLight.ShadowMapFramebufferDescriptor.HasValue)
            {
                continue;
            }
            
            var dimensions = globalLight.Dimensions;
            var eye = -Vector3.Normalize(globalLight.Direction) * 10 + new Vector3(0, 2, 0);
            var projectionMatrix = Matrix.OrthoOffCenterRH(-dimensions.X / 2, dimensions.X / 2, -dimensions.Y / 2, dimensions.Y / 2, globalLight.Near, globalLight.Far);
            var viewMatrix = Matrix.LookAtRH(eye, eye + globalLight.Direction, Vector3.Up);

            var gpuGlobalLight = new GpuGlobalLight
            {
                ProjectionMatrix = projectionMatrix,
                ViewMatrix = viewMatrix,
                Direction = new Vector4(globalLight.Direction, 1.0f/*globalLight.ShadowQuality*/),
                Color = new Vector4(globalLight.Color, 1.0f)
            };

            globalLightBuffer.Update(gpuGlobalLight);
            
            _graphicsContext.BeginRenderToFramebuffer(globalLight.ShadowMapFramebufferDescriptor.Value);
            
            _shadowPassGraphicsPipeline.BindVertexBuffer(_meshPool.VertexBuffer, 0, 0);
            _shadowPassGraphicsPipeline.BindIndexBuffer(_meshPool.IndexBuffer);
            _shadowPassGraphicsPipeline.BindUniformBuffer(globalLightBuffer, 2);
            _shadowPassGraphicsPipeline.BindShaderStorageBuffer(_instanceBuffer, 3);
            _shadowPassGraphicsPipeline.MultiDrawElementsIndirect(_indirectBuffer, _meshInstances.Count);

            _graphicsContext.EndRender();
        }
        
        globalLightBuffer.Dispose();
    }

    private void RenderGlobalLights()
    {
        if (_lightsGlobalPassGraphicsPipeline == null || 
            _shadowSettingsBuffer == null || 
            _globalLightsBuffer == null ||
            _cameraInformationBuffer == null ||
            _nearestSampler == null || 
            _depthBufferTexture == null || 
            _gBufferAlbedoTexture == null || 
            _gBufferNormalTexture == null ||
            _gBufferMaterialTexture == null || 
            _gBufferEmissiveTexture == null || 
            _linearMipmapLinearRepeatSampler == null ||
            _preparePrepareImageBasedLightingPass.IrradianceCubeTexture == null ||
            _preparePrepareImageBasedLightingPass.PrefilteredCubeTexture == null ||
            _preparePrepareImageBasedLightingPass.BrdfIntegrationLutTexture == null ||
            _blueNoiseTexture == null)
        {
            return;
        }
        
        _graphicsContext.BindGraphicsPipeline(_lightsGlobalPassGraphicsPipeline);
        
        _shadowSettingsBuffer.Update(_shadowSettings);
       
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < _globalLights.Count; i++)
        {
            var globalLight = _globalLights[i];
            var dimensions = globalLight.Dimensions;

            var eye = -Vector3.Normalize(globalLight.Direction) * 10 + new Vector3(0, 2, 0);
            
            var projectionMatrix = Matrix.OrthoOffCenterRH(-dimensions.X / 2, dimensions.X / 2, -dimensions.Y / 2, dimensions.Y / 2, globalLight.Near, globalLight.Far);
            var viewMatrix = Matrix.LookAtRH(eye, eye + globalLight.Direction, Vector3.Up);

            var gpuGlobalLight = new GpuGlobalLight
            {
                ProjectionMatrix = projectionMatrix,
                ViewMatrix = viewMatrix,
                Direction = new Vector4(globalLight.Direction, globalLight.ShadowQuality),
                Color = new Vector4(globalLight.Color, globalLight.Intensity),
                ShadowMapTextureHandle = globalLight.ShadowMapTexture.TextureHandle
            };

            _globalLightsBuffer.Update(gpuGlobalLight, i);
        }
        
        _localLightsBuffer.Update(_localLights.ToArray());
        
        _lightsGlobalPassGraphicsPipeline.BindUniformBuffer(_cameraInformationBuffer, 0);
        _lightsGlobalPassGraphicsPipeline.BindUniformBuffer(_shadowSettingsBuffer, 1);
        _lightsGlobalPassGraphicsPipeline.BindShaderStorageBuffer(_globalLightsBuffer, 2);
        _lightsGlobalPassGraphicsPipeline.BindShaderStorageBuffer(_localLightsBuffer, 3);
            
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _depthBufferTexture, 0);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _gBufferAlbedoTexture, 1);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _gBufferNormalTexture, 2);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _gBufferMaterialTexture, 3);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _gBufferEmissiveTexture, 4);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_linearMipmapLinearRepeatSampler, _blueNoiseTexture, 5);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_linearMipmapLinearClampedSampler, _preparePrepareImageBasedLightingPass.BrdfIntegrationLutTexture, 6);        
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_linearMipmapLinearRepeatSampler, _preparePrepareImageBasedLightingPass.IrradianceCubeTexture, 7);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_linearMipmapLinearRepeatSampler, _preparePrepareImageBasedLightingPass.PrefilteredCubeTexture, 8);
            
        _lightsGlobalPassGraphicsPipeline.DrawArrays(3);
    }

    private void RenderLocalLights()
    {
        if (_localLightsBuffer == null || 
            _lightsLocalPassGraphicsPipeline == null ||
            _meshPool == null ||
            _shadowSettingsBuffer == null)
        {
            return;
        }
        if (_updateLocalLights)
        {
            _localLightsBuffer.Update(_localLights.ToArray());
            _updateLocalLights = false;
        }

        _graphicsContext.BindGraphicsPipeline(_lightsLocalPassGraphicsPipeline);
        _lightsLocalPassGraphicsPipeline.BindVertexBuffer(_meshPool.VertexBuffer, 0, 0);
        _lightsLocalPassGraphicsPipeline.BindIndexBuffer(_meshPool.IndexBuffer);

        _lightsLocalPassGraphicsPipeline.BindUniformBuffer(_shadowSettingsBuffer, 0);
        
        //TODO(deccer) this is fucked
        _lightsLocalPassGraphicsPipeline.BindShaderStorageBuffer(_localLightsBuffer, 1);
        _lightsLocalPassGraphicsPipeline.DrawElementsInstanced((int)_pointLightVolumePooledMesh.IndexCount, 0, _localLightsBuffer.Count);        
        _lightsLocalPassGraphicsPipeline.DrawElementsInstanced((int)_spotLightVolumePooledMesh.IndexCount, 0, _localLightsBuffer.Count);
    }

    private void RenderPassFinal(ICamera camera)
    {
        if (_cameraInformationBuffer == null || 
            _nearestSampler == null || 
            _depthBufferTexture == null ||
            _lightsTexture == null || 
            _linearMipmapLinearRepeatSampler == null || 
            _preparePrepareImageBasedLightingPass.PrefilteredCubeTexture == null ||
            _uchimuraSettingsBuffer == null || 
            _finalPassGraphicsPipeline == null)
        {
            return;
        }
        _cameraInformation.ProjectionMatrix = camera.ProjectionMatrix;
        _cameraInformation.ViewMatrix = camera.ViewMatrix;
        _cameraInformationBuffer.Update(_cameraInformation);
        
        _uchimuraSettingsBuffer.Update(_uchimuraSettings);
        
        _graphicsContext.BindGraphicsPipeline(_finalPassGraphicsPipeline!);
        _graphicsContext.BeginRenderToFramebuffer(_finalPassDescriptor);
        _finalPassGraphicsPipeline.BindUniformBuffer(_cameraInformationBuffer, 0);
        _finalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _depthBufferTexture, 0);
        _finalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _lightsTexture, 1);
        _finalPassGraphicsPipeline.BindSampledTexture(_linearMipmapLinearRepeatSampler, _preparePrepareImageBasedLightingPass.PrefilteredCubeTexture, 2);
        
        _finalPassGraphicsPipeline.BindUniformBuffer(_uchimuraSettingsBuffer, 3);
        _finalPassGraphicsPipeline.DrawArrays(3);
        _graphicsContext.EndRender();
    }
    
    private static float Uchimura(float x, float p, float a, float m, float l, float c, float b)
    {
        var l0 = (p - m) * l / a;
        var s0 = m + l0;
        var s1 = m + a * l0;
        var c2 = a * p / (p - s1);
        var cP = -c2 / p;

        var w0 = 1.0f - MathHelper.SmoothStep(0.0f, m, x);
        var w2 = MathHelper.Step(m + l0, x);
        var w1 = 1.0f - w0 - w2;

        var T = m * MathF.Pow(x / m, c) + b;
        var s = p - (p - s1) * MathF.Exp(cP * (x - s0));
        var lbow = m + a * (x - m);

        return T * w0 + lbow * w1 + s * w2;
    }
    
    private static Vector3 Uchimura(Vector3 x, float p, float a, float m, float l, float c, float b)
    {
        return new Vector3(
            Uchimura(x.X, p, a, m, l, c, b),
            Uchimura(x.Y, p, a, m, l, c, b),
            Uchimura(x.Z, p, a, m, l, c, b)
        );
    }
    
    private Task SwitchedToGameMode(SwitchedToGameModeMessage message)
    {
        _programMode = message.ProgramMode;
        _uchimuraSettings.CorrectGamma = false;
        return Task.CompletedTask;
    }

    private Task SwitchedToEditorMode(SwitchedToEditorModeMessage message)
    {
        _programMode = message.ProgramMode;
        _uchimuraSettings.CorrectGamma = true;
        return Task.CompletedTask;
    }
}