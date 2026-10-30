#version 450
#extension GL_ARB_separate_shader_objects : enable

// Selection outline: the mesh is pushed along its normals and drawn with the front faces culled, so only
// the rim around the object survives. Pushing in clip space keeps the rim about the same width on screen
// whatever the object's distance or scale.

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inNormal;

layout(set = 0, binding = 0) uniform CameraUbo {
    mat4 viewProj;
} cameraUbo;

layout(push_constant) uniform PushConstants {
    mat4 model;
} pushConstants;

// Roughly 1.4% of the distance from the centre of the screen, which reads as a two-pixel rim.
const float OUTLINE_WIDTH = 0.014;

void main()
{
    mat4 modelViewProjection = cameraUbo.viewProj * pushConstants.model;

    vec4 clipPosition = modelViewProjection * vec4(inPosition, 1.0);
    vec4 clipNormal = modelViewProjection * vec4(inNormal, 0.0);

    vec2 direction = clipNormal.xy;
    float length = max(length(direction), 1e-5);

    // Scaling by w cancels the perspective divide, so the rim stays a fixed size after projection.
    clipPosition.xy += (direction / length) * OUTLINE_WIDTH * clipPosition.w;

    gl_Position = clipPosition;
}
