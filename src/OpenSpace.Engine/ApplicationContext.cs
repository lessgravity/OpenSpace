using EngineKit.Mathematics;

namespace OpenSpace.Engine;

internal sealed class ApplicationContext : IApplicationContext
{
    public Point ScreenSize { get; set; }

    public Point WindowSize { get; set; }

    public Point FramebufferSize { get; set; }

    public Point ScaledFramebufferSize { get; set; }

    public bool ShowResizeInLog { get; set; }
}