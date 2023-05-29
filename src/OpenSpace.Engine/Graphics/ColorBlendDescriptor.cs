namespace OpenSpace.Engine.Graphics;

public record struct ColorBlendDescriptor(
    ColorBlendAttachmentDescriptor[] ColorBlendAttachmentDescriptors,
    float[] BlendConstants);