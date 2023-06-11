using EngineKit.Mathematics;

namespace OpenSpace;

public readonly struct GpuLocalLight
{
    public readonly Vector4 Position;
    public readonly Vector4 Direction;
    public readonly Vector4 Color; // Color, Intensity
    public readonly Vector4 LightProperties; // Range, CutOffAngle, OuterCutOffAngle, LightType

    private GpuLocalLight(
        Vector4 position,
        Vector4 direction,
        Vector4 color,
        Vector4 lightProperties)
    {
        Position = position;
        Direction = direction;
        Color = color;
        LightProperties = lightProperties;
    }

    public static GpuLocalLight CreateSpotLight(
        Vector3 position,
        Vector3 color,
        Vector3 direction,
        float intensity,
        float range,
        float cutOffAngle,
        float outerCutOffAngle)
    {
        return new GpuLocalLight(
            new Vector4(position, 1.0f),
            new Vector4(direction, 1.0f),
            new Vector4(color, intensity),
            new Vector4(range, cutOffAngle, outerCutOffAngle, 2));
    }

    public static GpuLocalLight CreatePointLight(Vector3 position, Vector3 color, float intensity)
    {
        return new GpuLocalLight(
            new Vector4(position, 1.0f),
            new Vector4(Vector3.UnitZ, 1.0f),
            new Vector4(color, intensity),
            new Vector4(0.25f, 0.05f, 0.001f, 1));
    }
}