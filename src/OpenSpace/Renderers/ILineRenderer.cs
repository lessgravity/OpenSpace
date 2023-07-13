using System;
using System.Collections.Generic;
using EngineKit.Graphics;

namespace OpenSpace.Renderers;

public interface ILineRenderer : IDisposable
{
    bool Load();
    
    void SetLines(IEnumerable<VertexPositionColor> vertices);
    
    void Draw(IUniformBuffer cameraInformationBuffer);
}