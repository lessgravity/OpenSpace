using System;

namespace OpenSpace.Engine.Graphics;

internal interface IFramebufferCache : IDisposable
{
    uint GetOrCreateFramebuffer(FramebufferDescriptor framebufferDescriptor);

    void RemoveFramebuffer(FramebufferDescriptor framebufferDescriptor);
}