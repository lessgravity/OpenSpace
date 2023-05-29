namespace OpenSpace.Engine.Graphics;

public interface IUniformBuffer : IBuffer
{
    void Bind(uint bindingIndex);
}