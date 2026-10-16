# Render graph + pass execution: current flow, mismatch points, and refactor plan

This note documents how rendering currently works and where the old "single render pass/pipeline" path still leaks into the new render-graph execution model.

## 1) How the render graph currently works

### Authoring (frame graph description)
- The graph is authored in `EidolonEditor.BuildInitialGraph`.
- Resources are declared (`CreateImage` for transient, `ImportImage` for swapchain/depth external resources).
- Passes are declared with read/write dependencies (`Read`, `Write`) and pass types.
- `RenderGraphBuilder.Compile` computes pass dependencies, topological order, and resource lifetimes (`FirstUsePass`, `LastUsePass`).

### Runtime execution (FrameHandler)
- `MainRenderer.CreateRenderers` passes a compiled graph into `FrameHandler.SetCompiledGraph`.
- `FrameHandler` registers compiled resources with `GraphResourceRuntimeManager` and records imported-resource mapping (`GraphResourceImportMap`).
- Per-frame, `BeginFrame` acquires swapchain image and binds imported resources for the current image.
- `ExecutePasses` iterates compiled passes in execution order:
    1. `GraphBarrierHandler.TransitionLayouts` applies layout transitions for reads/writes.
    2. `PassExecutionHandler.GetOrCreate` resolves color/depth attachments for that graph pass and provides:
        - Vulkan `RenderPass`
        - `Framebuffer`
        - clear/load policy inferred from graph lifetime + pass index
    3. FrameHandler begins/ends render pass and records `DrawSubmission`s whose `PassType` matches current graph pass.

So yes: render pass setup is already graph-driven through `PassExecutionHandler`; draw recording still relies on submissions and pass-type matching.

## 2) Where the mismatch is (old path vs graph path)

### A) `DrawData.PipelineData` is still treated like a global default
`FrameHandler.Draw/BeginFrame/RecreateSwapchain` still require `DrawData.PipelineData`, even though graph passes own their own render passes/framebuffers.

Effect:
- startup path still builds a legacy pipeline in `MainRenderer.BuildInitialDrawData`.
- swapchain recreation still takes a legacy pipeline render pass argument.
- graph execution and old single-pass assumptions are mixed.

### B) `MainRenderer` still builds legacy render pass/pipeline for bootstrap
`MainRenderer.BuildInitialDrawData` creates a `RenderPassKey`, pipeline, framebuffer setup, and returns one default `DrawSubmission`.

Effect:
- when graph is set, this becomes mostly compatibility scaffolding.
- comments in file already indicate uncertainty and temporary behavior.

### C) `GameViewRenderer` represents old construction style and is not integrated
`Vulkan/Rendering/GameView/GameViewRenderer.cs` manually creates render-pass/pipeline keys and draw data, but graph execution is centralized in `FrameHandler + PassExecutionHandler`.

Effect:
- extending game view through this class fights the graph model.
- risk of duplicate path for render pass ownership.

### D) Render pass compatibility hidden behind `PassType`
Submissions are filtered only by enum `PassType`; there is no explicit binding between a submission and graph resource targets.

Effect:
- easy to accidentally draw into a pass with incompatible pipeline render pass (or wrong expectations).
- harder to evolve to multiple pipelines/subpasses per logical graph pass.

### E) Dead/experimental classes increase confusion
`Vulkan/Rendering/Refactor/PipelineFactory.cs` is experimental and recursive/incomplete; it does not participate in runtime path.

## 3) Suggested class-level changes (priority order)

## Phase 1 (minimal disruption, biggest clarity win)

1. **Decouple frame lifecycle from global `PipelineData`**
    - Change `FrameHandler.BeginFrame`, `EndFrame`, and `RecreateSwapchain` signatures to not require `PipelineData`.
    - Make swapchain recreation graph-aware (use imported backbuffer info, not caller pipeline render pass).

2. **Split `DrawData` into graph-era payload**
    - Keep only submissions (and optional frame constants) in `DrawData`.
    - Remove/obsolete `DrawData.PipelineData` once lifecycle no longer depends on it.

3. **Move graph-only bootstrap into one place**
    - In `MainRenderer`, stop creating legacy default pipeline/render pass when compiled graph is provided.
    - Keep one temporary compatibility path only when no graph is provided.

## Phase 2 (make game view extension smoother)

4. **Replace/retire `GameViewRenderer` as render-pass owner**
    - Convert it into a `GameViewSubmissionBuilder` (or similar) that only emits `DrawSubmission[]` for `RenderPassType.GameView`.
    - Do not let it create render passes or framebuffers.

5. **Introduce a per-pass submission registry**
    - e.g. `Dictionary<RenderPassType, List<DrawSubmission>>` or `IRenderPassSubmissionSource` interface.
    - `FrameHandler.ExecutePasses` asks the registry for submissions for `pass.Type`.
    - This removes array merge boilerplate from `MainRenderer` and makes pass ownership explicit.

6. **Add validation in `FrameHandler.RecordSubmission`**
    - Optional debug check: pipeline render pass compatibility with `PassExecutionContext.RenderPass`.
    - Fails fast when a submission/pipeline is incorrectly routed to a pass.

## Phase 3 (cleanup)

7. **Remove dead refactor scaffold**
    - Delete `Vulkan/Rendering/Refactor/PipelineFactory.cs` or move to docs/scratch area.

8. **Consolidate `RenderPassFactory` API around graph key**
    - keep `CreateRenderPass(PassExecutionKey)` as primary path.
    - mark/phase out direct `RenderPassKey` entry point where possible.

## 4) Concrete “change or remove” list for your goal (game view)

If your immediate goal is smoother game-view rendering extension, change/remove these first:

- **Change** `FrameHandler` (remove hard dependency on `DrawData.PipelineData` in frame lifecycle).
- **Change** `DrawData` (submissions-only payload).
- **Change** `MainRenderer` (graph-first bootstrap; no default render pass/pipeline when graph exists).
- **Change** `GameViewRenderer` into submission-builder role only.
- **Remove** `Vulkan/Rendering/Refactor/PipelineFactory.cs` (dead/confusing).

This aligns ownership as:
- graph decides pass order/resources/layout transitions,
- `PassExecutionHandler` decides Vulkan render pass/framebuffer per compiled pass,
- feature renderers (scene/gameview/ui) only emit draw submissions for pass types.

## 5) Short answer to your question

You are understanding it correctly: the *current intended architecture* is graph-driven pass execution via `CompiledRenderGraph + PassExecutionHandler`. 
The mismatch comes from leftover legacy assumptions (`DrawData.PipelineData`, bootstrap pipeline creation, and old-style GameView renderer responsibilities).
Cleaning those responsibilities up will make game-view extension significantly simpler and less error-prone.