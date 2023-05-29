using System.Collections.Generic;

namespace OpenSpace.Engine.Graphics;

public class Model
{
    public Model(string name, IEnumerable<ModelMesh> modelMeshes)
    {
        Name = name;
        ModelMeshes = modelMeshes;
    }
    
    public string Name { get; set; }
    
    public IEnumerable<ModelMesh> ModelMeshes { get; }
}