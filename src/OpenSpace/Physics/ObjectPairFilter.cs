using JoltPhysicsSharp;

namespace OpenSpace.Physics;

public class ObjectPairFilter : ObjectLayerPairFilter
{
    protected override bool ShouldCollide(ObjectLayer object1, ObjectLayer object2)
    {
        return true;
    }
}