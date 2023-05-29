#version 460 core

#include <Constants.glsl>

layout(location = 1) in vec3 v_sky_ray;

layout(location = 0) out vec4 o_color;

layout(binding = 0) uniform sampler2D s_depth;
layout(binding = 1) uniform sampler2D s_light;
layout(binding = 2) uniform samplerCube s_skybox;

layout(binding = 3) uniform GpuUchimuraSettings
{
    float MaxDisplayBrightness; // 1.0
    float Contrast; // 1.0
    float LinearSectionStart; // 0.22
    float LinearSectionLength; // 0.4

    float Black; // 1.33
    float Pedestal; // 0.0
    float Gamma; // 2.2
    bool CorrectGamma;
} uchimuraSettings;

vec3 uchimura(vec3 x, float P, float a, float m, float l, float c, float b) {
    float l0 = ((P - m) * l) / a;
    float L0 = m - m / a;
    float L1 = m + (1.0 - m) / a;
    float S0 = m + l0;
    float S1 = m + a * l0;
    float C2 = (a * P) / (P - S1);
    float CP = -C2 / P;

    vec3 w0 = vec3(1.0 - smoothstep(0.0, m, x));
    vec3 w2 = vec3(step(m + l0, x));
    vec3 w1 = vec3(1.0 - w0 - w2);

    vec3 T = vec3(m * pow(x / m, vec3(c)) + b);
    vec3 S = vec3(P - (P - S1) * exp(CP * (x - S0)));
    vec3 L = vec3(m + a * (x - m));

    return T * w0 + L * w1 + S * w2;
}

vec3 uchimura(vec3 x) {
    const float P = uchimuraSettings.MaxDisplayBrightness;
    const float a = uchimuraSettings.Contrast;
    const float m = uchimuraSettings.LinearSectionStart;
    const float l = uchimuraSettings.LinearSectionLength;
    const float c = uchimuraSettings.Black;
    const float b = uchimuraSettings.Pedestal;

    return uchimura(x, P, a, m, l, c, b);
}

void main()
{
    vec2 texture_size = textureSize(s_light, 0);
    vec2 uv = gl_FragCoord.xy / texture_size;
    float depth = textureLod(s_depth, uv, 0).r;
    vec3 gamma = vec3(1.0 / uchimuraSettings.Gamma);
    if (depth == 1.0)
    {
        vec3 radiance = uchimura(textureLod(s_skybox, v_sky_ray, 0).rgb);
        if (uchimuraSettings.CorrectGamma)
        {
            o_color = vec4(pow(radiance, gamma), 1.0);            
        }
        else
        {
            o_color = vec4(radiance, 1.0);    
        }
        return;
    }

    vec3 light = textureLod(s_light, uv, 0).rgb;
    
    if (uchimuraSettings.CorrectGamma)
    {
        o_color = vec4(pow(uchimura(light), gamma), 1.0);
    }
    else
    {
        o_color = vec4(uchimura(light), 1.0);
    }
}