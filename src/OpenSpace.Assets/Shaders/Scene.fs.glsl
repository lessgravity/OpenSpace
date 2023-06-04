#version 460 core

#extension GL_ARB_bindless_texture : enable

layout(location = 0) in vec3 v_position;
layout(location = 1) in vec3 v_normal;
layout(location = 2) in vec2 v_uv;
layout(location = 3) in flat int v_model_material_id;

layout(location = 0) out vec4 o_color;

layout(binding = 0) uniform sampler2D s_base_color;

struct GpuMaterial
{
    vec4 BaseColor;
    vec4 EmissiveColorFactor;
    float MetalnessFactor;
    float RoughnessFactor;
    float AlphaCutOff;
    int AlphaMode;
    uvec2 BaseColorTextureId;
    uvec2 NormalTextureId;
    uvec2 MetalnessRoughnessTextureId;
    uvec2 SpecularTextureId;    
};

layout(binding = 2, std430) buffer MaterialBuffer
{
    GpuMaterial[] Materials;
} materialBuffer;

void main()
{
    GpuMaterial material = materialBuffer.Materials[v_model_material_id];
    vec4 color = material.BaseColor.rgba * texture(sampler2D(material.BaseColorTextureId), v_uv);

    o_color = vec4(color.rgb, 1);
}