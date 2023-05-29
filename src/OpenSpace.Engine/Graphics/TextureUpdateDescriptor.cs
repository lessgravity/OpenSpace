using lessGravity.Mathematics;

namespace OpenSpace.Engine.Graphics;

public record struct TextureUpdateDescriptor
{
    public UploadDimension UploadDimension;

    public int Level;

    public Int3 Offset;

    public Int3 Size;

    public UploadFormat UploadFormat;

    public UploadType UploadType;
}