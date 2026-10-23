# Nexus

Nexus is the over-arching system that defines Eidolon. It is the combination of physics,
SDF, Animation and Machine Learning. The goal of Eidolon is to make these systems work together
from the start.

Phrased differently: Systems express intent, targets, and constraints. Nexus resolves those into authoritative 
simulation state.

**SDF:**
SDF will be tightly bound to all World systems. While Raycasts and such will most likely still exist, distance queries and
collision will be based on SDF gradients.

- Collision
- Distance queries
- All the other SDF functions: min/max, difference/union etc
- Possibly rendering: Ambient Occlusion, Depth of Field, calculating normals etc [undecided]
- 


**Physics:** 
- XPBD based physics
- SDF collision
- Animation integration

**Animation:**
- Clear states
- Physics integration
- ML integration

The animation system will likely require the most thought. The goal is the have a very clear Resolved State that
runs after all other systems. 

Proposed resolve state:

___

# Eidolon Nexus

## Overview

**Nexus** is Eidolon's integrated simulation and intelligence framework.

Its primary systems are:

- **XPBD** — physically based constraint solving and simulation
- **SDF** — signed distance field generation and spatial queries
- **Animation** — animation evaluation, procedural animation, pose generation and modification
- **ML** — machine-learning-driven behavior, control and decision making

The goal is not simply to provide four independent systems in one engine.

The goal is for these systems to **work together as parts of a shared simulation model**, allowing animation, physics, spatial information and machine learning to influence one another without requiring direct coupling between their implementations.

Nexus should make complex behaviors possible without requiring the user to manually bridge unrelated systems.

---

# Core Principle

> **Systems express intent and constraints. Nexus resolves them into authoritative state.**

A Nexus system should generally contribute to simulation rather than directly owning the final state of an entity.

For example:

- Animation can produce a desired pose.
- ML can produce behavioral intent or targets.
- SDF can provide spatial information and constraints.
- XPBD can resolve physical constraints.
- Nexus ultimately produces the authoritative resulting state.

This should allow the systems to compose rather than compete.

---

# The Shared Simulation Model

Nexus should be thought of as a collection of systems operating on shared simulation state rather than as four systems communicating through direct references.

Conceptually:

```text
                 ┌─────────────┐
                 │     ML      │
                 └──────┬──────┘
                        │
                  Intent / Targets
                        │
                        ▼
                 ┌─────────────┐
                 │  Animation  │
                 └──────┬──────┘
                        │
                 Desired Pose/State
                        │
                        ▼
                 ┌─────────────┐
                 │    XPBD     │◄──── SDF
                 └──────┬──────┘     Queries
                        │
                 Resolved State
                        │
                        ▼
                 ┌─────────────┐
                 │Nexus State  │
                 └─────────────┘
```

This diagram is conceptual rather than a fixed execution order.

In particular, **SDF is primarily a provider of spatial information**, not necessarily a sequential stage in the simulation pipeline. XPBD, Animation, ML and other systems may query SDF data as required.

Similarly, ML and Animation should not necessarily form a strict linear chain. Both may contribute intent, targets or desired state.

---

# Desired State, Constraints and Resolved State

A useful preliminary model is to distinguish three concepts.

## Desired State

What systems want to happen.

Examples:

- Animation wants a hand at a particular pose.
- ML wants an agent to move toward a target.
- Gameplay wants a character to perform an action.
- A procedural animation system wants a foot placed on the ground.

Multiple systems may contribute to desired state.

## Constraints

What the simulation says is physically or logically allowed.

Examples:

- Joint limits.
- Collision constraints.
- SDF surface constraints.
- Contact constraints.
- Physical attachment.
- User-defined XPBD constraints.

## Resolved State

The resulting authoritative simulation state after desired states and constraints have been composed and resolved.

For example:

```text
Animation:
    Hand should be here.

ML:
    Agent wants to reach toward this target.

SDF:
    There is a surface here.

XPBD:
    Elbow and shoulder constraints must remain valid.

                    ↓

              Nexus Resolution

                    ↓

Final:
    Physically valid pose and state.
```

The resolved state becomes the authoritative state observed by systems on subsequent simulation steps.

---

# Layering and Priority

Nexus should support **layering of influence**, rather than treating systems purely as competing sources of truth.

A system may modify the result produced by a previous layer without replacing the entire state.

For example:

```text
Authored Animation
        ↓
Animation Evaluation
        ↓
Procedural Animation
        ↓
IK / Pose Modification
        ↓
ML-driven Targets
        ↓
Physical Constraints
        ↓
Resolved State
```

This is an example, not a mandatory ordering.

The important distinction is:

### Priority

A higher-priority source wins when two sources directly conflict.

### Layering

A system modifies only the portion of state or degrees of freedom that it is responsible for.

### Resolution

The final simulation determines what is actually possible.

Nexus should support these concepts independently rather than reducing the entire system to a single global priority list.

---

# Animation

Animation in Nexus refers to the **animation system**, not merely authored animation clips.

Authored animation is one input to the system.

The Animation system may also perform:

- animation state selection
- blending
- pose generation
- procedural animation
- IK
- pose modification
- target-driven animation
- physical correction
- other runtime pose adjustments

The resulting pose should be treated as a **desired state** rather than automatically being the final authoritative physical state.

This allows animation to interact naturally with XPBD and other Nexus systems.

For example:

```text
Animation Clip
      ↓
Animation Evaluation
      ↓
Desired Pose
      ↓
Procedural Modification
      ↓
IK
      ↓
Physical Constraints
      ↓
Resolved Pose
```

This model should allow physically constrained animation without requiring animation and physics to fight over ownership of transforms.

---

# XPBD

XPBD is intended to provide the core physical constraint and simulation layer within Nexus.

It should be capable of resolving constraints arising from:

- physical bodies
- joints
- contacts
- collision
- animation targets
- procedural animation
- gameplay
- ML-driven targets
- other simulation requirements

Animation and other systems should generally express **targets or constraints**, rather than directly manipulating the final physically simulated state.

XPBD then determines a physically consistent result.

The system should support iterative solving where required rather than assuming that physics can always be represented as a single pass through the pipeline.

---

# SDF

SDF functionality should provide a general spatial representation and query system rather than being treated solely as a collision implementation.

Potential capabilities include:

- distance queries
- surface queries
- gradients
- inside/outside tests
- closest-surface queries
- collision-related queries
- other spatial operations

SDF should ideally be usable by multiple Nexus systems.

For example:

```text
                 SDF
              /   |   \
             /    |    \
          XPBD Animation ML
```

The SDF system should not need to know which subsystem is consuming its information.

This allows the same spatial representation to support physics, animation, procedural behavior and machine learning.

---

# Machine Learning

ML should be treated as another participant in the simulation rather than as an external system that directly manipulates game objects.

ML should primarily produce **intent, decisions, actions or targets**.

Examples:

- desired movement
- desired direction
- target selection
- pose targets
- action selection
- behavioral decisions
- control inputs

ML should generally not directly set authoritative transforms.

Instead:

```text
ML
 ↓
Action / Intent
 ↓
Animation / Gameplay / Physics
 ↓
Resolved State
```

The resulting simulation state can then become the basis for subsequent ML observations.

This creates a closed loop:

```text
Observations
     ↓
    ML
     ↓
 Actions / Intent
     ↓
   Simulation
     ↓
 Resolved State
     ↓
 Observations
```

This should allow ML agents to operate inside the same physical and animation systems used by the game.

---

# Cross-System Interaction

The primary architectural objective of Nexus is to make interactions between systems **natural rather than bespoke**.

Examples:

### ML + Animation

ML selects or drives a behavior.

Animation converts that intent into movement and pose.

### Animation + XPBD

Animation produces a desired pose.

XPBD ensures that the resulting pose respects physical constraints.

### SDF + XPBD

SDF provides spatial information.

XPBD uses that information to create or resolve physical constraints.

### SDF + Animation

Animation can query surfaces for foot placement, body adaptation or other procedural adjustments.

### ML + Physics

ML can observe physical state and produce actions without needing to understand the underlying solver.

### ML + SDF

ML may use spatial information as observations or targets without needing to know how the SDF itself is implemented.

The desired result is a network of composable systems rather than a collection of special-case integrations.

---

# Ownership of State

One of the most important architectural questions in Nexus is **who owns the final state**.

The preliminary answer is:

> Individual systems should not independently own the final state when multiple systems are capable of influencing it.

Instead:

```text
Systems
   ↓
Intent / Desired State / Constraints
   ↓
Nexus Resolution
   ↓
Authoritative Simulation State
```

The exact implementation of this resolution model remains to be designed.

In particular, Nexus still needs to determine:

- how state is represented
- how partial state ownership works
- how layers declare what they modify
- how conflicting modifications are handled
- how priorities are represented
- how constraints interact
- how simulation steps are scheduled
- where state is committed
- how multi-threading affects the model

These should be solved before heavily coupling the individual subsystems.

---

# Dependency Philosophy

Nexus systems should communicate primarily through **shared data, capabilities and simulation state**, not direct 
references to other subsystem implementations.

Avoid:

```text
XPBD → Animation implementation
Animation → ML implementation
ML → XPBD implementation
```

Prefer:

```text
             Nexus State / Capabilities
              /        |        \
            XPBD   Animation     ML
              \        |        /
                    SDF
```

A system should know what information or capability it needs, rather than who happens to provide the implementation.

This follows the broader Eidolon architectural principle of keeping systems decoupled from backend implementation details.

---

# Preliminary Simulation Flow

A possible high-level simulation cycle is:

```text
1. Gather current authoritative state
        ↓
2. Gather observations / inputs
        ↓
3. ML produces intent / actions
        ↓
4. Animation produces desired pose / motion
        ↓
5. Procedural systems modify desired state
        ↓
6. SDF provides spatial information
        ↓
7. XPBD constructs and solves constraints
        ↓
8. Nexus resolves / commits authoritative state
        ↓
9. Results become available to rendering, gameplay and ML
        ↓
10. Next simulation step
```

This is **not yet a fixed execution schedule**.

XPBD may require multiple iterations, systems may operate at different frequencies, and some interactions may require feedback within a simulation step.

The purpose of this flow is to establish the conceptual direction rather than dictate implementation.

---

# Initial Architectural Questions

Before implementing the Nexus integration layer, investigate:

1. **What exactly constitutes Nexus simulation state?**
2. **Which state is desired state versus authoritative state?**
3. **How are partial state modifications represented?**
4. **How are priorities and layers represented?**
5. **How are constraints registered and resolved?**
6. **How does XPBD consume animation targets?**
7. **How does Animation consume physical state?**
8. **How does ML expose actions and observations?**
9. **How are SDF queries exposed without coupling SDF to its consumers?**
10. **How are simulation phases scheduled?**
11. **Which systems need iterative feedback during one simulation step?**
12. **How is multi-threading handled?**
13. **Where is authoritative state committed?**
14. **How are race conditions prevented without turning Nexus into a giant synchronization layer?**

These questions should be answered incrementally rather than attempting to design the entire framework upfront.

---

# Initial Implementation Strategy

Do not begin by implementing the complete Nexus framework.

Start with the smallest useful integration:

### Step 1 — Shared simulation state

Define the basic state representation and ownership model.

### Step 2 — XPBD + SDF

Create a simple physically constrained object interacting with an SDF.

### Step 3 — Animation + XPBD

Drive a skeleton or articulated object with animation targets while XPBD resolves physical constraints.

### Step 4 — Procedural animation

Add procedural pose modification and verify that it composes cleanly with authored animation and physics.

### Step 5 — ML

Expose observations and actions to ML and allow an agent to control an existing Nexus simulation.

### Step 6 — Integration tests

Build small scenarios specifically designed to test system interaction rather than individual subsystem functionality.

Examples:

- animated character constrained by terrain
- physically reactive animation
- ML-controlled humanoid
- ML-controlled physically simulated body
- SDF-aware procedural locomotion
- ML agent learning movement through the same XPBD system used by gameplay

---

# Design Mantra

> **Systems express intent. Constraints define what is allowed. Nexus resolves what actually happens.**

And more broadly:

> **Nexus is not four systems glued together. It is a shared simulation framework in which XPBD, SDF, Animation and ML can participate in the same state.**

---

# Status

**Very preliminary.**

The architecture described here is a conceptual starting point, not an implementation specification.

The next stage should be to challenge these assumptions against Eidolon's existing architecture and determine what the
smallest concrete Nexus prototype should look like.