using System.Collections.Generic;

namespace OpenSpace.Engine.Graphics;
public interface IMeshLoader
{
    IReadOnlyCollection<MeshPrimitive> LoadMeshPrimitivesFromFile(string filePath);
}