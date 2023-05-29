using lessGravity.Mathematics;

namespace OpenSpace.Ecs.Components;

public class GlobalLightComponent : Component
{
    private Vector3 _direction;

    public Vector3 Direction
    {
        get => _direction;
        set => SetValue(ref _direction, value);
    }

    private Vector3 _color;

    public Vector3 Color
    {
        get => _color;
        set => SetValue(ref _color, value);
    }

    private float _intensity;

    public float Intensity
    {
        get => _intensity;
        set => SetValue(ref _intensity, value);
    }

    private Vector2 _dimensions;

    public Vector2 Dimensions
    {
        get => _dimensions;
        set => SetValue(ref _dimensions, value);
    }

    private float _near;

    public float Near
    {
        get => _near;
        set => SetValue(ref _near, value);
    }

    private float _far;

    public float Far
    {
        get => _far;
        set => SetValue(ref _far, value);
    }

    private bool _isShadowCaster;

    public bool IsShadowCaster
    {
        get => _isShadowCaster;
        set => SetValue(ref _isShadowCaster, value);
    }

    private int _shadowQuality;
    public int ShadowQuality
    {
        get => _shadowQuality;
        set => SetValue(ref _shadowQuality, value);
    }
}