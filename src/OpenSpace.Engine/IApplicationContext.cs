using lessGravity.Mathematics;

namespace OpenSpace.Engine;

public interface IApplicationContext
{
    Point ScreenSize { get; set; }

    Point WindowSize { get; set; }

    Point FramebufferSize { get; set; }

    Point ScaledFramebufferSize { get; set; }

    bool ShowResizeInLog { get; set; }
}