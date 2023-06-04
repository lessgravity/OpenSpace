#version 460 core

const float PI = 3.141592;
const float TwoPI = 2 * PI;
const float Epsilon = 0.00001;

const uint NumSamples = 1024;
const float InvNumSamples = 1.0 / float(NumSamples);

const int NumMipLevels = 1;
layout(binding = 0) uniform samplerCube s_environment;
layout(binding = 0, rgba16f) restrict writeonly uniform imageCube s_prefiltered;

layout(location = 0) uniform float roughness;

#include <Module.Hammersley.glsl>

// Based omn http://byteblacksmith.com/improvements-to-the-canonical-one-liner-glsl-rand-for-opengl-es-2-0/
float Random(vec2 co)
{
	float a = 12.9898;
	float b = 78.233;
	float c = 43758.5453;
	float dt= dot(co.xy ,vec2(a,b));
	float sn= mod(dt,3.14);
	return fract(sin(sn) * c);
}


// Based on http://blog.selfshadow.com/publications/s2013-shading-course/karis/s2013_pbs_epic_slides.pdf
vec3 ImportanceSample_GGX(vec2 xi, float roughness, vec3 normal) 
{
	// Maps a 2D point to a hemisphere with spread based on roughness
	float alpha = roughness * roughness;
	float phi = 2.0 * PI * xi.x + Random(normal.xz) * 0.1;
	float cosTheta = sqrt((1.0 - xi.y) / (1.0 + (alpha * alpha - 1.0) * xi.y));
	float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
	vec3 H = vec3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);

	// Tangent space
	vec3 up = abs(normal.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
	vec3 tangentX = normalize(cross(up, normal));
	vec3 tangentY = normalize(cross(normal, tangentX));

	// Convert to world Space
	return normalize(tangentX * H.x + tangentY * H.y + normal * H.z);
}

// GGX/Towbridge-Reitz normal distribution function.
// Uses Disney's reparametrization of alpha = roughness^2.
float NormalDistributionGGXTowbridgeReitz(float cosLh, float roughness)
{
    float alpha   = roughness * roughness;
    float alphaSq = alpha * alpha;

    float denom = (cosLh * cosLh) * (alphaSq - 1.0) + 1.0;
    return alphaSq / (PI * denom * denom);
}

// Calculate normalized sampling direction vector based on current fragment coordinates (gl_GlobalInvocationID.xyz).
// This is essentially "inverse-sampling": we reconstruct what the sampling vector would be if we wanted it to "hit"
// this particular fragment in a cubemap.
// See: OpenGL core profile specs, section 8.13.
vec3 GetSamplingVector(vec2 imageDimensions)
{
    vec2 st = gl_GlobalInvocationID.xy / imageDimensions;
    vec2 uv = 2.0 * vec2(st.x, 1.0 - st.y) - vec2(1.0);

    vec3 ret;
    // Sadly 'switch' doesn't seem to work, at least on NVIDIA.
    if(gl_GlobalInvocationID.z == 0)      ret = vec3(1.0,  uv.y, -uv.x);
    else if(gl_GlobalInvocationID.z == 1) ret = vec3(-1.0, uv.y,  uv.x);
    else if(gl_GlobalInvocationID.z == 2) ret = vec3(uv.x, 1.0, -uv.y);
    else if(gl_GlobalInvocationID.z == 3) ret = vec3(uv.x, -1.0, uv.y);
    else if(gl_GlobalInvocationID.z == 4) ret = vec3(uv.x, uv.y, 1.0);
    else if(gl_GlobalInvocationID.z == 5) ret = vec3(-uv.x, uv.y, -1.0);
    return normalize(ret);
}

layout(local_size_x = 32, local_size_y = 32, local_size_z = 1) in;
void main(void)
{
    // Make sure we won't write past output when computing higher mipmap levels.
    ivec2 outputSize = imageSize(s_prefiltered);
    if(gl_GlobalInvocationID.x >= outputSize.x || gl_GlobalInvocationID.y >= outputSize.y)
    {
        return;
    }
    
    // Solid angle associated with a single cubemap texel at zero mipmap level.
    // This will come in handy for importance sampling below.
    float envMapDim = float(textureSize(s_environment, 0).s);
    float wt = 4.0 * PI / (6 * envMapDim);
    
    vec3 color = vec3(0);
    float totalWeight = 0;
	vec3 N = GetSamplingVector(outputSize);
    // Convolve environment map using GGX NDF importance sampling.
    // Weight by cosine term since Epic claims it generally improves quality.
    float mipLevel = max(0.5 * log2((1 / NumSamples) / wt) + 1.0, 0.0);
    if (mipLevel == 0.0)
    {
        color += textureLod(s_environment, N, 0).rgb;
    }
    else
    {
	    vec3 V = GetSamplingVector(outputSize);
    
        for(uint i = 0; i < NumSamples; ++i)
        {
            vec2 xi = Hammersley(i, NumSamples);
            vec3 H = ImportanceSample_GGX(xi, roughness, N);
    
            vec3 L = 2.0 * dot(V, H) * H - V;
            float dotNL = clamp(dot(N, L), 0.0, 1.0);
            if(dotNL > 0.0)
            {
                // Filtering based on https://placeholderart.wordpress.com/2015/07/28/implementation-notes-runtime-environment-map-filtering-for-image-based-lighting/
                float dotNH = clamp(dot(N, H), 0.0, 1.0);
                float dotVH = clamp(dot(V, H), 0.0, 1.0);
    
                // Probability Distribution Function
                float pdf = NormalDistributionGGXTowbridgeReitz(dotNH, roughness) * dotNH / (4.0 * dotVH) + 0.0001;
                // Slid angle of current smple
                float omegaS = 1.0 / (float(NumSamples) * pdf);
                // Solid angle of 1 pixel across all cube faces
                float omegaP = 4.0 * PI / (6.0 * envMapDim * envMapDim);
                // Biased (+1.0) mip level for better result
                float mipLevel = roughness == 0.0 ? 0.0 : max(0.5 * log2(omegaS / omegaP) + 1.0, 0.0f);
                color += textureLod(s_environment, L, mipLevel).rgb * dotNL;
                totalWeight += dotNL;
            }
        }
        color /= totalWeight;
    }

    imageStore(s_prefiltered, ivec3(gl_GlobalInvocationID), vec4(color, 1.0));
}