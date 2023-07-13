using EngineKit.Mathematics;
using OpenSpace.Game;

namespace OpenSpace;

public readonly struct GpuShadowGlobalLight
{
    public GpuShadowGlobalLight(GlobalLight globalLight)
    {
        ProjectionMatrix = globalLight.ProjectionMatrix;
        ViewMatrix = globalLight.ViewMatrix;
    }

    public readonly Matrix ProjectionMatrix;

    public readonly Matrix ViewMatrix;
}