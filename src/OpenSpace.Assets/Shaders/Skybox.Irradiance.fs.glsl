#version 460

layout (location = 0) in vec3 i_position;
layout (location = 0) out vec4 o_color;
layout (binding = 0) uniform samplerCube s_environment;

layout (binding = 0, std140) uniform IrradianceSettings
{
    float DeltaPhi;
    float DeltaTheta;
} irradianceSettings;

#define PI 3.1415926535897932384626433832795

void main()
{
    vec3 N = normalize(i_position);
    vec3 up = vec3(0.0, 1.0, 0.0);
    vec3 right = normalize(cross(up, N));
    up = cross(N, right);

    const float TWO_PI = PI * 2.0;
    const float HALF_PI = PI * 0.5;

    vec3 color = vec3(0.0);
    uint sampleCount = 0u;
    for (float phi = 0.0; phi < TWO_PI; phi += consts.deltaPhi)
    {
        for (float theta = 0.0; theta < HALF_PI; theta += consts.deltaTheta)
        {
            vec3 tempVec = cos(phi) * right + sin(phi) * up;
            vec3 sampleVector = cos(theta) * N + sin(theta) * tempVec;
            color += texture(s_environment, sampleVector).rgb * cos(theta) * sin(theta);
            sampleCount++;
        }
    }
    o_color = vec4(PI * color / float(sampleCount), 1.0);
}
