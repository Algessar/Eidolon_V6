#version 450
#extension GL_ARB_separate_shader_objects : enable

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inColor;

layout(set = 0, binding = 0) uniform CameraUbo {
    mat4 viewProj;
} cameraUbo;

layout(push_constant) uniform PushConstants {
    mat4 model;
    vec4 tint;
} pushConstants;

layout(location = 0) out vec3 fragColor;

void main()
{
    fragColor = inColor * pushConstants.tint.rgb;
    gl_Position = cameraUbo.viewProj * pushConstants.model * vec4(inPosition, 1.0);
}
