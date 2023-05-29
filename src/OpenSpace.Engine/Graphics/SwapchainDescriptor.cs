using lessGravity.Mathematics;

namespace OpenSpace.Engine.Graphics;

public record struct SwapchainDescriptor
{
    public Viewport Viewport;

    public Viewport? ScissorRect;

    public bool ClearColor;

    public ClearColorValue ClearColorValue;

    public bool ClearDepth;

    public float ClearDepthValue;

    public bool ClearStencil;

    public int ClearStencilValue;

    public bool EnableSrgb;
}