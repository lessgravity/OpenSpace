namespace OpenSpace.Engine.Graphics.Shaders;

public interface IShaderIncludeHandler
{
    string? HandleInclude(string? include);
}