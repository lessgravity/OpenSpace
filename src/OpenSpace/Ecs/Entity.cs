using System;
using System.Collections.Generic;
using lessGravity.Mathematics;
using OpenSpace.Ecs.Components;

namespace OpenSpace.Ecs;

public class Entity
{
    private Entity? _parent;

    public Entity(string name, Entity? parent)
    {
        Name = name;
        Components = new Dictionary<Type, Component>();
        Parent = parent;
        Children = new List<Entity>();
    }

    public Entity? Parent
    {
        get => _parent;
        set
        {
            _parent?.Children.Remove(this);
            _parent = value;
            _parent?.Children.Add(this);
        }
    }

    public IList<Entity> Children { get; }

    public EntityId Id;

    public string Name;

    public readonly Dictionary<Type, Component> Components;

    public T GetComponent<T>() where T : Component
    {
        return (T)Components[typeof(T)];
    }

    public void UpdateTransforms(Vector3 position)
    {
        var transform = GetComponent<TransformComponent>();
        transform.LocalPosition = position;

        Matrix matrix = default;
        Matrix3x3.RotationQuaternion(ref transform.LocalRotation, out var rotationMatrix);

        matrix.Row1 = new Vector4(rotationMatrix.Column1 * transform.LocalScale.X, 0f);
        matrix.Row2 = new Vector4(rotationMatrix.Column2 * transform.LocalScale.Y, 0f);
        matrix.Row3 = new Vector4(rotationMatrix.Column3 * transform.LocalScale.Z, 0f);
        matrix.Row4 = new Vector4(transform.LocalPosition, 1f);
        if (_parent != null)
        {
            _parent.UpdateTransforms();
            var parentTransform = _parent.GetComponent<TransformComponent>();
            var parentMatrix = parentTransform.GlobalWorldMatrix;
            matrix = Matrix.Multiply(matrix, parentMatrix);
        }

        transform.GlobalWorldMatrix = matrix;
    }

    public void UpdateTransforms()
    {
        var transform = GetComponent<TransformComponent>();

        Matrix matrix = default;
        Matrix3x3.RotationQuaternion(ref transform.LocalRotation, out var rotationMatrix);

        matrix.Row1 = new Vector4(rotationMatrix.Column1 * transform.LocalScale.X, 0f);
        matrix.Row2 = new Vector4(rotationMatrix.Column2 * transform.LocalScale.Y, 0f);
        matrix.Row3 = new Vector4(rotationMatrix.Column3 * transform.LocalScale.Z, 0f);
        matrix.Row4 = new Vector4(transform.LocalPosition, 1f);
        if (_parent != null)
        {
            _parent.UpdateTransforms();
            var parentTransform = _parent.GetComponent<TransformComponent>();
            var parentMatrix = parentTransform.GlobalWorldMatrix;
            matrix = Matrix.Multiply(matrix, parentMatrix);
        }

        transform.GlobalWorldMatrix = matrix;
    }
}