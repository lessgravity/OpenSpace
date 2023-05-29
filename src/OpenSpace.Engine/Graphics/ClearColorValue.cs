using System.Runtime.InteropServices;

namespace OpenSpace.Engine.Graphics;

[StructLayout(LayoutKind.Explicit)]
public record struct ClearColorValue
{
    [FieldOffset(0)] public float[] ColorFloat;

    [FieldOffset(0)] public uint[] ColorUnsignedInteger;

    [FieldOffset(0)] public int[] ColorSignedInteger;
}