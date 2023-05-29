using System.Runtime.InteropServices;
using lessGravity.Mathematics;

namespace OpenSpace.Engine.Graphics;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct VertexPositionNormal
{
    public VertexPositionNormal(Vector3 position, Vector3 normal)
    {
        Position = position;
        Normal = normal;
    }

    public readonly Vector3 Position;

    public readonly Vector3 Normal;
}