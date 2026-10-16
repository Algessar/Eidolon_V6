
# Rendering Refactor Plan (Updated)

## Current statusCmdDrawIndexed

### ✅ Landed recently

1. **Pass-scoped execution keying is in place**
  - `PassExecutionKey` exists and carries pass-level render state/materialization inputs.
  - `PassExecutionFactory` now resolves pass targets and builds render pass + framebuffer contexts from compiled graph passes.

2. **Descriptor keying/factory direction is in place**
  - `DescriptorKey` is now a real key type.
  - Descriptor creation is moving toward `GetOrCreate(key)` caching semantics.

These were two of the highest-value prerequisites for unifying divergent render paths.

---

## What changes in the plan now

Because key-based pass execution + descriptor keying are underway, the next bottlenecks are no longer "how to build pass objects" — they are now:

1. **FrameHandler ownership explosion** (still too many concerns in one class).
2. **Scene and UI submission divergence** (separate data paths and custom UI-only buffer lifecycle).
3. **Lifecycle/sync coupling** (acquire/present/recreate mixed with draw recording logic).

So the plan should now pivot to **structural decomposition** and **single submission path**.

---

## Updated phased plan

## Phase 1 — Slim down FrameHandler (immediate)

**Goal:** Reduce `FrameHandler` to orchestration only.

- Extract graph runtime image/resource ownership into a dedicated runtime manager.
  - Resource lookup/import resolution.
  - Graph image creation/destruction.
  - Runtime attachment lookup by resource handle.

- Extract barrier/layout transitions into a dedicated planner/emitter.
  - Keep all transition policy in one place.
  - Make pass execution call one transition entrypoint.

- Extract UI geometry buffer lifecycle/upload into a dedicated uploader.
  - Stop keeping bespoke UI allocation logic embedded in frame orchestration.

**Definition of done:** `FrameHandler` mostly reads like: begin frame → resolve runtime state → execute pass loop → end frame.

## Phase 2 — Unify draw submission contract (high priority)

**Goal:** UI and scene draws use the same submission model.

- Replace the mixed `DrawData` shape with draw item(s) carrying common fields:
  - pipeline reference/key
  - descriptor set reference/key
  - vertex/index buffers
  - draw ranges and optional scissor/viewport
  - push constants / per-draw constants

- Keep UI-specific extraction in `ImGuiRenderer`, but have it emit the same draw item type consumed by scene rendering.

- Remove special-case fields from `DrawData` (`UiPipelineData`, `UiDescriptorSet`) once parity is reached.

**Definition of done:** One binder/draw path for all graphics draws; pass type controls scheduling, not a separate codepath.

## Phase 3 — Isolate frame lifecycle and swapchain flow (high priority)

**Goal:** Decouple sync/recreate logic from draw recording.

- Move acquire/fence/present/recreate flow into `FrameScheduler`/`FrameSyncCoordinator`.
- `FrameHandler` consumes a frame ticket (`frameIndex`, `imageIndex`, semaphores/fence handles).
- Centralize suboptimal/out-of-date handling and signal semaphore recreation.

**Definition of done:** Frame lifecycle policies are testable without touching pass recording code.

## Phase 4 — Complete key-first factories (follow-up)

**Goal:** Finish migrating ad hoc resource construction to key-driven caches.

- Keep extending descriptor bundle and pipeline/pass caches behind keys.
- Add/finish buffer key coverage where allocation policy differs (per-frame, transient, static).
- Remove duplicated legacy creation methods after callsites are migrated.

**Definition of done:** New render features are added by introducing/expanding keys, not giant procedural methods.

---

## Guardrails for ongoing work

- Prefer **more keys + small factories** over more branches in `FrameHandler`.
- No new "special UI path" logic in frame orchestration.
- Any new pass type should be executable via the same pass context + draw submission contract.
- Keep old/legacy methods only while migrating; delete as soon as equivalent keyed path is stable.

---

## Short actionable backlog

1. Create `GraphResourceRuntimeManager` and move graph image lifetime/import resolution out of `FrameHandler`.
2. Create `GraphBarrierPlanner` and move transition methods out of `FrameHandler`.
3. Create `UiGeometryUploader` and move per-frame UI buffer allocation/upload out of `FrameHandler`.
4. Introduce unified draw item model and adapt both scene + ImGui to emit it.
5. Introduce frame scheduler abstraction and move acquire/present/recreate flow.
6. Remove deprecated duplicate methods after migration compiles cleanly.


