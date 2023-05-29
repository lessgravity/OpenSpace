using System;
using lessGravity.Mathematics;
using OpenSpace.Engine.Graphics;

namespace OpenSpace.Game;

public class GlobalLight : IDisposable
{
    public Vector3 Direction;
    
    public Vector3 Color;
    
    public Vector2 Dimensions;
    
    public float Near;
    
    public float Far;

    public bool IsShadowCaster;

    public FramebufferDescriptor? ShadowMapFramebufferDescriptor;
    
    public ITexture? ShadowMapTexture;

    public int ShadowQuality;
    
    public float Intensity;

    public void Dispose()
    {
        ShadowMapTexture?.Dispose();
    }
}