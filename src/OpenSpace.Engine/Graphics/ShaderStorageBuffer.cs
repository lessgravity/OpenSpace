using lessGravity.Native.OpenGL;
using OpenSpace.Engine.Extensions;

namespace OpenSpace.Engine.Graphics;

internal sealed class ShaderStorageBuffer<T> : Buffer<T>, IShaderStorageBuffer
    where T : unmanaged
{
    internal ShaderStorageBuffer(Label label)
        : base(BufferTarget.ShaderStorageBuffer, label)
    {
    }

    public void Bind(uint bindingIndex)
    {
        GL.BindBufferBase(BufferTarget.ShaderStorageBuffer.ToGL(), bindingIndex, Id);
    }
}