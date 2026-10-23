#version 450
#extension GL_ARB_separate_shader_objects : enable

layout(location = 0) in vec3 fragNormal;

layout(push_constant) uniform PushConstants {
    mat4 model;
    vec4 baseColor;
} pushConstants;

layout(location = 0) out vec4 outColor;

void main()
{
    // Fixed key light until there is a real lighting model.
    vec3 lightDirection = normalize(vec3(0.4, 0.8, 0.35));
    vec3 normal = normalize(fragNormal);

    float diffuse = max(dot(normal, lightDirection), 0.0);
    float lighting = 0.28 + 0.72 * diffuse;

    outColor = vec4(pushConstants.baseColor.rgb * lighting, pushConstants.baseColor.a);
}
