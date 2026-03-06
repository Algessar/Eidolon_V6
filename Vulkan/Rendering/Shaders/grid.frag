#version 450

layout(location = 0) in vec2 uv;
layout(location = 0) out vec4 outColor;

layout(push_constant) uniform PushConstants
{
    mat4 invViewProj;
    vec3 cameraPos;
} pc;

vec3 reconstructWorld(vec2 uv)
{
    vec4 clip = vec4(uv * 2.0 - 1.0, 1.0, 1.0);
    vec4 world = pc.invViewProj * clip;
    return world.xyz / world.w;
}

float grid(vec2 coord, float scale)
{
    vec2 g = abs(fract(coord/scale - 0.5) - 0.5) / fwidth(coord/scale);
    return min(g.x,g.y);
}

void main()
{
    vec3 world = reconstructWorld(uv);

    vec3 rayDir = normalize(world - pc.cameraPos);

    float t = -pc.cameraPos.y / rayDir.y;

    if (t < 0)
    discard;

    vec3 hit = pc.cameraPos + rayDir * t;

    float minor = grid(hit.xz,1.0);
    float major = grid(hit.xz,5.0);

    float g = min(minor,major);

    vec3 color = vec3(0.2);

    if (major < 1.0)
    color = vec3(0.35);

    if (abs(hit.x) < 0.02)
    color = vec3(0.9,0.2,0.2);

    if (abs(hit.z) < 0.02)
    color = vec3(0.2,0.5,0.9);

    float alpha = 1.0 - clamp(g,0,1);

    outColor = vec4(color,alpha);
}