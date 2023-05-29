namespace OpenSpace.Engine.Graphics;

public record struct DepthStencilDescriptor(
    bool IsDepthTestEnabled = true,
    bool IsDepthWriteEnabled = true,
    CompareFunction DepthCompareFunction = CompareFunction.Less);