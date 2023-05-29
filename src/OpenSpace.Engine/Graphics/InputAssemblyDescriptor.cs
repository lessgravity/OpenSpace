namespace OpenSpace.Engine.Graphics;

public record struct InputAssemblyDescriptor(
    PrimitiveTopology PrimitiveTopology,
    bool IsPrimitiveRestartEnabled);