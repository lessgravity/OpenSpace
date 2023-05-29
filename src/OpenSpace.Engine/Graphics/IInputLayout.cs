using System;

namespace OpenSpace.Engine.Graphics;

public interface IInputLayout : IDisposable
{
    uint Id { get; }

    void Bind();
}