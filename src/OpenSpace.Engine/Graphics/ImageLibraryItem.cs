using SixLabors.ImageSharp;

namespace OpenSpace.Engine.Graphics;

public struct ImageLibraryItem
{
    public string ImageName { get; set; }

    public string? ImageFilePath { get; set; }

    public Image? Image { get; set; }

    public int TextureArrayIndex { get; set; }
}