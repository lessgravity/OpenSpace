using JoltPhysicsSharp;

namespace OpenSpace.Ecs.Components;

public class PhysicsBodyComponent : Component
{
    public PhysicsBodyComponent(Body rigidBody)
    {
        RigidBody = rigidBody;
    }

    public Body RigidBody { get; }
}