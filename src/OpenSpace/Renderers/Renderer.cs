using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ImGuiNET;
using EngineKit.Mathematics;
using EngineKit.Native.OpenGL;
using EngineKit;
using EngineKit.Graphics;
using OpenSpace.Extensions;
using OpenSpace.Game;
using OpenSpace.Messages;
using OpenSpace.Windows;
using Serilog;
using Num = System.Numerics;
using Vector2 = EngineKit.Mathematics.Vector2;
using Vector3 = EngineKit.Mathematics.Vector3;
using Vector4 = EngineKit.Mathematics.Vector4;

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
    private readonly ILineRenderer _lineRenderer;

    private ITexture? _blueNoiseTexture;

    private ISampler? _nearestSampler;
    private ISampler? _linearMipmapLinearRepeatSampler;
    private ISampler? _linearMipmapLinearClampedSampler;
    
    private SwapchainDescriptor _swapchainDescriptor;


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

    private GpuLightPassParameters _lightPassParameters;
    private IUniformBuffer? _lightPassParametersBuffer;

    private readonly IList<GlobalLight> _globalLights;

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

    private readonly PrepareImageBasedLightingPass _preparePrepareImageBasedLightingPass;
    private readonly GBufferPass _gBufferPass;

    public Renderer(
        ILogger logger,
        IApplicationContext applicationContext,
        IGraphicsContext graphicsContext,
        IMeshLoader meshLoader,
        ISamplerLibrary samplerLibrary,
        IMessageBus messageBus,
        ILineRenderer lineRenderer)
    {
        _logger = logger;
        _applicationContext = applicationContext;
        _graphicsContext = graphicsContext;
        _meshLoader = meshLoader;
        _samplerLibrary = samplerLibrary;
        _lineRenderer = lineRenderer;

        _cameraInformation = new GpuCameraInformation();
        _meshInstances = new List<MeshInstance>(2048);

        _globalLights = new List<GlobalLight>();
        _localLights = new List<GpuLocalLight>();
        
        _shadowSettings = new GpuShadowSettings();
        _tonemapSamples = new float[100];
        _lightPassParameters = new GpuLightPassParameters();
        
        messageBus.Subscribe<SwitchedToGameModeMessage>(SwitchedToGameMode);
        messageBus.Subscribe<SwitchedToEditorModeMessage>(SwitchedToEditorMode);

        _preparePrepareImageBasedLightingPass = new PrepareImageBasedLightingPass(logger, graphicsContext);
        _gBufferPass = new GBufferPass(logger, graphicsContext, applicationContext);
    }

    public int SelectedTextureToBeRendered => _selectedTextureToBeRendered;

    public TextureView? FinalTexture => _finalTextureView;

    public TextureView? DepthTexture => _gBufferPass.DepthBufferTextureView;

    public TextureView? GBufferAlbedoTexture => _gBufferPass.GBufferAlbedoTextureView;

    public TextureView? GBufferNormalsTexture => _gBufferPass.GBufferNormalTextureView;

    public TextureView? GBufferMaterialTexture => _gBufferPass.GBufferMaterialTextureView;

    public TextureView? GBufferMotionTexture => _gBufferPass.GBufferMotionTextureView;

    public TextureView? GBufferEmissiveTexture => _gBufferPass.GBufferEmissiveTextureView;

    public TextureView? LightsTexture => _lightsTextureView;

    public TextureView? FirstGlobalLightShadowMap => _globalLights[0].ShadowMapTextureView;

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
        
        _finalPassGraphicsPipeline?.Dispose();

        _shadowSettingsBuffer?.Dispose();
        _localLightsBuffer?.Dispose();
        
        _cameraInformationBuffer?.Dispose();
        _indirectBuffer?.Dispose();
        _instanceBuffer?.Dispose();
        
        _materialPool?.Dispose();
        _meshPool?.Dispose();
        
        _lightPassParametersBuffer?.Dispose();
        
        _gBufferPass.Dispose();
        _preparePrepareImageBasedLightingPass.Dispose();
        
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
        int shadowQuality,
        int shadowMapSize)
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
        
        globalLight.CreateGlobalLightShadowTexture(_graphicsContext, _linearMipmapLinearClampedSampler, 1024);
        
        _globalLights.Add(globalLight);
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
        /*
        var sourcePath = Debugger.IsAttached
            ? "../../../../OpenSpace.Assets"
            : "../../../../../OpenSpace.Assets";
            */
        var sourcePath = "../../../../OpenSpace.Assets";
        if (!ReloadPipelines(sourcePath))
        {
            return false;
        }

        if (!_lineRenderer.Load())
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

        _shadowSettingsBuffer = _graphicsContext.CreateUniformBuffer<GpuShadowSettings>("ShadowSettings");
        _shadowSettingsBuffer.AllocateStorage(Marshal.SizeOf<GpuShadowSettings>(), StorageAllocationFlags.Dynamic);

        _lightPassParametersBuffer = _graphicsContext.CreateUniformBuffer<GpuLightPassParameters>("LightPassParameters");
        _lightPassParametersBuffer.AllocateStorage(Marshal.SizeOf<GpuLightPassParameters>(), StorageAllocationFlags.Dynamic);

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

        var imageBasedLightingPassOptions = new ImageBasedLightingPassOptions(
            128, 
            1024, 
            256, 
            "rye-field-sunset");
        if (!_preparePrepareImageBasedLightingPass.Load(imageBasedLightingPassOptions))
        {
            return false;
        }

        _lightPassParameters.Gamma = 2.2f;
        _lightPassParameters.Exposure = 4.0f;
        _lightPassParameters.PrefilteredCubeMipLevels = _preparePrepareImageBasedLightingPass.PrefilteredCubeTextureMipLevels;
        _lightPassParameters.ScaleIblAmbient = 2.0f;
        _lightPassParametersBuffer.Update(_lightPassParameters);        
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

        UpdateCameraInformation(camera);
        UpdateGeometryBuffersIfNecessary();

        _gBufferPass.Render(
            ref _meshPool,
            ref _materialPool,
            ref _cameraInformationBuffer,
            ref _instanceBuffer,
            ref _indirectBuffer, _meshInstances.Count);

        RenderLights(camera);
        RenderFinal(camera);
        
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
        if (ImGui.Begin("Lights"))
        {
            for (var i = 0; i < _globalLights.Count; i++)
            {
                var globalLight = _globalLights[i];
                if (ImGui.TreeNodeEx($"Global Light {i}"))
                {
                    var lightAzimuth = globalLight.Azimuth;
                    var lightAltitude = globalLight.Altitude;
                    var lightColor = globalLight.Color.ToNumVector3();
                    //var lightDirection = globalLight.Direction.ToNumVector3();
                    var lightDimension = globalLight.Dimensions.ToNumVector2();
                    var lightNear = globalLight.Near;
                    var lightFar = globalLight.Far;
                    var lightIntensity = globalLight.Intensity;
                    if (ImGui.SliderFloat2("Dimension", ref lightDimension, 0.1f, 1024))
                    {
                        globalLight.Dimensions = lightDimension.ToVector2();
                    }

                    if (ImGui.SliderFloat3("Color", ref lightColor, 0.0f, 1.0f))
                    {
                        globalLight.Color = lightColor.ToVector3();
                    }

                    if (ImGui.SliderFloat("Intensity", ref lightIntensity, 0.0f, 256f))
                    {
                        globalLight.Intensity = lightIntensity;
                    }

                    if (ImGui.SliderFloat("Azimuth", ref lightAzimuth, -(MathF.PI * MathHelper.ToDegree) + 0.01f, (MathF.PI * MathHelper.ToDegree) - 0.01f))
                    {
                        globalLight.Azimuth = lightAzimuth;
                    }
                    if (ImGui.SliderFloat("Altitude", ref lightAltitude, -(MathF.PI * MathHelper.ToDegree) + 0.01f, (MathF.PI * MathHelper.ToDegree) - 0.01f))
                    {
                        globalLight.Altitude = lightAltitude;
                    }
                    if (ImGui.SliderFloat("Near", ref lightNear, -1024.0f, 0.0f))
                    {
                        globalLight.Near = lightNear;
                    }

                    if (ImGui.SliderFloat("Far", ref lightFar, 0, 1024))
                    {
                        globalLight.Far = lightFar;
                    }
                    
                    ImGuiExtensions.ShowImage(globalLight.ShadowMapTextureView, new Num.Vector2(384, 384));

                    ImGui.TreePop();
                }
            }
        }
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
    
    private void UpdateCameraInformation(ICamera camera)
    {
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
    }
    
    private IEnumerable<VertexPositionColor> CalculateGlobalLightsFrustumVertices(ICamera camera)
    {
        var vertices = new List<VertexPositionColor>(24 * _globalLights.Count);
        var lightColor = Color.Red.ToVector3();
        for (var i = 0; i < _globalLights.Count; i++)
        {
            var globalLight = _globalLights[i];
            globalLight.UpdateMatrices(camera);
            var lightFrustum = globalLight.BoundingFrustum;
            var lightFrustumCorners = lightFrustum.GetCorners();

            var nearBottomRight = lightFrustumCorners[0];
            var nearTopRight = lightFrustumCorners[1];
            var nearTopLeft = lightFrustumCorners[2];
            var nearBottomLeft = lightFrustumCorners[3];
            
            var farBottomRight = lightFrustumCorners[4];
            var farTopRight = lightFrustumCorners[5];
            var farTopLeft = lightFrustumCorners[6];
            var farBottomLeft = lightFrustumCorners[7];
            
            vertices.Add(new VertexPositionColor(nearBottomRight, lightColor));
            vertices.Add(new VertexPositionColor(nearTopRight, lightColor));
            
            vertices.Add(new VertexPositionColor(nearTopRight, lightColor));
            vertices.Add(new VertexPositionColor(nearTopLeft, lightColor));

            vertices.Add(new VertexPositionColor(nearTopLeft, lightColor));
            vertices.Add(new VertexPositionColor(nearBottomLeft, lightColor));

            vertices.Add(new VertexPositionColor(nearBottomLeft, lightColor));
            vertices.Add(new VertexPositionColor(nearBottomRight, lightColor));
            
            // ---
            
            vertices.Add(new VertexPositionColor(nearBottomRight, lightColor));
            vertices.Add(new VertexPositionColor(farBottomRight, lightColor));
            
            vertices.Add(new VertexPositionColor(nearTopRight, lightColor));
            vertices.Add(new VertexPositionColor(farTopRight, lightColor));

            vertices.Add(new VertexPositionColor(nearTopLeft, lightColor));
            vertices.Add(new VertexPositionColor(farTopLeft, lightColor));

            vertices.Add(new VertexPositionColor(nearBottomLeft, lightColor));
            vertices.Add(new VertexPositionColor(farBottomLeft, lightColor));
            
            // ---

            vertices.Add(new VertexPositionColor(farBottomRight, lightColor));
            vertices.Add(new VertexPositionColor(farTopRight, lightColor));
            
            vertices.Add(new VertexPositionColor(farTopRight, lightColor));
            vertices.Add(new VertexPositionColor(farTopLeft, lightColor));

            vertices.Add(new VertexPositionColor(farTopLeft, lightColor));
            vertices.Add(new VertexPositionColor(farBottomLeft, lightColor));

            vertices.Add(new VertexPositionColor(farBottomLeft, lightColor));
            vertices.Add(new VertexPositionColor(farBottomRight, lightColor));
        }

        return vertices;
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
        _gBufferPass.DestroyResolutionDependentFramebuffers();
        
        _finalTexture?.Dispose();
        _graphicsContext.RemoveFramebuffer(_finalPassDescriptor);
        
        _lightsTexture?.Dispose();
        _graphicsContext.RemoveFramebuffer(_lightsPassDescriptor);
    }

    private void CreateFramebuffers()
    {
        _gBufferPass.CreateResolutionDependentFramebuffers();
        
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
    
    public bool ReloadPipelines(string? sourcePath)
    {
        const string globalShadowMapVertexShader = "Shaders/Shadow.vs.glsl";
        const string globalShadowMapFragmentShader = "Shaders/Shadow.fs.glsl";
        
        const string lightsGlobalVertexShader = "Shaders/FST.vs.glsl";
        const string lightsGlobalFragmentShader = "Shaders/LightsGlobal.fs.glsl";

        const string lightsLocalVertexShader = "Shaders/Lights.vs.glsl";
        const string lightsLocalFragmentShader = "Shaders/LightsLocal.fs.glsl";

        const string finalVertexShader = "Shaders/Final.vs.glsl";
        const string finalFragmentShader = "Shaders/Final.fs.glsl";
        
//#if DEBUG
        var destinationPath = AppDomain.CurrentDomain.BaseDirectory;
        if (!string.IsNullOrEmpty(sourcePath))
        {
            File.Copy(Path.Combine(sourcePath, globalShadowMapVertexShader), Path.Combine(destinationPath, globalShadowMapVertexShader), true);
            File.Copy(Path.Combine(sourcePath, globalShadowMapFragmentShader), Path.Combine(destinationPath, globalShadowMapFragmentShader), true);
            
            File.Copy(Path.Combine(sourcePath, lightsGlobalVertexShader), Path.Combine(destinationPath, lightsGlobalVertexShader), true);
            File.Copy(Path.Combine(sourcePath, lightsGlobalFragmentShader), Path.Combine(destinationPath, lightsGlobalFragmentShader), true);
                    
            File.Copy(Path.Combine(sourcePath, lightsLocalVertexShader), Path.Combine(destinationPath, lightsLocalVertexShader), true);
            File.Copy(Path.Combine(sourcePath, lightsLocalFragmentShader), Path.Combine(destinationPath, lightsLocalFragmentShader), true);
            
            File.Copy(Path.Combine(sourcePath, finalVertexShader), Path.Combine(destinationPath, finalVertexShader), true);
            File.Copy(Path.Combine(sourcePath, finalFragmentShader), Path.Combine(destinationPath, finalFragmentShader), true);
        }
        
        _gBufferPass.ReloadPipeline(sourcePath, destinationPath);
//#endif
        var lightsGlobalPassGraphicsPipelineResult = _graphicsContext.CreateGraphicsPipelineBuilder()
            .WithShadersFromFiles(lightsGlobalVertexShader, lightsGlobalFragmentShader)
            .WithVertexInput(new VertexInputDescriptorBuilder()
                .AddAttribute(0, DataType.Float, 3, 0)
                .Build(nameof(VertexPosition)))
            .WithTopology(PrimitiveTopology.Triangles)
            .WithFaceWinding(FaceWinding.CounterClockwise)
            .EnableDepthTest()
            .EnableBlending(ColorBlendAttachmentDescriptor.Additive)
            .Build("LightsGlobalPass");
        if (lightsGlobalPassGraphicsPipelineResult.IsFailure)
        {
            _logger.Error("{Category}: Unable to build graphics pipeline. {Details}",
                "LightsGlobalPass", lightsGlobalPassGraphicsPipelineResult.Error);
            return false;
        }
        _lightsGlobalPassGraphicsPipeline?.Dispose();
        _lightsGlobalPassGraphicsPipeline = lightsGlobalPassGraphicsPipelineResult.Value;
        _logger.Debug("{Category}: Pipeline built", _lightsGlobalPassGraphicsPipeline.Label);
        
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
            _logger.Error("{Category}: Unable to build graphics pipeline. {Details}",
                "LightsLocalPass", lightsLocalPassGraphicsPipelineResult.Error);
            return false;
        }
        _lightsLocalPassGraphicsPipeline?.Dispose();
        _lightsLocalPassGraphicsPipeline = lightsLocalPassGraphicsPipelineResult.Value;
        _logger.Debug("{Category}: Pipeline built", _lightsLocalPassGraphicsPipeline.Label);

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
            _logger.Error("{Category}: Unable to build graphics pipeline. {Details}",
                "FinalPass", finalPassGraphicsPipelineResult.Error);
            return false;
        }
        _finalPassGraphicsPipeline?.Dispose();
        _finalPassGraphicsPipeline = finalPassGraphicsPipelineResult.Value;
        _logger.Debug("{Category}: Pipeline built", _finalPassGraphicsPipeline.Label);

        var shadowPassGraphicsPipelineResult = _graphicsContext.CreateGraphicsPipelineBuilder()
            .WithShadersFromFiles(globalShadowMapVertexShader, globalShadowMapFragmentShader)
            .WithVertexInput(new VertexInputDescriptorBuilder()
                .AddAttribute(0, DataType.Float, 3, 0)
                .Build(nameof(VertexPosition)))
            .WithTopology(PrimitiveTopology.Triangles)
            .WithFaceWinding(FaceWinding.CounterClockwise)
            .EnableCulling(CullMode.Front)
            .EnableDepthTest()
            .Build("GlobalLightShadowPass");
        if (shadowPassGraphicsPipelineResult.IsFailure)
        {
            _logger.Error("{Category}: Unable to build graphics pipeline. {Details}",
                "GlobalLightShadowPass", shadowPassGraphicsPipelineResult.Error);
            return false;
        }
        _shadowPassGraphicsPipeline?.Dispose();
        _shadowPassGraphicsPipeline = shadowPassGraphicsPipelineResult.Value;
        _logger.Debug("{Category}: Pipeline built", _shadowPassGraphicsPipeline.Label);

        return true;
    }

    private void RenderLights(ICamera camera)
    {
        if (_shadowPassGraphicsPipeline == null)
        {
            return;
        }
        
        GL.PushDebugGroup("Render-Shadow-Maps");
        RenderGlobalLightShadowMaps(camera);
        GL.PopDebugGroup();
        
        _graphicsContext.BeginRenderToFramebuffer(_lightsPassDescriptor);
        RenderGlobalLights(camera);
        //RenderLocalLights();
        _graphicsContext.EndRender();
    }

    private void RenderGlobalLightShadowMaps(ICamera camera)
    {
        if (_shadowPassGraphicsPipeline == null ||
            _meshPool == null ||
            _instanceBuffer == null ||
            _indirectBuffer == null)
        {
            return;
        }
       
        _graphicsContext.BindGraphicsPipeline(_shadowPassGraphicsPipeline);

        
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < _globalLights.Count; i++)
        {
            var globalLight = _globalLights[i];
            globalLight.UpdateMatrices(camera);
            
            using var globalLightForShadowMapBuffer = _graphicsContext.CreateUniformBuffer<GpuShadowGlobalLight>("GlobalShadowLightBuffer");
            globalLightForShadowMapBuffer.AllocateStorage(new GpuShadowGlobalLight(globalLight), StorageAllocationFlags.None);
            
            _graphicsContext.BeginRenderToFramebuffer(globalLight.ShadowMapFramebufferDescriptor.Value);

            _shadowPassGraphicsPipeline.BindVertexBuffer(_meshPool.VertexBuffer, 0, 0);
            _shadowPassGraphicsPipeline.BindIndexBuffer(_meshPool.IndexBuffer);
            _shadowPassGraphicsPipeline.BindShaderStorageBuffer(_instanceBuffer, 3);
            
            _shadowPassGraphicsPipeline.BindUniformBuffer(globalLightForShadowMapBuffer, 2);
            _shadowPassGraphicsPipeline.MultiDrawElementsIndirect(_indirectBuffer, _meshInstances.Count);

            _graphicsContext.EndRender();
        }
    }

    private void RenderGlobalLights(ICamera camera)
    {
        if (_lightsGlobalPassGraphicsPipeline == null ||
            _shadowSettingsBuffer == null ||
            _cameraInformationBuffer == null ||
            _nearestSampler == null ||
            _linearMipmapLinearRepeatSampler == null ||
            _preparePrepareImageBasedLightingPass.IrradianceCubeTexture == null ||
            _preparePrepareImageBasedLightingPass.PrefilteredCubeTexture == null ||
            _preparePrepareImageBasedLightingPass.BrdfIntegrationLutTexture == null ||
            _blueNoiseTexture == null)
        {
            return;
        }

        _shadowSettingsBuffer.Update(_shadowSettings);

        using var globalLightsBuffer = _graphicsContext.CreateShaderStorageBuffer<GpuGlobalLight>("GlobalGpuLight");
        globalLightsBuffer.AllocateStorage(Marshal.SizeOf<GpuGlobalLight>() * _globalLights.Count, StorageAllocationFlags.Dynamic);

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < _globalLights.Count; i++)
        {
            var globalLight = _globalLights[i];
            globalLight.UpdateMatrices(camera);
            var gpuGlobalLight = globalLight.ToGpuGlobalLight();

            globalLightsBuffer.Update(gpuGlobalLight, i);
        }
        //_localLightsBuffer.Update(_localLights.ToArray());

        _graphicsContext.BindGraphicsPipeline(_lightsGlobalPassGraphicsPipeline);

        _lightsGlobalPassGraphicsPipeline.BindUniformBuffer(_cameraInformationBuffer, 0);
        _lightsGlobalPassGraphicsPipeline.BindUniformBuffer(_shadowSettingsBuffer, 1);
        _lightsGlobalPassGraphicsPipeline.BindUniformBuffer(_lightPassParametersBuffer, 2);
        _lightsGlobalPassGraphicsPipeline.BindShaderStorageBuffer(globalLightsBuffer, 3);
        _lightsGlobalPassGraphicsPipeline.BindShaderStorageBuffer(_localLightsBuffer, 4);

        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _gBufferPass.DepthBufferTexture, 0);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _gBufferPass.GBufferAlbedoTexture, 1);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _gBufferPass.GBufferNormalTexture, 2);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _gBufferPass.GBufferMaterialTexture, 3);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _gBufferPass.GBufferEmissiveTexture, 4);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_linearMipmapLinearRepeatSampler, _blueNoiseTexture, 5);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_linearMipmapLinearClampedSampler,
            _preparePrepareImageBasedLightingPass.BrdfIntegrationLutTexture, 6);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_linearMipmapLinearRepeatSampler,
            _preparePrepareImageBasedLightingPass.IrradianceCubeTexture, 7);
        _lightsGlobalPassGraphicsPipeline.BindSampledTexture(_linearMipmapLinearRepeatSampler,
            _preparePrepareImageBasedLightingPass.PrefilteredCubeTexture, 8);

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

    private void RenderFinal(ICamera camera)
    {
        if (_cameraInformationBuffer == null || 
            _nearestSampler == null || 
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
        _finalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _gBufferPass.DepthBufferTexture, 0);
        _finalPassGraphicsPipeline.BindSampledTexture(_nearestSampler, _lightsTexture, 1);
        _finalPassGraphicsPipeline.BindSampledTexture(_linearMipmapLinearRepeatSampler, _preparePrepareImageBasedLightingPass.PrefilteredCubeTexture, 2);
        
        _finalPassGraphicsPipeline.BindUniformBuffer(_uchimuraSettingsBuffer, 3);
        _finalPassGraphicsPipeline.DrawArrays(3);
        
        _graphicsContext.EndRender();
        
        {
            var globalLightsVertices = CalculateGlobalLightsFrustumVertices(camera);
            _lineRenderer.SetLines(globalLightsVertices);
        }
        _lineRenderer.Draw(_cameraInformationBuffer);    
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
        var elbow = m + a * (x - m);

        return T * w0 + elbow * w1 + s * w2;
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