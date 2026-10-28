# Eidolon Development Guidelines

Treat current DevDoc material as project context, but reference the actual source code and Unity assets when determining
how the current implementation behaves.

Do not assume archived or explicitly obsolete documentation represents the current design.

If a document begins with `[obsolete]` or `[ai-ignore]`, skip it completely.
If a document begins with `[under revision]`, it is not canonical and should be treated with caution.
If a document begins with `[WIP]`, it will definitely change. 

## Task Boundaries

- If asked for a plan, summary, analysis, assessment, or description of the current project state, do not modify any files or implement changes.
- Planning and implementation are separate tasks. Approval of a plan does not authorise implementation unless I explicitly request that implementation begin.
- When asked to assess the current implementation against design documentation, inspect the relevant code and documents, identify gaps, and estimate the scope of required changes without making modifications.


Do not make types public merely to bypass an ownership or dependency boundary.

Prefer keeping APIs narrow.

Do not introduce abstractions unless they solve an actual architectural problem.

Do not modify working code merely for stylistic reasons.

Preserve existing architecture unless the task explicitly requires changing it.