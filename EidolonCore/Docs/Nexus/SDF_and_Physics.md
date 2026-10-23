# Eidolon SDF & XPBD

## Purpose

Eidolon needs a general geometric representation that can be used by physics, animation, gameplay, AI, and potentially 
rendering.

The first major consumer will be XPBD physics.

The goal is **not** to create an "SDF physics engine". SDF is a geometric representation/query mechanism. XPBD is the 
simulation system that consumes geometric information to construct constraints.

The architecture should keep those responsibilities separate.

---

# 1. Design Goals

The system should provide:

- Distance queries against surfaces.
- Surface normal/gradient information.
- Support for analytic primitives.
- Support for composed SDFs.
- A path toward mesh-derived and sampled SDFs.
- Efficient spatial queries.
- A representation usable by CPU physics initially.
- A future path toward GPU evaluation without requiring the physics architecture to change.
- Explicit ownership and lifetime.
- No dependency on XPBD-specific concepts inside the SDF system.

The SDF system should be useful even if XPBD does not exist.

---

# 2. Core Mental Model

An SDF represents a surface through a scalar field.

For a point `p`:

```text
distance = SDF(p)
```

Conceptually:

```text
distance < 0  → inside
distance = 0  → surface
distance > 0  → outside
```

The gradient provides the surface direction.

For collision purposes, the important result is therefore approximately:

```text
position
    ↓
SDF evaluation
    ↓
distance + normal
```

The physics system should consume that result rather than know how the SDF was constructed.

A useful conceptual result type is:

```text
SurfaceSample
    Distance
    Normal
```

The exact API should be determined from the existing Eidolon architecture rather than invented independently of it.

---

# 3. SDF Is Not A Collider Component

Avoid designing SDF as a special-purpose replacement for traditional collider components.

Instead, treat geometry as a source of surface information.

Possible representations include:

```text
Sphere
Capsule
Box
Plane
Mesh
Voxel/Sampled SDF
Composite SDF
```

These may have completely different internal representations while exposing the same conceptual query:

```text
Evaluate(position)
    → distance
    → gradient / normal
```

This means physics does not need separate collision implementations for every geometry representation.

---

# 4. Analytic SDFs

The initial implementation should use analytic primitives.

At minimum:

- Sphere
- Plane
- Box
- Capsule

These provide simple, deterministic implementations and are useful for testing the architecture.

For example:

```text
Sphere
    center
    radius

Evaluate(position)
    → signed distance
    → normal
```

The exact mathematical implementation belongs to the SDF implementation, not to consumers.

The consumer should not need to know whether a surface is a sphere, capsule, mesh, or voxel field.

---

# 5. SDF Composition

SDFs should eventually be composable.

Basic operations include:

```text
Union
Intersection
Subtraction
```

Potentially later:

```text
Smooth Union
Smooth Intersection
Smooth Subtraction
```

The important architectural property is that composition produces another SDF-like surface.

For example:

```text
Sphere
   +
Capsule
   ↓
Union
   ↓
Composite SDF
```

The consumer still performs:

```text
Evaluate(position)
```

without knowing that the result came from multiple surfaces.

Do not build a large boolean-geometry framework prematurely. The first implementation should establish whether composition 
is useful and how it fits the engine's ownership model.

---

# 6. Normal / Gradient

A distance alone is insufficient for physics.

XPBD needs a direction in which a particle should be corrected.

Therefore an SDF query should provide enough information to determine the surface normal.

Conceptually:

```text
Evaluate(position)
    ↓
distance
normal
```

For analytic SDFs, the normal should preferably be calculated analytically.

For sampled SDFs, the gradient may need to be approximated from neighboring samples.

The consumer should not need to know which method was used.

---

# 7. Meshes

Mesh geometry should eventually be usable as an SDF source.

However, this should not require every mesh to immediately become a dense voxel grid.

Possible representations include:

```text
Triangle mesh
    ↓
spatial acceleration structure
    ↓
distance query
```

and:

```text
Triangle mesh
    ↓
generated SDF
    ↓
sampled representation
```

These solve different problems.

The architecture should therefore avoid assuming that:

```text
SDF == voxel texture
```

An SDF is the mathematical representation/query. A voxel grid is only one possible implementation.

---

# 8. Performance

SDF evaluation must not automatically mean expensive global computation.

Different representations can have different costs.

For example:

```text
Sphere       → extremely cheap
Plane        → extremely cheap
Capsule      → cheap
Box          → cheap/moderate
Mesh + BVH   → spatially accelerated
Voxel SDF    → lookup
Composite    → dependent on composition
```

The system should allow the appropriate representation to be selected for the geometry.

Do not prematurely force all geometry through a single universal representation.

---

# 9. Spatial Queries

SDF should eventually support more than point evaluation.

Potential future queries include:

```text
Point → surface
Sphere → surface
Capsule → surface
Ray → surface
Segment → surface
```

However, these should only be added when required by actual consumers.

The first milestone is:

```text
Point → SurfaceSample
```

This is sufficient to begin validating the architecture and to provide XPBD with a collision surface.

---

# 10. Continuous Collision

SDF does not inherently solve tunnelling.

For example:

```text
position A       surface       position B

     ●  ──────────│────────────  ●
```

Both endpoint evaluations can be outside the surface even though the particle crossed it.

Therefore continuous collision detection, swept queries, substeps, or other anti-tunnelling mechanisms remain physics 
responsibilities.

Do not hide this problem inside the SDF system.

SDF answers:

> "What is the surface relationship at this position?"

XPBD/physics answers:

> "How do I evolve the simulation while respecting that surface?"

---

# 11. Relationship With XPBD

XPBD consumes geometric information from SDF.

The conceptual flow is:

```text
Physics particle
      ↓
candidate collision query
      ↓
SDF evaluation
      ↓
distance + normal
      ↓
contact constraint
      ↓
XPBD solver
      ↓
corrected particle state
```

The SDF system should not create or solve XPBD constraints.

Likewise, XPBD should not know how an SDF evaluates its geometry.

This separation is important.

---

# 12. XPBD Responsibilities

XPBD owns:

- Simulation state.
- Particles/bodies.
- Constraints.
- Constraint compliance.
- Constraint solving.
- Iteration.
- Simulation timestep.
- Integration.
- Collision response.
- Contact persistence if required.
- Synchronization between simulation and consumers.

SDF owns:

- Geometry representation.
- Distance evaluation.
- Surface information.
- SDF composition.
- Spatial representation/acceleration relevant to evaluation.

---

# 13. Collision Representation

Do not create a separate "classic collision physics system" merely as an alternative to SDF.

Instead, allow different geometric representations to provide collision information.

Conceptually:

```text
                 Geometry
                    │
       ┌────────────┼────────────┐
       ↓            ↓            ↓
    Analytic       Mesh       Sampled SDF
       │            │            │
       └────────────┼────────────┘
                    ↓
             Surface Query
                    ↓
             XPBD Constraint
```

This allows Eidolon to use cheap analytic geometry where appropriate while still supporting SDF-based geometry.

A mesh/BVH representation can also remain useful where exact triangle geometry or efficient broad-phase queries are preferable.

The physics architecture should not require a single universal collision representation.

---

# 14. XPBD Initial Scope

XPBD should initially remain deliberately small.

The first implementation should establish:

```text
Simulation
    ↓
Particles
    ↓
Integration
    ↓
Constraints
    ↓
Solver iterations
    ↓
Position update
```

Then add collision:

```text
Particle
    ↓
SDF query
    ↓
Contact constraint
    ↓
XPBD solver
```

The goal is not to build a complete production physics engine immediately.

The goal is to prove that Eidolon's simulation architecture is sound.

---

# 15. Collision Constraint

A basic SDF contact can be expressed conceptually as:

```text
C(x) = distance(x)
```

For a particle penetrating the surface:

```text
C(x) < 0
```

The solver should enforce:

```text
C(x) >= 0
```

using the surface normal supplied by the SDF.

The exact XPBD formulation should remain inside the physics system.

SDF should not know about compliance, lambda accumulation, solver iterations, or XPBD state.

---

# 16. Static vs Dynamic Geometry

The initial implementation should distinguish between:

```text
Static geometry
Dynamic bodies
```

Static geometry can be represented by persistent SDF/geometry data.

Dynamic bodies should generally be represented through their simulation state and transformed geometry rather than 
requiring the SDF representation itself to be rebuilt every simulation step.

This distinction should be preserved when designing ownership.

---

# 17. CPU First

The initial SDF implementation should target CPU execution.

The purpose is to establish:

- Correct mathematical behavior.
- Ownership.
- API shape.
- Composition.
- Physics integration.
- Testing.

GPU acceleration should not drive the initial architecture.

However, avoid an API that fundamentally assumes:

```text
"C# object with arbitrary virtual method calls"
```

is the only possible representation.

The long-term engine direction includes GPU computation, so the underlying data should remain amenable to compact, 
transferable representations.

---

# 18. Future GPU Direction

Eventually, SDF evaluation may become useful on the GPU for:

- Large numbers of collision queries.
- Particle simulation.
- GPU XPBD.
- Animation queries.
- AI/environment queries.
- Procedural geometry.
- Rendering effects.

The CPU implementation should therefore establish the semantic contract first:

```text
position → surface sample
```

while leaving the execution strategy open.

The same conceptual query should eventually be executable through different backends.

Do not implement the GPU system until there is a demonstrated workload that benefits from it.

---

# 19. Nexus Integration

Nexus should be designed alongside SDF and XPBD, but neither system should become dependent on a large Nexus abstraction 
before there is a concrete requirement.

The architectural goal is:

```text
Nexus
 ├── can provide/use simulation systems
 ├── can provide/use animation systems
 ├── can provide/use ML systems
 └── can coordinate data/behaviour
```

rather than:

```text
SDF
    ↓
NexusSdfManager
    ↓
NexusPhysicsProvider
    ↓
NexusWhatever
```

Avoid creating layers whose only purpose is forwarding calls.

Nexus should earn each responsibility through actual system requirements.

---

# 20. Initial Implementation Milestones

## Milestone 1 — SDF Core

Implement enough to evaluate:

```text
Sphere
Plane
Box
Capsule
```

and obtain:

```text
Distance
Normal
```

Validate the mathematics independently of physics.

DONE when the representations can be evaluated at arbitrary positions and produce correct signed distances and normals.

---

## Milestone 2 — Composition

Implement basic:

```text
Union
Intersection
Subtraction
```

only if the resulting architecture remains simple.

DONE when composed surfaces can be queried without consumers knowing their internal structure.

---

## Milestone 3 — XPBD Core

Implement:

```text
Particles
Integration
Constraints
Solver
```

without requiring SDF.

DONE when a simple constrained simulation behaves correctly.

---

## Milestone 4 — SDF Collision

Add:

```text
Particle
    ↓
SDF
    ↓
Contact constraint
    ↓
XPBD
```

DONE when particles can collide with analytic SDF surfaces and remain outside them under normal simulation conditions.

---

## Milestone 5 — Stress Cases

Test:

- High velocity.
- Deep initial penetration.
- Multiple contacts.
- Thin geometry.
- Multiple SDF surfaces.
- Composite surfaces.
- Different solver iteration counts.
- Different timesteps.

This stage should identify whether continuous collision, substepping, or additional query types are required.

---

# 21. Explicit Non-Goals

The first implementation should NOT attempt to build:

- A complete game physics engine.
- GPU physics.
- A universal collision framework.
- A full mesh-to-SDF baking pipeline.
- Every possible SDF primitive.
- A complete boolean modelling system.
- Continuous collision detection for every geometry type.
- A general-purpose physics material system.
- Networking/rollback physics.
- A complete Nexus framework.

The purpose is to establish the core architecture and prove the data flow.

---

# 22. Architectural Principle

The key separation is:

```text
SDF:
"What does the geometry say about this position?"

XPBD:
"How does the simulation satisfy its constraints?"

Nexus:
"How do Eidolon's systems participate in the larger engine?"

These should remain distinct.

The systems should communicate through explicit data and flow rather than through implicit ownership or large manager
abstractions.

---

# 23. Target Architecture

The intended conceptual architecture is:

```text
                    ┌──────────────┐
                    │    Nexus     │
                    └──────┬───────┘
                           │
             ┌─────────────┴─────────────┐
             │                           │
       ┌─────▼─────┐               ┌─────▼─────┐
       │    SDF    │               │   XPBD    │
       │  Geometry │◄──────────────│ Simulation│
       └─────┬─────┘   queries     └─────┬─────┘
             │                           │
       ┌─────┴─────┐                     │
       │           │                     │
    Analytic     Mesh                 Particles
    surfaces     /SDF                 Constraints
                                      Solver
```

This is a conceptual model, not a prescription for the final class hierarchy.

The implementation should follow Eidolon's existing module boundaries and avoid introducing types solely to make the 
diagram look clean.

---

# Final Principle

**SDF is geometry. XPBD is simulation. Nexus is orchestration/integration.**

SDF should be useful without XPBD.

XPBD should be capable of solving constraints without SDF.

Their integration should be explicit and small.

This keeps the architecture flexible enough to support traditional geometry, analytic surfaces, sampled SDFs, 
CPU execution, future GPU execution, animation queries, and ML/environment queries without turning SDF into a physics-specific subsystem.