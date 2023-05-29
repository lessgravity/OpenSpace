namespace OpenSpace.Ecs.Components;

public class ParentComponent : Component
{
    public ParentComponent(int? parentEntity)
    {
        ParentEntity = parentEntity;
    }
    
    public int? ParentEntity { get; }
}