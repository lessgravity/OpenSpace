using System.Runtime.InteropServices;
using EngineKit.Mathematics;

namespace OpenSpace.Engine.Graphics;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct VertexPosition
{
    public VertexPosition(Vector3 position)
    {
        Position = position;
    }

    public readonly Vector3 Position;
}