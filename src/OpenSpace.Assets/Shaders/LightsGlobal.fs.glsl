#version 460 core

#extension GL_ARB_bindless_texture : enable

#include <Constants.glsl>

layout (location = 0) in vec2 v_uv;

layout (location = 0) out vec4 o_value;

layout (binding = 0) uniform sampler2D s_depth;
layout (binding = 1) uniform sampler2D s_albedo;
layout (binding = 2) uniform sampler2D s_normal;
layout (binding = 3) uniform sampler2D s_material;
layout (binding = 4) uniform sampler2D s_emissive;

layout (binding = 5) uniform sampler2D s_blue_noise;
layout (binding = 6) uniform sampler2D s_lut_brdf;

layout (binding = 7) uniform samplerCube s_irradiance;
layout (binding = 8) uniform samplerCube s_environment_prefiltered;

layout(binding = 0, std140) uniform CameraInformation
{
    mat4 ProjectionMatrix;
    mat4 ViewMatrix;
    vec4 Viewport;
    vec4 CameraPosition; // xyz = position, w = fieldOfView
    vec4 CameraDirection; // xyz = direction, w = aspectRatio
} cameraInformation;

layout (binding = 1, std140) uniform ShadowSettings
{
    float LinearBias; // 0.02
    float ConstantBias; // 0.0015
    float RMax; // 0.005;
    float AccumFactor; // 1.0
    int Samples; // 4
    int RandomOffset; // 10000
    int _padding1;
    int _padding2;
} shadowSettings;

struct GpuGlobalLight
{
    mat4 ProjectionMatrix;
    mat4 ViewMatrix;
    vec4 Direction;
    vec4 Color;
    uvec2 ShadowMapTexture;
    uvec2 _padding1;
    uvec2 _padding2;
    uvec2 _padding3;
};

struct GpuLocalLight
{
    vec4 Position;
    vec4 Direction;
    vec4 Color;
    vec4 LightParameters;
};

layout (binding = 2, std430) readonly buffer GlobalLights
{
    GpuGlobalLight[] Lights;
} globalLights;

layout (binding = 3, std430) readonly buffer LocalLights
{
    GpuLocalLight[] Lights;
} localLights;

struct PBRParameters
{
    vec3 Albedo;
    float Roughness;
    float Metalness;

    vec3 WorldPosition;
    vec3 Normal;
    vec3 View;
    float NdotV;
} pbrParameters;

#include <Common.glsl>

const float EPSILON = 1.17549435E-38;
const int PCF_SAMPLES = 64;
const vec2 gPoissonDisk[64] = vec2[](
    vec2(-0.884081, 0.124488),
    vec2(-0.714377, 0.027940),
    vec2(-0.747945, 0.227922),
    vec2(-0.939609, 0.243634),
    vec2(-0.985465, 0.045534),
    vec2(-0.861367, -0.136222),
    vec2(-0.881934, 0.396908),
    vec2(-0.466938, 0.014526),
    vec2(-0.558207, 0.212662),
    vec2(-0.578447, -0.095822),
    vec2(-0.740266, -0.095631),
    vec2(-0.751681, 0.472604),
    vec2(-0.553147, -0.243177),
    vec2(-0.674762, -0.330730),
    vec2(-0.402765, -0.122087),
    vec2(-0.319776, -0.312166),
    vec2(-0.413923, -0.439757),
    vec2(-0.979153, -0.201245),
    vec2(-0.865579, -0.288695),
    vec2(-0.243704, -0.186378),
    vec2(-0.294920, -0.055748),
    vec2(-0.604452, -0.544251),
    vec2(-0.418056, -0.587679),
    vec2(-0.549156, -0.415877),
    vec2(-0.238080, -0.611761),
    vec2(-0.267004, -0.459702),
    vec2(-0.100006, -0.229116),
    vec2(-0.101928, -0.380382),
    vec2(-0.681467, -0.700773),
    vec2(-0.763488, -0.543386),
    vec2(-0.549030, -0.750749),
    vec2(-0.809045, -0.408738),
    vec2(-0.388134, -0.773448),
    vec2(-0.429392, -0.894892),
    vec2(-0.131597, 0.065058),
    vec2(-0.275002, 0.102922),
    vec2(-0.106117, -0.068327),
    vec2(-0.294586, -0.891515),
    vec2(-0.629418, 0.379387),
    vec2(-0.407257, 0.339748),
    vec2(0.071650, -0.384284),
    vec2(0.022018, -0.263793),
    vec2(0.003879, -0.136073),
    vec2(-0.137533, -0.767844),
    vec2(-0.050874, -0.906068),
    vec2(0.114133, -0.070053),
    vec2(0.163314, -0.217231),
    vec2(-0.100262, -0.587992),
    vec2(-0.004942, 0.125368),
    vec2(0.035302, -0.619310),
    vec2(0.195646, -0.459022),
    vec2(0.303969, -0.346362),
    vec2(-0.678118, 0.685099),
    vec2(-0.628418, 0.507978),
    vec2(-0.508473, 0.458753),
    vec2(0.032134, -0.782030),
    vec2(0.122595, 0.280353),
    vec2(-0.043643, 0.312119),
    vec2(0.132993, 0.085170),
    vec2(-0.192106, 0.285848),
    vec2(0.183621, -0.713242),
    vec2(0.265220, -0.596716),
    vec2(-0.009628, -0.483058),
    vec2(-0.018516, 0.435703)
);

float DistributionGGX(const vec3 N, const vec3 H, const float a2)
{
    float NdotH = max(dot(N, H), 0.0);
    float denom = (NdotH * NdotH) * (a2 - 1.0) + 1.0;
    return a2 / (M_PI * denom * denom);
}

// N: Normal, V: View, L: Light, k: (roughness + 1)^2 / 8.0
float GeometrySmith(const float NdotL, const float NdotV, const float k)
{
    float ggx1 = NdotV / (NdotV * (1.0 - k) + k);
    float ggx2 = NdotL / (NdotL * (1.0 - k) + k);

    return ggx1 * ggx2;
}

vec3 Fresnel(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

vec3 FresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness)
{
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(1.0 - cosTheta, 5.0);
}

float CalculateHardShadows(uvec2 shadowmapTexture, vec3 shadowCoords, float bias)
{
    float z = texture(sampler2D(shadowmapTexture), shadowCoords.xy).r;
    return shadowCoords.z - bias > z ? 0.0 : 1.0;
}

float CalculateSoftShadows(uvec2 shadowmapTexture, vec3 shadowCoords, float bias)
{
    vec2 texelSize = vec2(1.0 / 1024.0);
    int sampleCount = 4;
    float shadow = 0.0;
    for(int x = -sampleCount; x <= sampleCount; ++x)
    {
        for(int y = -sampleCount; y <= sampleCount; ++y)
        {
            float z = texture(sampler2D(shadowmapTexture), shadowCoords.xy + vec2(x, y) * texelSize).r;
            shadow += shadowCoords.z - bias > z ? 0.0 : 1.0;
        }
    }
    float tmp = sampleCount * 2 + 1;
    shadow /= (tmp * tmp);
    return shadow;
}

float CalculatePCSS(uvec2 shadowmapTexture, vec3 shadowCoords, float bias)
{
    vec2 texelSizeMultiplier = vec2(3.0 / 1024.0);
    float shadow = 0.0;
    for (int i = 0; i < PCF_SAMPLES; i++)
    {
        vec2 offset = gPoissonDisk[i] * texelSizeMultiplier;
        float z = texture(sampler2D(shadowmapTexture), shadowCoords.xy + offset).r;
        shadow += shadowCoords.z - bias > z ? 0.0 : 1.0;
    }
    return shadow / float(PCF_SAMPLES);
}

float CalculateDirectionalShadowFactor(GpuGlobalLight light, const float NdotL, int shadowmapIndex)
{
    vec4 dirLightViewProj = light.ProjectionMatrix * light.ViewMatrix * vec4(pbrParameters.WorldPosition, 1);
    vec3 projCoords = dirLightViewProj.xyz / dirLightViewProj.w;
    projCoords = (projCoords * 0.5) + 0.5;

    if(projCoords.z > 1.0)
    {
        return 0.0;   
    }

    float bias = max(0.0008 * (1.0 - NdotL), 0.0008);
    int shadowQuality = int(light.Direction.w);
    /*
    if (shadowQuality == 0)
    {
        return CalculateHardShadows(light.ShadowMapTexture, projCoords, bias);
    }
    else if (shadowQuality == 1)
    {
        return CalculateSoftShadows(light.ShadowMapTexture, projCoords, bias);
    }
    */
    return CalculatePCSS(light.ShadowMapTexture, projCoords, bias);
}

float LengthSq(const vec3 v)
{
    return dot(v, v);
}

vec3 RotateVectorAboutY(float angle, vec3 vec)
{
    angle = radians(angle);
    mat3x3 rotationMatrix = { vec3(cos(angle), 0.0, sin(angle)),
        vec3(0.0, 1.0, 0.0),
        vec3(-sin(angle), 0.0, cos(angle)) };
    return rotationMatrix * vec;
}

float bayer_16x16(vec2 coordinate)
{
    const float RCP_MAX_INT = 1.52588e-05; // 1.0 / exp2(16.0)
    uvec2 x = uvec2(coordinate);
    uvec2 xy = uvec2(x.x ^ x.y, x.y);
    xy = (xy | (xy << 12u)) & 0xf0f0u;
    xy = (xy | (xy >> 6u)) & 0x3333u;
    xy = (xy | (xy << 3u)) & 0xaaaau;
    return float(xy.y >> 1u | xy.x) * RCP_MAX_INT;
}

/*
vec3 IBL(vec3 F0, vec2 uv)
{
    float NoV = clamp(pbrParameters.NdotV, 0.0, 1.0);
    vec3 F = FresnelSchlickRoughness(NoV, F0, pbrParameters.Roughness);
    vec3 kd = (1.0 - F) * (1.0 - pbrParameters.Metalness);

    vec3 irradiance = texture(s_irradiance, pbrParameters.Normal).rgb;
    vec3 diffuseIBL = pbrParameters.Albedo * irradiance;

    int envRadianceTexLevels = textureQueryLevels(s_radiance);
    vec3 Lr = 2.0 * pbrParameters.NdotV * pbrParameters.Normal - pbrParameters.View;
    vec3 specularIrradiance = textureLod(s_radiance, Lr, pbrParameters.Roughness * envRadianceTexLevels).rgb;

    vec2 specularBRDF = texture(s_lut_brdf, vec2(NoV, pbrParameters.Roughness)).rg;
    vec3 specularIBL = bayer_16x16(uv * vec2(1920, 1080)) * specularIrradiance * (F) * (specularBRDF.x + specularBRDF.y);

    return (kd * diffuseIBL + specularIBL) * 2.0;
}
*/

vec3 approximate_specular_ibl(vec3 specular_color, float roughness, vec3 n, vec3 v)
{
    float n_dot_v = max(0.0, dot(n, v));
    vec3 r = reflect(-v, n);
    vec3 prefiltered_color = textureLod(s_environment_prefiltered, r, roughness * 5.0).rgb;
    vec2 brdf = texture(s_lut_brdf, vec2(roughness, n_dot_v)).rg;
    return prefiltered_color * (specular_color * brdf.x + brdf.y);
}

vec3 FresnelSchlick2(vec3 F0, float cosTheta)
{
	return F0 + (vec3(1.0) - F0) * pow(1.0 - cosTheta, 5.0);
}

void main()
{
    float depth = textureLod(s_depth, v_uv, 0).r;
    if (depth == 1.0)
    {
        discard;
    }

    mat4 inverseViewProjection = inverse(cameraInformation.ProjectionMatrix * cameraInformation.ViewMatrix);
    vec3 world_position = ReconstructFragmentWorldPositionFromDepth(depth, cameraInformation.Viewport.xy, inverseViewProjection);
    
    vec4 albedo = textureLod(s_albedo, v_uv, 0);
    vec4 normals = textureLod(s_normal, v_uv, 0);
    vec4 material = textureLod(s_material, v_uv, 0);
    vec4 emissive = textureLod(s_emissive, v_uv, 0);

    vec3 v = normalize(cameraInformation.CameraPosition.xyz - world_position);
    vec3 normal = normalize(normals.rgb);
   
    float metalness = material.r;
    float roughness = material.g;
    float ambientOcclusion = material.b;
    
    vec3 Lo = v;
	// Angle between surface normal and outgoing light direction.
	float cosLo = max(0.0, dot(normal, Lo));
		
	// Specular reflection vector.
	vec3 Lr = (2.0 * cosLo * normal) - Lo;

	// Fresnel reflectance at normal incidence (for metals use albedo color).
	vec3 F0 = mix(vec3(0.04), albedo.rgb, metalness);    
    
    vec3 ambientLighting;
	{
		// Sample diffuse irradiance at normal direction.
		vec3 irradiance = texture(s_irradiance, normal).rgb;

		// Calculate Fresnel term for ambient lighting.
		// Since we use pre-filtered cubemap(s) and irradiance is coming from many directions
		// use cosLo instead of angle with light's half-vector (cosLh above).
		// See: https://seblagarde.wordpress.com/2011/08/17/hello-world/
		vec3 F = FresnelSchlick2(F0, cosLo);

		// Get diffuse contribution factor (as with direct lighting).
		vec3 kd = mix(vec3(1.0) - F, vec3(0.0), metalness);

		// Irradiance map contains exitant radiance assuming Lambertian BRDF, no need to scale by 1/PI here either.
		vec3 diffuseIBL = kd * albedo.rgb * irradiance + emissive.rgb;

		// Sample pre-filtered specular reflection environment at correct mipmap level.
		int specularTextureLevels = textureQueryLevels(s_environment_prefiltered);
		vec3 specularIrradiance = textureLod(s_environment_prefiltered, Lr, roughness * specularTextureLevels).rgb;

		// Split-sum approximation factors for Cook-Torrance specular BRDF.
		vec2 specularBRDF = texture(s_lut_brdf, vec2(cosLo, roughness)).rg;

		// Total specular IBL contribution.
		vec3 specularIBL = (F0 * specularBRDF.x + specularBRDF.y) * specularIrradiance;

		// Total ambient lighting contribution.
		ambientLighting = diffuseIBL + specularIBL;
	}
    
    /*
    vec3 base_color = albedo.rgb;
    vec3 diffuse_color = base_color * (1.0 - metalness);
    vec3 specular_color = mix(vec3(0.04), base_color, vec3(metalness));

    vec3 irradiance = texture(s_irradiance, normal).rgb;
    vec3 indirect_diffuse = diffuse_color * irradiance + emissive.rgb;
    vec3 indirect_specular = approximate_specular_ibl(specular_color, roughness, normal, v);
    //indirect_specular += bayer_16x16(v_uv * vec2(1280, 720)) / 256.0;
    o_value = vec4(ambientOcclusion * albedo.a * (indirect_diffuse + indirect_specular), 1.0);
    */
    
    o_value = vec4(ambientLighting, 1.0);
}