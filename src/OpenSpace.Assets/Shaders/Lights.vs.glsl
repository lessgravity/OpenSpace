#version 460 core

struct GpuLight
{
    vec4 Color;
    vec4 Position;
    vec4 Direction;
    float Intensity;
    float Radius;
    float Type;
    float Angle;
};

layout(location = 0) in vec3 i_position;

layout (location = 0) out gl_PerVertex
{
    vec4 gl_Position;
};
layout(location = 0) out vec2 v_position;
layout(location = 3) out flat GpuLight v_light;

layout(binding = 0, std140) uniform CameraInformation
{
    mat4 ProjectionMatrix;
    mat4 ViewMatrix;
    vec4 Viewport;
    vec4 CameraPosition; // xyz = position, w = fieldOfView
    vec4 CameraDirection; // xyz = direction, w = aspectRatio
} cameraInformation;

layout(binding = 1, std430) readonly buffer LightsBuffer
{
    GpuLight Lights[];
} lightsBuffer;

void main()
{
    GpuLight light = lightsBuffer.Lights[gl_InstanceID];
    gl_Position = cameraInformation.ProjectionMatrix * cameraInformation.ViewMatrix * vec4(light.Position.xyz, 1.0);
    v_light = light;
}