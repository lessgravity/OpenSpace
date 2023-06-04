using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenSpace.Ecs.Components;
using OpenSpace.Physics;

namespace OpenSpace.Ecs.Systems;

public class TransformSystem : ISystem
{
    private readonly IEntityWorld _entityWorld;
    private readonly IPhysicsWorld _physicsWorld;
    private readonly IStatistics _statistics;

    public TransformSystem(IEntityWorld entityWorld, IPhysicsWorld physicsWorld, IStatistics statistics)
    {
        _entityWorld = entityWorld;
        _physicsWorld = physicsWorld;
        _statistics = statistics;
    }
    
    public void Update(float deltaTime)
    {
        var sw = Stopwatch.StartNew();
        var entities = _entityWorld.GetEntitiesWithComponents<TransformComponent>();
        var entitiesSpan = CollectionsMarshal.AsSpan(entities);
        ref var entityRef = ref MemoryMarshal.GetReference(entitiesSpan);
        for (var i = 0; i < entitiesSpan.Length; i++)
        {
            var entity = Unsafe.Add(ref entityRef, i);

            var physicsBody = _entityWorld.GetComponent<PhysicsBodyComponent>(entity.Id);
            if (physicsBody != null)
            {
                entity.UpdateTransforms(_physicsWorld.GetPosition(physicsBody.RigidBody.ID));
            }
            else
            {
                entity.UpdateTransforms();
            }

            entity.UpdateTransforms();
        }
        sw.Stop();
        _statistics.UpdateTransformSystemDuration = sw.ElapsedMilliseconds;
    }
}