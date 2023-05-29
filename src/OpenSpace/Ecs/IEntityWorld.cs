using System;
using System.Collections.Generic;
using OpenSpace.Ecs.Components;

namespace OpenSpace.Ecs;

public interface IEntityWorld
{
    event Action<Component>? ComponentAdded;
    
    event Action<Component>? ComponentRemoved;
    
    event Action<Component>? ComponentChanged;
    
    EntityId CreateEntity(string name, EntityId? parent = null);

    Entity GetEntity(EntityId entityId);

    void AddComponent<T>(EntityId entityId, T component) where T : Component;

    void RemoveComponent<T>(EntityId entityId) where T : Component;

    List<Entity> GetEntitiesWithComponents<TComponent>()
        where TComponent : Component;

    List<Entity> GetEntitiesWithComponents<TComponent1, TComponent2>()
        where TComponent1 : Component
        where TComponent2 : Component;

    void Update(float deltaTime);

    T? GetComponent<T>(EntityId entityId) where T : Component;

    IList<T> GetComponents<T>() where T : Component;
}