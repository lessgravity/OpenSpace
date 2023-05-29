using System;

namespace OpenSpace.Engine.Graphics;

public interface ISamplerLibrary : IDisposable
{
    ISampler GetSampler(SamplerInformation samplerInformation);
    
    void AddSamplerIfNotExists(SamplerInformation? samplerInformation);
}