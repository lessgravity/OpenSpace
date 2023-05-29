namespace OpenSpace.Engine.Graphics;

public record struct FramebufferRenderAttachment(
    ITexture Texture,
    ClearValue ClearValue,
    bool Clear);