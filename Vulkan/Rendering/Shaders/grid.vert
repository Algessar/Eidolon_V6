#version 450

vec2 positions[6] = vec2[](
    vec2(-1,-1),
    vec2( 1,-1),
    vec2( 1, 1),
    vec2(-1,-1),
    vec2( 1, 1),
    vec2(-1, 1)
);

layout(location = 0) out vec2 uv;

void main()
{
    vec2 pos = positions[gl_VertexIndex];
    uv = pos * 0.5 + 0.5;
    gl_Position = vec4(pos,0,1);
}