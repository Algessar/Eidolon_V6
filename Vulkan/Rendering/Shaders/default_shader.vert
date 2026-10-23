#version 450
#extension GL_ARB_separate_shader_objects : enable

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inNormal;

layout(set = 0, binding = 0) uniform CameraUbo {
    mat4 viewProj;
} cameraUbo;

layout(push_constant) uniform PushConstants {
    mat4 model;
    vec4 baseColor;
} pushConstants;

layout(location = 0) out vec3 fragNormal;

void main()
{
    vec4 worldPosition = pushConstants.model * vec4(inPosition, 1.0);
    gl_Position = cameraUbo.viewProj * worldPosition;

    // Good enough for uniform scale; a non-uniform scale would need the inverse transpose.
    fragNormal = mat3(pushConstants.model) * inNormal;
}
