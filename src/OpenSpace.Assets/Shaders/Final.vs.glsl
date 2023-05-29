#version 460 core

layout(location = 0) out gl_PerVertex
{
    vec4 gl_Position;
};
layout(location = 0) out vec2 v_uv;
layout(location = 1) out vec3 v_sky_ray;

layout(binding = 0, std140) uniform CameraInformation
{
    mat4 ProjectionMatrix;
    mat4 ViewMatrix;
    vec4 Viewport;
    vec4 CameraPosition; // xyz = position, w = fieldOfView
    vec4 CameraDirection; // xyz = direction, w = aspectRatio
} cameraInformation;

vec3 skyray(vec2 uv, float fieldOfView, float aspectRatio)
{
    float d = 0.5 / tan(fieldOfView / 2.0);
    return vec3((uv.x - 0.5) * aspectRatio, uv.y - 0.5, -d);
}

void main()
{
    float x = -1.0 + float((gl_VertexID & 1) << 2);
    float y = -1.0 + float((gl_VertexID & 2) << 1);

    gl_Position = vec4(x, y, 0.0, 1.0);
    v_uv = vec2((x + 1.0) * 0.5, (y + 1.0) * 0.5);
    v_sky_ray = mat3(inverse(cameraInformation.ViewMatrix)) * skyray(v_uv, cameraInformation.CameraPosition.w, cameraInformation.CameraDirection.w);
}