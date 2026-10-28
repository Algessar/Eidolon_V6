[WIP]

# Skeletal System

A Highly Opinionated Wish-list For Skeletal Armature Implementation

- Root Motion must be simple to set up and not default to any particular bone.
    - Rotation and Vertical movement should be baked by default
- 

---

# Implemented So Far

The runtime side of authored animation exists. Nothing below is about skinning or imported rigs yet, so
this section is about *clip playback*, not about the skeletal wishlist above.

- **`AnimationClip` → `AnimationTrack` → `PoseKey`**: keyed local position/rotation/scale per bone path,
  sorted on construction, slerped along the shortest arc, holding the ends instead of extrapolating.
- **`AnimationPlayer`** on the entity that owns the hierarchy: clip, playhead, speed (negative plays
  backwards), loop, playing. Per-instance state, so one clip can run on several entities independently.
- **`Name` + `Hierarchy.TryFindByPath`**: clips address bones as paths (`"Rotor/Blade1"`, or `""` for the
  player's own entity), which keeps them readable and reusable and gives an importer something to map
  bone names onto.
- **Desired state, not authority**: `AnimationSystem` publishes `DesiredPose` and `PoseResolutionSystem`
  commits it. Animation never writes transforms directly, so XPBD can outrank it later without the two
  fighting over the entity.
- Scene files carry names and clips (format version 3), and play-mode clones get their own playhead while
  sharing the clip.

Still open: the timeline UI, blending and layers, root motion, skinning, import (glTF first), and the
priority rules for when physics and animation both claim the same bone.
