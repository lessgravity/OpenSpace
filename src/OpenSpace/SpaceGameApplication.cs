using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ImGuiNET;
using EngineKit.Mathematics;
using EngineKit.Native.Glfw;
using EngineKit.Native.OpenGL;
using Microsoft.Extensions.Options;
using OpenSpace.Ecs;
using OpenSpace.Ecs.Components;
using OpenSpace.Ecs.Systems;
using EngineKit;
using EngineKit.Graphics;
using EngineKit.Input;
using EngineKit.Native.Ktx;
using EngineKit.UI;
using OpenSpace.Messages;
using OpenSpace.Renderers;
using OpenSpace.Windows;
using Serilog;
using Vector2 = EngineKit.Mathematics.Vector2;
using Vector3 = EngineKit.Mathematics.Vector3;

namespace OpenSpace;

internal sealed class SpaceGameApplication : GameApplication
{
    private readonly ILogger _logger;
    private readonly IApplicationContext _applicationContext;
    private readonly IMetrics _metrics;
    private readonly ILimits _limits;

    private readonly ICamera _camera;
    private readonly IModelLibrary _modelLibrary;
    private readonly IMaterialLibrary _materialLibrary;
    private readonly IRenderer _renderer;
    
    private readonly AssetUiWindow _assetUiWindow;
    private readonly PropertiesUiWindow _propertiesUiWindow;
    private readonly HierarchyUiWindow _hierarchyUiWindow;
    private readonly SceneUiWindow _sceneUiWindow;

    private readonly CurvePlot _frameTimePlot;

    private readonly IEntityWorld _entityWorld;
    private readonly PreRenderSystem _preRenderSystem;
    private readonly IStatistics _statistics;

    public SpaceGameApplication(
        ILogger logger,
        IOptions<WindowSettings> windowSettings,
        IOptions<ContextSettings> contextSettings,
        IApplicationContext applicationContext,
        IMetrics metrics,
        ILimits limits,
        IInputProvider inputProvider,
        IGraphicsContext graphicsContext,
        IUIRenderer uiRenderer,
        ICamera camera,
        IModelLibrary modelLibrary,
        IMaterialLibrary materialLibrary,
        IRenderer renderer,
        IEnumerable<UiWindow> uiWindows,
        IMessageBus messageBus,
        IEntityWorld entityWorld,
        IStatistics statistics)
        : base(
            logger,
            windowSettings,
            contextSettings,
            applicationContext,
            metrics,
            limits,
            inputProvider,
            graphicsContext,
            uiRenderer,
            messageBus,
            entityWorld)
    {
        _logger = logger;
        _applicationContext = applicationContext;
        _metrics = metrics;
        _limits = limits;
        _camera = camera;
        _modelLibrary = modelLibrary;
        _materialLibrary = materialLibrary;
        _entityWorld = entityWorld;
        _renderer = renderer;
        _statistics = statistics;

        messageBus.Subscribe<SwitchedToGameModeMessage>(SwitchedToGameMode);
        messageBus.Subscribe<SwitchedToEditorModeMessage>(SwitchedToEditorMode);
        
        var windows = uiWindows.ToList();
        _assetUiWindow = windows.OfType<AssetUiWindow>().First();
        _propertiesUiWindow = windows.OfType<PropertiesUiWindow>().First();
        _sceneUiWindow = windows.OfType<SceneUiWindow>().First();
        _hierarchyUiWindow = windows.OfType<HierarchyUiWindow>().First();
        _frameTimePlot = new CurvePlot("ms") { MinValue = 0.0f, MaxValue = 30.0f, Damping = true };

        _preRenderSystem = new PreRenderSystem(logger, entityWorld, renderer, materialLibrary, statistics, camera);
    }
    
    protected override void FramebufferResized()
    {
        base.FramebufferResized();
        _renderer.FramebufferResized();
    }

    protected override void HandleDebugger(out bool breakOnError)
    {
        breakOnError = true;
    }

    protected override bool Load()
    {
        if (!base.Load())
        {
            return false;
        }
        
        if (!_renderer.Load())
        {
            return false;
        }

        if (!LoadMeshes())
        {
            return false;
        }
        
        _camera.ProcessMouseMovement();

        return true;
    }

    protected override void Render(float deltaTime)
    {
        _renderer.RenderWorld(_camera);

        UIRenderer.BeginLayout();
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu($"{MaterialDesignIcons.File}  File"))
                {
                    if (ImGui.MenuItem($"{MaterialDesignIcons.ExitToApp}  Quit"))
                    {
                        Close();
                    }

                    ImGui.EndMenu();
                }
#if DEBUG
                if (ImGui.Button("Reload Pipelines"))
                {
                    _renderer.ReloadPipelines("../../../../OpenSpace.Assets");
                }
#endif
                ImGui.SetCursorPosX(ImGui.GetWindowViewport().Size.X / 2);
                if (ImGui.Button($"{(ProgramMode == ProgramMode.Editor ? MaterialDesignIcons.Play : MaterialDesignIcons.Stop)}  "))
                {
                    if (ProgramMode == ProgramMode.Editor)
                    {
                        SwitchToGameMode();
                    }
                    else
                    {
                        SwitchToEditorMode();
                    }
                }
                ImGui.TextUnformatted(ProgramMode.ToString());
                
                //ImGui.SetCursorPos(new Num.Vector2(ImGui.GetWindowViewport().Size.X - 196, 0));
                //ImGui.TextUnformatted($"FT: {_metrics.AverageFrameTime:F4} ms");

                if (ImGui.Begin("Frametime"))
                {
                    _frameTimePlot.Draw((float)_metrics.AverageFrameTime);
                    ImGui.TextUnformatted($"PreRender.Clear: {_statistics.PreRenderClearMeshDuration:F2} ms");
                    ImGui.TextUnformatted($"PreRender.GetEntities: {_statistics.PreRenderGetEntitiesDuration:F2} ms");
                    ImGui.TextUnformatted($"PreRender.AddMesh: {_statistics.PreRenderAddMeshDuration:F2} ms");
                    ImGui.TextUnformatted($"PreRender.MeshCount: {_statistics.PreRenderMeshCount}");
                    ImGui.Separator();
                    ImGui.TextUnformatted($"TransformUpdate: {_statistics.UpdateTransformSystemDuration} ms");
                    ImGui.End();
                }

                ImGui.EndMenuBar();
                ImGui.EndMainMenuBar();
            }
        }

        if (ProgramMode == ProgramMode.Editor)
        {
            _assetUiWindow?.Render();
            switch (_renderer.SelectedTextureToBeRendered)
            {
                default:
                    _sceneUiWindow.SetTexture(_renderer.FinalTexture);
                    break;
                case 0:
                    _sceneUiWindow.SetTexture(_renderer.DepthTexture);
                    break;
                case 1:
                    _sceneUiWindow.SetTexture(_renderer.GBufferAlbedoTexture);
                    break;
                case 2:
                    _sceneUiWindow.SetTexture(_renderer.GBufferNormalsTexture);
                    break;
                case 3:
                    _sceneUiWindow.SetTexture(_renderer.GBufferMaterialTexture);
                    break;
                case 4:
                    _sceneUiWindow.SetTexture(_renderer.GBufferMotionTexture);
                    break;
                case 5:
                    _sceneUiWindow.SetTexture(_renderer.GBufferEmissiveTexture);
                    break;
                case 6:
                    _sceneUiWindow.SetTexture(_renderer.LightsTexture);
                    break;
            }

            _renderer.RenderDebugUI(_camera);
            UIRenderer.ShowDemoWindow();
            
            _sceneUiWindow.Render();
            _sceneUiWindow.SetOsdTexture(_renderer.FirstGlobalLightShadowMap);
        }

        UIRenderer.EndLayout();
        GL.PopDebugGroup();
        if (_limits.IsLaunchedByNSightGraphicsOnLinux)
        {
            GraphicsContext.Finish();
        }
    }

    protected override void Unload()
    {
        _renderer.Dispose();
        base.Unload();
    }

    private float _globalLight1Radius = 20f;
    private float _elapsedTime;

    protected override void Update(float deltaTime)
    {
        _elapsedTime += deltaTime;
        
        base.Update(deltaTime);
        _entityWorld.Update(deltaTime);
        _preRenderSystem.Update(deltaTime);
        
        if (IsMousePressed(Glfw.MouseButton.ButtonRight))
        {
            _camera.ProcessMouseMovement();
        }

        var movement = Vector3.Zero;
        var speedFactor = 40.0f;
        if (IsKeyPressed(Glfw.Key.KeyW))
        {
            movement += _camera.Direction;
        }
        if (IsKeyPressed(Glfw.Key.KeyS))
        {
            movement -= _camera.Direction;
        }
        if (IsKeyPressed(Glfw.Key.KeyA))
        {
            movement += -_camera.Right;
        }
        if (IsKeyPressed(Glfw.Key.KeyD))
        {
            movement += _camera.Right;
        }
        if (IsKeyPressed(Glfw.Key.KeyQ))
        {
            movement -= _camera.Up;
        }
        if (IsKeyPressed(Glfw.Key.KeyE))
        {
            movement += _camera.Up;
        }

        movement = Vector3.Normalize(movement) * deltaTime * 10;
        if (IsKeyPressed(Glfw.Key.KeyLeftShift))
        {
            movement *= speedFactor;
        }
        if (movement.Length() > 0.0f)
        {
            _camera.ProcessKeyboard(movement, deltaTime);
        }
    }

    protected override bool GameLoad()
    {
        
        return true;
    }

    protected override void GameUnload()
    {
    }

    private unsafe void KtxTest()
    {
        var ktxFileName =
            "/home/deccer/Personal/Code/External/Oxylus-Engine/build/_deps/ktx-src/tests/testimages/pattern_02_bc2.ktx2";
        var ktxTexture = Ktx.LoadFromFile(ktxFileName);
        if (ktxTexture->CompressionScheme != Ktx.SuperCompressionScheme.None || Ktx.NeedsTranscoding(ktxTexture))
        {
            var transcodeResult = Ktx.Transcode(ktxTexture, Ktx.TranscodeFormat.Bc7Rgba, Ktx.TranscodeFlagBits.HighQuality);
            if (transcodeResult != Ktx.KtxErrorCode.KtxSuccess)
            {
                
            }
        }
        
        Ktx.Destroy(ktxTexture);
    }

    private bool LoadMeshes()
    {
        if (!Ktx.Init())
        {
            return false;
        }

        KtxTest();
        Ktx.Terminate();
        
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/material_test_shadow_casting_on_metal_materials/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/eas_agamemnon/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/ReferencePbr/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/ReferencePbr2/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/corroded_metal_with_stripes_2k/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/DarkSideOfTheMoon/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_IntelSponza", "Data/Props/IntelSponza/NewSponza_Main_glTF_002.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/IntelSponzaPacked/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_IntelSponza_Curtains", "Data/Props/IntelSponzaCurtains/NewSponza_Curtains_glTF.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/someone-crate/scene.gltf");        
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/Sponza/Sponza.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/sci-fi_floor_panel/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/stylized_material_test/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/venice_mask/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/sci-fi_hallway/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/shader_ball_jl_01.glb");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/material_ball_in_3d-coat.glb");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/iridescent__shader__blender.glb");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/apollo_material_ball.glb");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/3d_material_ball.glb");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/SunTemple/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/mira_up/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/cubes/untitled.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/UE/RuinC2k/scene.gltf");
        _modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/UE/RuinC2kPacked/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/wk7_unit_blocks_advanced_huth_will/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/waterbottle/WaterBottle.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/FlightHelmet/FlightHelmet.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/DamagedHelmet/DamagedHelmet.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/x_sphere_sci-fi/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/Skull/SM_Skull_Optimized_point2.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/metalnessspecular_and_albedo/scene.gltf");
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Props/radar_robot_-_ngchipv/scene.gltf");
        /*
        _modelLibrary.AddModelFromFile("SM_Asteroid01", "Data/Props/Asteroids/Asteroid01/SM_Asteroid01.gltf");
        _modelLibrary.AddModelFromFile("SM_Asteroid02", "Data/Props/Asteroids/Asteroid02/SM_Asteroid02.gltf");
        _modelLibrary.AddModelFromFile("SM_Asteroid03", "Data/Props/Asteroids/Asteroid03/SM_Asteroid03.gltf");
        _modelLibrary.AddModelFromFile("SM_Asteroid04", "Data/Props/Asteroids/Asteroid04/SM_Asteroid04.gltf");
        _modelLibrary.AddModelFromFile("SM_Asteroid05", "Data/Props/Asteroids/Asteroid05/SM_Asteroid05.gltf");
        _modelLibrary.AddModelFromFile("SM_Asteroid06", "Data/Props/Asteroids/Asteroid06/SM_Asteroid06.gltf");
        _modelLibrary.AddModelFromFile("SM_Asteroid07", "Data/Props/Asteroids/Asteroid07/SM_Asteroid07.gltf");
        _modelLibrary.AddModelFromFile("SM_Asteroid08", "Data/Props/Asteroids/Asteroid08/SM_Asteroid08.gltf");
        _modelLibrary.AddModelFromFile("SM_Asteroid09", "Data/Props/Asteroids/Asteroid09/SM_Asteroid09.gltf");
        _modelLibrary.AddModelFromFile("SM_Asteroid10", "Data/Props/Asteroids/Asteroid10/SM_Asteroid10.gltf");
        */
        
        var defaultPbrMaterial  = new Material("M_Test_Pbr")
        {
            BaseColor = new Color4(0.1f, 0.2f, 0.3f, 1.0f),
            BaseColorImage = new ImageInformation("T_Default_B", null, null, "Data/Default/T_PlasticMesh_B.jpg"),
            NormalImage = new ImageInformation("T_Default_N", null, null, "Data/Default/T_PlasticMesh_N.jpg"),
            MetallicFactor = 1.0f,
            RoughnessFactor = 0.0f,
            MetalnessRoughnessImage = new ImageInformation("T_Default_MR", null, null, "Data/Default/T_PlasticMesh_S.jpg"),
            SpecularImage = new ImageInformation("T_Default_S", null, null, "Data/Default/T_PlasticMesh_S.jpg")
        };
        _materialLibrary.AddMaterial(defaultPbrMaterial);
        //_modelLibrary.AddModelFromFile("SM_Kentaur", "Data/Default/SM_Cube.gltf");
        
        var model1 = _modelLibrary.GetModelByName("SM_Kentaur");
        //var model1 = _modelLibrary.GetModelByName("SM_IntelSponza");
        if (model1 == null)
        {
            return false;
        }
        
        var entity1 = _entityWorld.CreateEntity(model1.Name);
        _entityWorld.AddComponent(entity1, new TransformComponent
        {
            LocalPosition = new Vector3(0, 0, 0),
            LocalScale = new Vector3(1.0f)
        });
        _entityWorld.AddComponent(entity1, new ModelRendererComponent { Model = model1/*, Material = defaultPbrMaterial*/});
        
        /*
        var model2 = _modelLibrary.GetModelByName("SM_IntelSponza_Curtains");
        if (model2 == null)
        {
            return false;
        }
        
        var entity2 = _entityWorld.CreateEntity(model2.Name);
        _entityWorld.AddComponent(entity2, new TransformComponent
        {
            LocalPosition = new Vector3(0, 0, 0),
            LocalScale = new Vector3(1.00f)
        });
        _entityWorld.AddComponent(entity2, new ModelRendererComponent { Model = model2 });
        */

        /*
        var random = new Random();
        var minVector = new Vector3(-100, -100, -100);
        var maxVector = new Vector3(100, 100, 100);
        for (var i = 0; i < 400; i++)
        {
            var asteroidName = $"SM_Asteroid{random.Next(1, 10):00}";
            var asteroidModel = _modelLibrary.GetModelByName(asteroidName);

            var transformComponent = new TransformComponent()
            {
                LocalPosition = random.NextVector3(minVector, maxVector),
                LocalRotation = new Quaternion(random.NextVector3(Vector3.Zero, Vector3.One),
                    random.NextFloat(0.0f, 1.0f)),
                LocalScale = random.NextVector3(Vector3.One * 0.25f, Vector3.One * 5)
            };
            
            var asteroidEntity = _entityWorld.CreateEntity($"asteroidInstance{i}");
            _entityWorld.AddComponent(asteroidEntity, transformComponent);
            _entityWorld.AddComponent(asteroidEntity, new ModelRendererComponent
            {
                Model = asteroidModel, 
                Material = defaultPbrMaterial,
                BoundingBox = null
            });
        }
        */
        
        _renderer.AddDirectionalLight(new Vector3(6, -4, 6), Color.Red.ToVector3(), 3f, new Vector2(32, 32), 1, 128, true, 0);
        //_renderer.AddDirectionalLight(new Vector3(-6, -7, -6), Color.Orange.ToVector3(), 40f, new Vector2(64, 64), 1, 128, true, 1);
        
        //_renderer.AddSpotLight(new Vector3(0, 10, -3), -Vector3.UnitY, Color.Red.ToVector3(), 100, 2, 12.5f, 17.5f);
        //_renderer.AddPointLight(new Vector3(0, 1, -3), Color.Teal.ToVector3(), 170);
        //_renderer.AddPointLight(new Vector3(0, 5, -1), Color.LimeGreen.ToVector3(), 90);
        //_renderer.AddPointLight(new Vector3(0, 1, 1), Color.Purple.ToVector3(), 10);
        //_renderer.AddPointLight(new Vector3(0, 3, 3), Color.LimeGreen.ToVector3(), 30);
        //_renderer.AddPointLight(new Vector3(0, 5, 5), Color.Purple.ToVector3(), 15);
        
        //_renderer.AddSpotLight(new Vector3(0, 2, 0), -Vector3.UnitY, Color.Orange.ToVector3(), 40, 3, 12.5f, 17.5f);

        return true;
    }

    private Task SwitchedToGameMode(SwitchedToGameModeMessage message)
    {
        return Task.CompletedTask;
    }

    private Task SwitchedToEditorMode(SwitchedToEditorModeMessage message)
    {
        return Task.CompletedTask;
    }
}
