using System;
using EngineKit;
using EngineKit.Mathematics;
using EngineKit.Graphics;

namespace OpenSpace.Game;

public class GlobalLight : IDisposable
{
    public float Azimuth;
    public float Altitude;
    
    public Vector3 Direction;
    
    public Vector3 Color;
    
    public Vector2 Dimensions;

    public float Near;
    
    public float Far;

    public bool IsShadowCaster;

    public FramebufferDescriptor? ShadowMapFramebufferDescriptor;
    
    public ITexture? ShadowMapTexture;

    public TextureView? ShadowMapTextureView;

    public int ShadowQuality;
    
    public float Intensity;

    public BoundingFrustum BoundingFrustum => new BoundingFrustum(ViewMatrix * ProjectionMatrix);

    public Matrix ProjectionMatrix;

    public Matrix ViewMatrix;
    
    public GpuGlobalLight ToGpuGlobalLight()
    {
        return new GpuGlobalLight
        {
            ProjectionMatrix = ProjectionMatrix,
            ViewMatrix = ViewMatrix,
            Direction = new Vector4(Direction, ShadowQuality),
            Color = new Vector4(Color, Intensity),
            ShadowMapTextureHandle = ShadowMapTexture?.TextureHandle ?? 0
        };
    }

    public void UpdateMatrices(ICamera camera)
    {
        var rotationMatrix = Matrix.RotationX(MathHelper.ToRadians(Altitude)) *
                             Matrix.RotationY(MathHelper.ToRadians(Azimuth));
        Direction = rotationMatrix.Forward;
        var eye = 100 * -Vector3.Normalize(Direction);
        ProjectionMatrix = Matrix.OrthoOffCenterRH(
            -Dimensions.X / 2.0f,
            Dimensions.X / 2.0f,
            -Dimensions.Y / 2.0f,
            Dimensions.Y / 2.0f,
            Near, 
            Far);
        ViewMatrix = Matrix.LookAtRH(/*camera.Position*/ + eye, /*camera.Position + */eye + Vector3.Normalize(Direction), Vector3.Up);
        Direction = eye;
    }
    
    public void CreateGlobalLightShadowTexture(IGraphicsContext graphicsContext, ISampler shadowMapSampler, int size)
    {
        var label = $"Directional-Light-ShadowMap-{GetHashCode()}";
        if (ShadowMapFramebufferDescriptor != null)
        {
            graphicsContext.RemoveFramebuffer(ShadowMapFramebufferDescriptor.Value);
        }
        ShadowMapTextureView?.Dispose();
        ShadowMapTexture?.Dispose();
        
        ShadowMapTexture = graphicsContext.CreateTexture2D(size, size, Format.D16UNorm, label);
        ShadowMapTexture.MakeResident(shadowMapSampler);
        ShadowMapTextureView = ShadowMapTexture.CreateTextureView(SwizzleMapping.CreateForDepthTextures());
        ShadowMapFramebufferDescriptor = new FramebufferDescriptorBuilder()
            .WithDepthAttachment(ShadowMapTexture, true)
            .WithViewport(size, size, 0)
            .Build(label);
    }

    public void Dispose()
    {
        ShadowMapTextureView?.Dispose();
        ShadowMapTexture?.Dispose();
    }
}