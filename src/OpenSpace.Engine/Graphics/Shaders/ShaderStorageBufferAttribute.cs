using System;

namespace OpenSpace.Engine.Graphics.Shaders;

[AttributeUsage(AttributeTargets.Struct)]
public class ShaderStorageBufferAttribute : GlslAttribute
{
    public int Binding { get; set; }

    public bool ReadOnly { get; set; } = true;

    public string? ArrayName { get; set; }

    public string? Alias { get; set; }
}