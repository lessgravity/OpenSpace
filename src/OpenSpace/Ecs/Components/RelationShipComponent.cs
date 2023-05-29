using System.Collections.Generic;

namespace OpenSpace.Ecs.Components;

public class RelationShipComponent : Component
{
    private readonly IList<int> _children;

    public RelationShipComponent(int? parent)
    {
        Parent = parent;
        _children = new List<int>();
    }

    public IList<int> Children => _children;

    public int? Parent { get; }

    public void AddChild(int child)
    {
        _children.Add(child);
    }

    public void RemoveChild(int child)
    {
        _children.Remove(child);
    }
}