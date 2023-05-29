using System;

namespace OpenSpace.Engine;

public interface IApplication : IDisposable
{
    void Run();
}