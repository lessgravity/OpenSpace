namespace OpenSpace.Engine.Graphics;

public interface IShaderStorageBuffer : IBuffer
{
    void Bind(uint bindingIndex);
}