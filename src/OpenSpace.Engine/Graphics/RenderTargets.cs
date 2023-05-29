namespace OpenSpace.Engine.Graphics;

internal record struct RenderTargets(
    TextureView[] ColorAttachments,
    TextureView? DepthAttachment,
    TextureView? StencilAttachment);