namespace OpenSpace.Engine.Graphics;

internal record struct ComputePipelineDescriptor
{
    public Label PipelineProgramLabel;

    public string ComputeShaderSource;
}