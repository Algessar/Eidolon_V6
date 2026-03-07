#version 450

layout(location = 0) in vec2 uv;
layout(location = 0) out vec4 outColor;

layout(push_constant) uniform PushConstants
{
    mat4 invViewProj;
    vec3 cameraPos;
} pc;

vec3 reconstructNear(vec2 uv)
{
    vec4 clip = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
    vec4 world = pc.invViewProj * clip;
    return world.xyz / world.w;
}

vec3 reconstructFar(vec2 uv)
{
    vec4 clip = vec4(uv * 2.0 - 1.0, 1.0, 1.0);
    vec4 world = pc.invViewProj * clip;
    return world.xyz / world.w;
}

float grid(vec2 coord, float scale)
{
    vec2 grid = abs(fract(coord / scale - 0.5) - 0.5) / fwidth(coord / scale);
    return min(grid.x, grid.y);
}

void main()
{
    vec3 nearPoint = reconstructNear(uv);
    vec3 farPoint  = reconstructFar(uv);

    vec3 rayOrigin = pc.cameraPos;
    vec3 rayDir = normalize(farPoint - rayOrigin);

    if (abs(rayDir.y) < 0.0001)
    discard;

    float denom = rayDir.y;

    if (abs(denom) < 1e-5)
    discard;

    float t = -rayOrigin.y / denom;

    if (t <= 0.0)
    discard;

    vec3 hit = rayOrigin + rayDir * t;

    float dist = length(hit.xz);

    // logarithmic grid scale
    float logScale = pow(10.0, floor(log(dist + 1.0)));
    float minorScale = logScale * 0.1;
    float majorScale = logScale;

    float minor = grid(hit.xz, minorScale);
    float major = grid(hit.xz, majorScale);

    float line = min(minor, major);

    vec3 color = vec3(0.2);

    if (major < 1.0)
    color = vec3(0.35);

    // axis highlight
    if (abs(hit.x) < minorScale * 0.5)
    color = vec3(0.9, 0.2, 0.2);

    if (abs(hit.z) < minorScale * 0.5)
    color = vec3(0.2, 0.5, 0.9);

    float alpha = 1.0 - clamp(line, 0.0, 1.0);

    // distance fade
    float fade = exp(-dist * 0.02);
    alpha *= fade;

    outColor = vec4(color, alpha);
}