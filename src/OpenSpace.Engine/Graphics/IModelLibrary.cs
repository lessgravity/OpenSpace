using System.Collections.Generic;

namespace OpenSpace.Engine.Graphics;

public interface IModelLibrary
{
    void AddModel(Model model);

    void AddModelFromFile(string name, string filePath);

    Model? GetModelByName(string name);

    IReadOnlyCollection<string> GetModelNames();
}