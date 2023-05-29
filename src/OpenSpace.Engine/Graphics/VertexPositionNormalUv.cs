using System.Runtime.InteropServices;
using lessGravity.Mathematics;

namespace OpenSpace.Engine.Graphics;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct VertexPositionNormalUv
{
    public VertexPositionNormalUv(
        Vector3 position,
        Vector3 normal,
        Vector2 uv)
    {
        Position = position;
        Normal = normal;
        Uv = uv;
    }

    public readonly Vector3 Position;

    public readonly Vector3 Normal;

    public readonly Vector2 Uv;
}