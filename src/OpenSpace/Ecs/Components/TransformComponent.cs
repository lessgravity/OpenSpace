using EngineKit.Mathematics;

namespace OpenSpace.Ecs.Components;

public class TransformComponent : Component
{
    public static TransformComponent CreateFromMatrix(Matrix worldMatrix)
    {
        /*
        scale.X = 1.0f / scale.X;
        scale.Y = 1.0f / scale.Y;
        scale.Z = 1.0f / scale.Z;
        */
        worldMatrix.Decompose(out var scale, out var rotation, out var translation);
        return new TransformComponent
        {
            LocalPosition = translation,
            LocalRotation = rotation,
            LocalScale = Vector3.One
        };
    }

    public Vector3 LocalPosition = Vector3.Zero;

    public Quaternion LocalRotation = Quaternion.Identity;

    public Vector3 LocalScale = Vector3.One;

    public Vector3 GlobalPosition;

    public Quaternion GlobalRotation;

    public Vector3 GlobalScale;

    public Matrix GlobalWorldMatrix;
}