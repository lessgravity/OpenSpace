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

layout (binding = 2, std140) uniform LightPassParameters
{
    float Gamma;
    float Exposure;
    float PrefilteredCubeMipLevels;
    float ScaleIblAmbient;
} lightPassParameters;

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

layout (binding = 3, std430) readonly buffer GlobalLights
{
    GpuGlobalLight Lights[];
} globalLights;

struct PbrParameters
{
    vec3 Albedo;
    float Roughness;
    float Metalness;

    vec3 WorldPosition;
    vec3 Normal;
    vec3 View;
    float NdotV;
} pbrParameters;

struct PbrInfo
{
	float NdotL;                  // cos angle between normal and light direction
	float NdotV;                  // cos angle between normal and view direction
	float NdotH;                  // cos angle between normal and half vector
	float LdotH;                  // cos angle between light direction and half vector
	float VdotH;                  // cos angle between view direction and half vector
	float PerceptualRoughness;    // roughness value, as authored by the model creator (input to shader)
	float Metalness;              // metallic value at the surface
	vec3 Reflectance0;            // full reflectance color (normal incidence angle)
	vec3 Reflectance90;           // reflectance color at grazing angle
	float AlphaRoughness;         // roughness mapped to a more linear change in the roughness (proposed by [2])
	vec3 DiffuseColor;            // color contribution from diffuse lighting
	vec3 SpecularColor;           // color contribution from specular lighting
};

#define MANUAL_SRGB 1

#include <Module.ReconstructFragmentWorldPositionFromDepth.glsl>
#include <Module.Hammersley.glsl>

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

vec3 Uncharted2Tonemap(vec3 color)
{
	float A = 0.15;
	float B = 0.50;
	float C = 0.10;
	float D = 0.20;
	float E = 0.02;
	float F = 0.30;
	float W = 11.2;
	return ((color*(A*color+C*B)+D*E)/(color*(A*color+B)+D*F))-E/F;
}

vec4 Tonemap(vec4 color)
{
	vec3 outColor = Uncharted2Tonemap(color.rgb * lightPassParameters.Exposure);
	outColor = outColor * (1.0f / Uncharted2Tonemap(vec3(11.2f)));	
	return vec4(pow(outColor, vec3(1.0f / lightPassParameters.Gamma)), color.a);
}

vec4 SrgbToLinear(vec4 srgbIn)
{
	#ifdef MANUAL_SRGB
	    #ifdef SRGB_FAST_APPROXIMATION
	    vec3 linearOut = pow(srgbIn.xyz, vec3(2.2));
	    #else //SRGB_FAST_APPROXIMATION
	    vec3 bLess = step(vec3(0.04045), srgbIn.xyz);
	    vec3 linearOut = mix(srgbIn.xyz / vec3(12.92), pow((srgbIn.xyz + vec3(0.055)) / vec3(1.055), vec3(2.4)), bLess);
	    #endif //SRGB_FAST_APPROXIMATION
	    return vec4(linearOut, srgbIn.w);
	#else //MANUAL_SRGB
	    return srgbIn;
	#endif //MANUAL_SRGB
}


vec3 GetIblContribution(PbrInfo pbrInputs, vec3 n, vec3 reflection)
{
	float lod = (pbrInputs.PerceptualRoughness * lightPassParameters.PrefilteredCubeMipLevels);
	// retrieve a scale and bias to F0. See [1], Figure 3
	vec3 brdf = (texture(s_lut_brdf, vec2(pbrInputs.NdotV, 1.0 - pbrInputs.PerceptualRoughness))).rgb;
	vec3 diffuseLight = texture(s_irradiance, n).rgb;

	vec3 specularLight = textureLod(s_environment_prefiltered, reflection, lod).rgb;

	vec3 diffuse = diffuseLight * pbrInputs.DiffuseColor;
	vec3 specular = specularLight * (pbrInputs.SpecularColor * brdf.x + brdf.y);

	// For presentation, this allows us to disable IBL terms
	// For presentation, this allows us to disable IBL terms
	diffuse *= lightPassParameters.ScaleIblAmbient;
	specular *= lightPassParameters.ScaleIblAmbient;

	return diffuse + specular;
}

vec3 FresnelSchlick2(vec3 F0, float cosTheta)
{
	return F0 + ((vec3(1.0) - F0) * pow(1.0 - cosTheta, 5.0));
}

// Basic Lambertian diffuse
// Implementation from Lambert's Photometria https://archive.org/details/lambertsphotome00lambgoog
// See also [1], Equation 1
vec3 Diffuse(PbrInfo pbrInputs)
{
	return pbrInputs.DiffuseColor / M_PI;
}

// The following equation models the Fresnel reflectance term of the spec equation (aka F())
// Implementation of fresnel from [4], Equation 15
vec3 SpecularReflection(PbrInfo pbrInputs)
{
	return pbrInputs.Reflectance0 + (pbrInputs.Reflectance90 - pbrInputs.Reflectance0) * pow(clamp(1.0 - pbrInputs.VdotH, 0.0, 1.0), 5.0);
}

// This calculates the specular geometric attenuation (aka G()),
// where rougher material will reflect less light back to the viewer.
// This implementation is based on [1] Equation 4, and we adopt their modifications to
// alphaRoughness as input as originally proposed in [2].
float GeometricOcclusion(PbrInfo pbrInputs)
{
	float NdotL = pbrInputs.NdotL;
	float NdotV = pbrInputs.NdotV;
	float r = pbrInputs.AlphaRoughness;

	float attenuationL = 2.0 * NdotL / (NdotL + sqrt(r * r + (1.0 - r * r) * (NdotL * NdotL)));
	float attenuationV = 2.0 * NdotV / (NdotV + sqrt(r * r + (1.0 - r * r) * (NdotV * NdotV)));
	return attenuationL * attenuationV;
}

// The following equation(s) model the distribution of microfacet normals across the area being drawn (aka D())
// Implementation from "Average Irregularity Representation of a Roughened Surface for Ray Reflection" by T. S. Trowbridge, and K. P. Reitz
// Follows the distribution function recommended in the SIGGRAPH 2013 course notes from EPIC Games [1], Equation 3.
float MicrofacetDistribution(PbrInfo pbrInputs)
{
	float roughnessSq = pbrInputs.AlphaRoughness * pbrInputs.AlphaRoughness;
	float f = (pbrInputs.NdotH * roughnessSq - pbrInputs.NdotH) * pbrInputs.NdotH + 1.0;
	return roughnessSq / (M_PI * f * f);
}

vec3 approximate_specular_ibl(vec3 specular_color, float roughness, vec3 n, vec3 v)
{
    float n_dot_v = max(0.0, dot(n, v));
    vec3 r = reflect(-v, n);
    vec3 prefiltered_color = textureLod(s_environment_prefiltered, r, roughness * 10.0).rgb;
    vec2 brdf = texture(s_lut_brdf, vec2(roughness, n_dot_v)).rg;
    return prefiltered_color * (specular_color * brdf.x + brdf.y);
}

vec3 ShadowFunction(GpuGlobalLight light, vec3 worldPosition, vec3 normal)
{
    vec4 shadowUv = light.ProjectionMatrix * light.ViewMatrix * vec4(worldPosition, 1.0);
    shadowUv = vec4(shadowUv.xy * 0.5 + 0.5, shadowUv.z, shadowUv.w);

    if (any(lessThan(shadowUv.xyz, vec3(0))) || any(greaterThan(shadowUv.xyz, vec3(1))))
    {
        return vec3(0.0, 0.0, 1.0);
    }

    float nDotL = max(0.0, dot(normalize(light.Direction.xyz), normal));

    float bias = (1.0 - nDotL) * shadowSettings.LinearBias;
    bias += shadowSettings.ConstantBias;
    
    ivec2 uvNoise = ivec2(gl_FragCoord.xy) % textureSize(s_blue_noise, 0);
    vec4 noiseSample = texelFetch(s_blue_noise, uvNoise, 0);

    float z = 0.5 * shadowUv.z + 0.5;
    //float z = shadowUv.z;
    
    float accumShadow = 0;
    for (uint i = 0; i < shadowSettings.Samples; i++)
    {
        vec2 xi = mod(Hammersley(i, shadowSettings.Samples) + noiseSample.xy, 1);
        float r = xi.x * shadowSettings.RMax;
        float theta = xi.y * 2 * M_TAU;
        float shadowDepth = texture(sampler2D(light.ShadowMapTexture), shadowUv.xy + vec2(r * cos(theta), r * sin(theta))).r;
        if (shadowDepth + bias >= z)
        //if (shadowDepth > shadowUv.z - bias)
        {
            accumShadow += shadowSettings.AccumFactor;
        }
    }
   
    float notInShadowAmount = accumShadow / shadowSettings.Samples;
    //return vec3(accumShadow);
    return vec3(notInShadowAmount);
    //return normal;
    //return light.Color.rgb;
    //return noiseSample.xyz;
    //return vec3(shadowUv.xy, 0.0);
}

void main()
{
    float depth = textureLod(s_depth, v_uv, 0).r;
    if (depth == 1.0)
    {
        discard;
    }

    mat4 inverseViewProjection = inverse(cameraInformation.ProjectionMatrix * cameraInformation.ViewMatrix);
    vec3 worldPosition = ReconstructFragmentWorldPositionFromDepth(depth, cameraInformation.Viewport.xy, inverseViewProjection);
    
    vec4 albedo = textureLod(s_albedo, v_uv, 0);
    vec3 baseColor = albedo.rgb;
    vec4 normals = textureLod(s_normal, v_uv, 0);
    vec4 material = textureLod(s_material, v_uv, 0);
    vec4 emissive = textureLod(s_emissive, v_uv, 0);

    vec3 v = normalize(cameraInformation.CameraPosition.xyz - worldPosition);
    vec3 normal = normalize(normals.rgb);
   
    float ambientOcclusion = material.r;
    float roughness = material.g;    
    float metalness = material.b;
    
    vec3 F0 = vec3(0.04);
    vec3 diffuseColor = baseColor * (vec3(1.0) - F0);
    diffuseColor *= 1.0 - metalness;
    
    float alphaRoughness = roughness * roughness;
    vec3 specularColor = mix(F0, baseColor, metalness);
    
	// Compute reflectance.
	float reflectance = max(max(specularColor.r, specularColor.g), specularColor.b);

	// For typical incident reflectance range (between 4% to 100%) set the grazing reflectance to 100% for typical fresnel effect.
	// For very low reflectance range on highly diffuse objects (below 4%), incrementally reduce grazing reflecance to 0%.
	float reflectance90 = clamp(reflectance * 25.0, 0.0, 1.0);
	vec3 specularEnvironmentR0 = specularColor.rgb;
	vec3 specularEnvironmentR90 = vec3(1.0, 1.0, 1.0) * reflectance90;
    vec3 shadowFactor = vec3(0.0);
    vec3 color = vec3(0.0);
    for (int i = 0; i < globalLights.Lights.length(); i++)
    {
        GpuGlobalLight globalLight = globalLights.Lights[i];
        vec3 l = normalize(globalLight.Direction.xyz);
        vec3 h = normalize(l + v);
        vec3 reflection = normalize(reflect(v, normal));
    
        float NdotL = clamp(dot(normal, l), 0.001, 1.0);
        float NdotV = clamp(abs(dot(normal, v)), 0.001, 1.0);
        float NdotH = clamp(dot(normal, h), 0.0, 1.0);
        float LdotH = clamp(dot(l, h), 0.0, 1.0);
        float VdotH = clamp(dot(v, h), 0.0, 1.0);
        
        PbrInfo pbrInputs = PbrInfo(
            NdotL,
            NdotV,
            NdotH,
            LdotH,
            VdotH,
            roughness,
            metalness,
            specularEnvironmentR0,
            specularEnvironmentR90,
            alphaRoughness,
            diffuseColor,
            specularColor
        );
        
        vec3 F = SpecularReflection(pbrInputs);
        float G = GeometricOcclusion(pbrInputs);
        float D = MicrofacetDistribution(pbrInputs);
    
        // Calculation of analytical lighting contribution
        vec3 diffuseContribution = (1.0 - F) * Diffuse(pbrInputs);
        vec3 specularContribution = F * G * D / (4.0 * NdotL * NdotV);
        
        shadowFactor = ShadowFunction(globalLight, worldPosition, normal);
        // Obtain final intensity as reflectance (BRDF) scaled by the energy of the light (cosine law)
        color += NdotL * globalLight.Color.rgb * globalLight.Color.w * (diffuseContribution + specularContribution) * shadowFactor;
        color *= GetIblContribution(pbrInputs, normal, reflection);
        //color *= shadowFactor;
	}
	
    color += emissive.rgb;
    
    o_value = vec4(color, 1.0);
}

