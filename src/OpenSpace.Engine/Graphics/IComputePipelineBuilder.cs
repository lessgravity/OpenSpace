using CSharpFunctionalExtensions;

namespace OpenSpace.Engine.Graphics;

public interface IComputePipelineBuilder
{
    Result<IComputePipeline> Build(Label label);

    IComputePipelineBuilder WithShaderFromFile(string computeShaderFilePath);

    IComputePipelineBuilder WithShaderFromSource(string computeShaderSource);
}