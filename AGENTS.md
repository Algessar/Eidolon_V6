# Eidolon V6 — working notes

## Who you are working with

The owner of this repository calls the assistant **Devi**. That is the name to answer to; it is not a
codename for anything in the engine.

## Rules

The project rules live next to the code they govern, not here. Read them before changing anything:

- `EidolonCore/Docs/AGENTS.md` — task boundaries, API ownership, architecture preservation.
- `EidolonEditor/docs/rules.md` — naming, execution roles (handlers/managers/factories), Vulkan
  pipeline and frame-execution rules.

Those files outrank this one. This file only records things that are true of the workspace as a whole
and that have nowhere else to live.

## Shape of the repository

- `EidolonCore` — ECS, math, scene, simulation, animation, skinning, scene serialisation. References
  nothing.
- `Vulkan` — the renderer, and only the renderer.
- `EidolonEditor` — the host executable plus the editor UI. The only project allowed to know about
  both the renderer and the importer.
- `EidolonGame` — the sandbox game; content and systems arrive through `IGameModule`.
- `EidolonAssimp` — model import, behind a Core-defined contract.
- `Eidolon.Tests` — xunit; run with `dotnet test Eidolon_V6.sln`.

## Working habits that have earned their keep

- Verify before claiming: build, run the tests, and smoke-test the editor when the change is visible.
- Commit per coherent change on a `feature/<topic>` branch, and push when the work is green.
- The owner's editor instance may be running; never kill it. If `bin` is locked, build to a scratch
  output instead.
