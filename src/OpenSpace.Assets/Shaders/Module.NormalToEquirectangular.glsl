vec2 NormalToEquirectangularUv(vec3 normal)
{
    float phi = acos(-normal.y);
    float theta = atan(normal.z, normal.x) + M_PI;
    vec2 uv = vec2(theta / M_TAU, phi / M_PI);
    return uv;
}