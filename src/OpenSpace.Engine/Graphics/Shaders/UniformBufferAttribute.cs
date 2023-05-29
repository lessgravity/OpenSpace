using System;

namespace OpenSpace.Engine.Graphics.Shaders;

[AttributeUsage(AttributeTargets.Struct)]
public class UniformBufferAttribute : GlslAttribute
{
    public int Binding { get; set; }
}