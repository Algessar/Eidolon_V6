#version 450
#extension GL_ARB_separate_shader_objects : enable

layout(set = 0, binding = 0) uniform sampler2D FontTexture;

layout(location = 0) in vec2 fragUV;
layout(location = 1) in vec4 fragColor;

layout(location = 0) out vec4 outColor;

void main()
{
    vec4 sampled = texture(FontTexture, fragUV);
    outColor = fragColor * sampled;
}