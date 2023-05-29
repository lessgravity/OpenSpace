using lessGravity.Mathematics;

namespace OpenSpace;

public struct GpuGlobalLight
{
    public Matrix ProjectionMatrix;
    
    public Matrix ViewMatrix;
    
    public Vector4 Direction;
    
    public Vector4 Color;

    public ulong ShadowMapTextureHandle;

    public ulong _padding1;
    
    public ulong _padding2;
    
    public ulong _padding3;
}