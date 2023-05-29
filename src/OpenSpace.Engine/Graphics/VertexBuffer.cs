using lessGravity.Native.OpenGL;

namespace OpenSpace.Engine.Graphics;

internal sealed class VertexBuffer<TVertex> : Buffer<TVertex>, IVertexBuffer where TVertex: unmanaged
{
    internal VertexBuffer(Label label)
        : base(BufferTarget.VertexBuffer, label)
    {
    }

    public void Bind(IInputLayout inputLayout, uint bindingIndex)
    {
        GL.VertexArrayVertexBuffer(
            inputLayout.Id,
            bindingIndex,
            Id,
            nint.Zero,
            Stride);
    }

    public void Bind(IInputLayout inputLayout, uint bindingIndex, uint offset)
    {
        GL.VertexArrayVertexBuffer(
            inputLayout.Id,
            bindingIndex,
            Id,
            (nint)offset,
            Stride);
    }
}