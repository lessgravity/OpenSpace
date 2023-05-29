using System.Collections.Generic;
using System.Linq;
using lessGravity.Mathematics;
using OpenSpace.Ecs;
using OpenSpace.Ecs.Components;
using OpenSpace.Engine.Graphics;

namespace OpenSpace.Game;

public class Scene
{
    private readonly IEntityWorld _entityWorld;
    private readonly IGraphicsContext _graphicsContext;

    private readonly IList<GlobalLight> _globalLights;
    private readonly IList<LocalLight> _localLights;

    public Scene(IEntityWorld entityWorld, IGraphicsContext graphicsContext)
    {
        _entityWorld = entityWorld;
        _entityWorld.ComponentAdded += EntityWorldOnComponentAdded;
        _entityWorld.ComponentRemoved += EntityWorldOnComponentRemoved;
        _entityWorld.ComponentChanged += EntityWorldOnComponentChanged;
        _graphicsContext = graphicsContext;
        _globalLights = new List<GlobalLight>();
        _localLights = new List<LocalLight>();
    }

    public EntityId CreateEntity(string name, EntityId? parentEntityId)
    {
        return _entityWorld.CreateEntity(name, parentEntityId);
    }

    public void AddComponent<TComponent>(EntityId entityId, TComponent component) where TComponent : Component
    {
        _entityWorld.AddComponent(entityId, component);
    }

    public void AddGlobalLight(Vector3 direction, float intensity, Color color, bool isShadowCaster)
    {
        if (_globalLights.Any(gl =>
                gl.Direction == direction || gl.Intensity == intensity || gl.Color == color.ToVector3() ||
                gl.IsShadowCaster == isShadowCaster))
        {
            return;
        }

        var globalLight = new GlobalLight
        {
            Direction = direction,
            Intensity = intensity,
            Color = color.ToVector3(),
            IsShadowCaster = isShadowCaster,
            Dimensions = new Vector2(128, 128),
            Near = -128,
            Far = 128,
            ShadowQuality = 0
        };
        _globalLights.Add(globalLight);
        
        
    }

    public void AddLocalLight(LocalLightType lightType)
    {
        
    }

    public void PrepareScene()
    {
        
    }
    
    private void CreateShadowMap(GlobalLight globalLight, int width, int height)
    {
        globalLight.ShadowMapTexture?.Dispose();
        globalLight.ShadowMapTexture = _graphicsContext.CreateTexture2D(width, height, Format.D16UNorm, $"Directional-Light-ShadowMap-{GetHashCode()}");
        //globalLight.ShadowMapTexture.MakeResident(_linearMipmapLinearRepeatSampler);

        if (globalLight.ShadowMapFramebufferDescriptor != null)
        {
            _graphicsContext.RemoveFramebuffer(globalLight.ShadowMapFramebufferDescriptor.Value);
        }

        globalLight.ShadowMapFramebufferDescriptor = new FramebufferDescriptorBuilder()
            .WithDepthAttachment(globalLight.ShadowMapTexture, true)
            .WithViewport(width, height, 0)
            .Build($"Directional-Light-Shadow-{GetHashCode()}");
    }
    
    private void EntityWorldOnComponentAdded(Component component)
    {
        if (component is GlobalLightComponent globalLightComponent)
        {
            globalLightComponent.ComponentChanged += EntityWorldOnComponentChanged;
        }
    }    
    
    private void EntityWorldOnComponentRemoved(Component component)
    {
        
    }    
    
    private void EntityWorldOnComponentChanged(Component component)
    {
        
    }
}