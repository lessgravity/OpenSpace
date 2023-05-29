float D_GGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness * roughness; // makes it look better apparently
    float cosTheta = max(dot(N, H), 0.0);
    float tmp = pow(cosTheta * ((a * a) - 1.0) + 1.0, 2.0);
    return (a * a) / (M_PI * tmp);
}

float G_SchlickGGX(vec3 N, vec3 I, float roughness)
{
    float k = pow(roughness + 1.0, 2.0) / 8.0;
    float cosTheta = max(dot(N, I), 0.0);
    return cosTheta / (cosTheta * (1.0 - k) + k);
}

float G_Smith(vec3 N, vec3 V, vec3 L, float roughness)
{
    return G_SchlickGGX(N, V, roughness) * G_SchlickGGX(N, L, roughness);
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(max(1.0 - cosTheta, 0.0), 5.0);
}

vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness)
{
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(max(1.0 - cosTheta, 0.0), 5.0);
}

vec3 ImportanceSampleGGX(vec2 xi, vec3 N, float roughness)
{
    float a = roughness * roughness;
    
    float phi = 2.0 * M_PI * xi.x;
    float cosTheta = sqrt((1.0 - xi.y) / (1.0 + (a * a - 1.0) * xi.y));
    float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
    
    vec3 H;
    H.x = cos(phi) * sinTheta;
    H.y = sin(phi) * sinTheta;
    H.z = cosTheta;
    
    vec3 up = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent = normalize(cross(up, N));
    vec3 bitangent = cross(N, tangent);
    
    vec3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
    return normalize(sampleVec);
}

float CalculateLod(uint samples, vec3 N, vec3 H, float roughness)
{
    float dist = D_GGX(N, H, roughness);
    return 0.5 * (log2(float(1920*1080/*cameraInformation.Viewport.x * cameraInformation.Viewport.y*/) / samples) - log2(dist));
}

vec2 NormToEquirectangularUV(vec3 normal)
{
    // float phi = (-0.6981 * -normal.y * -normal.y - 0.8726) * -normal.y + 1.5707;
    // float theta = normal.xz * (-0.1784 * normal.xz - 0.0663 * normal.xz * normal.xz + 1.0301);
    // vec2 uv = vec2(theta * 0.1591, phi * 0.3183);

    float phi = acos(-normal.y);
    float theta = atan(normal.z, normal.x) + M_PI; // or normal.xz?
    vec2 uv = vec2(theta / M_TAU, phi / M_PI);
    return uv;

    // const vec2 invAtan = vec2(0.1591f, 0.3183f);
    // vec2 uv = vec2(atan(normal.z, normal.x), asin(normal.y));
    // uv *= invAtan;
    // uv += 0.5;
    // return uv;
}

vec3 ComputeSpecularRadiance(vec3 N, vec3 V, vec3 F0, float roughness)
{
    vec3 up = abs(N.z) < 0.999 ? vec3(0, 0, 1) : vec3(1, 0, 0);
    vec3 tanX = normalize(cross(up, N));
    vec3 tanY = cross(N, tanX);

    float NoV = abs(dot(N, V));

    vec3 accumColor = vec3(0.0);
    const uint samples = 8;
    for (uint i = 0; i < samples; i++)
    {
        vec2 Xi = Hammersley(i, samples);
        vec3 H = ImportanceSampleGGX(Xi, N, roughness);
        vec3 L = normalize(-reflect(V, H));

        float NoL = abs(dot(N, L));
        float NoH = abs(dot(N, H));
        float VoH = abs(dot(V, H));
        float lod = CalculateLod(samples, N, H, roughness);

        vec3 F_ = fresnelSchlick(VoH, F0);
        float G_ = G_Smith(N, V, L, roughness);
        vec3 uuv = L;
        vec3 LColor = textureLod(s_radiance, uuv, lod).rgb;

        accumColor += F_ * G_ * LColor * VoH / max((NoH * NoV), 0.001);
    }

    //return accumColor / float(samples);
    return accumColor;
}