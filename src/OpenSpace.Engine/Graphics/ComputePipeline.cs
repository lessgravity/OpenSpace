using System;
using EngineKit.Native.OpenGL;
using OpenSpace.Engine.Graphics.Shaders;

namespace OpenSpace.Engine.Graphics;

internal sealed class ComputePipeline : Pipeline, IComputePipeline
{
    private readonly ComputePipelineDescriptor _computePipelineDescriptor;

    internal ComputePipeline(ComputePipelineDescriptor computePipelineDescriptor, ShaderProgram shaderProgram)
    {
        _computePipelineDescriptor = computePipelineDescriptor;
        ShaderProgram = shaderProgram ?? throw new ArgumentNullException(nameof(shaderProgram));
        Label = computePipelineDescriptor.PipelineProgramLabel;
    }

    public void Dispatch(uint numGroupX, uint numGroupY, uint numGroupZ)
    {
        GL.Dispatch(numGroupX, numGroupY, numGroupZ);
    }

    public void DispatchIndirect(IIndirectBuffer indirectBuffer, int indirectElementIndex)
    {
        indirectBuffer.Bind();
        GL.DispatchIndirect(new nint(indirectElementIndex * indirectBuffer.Stride));
    }

    public void Uniform(int location, float value)
    {
        GL.ProgramUniform((int)ShaderProgram.ComputeShader.Id, location, value);
    }
}