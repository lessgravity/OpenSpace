using JoltPhysicsSharp;

namespace OpenSpace.Physics;

public class ObjectBroadPhaseFilter : ObjectVsBroadPhaseLayerFilter
{
    protected override bool ShouldCollide(ObjectLayer layer1, BroadPhaseLayer layer2)
    {
        return true;
    }
}