#version 450
#extension GL_ARB_separate_shader_objects : enable

// Selection colour. Unlit on purpose: the outline has to read as one flat rim, not as a lit surface.

layout(location = 0) out vec4 outColor;

void main()
{
    outColor = vec4(1.0, 0.52, 0.16, 1.0);
}
