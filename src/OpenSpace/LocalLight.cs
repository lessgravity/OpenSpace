using System;
using EngineKit.Mathematics;
using EngineKit.Graphics;

namespace OpenSpace;

public class LocalLight : IDisposable
{
    public Vector3 Position;
    
    public Vector3 Color;
    
    public float Intensity;

    public LocalLightType Type;
    
    public ITexture? ShadowMapTexture;
    
    public FramebufferDescriptor? ShadowMapFramebufferDescriptor;
    
    public void Dispose()
    {
        ShadowMapTexture?.Dispose();
    }
}