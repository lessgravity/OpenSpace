using System;

namespace OpenSpace.Renderers;

public interface IRenderPass : IDisposable
{
    void Render();
}