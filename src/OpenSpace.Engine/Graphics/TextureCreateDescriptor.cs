using EngineKit.Mathematics;

namespace OpenSpace.Engine.Graphics;

public record struct TextureCreateDescriptor
{
    public ImageType ImageType;

    public Format Format;

    public Int3 Size;

    public uint MipLevels;

    public uint ArrayLayers;

    public TextureSampleCount TextureSampleCount;

    public string? Label;
}