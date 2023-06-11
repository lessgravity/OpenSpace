using EngineKit.Mathematics;
using EngineKit.Graphics;

namespace OpenSpace.Ecs.Components;

public class ModelRendererComponent : Component
{
    public Model? Model;

    public Material? Material;

    public BoundingBox? BoundingBox;
}