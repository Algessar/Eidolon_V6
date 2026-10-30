#version 450
#extension GL_ARB_separate_shader_objects : enable

// Skinned geometry: the bind pose plus up to four bone influences per vertex. The bones are applied
// here, so the CPU never touches a deformed vertex.

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inNormal;
layout(location = 2) in vec4 inBoneWeights;
layout(location = 3) in vec4 inBoneIndices;

layout(set = 0, binding = 0) uniform CameraUbo {
    mat4 viewProj;
} cameraUbo;

// The bone palette for every skinned mesh drawn this frame; pushConstants.boneOffset picks this skin's slice.
layout(set = 0, binding = 1) readonly buffer BonePalette {
    mat4 bones[];
} palette;

layout(push_constant) uniform PushConstants {
    mat4 model;
    vec4 baseColor;
    uint boneOffset;
} pushConstants;

layout(location = 0) out vec3 fragNormal;

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

    // A vertex with no influence stays in its bind pose rather than collapsing onto the origin.
    vec3 localPosition = skinnedPosition.xyz;
    vec3 localNormal = skinnedNormal;

    if (dot(inBoneWeights, vec4(1.0)) <= 0.0)
    {
        localPosition = inPosition;
        localNormal = inNormal;
    }

    vec4 worldPosition = pushConstants.model * vec4(localPosition, 1.0);
    gl_Position = cameraUbo.viewProj * worldPosition;

    fragNormal = mat3(pushConstants.model) * normalize(localNormal);
}
