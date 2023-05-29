using System;

namespace OpenSpace.Engine.Graphics;

public interface IMaterialPool : IDisposable
{
    IShaderStorageBuffer MaterialBuffer { get; }
    
    PooledMaterial GetOrAdd(Material material);
}