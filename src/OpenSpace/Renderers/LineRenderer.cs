using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using EngineKit.Graphics;
using Serilog;

namespace OpenSpace.Renderers;

public class LineRenderer : ILineRenderer
{
    private readonly ILogger _logger;
    private readonly IGraphicsContext _graphicsContext;
    private IGraphicsPipeline? _lineGraphicsPipeline;

    private IVertexBuffer? _linesVertexBuffer;
    
    public LineRenderer(
        ILogger logger,
        IGraphicsContext graphicsContext)
    {
        _logger = logger.ForContext<LineRenderer>();
        _graphicsContext = graphicsContext;
    }
    public void Dispose()
    {
        _lineGraphicsPipeline?.Dispose();
        _linesVertexBuffer?.Dispose();
    }

    public bool Load()
    {
        var lineGraphicsPipelineResult = _graphicsContext.CreateGraphicsPipelineBuilder()
            .WithShadersFromFiles("Shaders/Line.vs.glsl", "Shaders/Line.fs.glsl")
            .WithVertexInput(new VertexInputDescriptorBuilder()
                .AddAttribute(0, DataType.Float, 3, 0)
                .AddAttribute(0, DataType.Float, 3, 12)
                .Build(nameof(VertexPositionColor)))
            .WithTopology(PrimitiveTopology.Lines)
            .WithFaceWinding(FaceWinding.Clockwise)
            .WithLineWidth(4.0f)
            .DisableBlending()
            .DisableCulling()
            .DisableDepthTest()
            .Build("LinePass");
        if (lineGraphicsPipelineResult.IsFailure)
        {
            _logger.Error("{Category}: Unable to build graphics pipeline. {Details}",
                "LinePass", lineGraphicsPipelineResult.Error);
            return false;
        }
        _lineGraphicsPipeline?.Dispose();
        _lineGraphicsPipeline = lineGraphicsPipelineResult.Value;

        return true;
    }

    public void SetLines(IEnumerable<VertexPositionColor> vertices)
    {
        _linesVertexBuffer?.Dispose();
        _linesVertexBuffer = _graphicsContext.CreateVertexBuffer<VertexPositionColor>("Lines");
        unsafe
        {
            var sizeInBytes = sizeof(VertexPositionColor) * vertices.Count();
            _linesVertexBuffer.AllocateStorage(sizeInBytes, StorageAllocationFlags.Dynamic);
        }

        _linesVertexBuffer.Update(vertices.ToArray());
        //_linesVertexBuffer.AllocateStorage(vertices.ToArray(), StorageAllocationFlags.None);
    }

    public void Draw(IUniformBuffer cameraInformationBuffer)
    {
        _graphicsContext.BindGraphicsPipeline(_lineGraphicsPipeline);
        _lineGraphicsPipeline.BindVertexBuffer(_linesVertexBuffer, 0, 0);
        _lineGraphicsPipeline.BindUniformBuffer(cameraInformationBuffer, 0);
        _lineGraphicsPipeline.DrawArrays((uint)_linesVertexBuffer.Count, 0);
    }
}