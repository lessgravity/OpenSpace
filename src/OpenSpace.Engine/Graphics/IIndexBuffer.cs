namespace OpenSpace.Engine.Graphics;

public interface IIndexBuffer : IBuffer
{
    void Bind(IInputLayout inputLayout);
}