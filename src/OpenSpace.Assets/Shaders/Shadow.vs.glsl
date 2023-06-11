#version 460 core

layout (location = 0) in vec3 i_position;

layout (location = 0) out gl_PerVertex
{
    vec4 gl_Position;
};

layout (binding = 2, std140) uniform GlobalLight
{
    mat4 ProjectionMatrix;
    mat4 ViewMatrix;
    vec4 Direction;
    vec4 Color;
    uvec2 _padding1;
    uvec2 _padding2;
    uvec2 _padding3;
    uvec2 _padding4;
} globalLight;

struct GpuModelMeshInstance
{
    mat4 WorldMatrix;
    ivec4 MaterialId;
};

layout(binding = 3, std430) readonly buffer InstanceBuffer
{
    GpuModelMeshInstance Instances[];
} instanceBuffer;

void main()
{
    gl_Position = globalLight.ProjectionMatrix *
                  globalLight.ViewMatrix *
                  instanceBuffer.Instances[gl_DrawID].WorldMatrix * vec4(i_position, 1.0);
}