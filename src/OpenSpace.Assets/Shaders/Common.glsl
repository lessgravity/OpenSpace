vec2 NormalToEquirectangularUv(vec3 normal)
{
    float phi = acos(-normal.y);
    float theta = atan(normal.z, normal.x) + M_PI;
    vec2 uv = vec2(theta / M_TAU, phi / M_PI);
    return uv;
}

vec2 Hammersley(uint i, uint N)
{
    return vec2(
        float(i) / float(N),
        float(bitfieldReverse(i)) * 2.3283064365386963e-10
    );
}

vec3 ReconstructFragmentWorldPositionFromDepth(float depth, vec2 screenSize, mat4 invViewProj)
{
    float z = depth * 2.0 - 1.0; // [0, 1] -> [-1, 1]
    vec2 position_cs = gl_FragCoord.xy / screenSize; // [0.5, screenSize] -> [0, 1]
    vec4 position_ndc = vec4(position_cs * 2.0 - 1.0, z, 1.0); // [0, 1] -> [-1, 1]

    // undo view + projection
    vec4 position_ws = invViewProj * position_ndc;
    position_ws /= position_ws.w;

    return position_ws.xyz;
}