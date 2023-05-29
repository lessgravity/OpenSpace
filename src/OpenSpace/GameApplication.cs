using lessGravity.Native.OpenGL;
using Microsoft.Extensions.Options;
using OpenSpace.Ecs;
using OpenSpace.Engine;
using OpenSpace.Engine.Graphics;
using OpenSpace.Engine.Input;
using OpenSpace.Messages;
using Serilog;
using ILimits = OpenSpace.Engine.ILimits;

namespace OpenSpace;

public abstract class GameApplication : Application
{
    private readonly ILogger _logger;

    private readonly IApplicationContext _applicationContext;

    private readonly IMetrics _metrics;
    private readonly IMessageBus _messageBus;

    protected IGraphicsContext GraphicsContext { get; }
    protected IUIRenderer UIRenderer { get; }

    protected ProgramMode ProgramMode;

    protected IEntityWorld EntityWorld { get; }

    protected GameApplication(
        ILogger logger,
        IOptions<WindowSettings> windowSettings,
        IOptions<ContextSettings> contextSettings,
        IApplicationContext applicationContext,
        IMetrics metrics,
        ILimits limits,
        IInputProvider inputProvider,
        IGraphicsContext graphicsContext,
        IUIRenderer uiRenderer,
        IMessageBus messageBus,
        IEntityWorld entityWorld)
        : base(logger, windowSettings, contextSettings, applicationContext, limits, metrics, inputProvider)
    {
        _logger = logger;
        _applicationContext = applicationContext;
        _metrics = metrics;
        _messageBus = messageBus;
        GraphicsContext = graphicsContext;
        UIRenderer = uiRenderer;
        EntityWorld = entityWorld;
        ProgramMode = ProgramMode.Game;
    }

    protected override void FramebufferResized()
    {
        base.FramebufferResized();
        UIRenderer.WindowResized(_applicationContext.FramebufferSize.X, _applicationContext.FramebufferSize.Y);
    }

    protected override bool Load()
    {
        if (!base.Load())
        {
            _logger.Error("{Category}: Unable to load", "App");
            return false;
        }

        if (!UIRenderer.Load(_applicationContext.FramebufferSize.X, _applicationContext.FramebufferSize.Y))
        {
            return false;
        }

        GL.Disable(GL.EnableType.Blend);
        GL.Disable(GL.EnableType.CullFace);
        GL.Disable(GL.EnableType.ScissorTest);
        GL.Disable(GL.EnableType.DepthTest);
        GL.Disable(GL.EnableType.StencilTest);
        GL.Disable(GL.EnableType.SampleCoverage);
        GL.Disable(GL.EnableType.SampleAlphaToCoverage);
        GL.Disable(GL.EnableType.PolygonOffsetFill);
        GL.Disable(GL.EnableType.Multisample);
        GL.Enable(GL.EnableType.FramebufferSrgb);

        if (!GameLoad())
        {
            return false;
        }
        
        return true;
    }

    protected virtual bool GameLoad()
    {
        return true;
    }

    protected virtual void GameUnload()
    {
        
    }

    protected override void Unload()
    {
        GameUnload();
        UIRenderer.Dispose();
        GraphicsContext.Dispose();
        base.Unload();
    }

    protected override void Update(float deltaTime)
    {
        UIRenderer.Update(deltaTime);
    }

    protected void SwitchToGameMode()
    {
        ProgramMode = ProgramMode.Game;
        _messageBus.PublishWait(new SwitchedToGameModeMessage());
    }

    protected void SwitchToEditorMode()
    {
        ProgramMode = ProgramMode.Editor;
        _messageBus.PublishWait(new SwitchedToEditorModeMessage());
    }
}