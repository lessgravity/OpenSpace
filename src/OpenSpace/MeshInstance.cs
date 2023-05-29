using System;
using lessGravity.Mathematics;
using OpenSpace.Engine.Graphics;

namespace OpenSpace;

public readonly struct MeshInstance : IEquatable<MeshInstance>
{
    public readonly PooledMesh Mesh;

    public readonly PooledMaterial Material;

    public readonly Matrix WorldMatrix;

    public MeshInstance(PooledMesh mesh, Matrix worldMatrix, PooledMaterial material)
    {
        Mesh = mesh;
        WorldMatrix = worldMatrix;
        Material = material;
    }

    public bool Equals(MeshInstance other)
    {
        return Mesh.Equals(other.Mesh) && Equals(Material, other.Material) && WorldMatrix.Equals(other.WorldMatrix);
    }

    public override bool Equals(object? obj)
    {
        return obj is MeshInstance other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Mesh, Material, WorldMatrix);
    }
}