#version 450
#extension GL_ARB_separate_shader_objects : enable

//layout(location = 0) in vec3 inPosition;
//layout(location = 1) in vec3 inNormal;
//layout(location = 2) in vec2 inUV;

layout(push_constant) uniform PushConstants {
    mat4 model;
} pushConstants;

layout(location = 0) out vec3 fragColor;

void main() {
//    gl_Position = pushConstants.model * vec4(inPosition, 1.0);
    const vec2 positions[3] = vec2[](
    vec2(0.0, -0.5),
    vec2(0.5, 0.5),
    vec2(-0.5, 0.5)
    );

    const vec3 colors[3] = vec3[](
    vec3(1.0, 0.2, 0.2),
    vec3(0.2, 1.0, 0.3),
    vec3(0.2, 0.5, 1.0)
    );

//    // Simple color based on normal
//    fragColor = normalize(inNormal) * 0.5 + 0.5;

    vec4 localPosition = vec4(positions[gl_VertexIndex], 0.0, 1.0);
    gl_Position = pushConstants.model * localPosition;
    fragColor = colors[gl_VertexIndex];
}