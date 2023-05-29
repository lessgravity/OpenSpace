using System;

namespace OpenSpace.Engine.Graphics;

public interface IMeshPool : IDisposable
{
    IVertexBuffer VertexBuffer { get; }
    
    IIndexBuffer IndexBuffer { get; }

    PooledMesh GetOrAdd(MeshPrimitive meshPrimitive);
}