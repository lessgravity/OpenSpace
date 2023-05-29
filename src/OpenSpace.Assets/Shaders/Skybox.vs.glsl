#version 450

layout(location = 0) in vec3 i_position;
layout (location = 0) out gl_PerVertex
{
    vec4 gl_Position;
};
layout (location = 1) out vec3 o_uvw;

layout(binding = 0) uniform CameraSettings
{
    mat4 ProjectionMatrix;
    mat4 ViewMatrix;
} cameraSettings;

void main()
{
    o_uvw = i_position;
    gl_Position = cameraSettings.ProjectionMatrix * cameraSettings.ViewMatrix * vec4(i_position.xyz, 1.0);
}