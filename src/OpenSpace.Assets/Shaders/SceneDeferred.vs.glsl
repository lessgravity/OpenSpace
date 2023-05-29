#version 460 core

layout(location = 0) in vec3 i_position;
layout(location = 1) in vec3 i_normal;
layout(location = 2) in vec2 i_uv;
layout(location = 3) in vec4 i_tangent;

layout (location = 0) out gl_PerVertex
{
    vec4 gl_Position;
};
layout(location = 0) out vec3 v_position;
layout(location = 1) out vec2 v_uv;
layout(location = 2) out flat int v_mesh_material_id;
layout(location = 3) out mat3 v_tbn;

layout(binding = 0, std140) uniform CameraInformation
{
    mat4 ProjectionMatrix;
    mat4 ViewMatrix;
    vec4 Viewport;
    vec4 CameraPosition; // xyz = position, w = fieldOfView
    vec4 CameraDirection; // xyz = direction, w = aspectRatio
} cameraInformation;

struct GpuModelMeshInstance
{
    mat4 WorldMatrix;
    ivec4 MaterialId;
};

layout(binding = 1, std430) readonly buffer InstanceBuffer
{
    GpuModelMeshInstance Instances[];
} instanceBuffer;

void main()
{
    GpuModelMeshInstance modelMeshInstance = instanceBuffer.Instances[gl_BaseInstance + gl_DrawID];
    v_position = (modelMeshInstance.WorldMatrix * vec4(i_position, 1.0)).xyz;
    
    mat3 normalMatrix = mat3(transpose(inverse(mat3(modelMeshInstance.WorldMatrix))));
    vec3 tangent = normalize(normalMatrix * i_tangent.xyz);
    vec3 normal = normalize(normalMatrix * i_normal);
    vec3 biTangent = normalize(cross(normal, tangent)) * i_tangent.w;

    v_uv = i_uv;
    v_mesh_material_id = modelMeshInstance.MaterialId.x;
    v_tbn = mat3(tangent, biTangent, normal);
    
    gl_Position = cameraInformation.ProjectionMatrix * cameraInformation.ViewMatrix * vec4(v_position, 1.0);
}