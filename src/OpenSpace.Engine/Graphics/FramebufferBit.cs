using System;

namespace OpenSpace.Engine.Graphics;

[Flags]
public enum FramebufferBit
{
    DepthBufferBit = 1,
    StencilBufferBit = 2,
    ColorBufferBit = 4,
}