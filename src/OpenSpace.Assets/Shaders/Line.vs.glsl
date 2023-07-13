#version 450

layout(location = 0) in vec3 i_position;
layout(location = 1) in vec3 i_color;
layout(location = 0) out gl_PerVertex
{
    vec4 gl_Position;
};
layout(location = 1) out vec3 v_color;

layout(binding = 0) uniform GpuCameraInformation
{
    mat4 ProjectionMatrix;
    mat4 ViewMatrix;
    vec4 Viewport;
    vec4 CameraPositionAndFieldOfView;
    vec4 CameraDirectionAndAspectRatio;    
} cameraInformation;

void main()
{
    gl_Position = cameraInformation.ProjectionMatrix * cameraInformation.ViewMatrix * vec4(i_position.xyz, 1.0);
    v_color = i_color;
}