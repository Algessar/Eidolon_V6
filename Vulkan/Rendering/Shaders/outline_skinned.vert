#version 450
#extension GL_ARB_separate_shader_objects : enable

// Selection outline for a skinned mesh: the same inverted hull as outline.vert, but the vertices are
// deformed by the bone palette first, so the rim follows the animated pose instead of the bind pose.

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inNormal;
layout(location = 2) in vec4 inBoneWeights;
layout(location = 3) in vec4 inBoneIndices;

layout(set = 0, binding = 0) uniform CameraUbo {
    mat4 viewProj;
} cameraUbo;

layout(set = 0, binding = 1) readonly buffer BonePalette {
    mat4 bones[];
} palette;

// Same block as skinned.vert, so one push constant range serves every skinned pipeline.
layout(push_constant) uniform PushConstants {
    mat4 model;
    vec4 baseColor;
    uint boneOffset;
} pushConstants;

const float OUTLINE_WIDTH = 0.014;

void main()
{
    int baseIndex = int(pushConstants.boneOffset);

    vec4 skinnedPosition = vec4(0.0);
    vec3 skinnedNormal = vec3(0.0);

    for (int i = 0; i < 4; i++)
    {
        float weight = inBoneWeights[i];

        if (weight <= 0.0)
            continue;

        mat4 bone = palette.bones[baseIndex + int(inBoneIndices[i])];
        skinnedPosition += (bone * vec4(inPosition, 1.0)) * weight;
        skinnedNormal += (mat3(bone) * inNormal) * weight;
    }

    vec3 localPosition = skinnedPosition.xyz;
    vec3 localNormal = skinnedNormal;

    if (dot(inBoneWeights, vec4(1.0)) <= 0.0)
    {
        localPosition = inPosition;
        localNormal = inNormal;
    }

    mat4 modelViewProjection = cameraUbo.viewProj * pushConstants.model;

    vec4 clipPosition = modelViewProjection * vec4(localPosition, 1.0);
    vec4 clipNormal = modelViewProjection * vec4(localNormal, 0.0);

    vec2 direction = clipNormal.xy;
    float length = max(length(direction), 1e-5);

    clipPosition.xy += (direction / length) * OUTLINE_WIDTH * clipPosition.w;

    gl_Position = clipPosition;
}
