using System.Diagnostics;
using System.Linq;
using EngineKit.Mathematics;
using OpenSpace.Ecs.Components;
using EngineKit.Graphics;
using OpenSpace.Renderers;
using Serilog;

namespace OpenSpace.Ecs.Systems;

public class PreRenderSystem : ISystem
{
    private readonly ILogger _logger;
    private readonly IEntityWorld _entityWorld;
    private readonly IRenderer _renderer;
    private readonly IMaterialLibrary _materialLibrary;
    private readonly IStatistics _statistics;
    private readonly ICamera _camera;

    public PreRenderSystem(
        ILogger logger,
        IEntityWorld entityWorld,
        IRenderer renderer,
        IMaterialLibrary materialLibrary,
        IStatistics statistics,
        ICamera camera)
    {
        _logger = logger;
        _entityWorld = entityWorld;
        _renderer = renderer;
        _materialLibrary = materialLibrary;
        _statistics = statistics;
        _camera = camera;
    }
    
    public void Update(float deltaTime)
    {
        var sw = Stopwatch.StartNew();
        _renderer.ClearMeshInstances();
        sw.Stop();
        _statistics.PreRenderClearMeshDuration = sw.Elapsed.TotalMilliseconds;
        
        sw.Restart();
        var entities = _entityWorld.GetEntitiesWithComponents<ModelRendererComponent, TransformComponent>();
        sw.Stop();
        _statistics.PreRenderGetEntitiesDuration = sw.Elapsed.TotalMilliseconds;

        var viewFrustum = BoundingFrustum.FromCamera(
            _camera.Position,
            _camera.Direction,
            Vector3.Up,
            _camera.FieldOfView,
            _camera.NearPlane,
            _camera.FarPlane,
            _camera.AspectRatio);
        
        sw.Restart();
        _statistics.PreRenderMeshCount = 0;
        foreach (var entity in entities)
        {
            entity.UpdateTransforms();
            var transform = entity.GetComponent<TransformComponent>();
            var modelRenderer = entity.GetComponent<ModelRendererComponent>();
            var model = modelRenderer.Model;
            var material = modelRenderer.Material;

            if (modelRenderer.BoundingBox == null)
            {
                modelRenderer.BoundingBox = CalculateBoundingBox(model, transform);
            }

            var pooledMeshes = model
                .ModelMeshes
                //.Where(meshPrimitive => viewFrustum.Contains(modelRenderer.BoundingBox.Value) != ContainmentType.Disjoint)
                .Select(meshPrimitive => _renderer.GetOrAddMeshPrimitive(meshPrimitive.MeshPrimitive));
            foreach (var pooledMesh in pooledMeshes)
            {
                var pooledMaterial = _renderer.GetOrAddMaterial(material ?? _materialLibrary.GetMaterialByName(pooledMesh.MaterialName));
                _renderer.AddMeshInstance(new MeshInstance(pooledMesh, transform.GlobalWorldMatrix, pooledMaterial));
                _statistics.PreRenderMeshCount++;
            }            
        }
        sw.Stop();
        _statistics.PreRenderAddMeshDuration = sw.Elapsed.TotalMilliseconds;
    }

    private BoundingBox CalculateBoundingBox(Model model, TransformComponent transformComponent)
    {
        var bb = new BoundingBox();
        foreach (var modelMesh in model.ModelMeshes)
        {
            bb = BoundingBox.Merge(bb, modelMesh.MeshPrimitive.BoundingBox);
        }

        var bbMin = Vector3.TransformPosition(bb.Minimum, transformComponent.GlobalWorldMatrix);
        var bbMax = Vector3.TransformPosition(bb.Maximum, transformComponent.GlobalWorldMatrix);

        return new BoundingBox(bbMin, bbMax);
    }

    private BoundingBox CalculateBoundingBox(MeshPrimitive meshPrimitive, TransformComponent transform)
    {
        var newPositions = meshPrimitive
            .Positions
            .Select(position => Vector3.TransformPosition(position, transform.GlobalWorldMatrix))
            .ToArray();
        return BoundingBox.FromPoints(newPositions);
    }
}