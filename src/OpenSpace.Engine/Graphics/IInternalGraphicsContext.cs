using CSharpFunctionalExtensions;

namespace OpenSpace.Engine.Graphics;

internal interface IInternalGraphicsContext
{
    Result<IGraphicsPipeline> CreateGraphicsPipeline(GraphicsPipelineDescriptor graphicsPipelineDescriptor);

    Result<IComputePipeline> CreateComputePipeline(ComputePipelineDescriptor computePipelineDescriptor);
}