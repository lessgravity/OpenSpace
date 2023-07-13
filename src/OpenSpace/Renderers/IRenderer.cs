using System;
using EngineKit.Mathematics;
using EngineKit.Graphics;

namespace OpenSpace.Renderers;

public interface IRenderer : IDisposable
{
    void AddMeshInstance(MeshInstance meshInstance);
    void AddDirectionalLight(Vector3 direction,
        Vector3 color,
        float intensity,
        Vector2 dimensions,
        float near,
        float far,
        bool isShadowCaster,
        int shadowQuality, int shadowMapSize);
    
    void AddPointLight(
        Vector3 position,
        Vector3 color,
        float intensity);
    
    void AddSpotLight(
        Vector3 position,
        Vector3 direction,
        Vector3 color,
        float intensity,
        float range,
        float cutOffAngle,
        float outerCutOffAngle);
    
    void ClearMeshInstances();
    PooledMesh GetOrAddMeshPrimitive(MeshPrimitive meshPrimitive);
    PooledMaterial GetOrAddMaterial(Material material);
    bool Load();
    void RenderWorld(ICamera camera);
    void FramebufferResized();
    void RenderDebugUI(ICamera camera);
#if DEBUG
    public bool ReloadPipelines(string? sourcePath);
#endif
    int SelectedTextureToBeRendered { get; }
    TextureView? FinalTexture { get; }
    TextureView? DepthTexture { get; }
    TextureView? GBufferAlbedoTexture { get; }
    TextureView? GBufferNormalsTexture { get; }
    TextureView? GBufferMaterialTexture { get; }
    TextureView? GBufferMotionTexture { get; }
    TextureView? GBufferEmissiveTexture { get; }
    TextureView? LightsTexture { get; }
    TextureView? FirstGlobalLightShadowMap { get; }
}