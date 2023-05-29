using System.Runtime.InteropServices;
using lessGravity.Mathematics;

namespace OpenSpace.Engine.Graphics;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct VertexPositionUv
{
    public VertexPositionUv(Vector3 position, Vector2 uv)
    {
        Position = position;
        Uv = uv;
    }

    public readonly Vector3 Position;

    public readonly Vector2 Uv;
}