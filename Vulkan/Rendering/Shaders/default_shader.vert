#version 450
#extension GL_ARB_separate_shader_objects : enable

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inColor;

layout(push_constant) uniform PushConstants {
    mat4 model;
} pushConstants;

layout(location = 0) out vec3 fragColor;

void main() {
    gl_Position = pushConstants.model * vec4(inPosition, 1.0);
    fragColor = inColor;
}