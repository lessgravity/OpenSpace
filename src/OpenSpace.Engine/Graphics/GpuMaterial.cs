using System.Runtime.InteropServices;
using lessGravity.Mathematics;

namespace OpenSpace.Engine.Graphics;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GpuMaterial
{
    public Vector4 BaseColor;

    public Vector4 MetalnessRoughnessOcclusion;

    public Vector4 EmissiveColor;
    
    public ulong BaseColorTexture;

    public ulong NormalTexture;
    
    public ulong MetalnessRoughnessTexture;
    
    public ulong SpecularTexture;

    public ulong OcclusionTexture;

    public ulong EmissiveTexture;
}