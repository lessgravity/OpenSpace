using System;

namespace OpenSpace.Engine.Graphics;

public interface ISampler : IDisposable
{
    uint Id { get; }
}