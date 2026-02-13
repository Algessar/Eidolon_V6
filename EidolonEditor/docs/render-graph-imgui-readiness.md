# Render Graph ImGui Readiness Assessment

## Verdict

**Not fully ready** for integrating ImGui as a first-class render-graph pass yet.

## Required method presence check

The following methods are present in `FrameHandler`:

- `TransitionLayouts`
- `EmitColorAttachmentTransition`
- `EmitShaderReadTransition`
- `EmitPresentTransition`

So the branch appears to include the expected transition work.

## Blocking issues for ImGui integration

1. **Imported resource resolution path is not wired into frame execution.**
    - `GraphResourceImportMap` exposes `TryResolve(...)`, but there is no call site in `FrameHandler`.
    - Without this, imported resources (including backbuffer/depth) are not materialized through the render-graph resource map.

2. **`EnsureGraphResources(...)` is referenced but not implemented in `FrameHandler`.**
    - `Draw(...)` and `CreateResources(...)` both call `EnsureGraphResources(_compiledGraph)`.
    - There is no implementation in the class, which indicates the graph resource setup path is incomplete/broken.

3. **Pass execution currently reuses the same render pass/framebuffer for every graph pass.**
    - Inside each compiled pass iteration, code binds the swapchain framebuffer and same render pass before draw.
    - This model is not yet a pass-specific render-target binding model, so adding an ImGui pass as an explicit graph node is likely to be brittle.

4. **Present transition is emitted on imported write resources, but current layout tracking for imported images is not clearly initialized per acquired image.**
    - `EmitPresentTransition` requires `CurrentLayout == ColorAttachmentOptimal`.
    - If imported swapchain image runtime state is not updated per acquisition, transitions can be skipped incorrectly.

## What is already good enough

- Transition helper methods are in place and structured in a way that can evolve to per-pass barriers.
- Render-graph data model supports adding a new pass type and resource dependencies.

## Suggested ImGui integration approach (after fixes)

1. Add a new pass type, e.g. `RenderPassType.Ui`.
2. Build graph like:
    - Geometry -> PostProcess -> Ui -> Present
    - `Ui` reads post-processed color (optional) and writes backbuffer (or a ui-composited intermediate image).
3. In backend execution:
    - Resolve imported resources into per-frame runtime images/views before pass execution.
    - Maintain per-resource layout state seeded from acquired swapchain image state.
    - Execute `Ui` pass last in graphics queue, with alpha blending enabled and no depth writes.
4. Use ImGui backend draw-data emission as the body of that `Ui` pass only.
5. Keep present barrier emission centralized after final writer of backbuffer.

## Recommended immediate TODOs

- Implement/fix `EnsureGraphResources(...)` in `FrameHandler`.
- Wire `GraphResourceImportMap.TryResolve(...)` into resource runtime creation/update every frame.
- Make pass framebuffer/render pass selection derive from graph pass targets rather than always swapchain-only.
- Add validation/logging assertions for imported resource layout initialization per frame.
