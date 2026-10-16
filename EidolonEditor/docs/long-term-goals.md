## Eidolon Engine — Long-Term Goals

- **Custom animation system:** Explicit, flexible animation state and transitions, procedural animation, and tight integration with physics, without the limitations of Unity's Mecanim architecture.
- **XPBD physics:** A constraint-based physics foundation supporting stable simulation, articulated characters, and eventually deformable bodies.
- **GPU-accelerated SDF system:** First-class signed distance field operations for collision detection, surface gradients, spatial queries, and advanced physical interactions.
- **Integrated ML agent framework:** Infrastructure for training agents to control physical characters through root movement, joint targets, and physics-based corrections.
- **Hybrid animation and physics:** Animation provides desired poses and movement, while physics and procedural corrections determine how characters physically realize them.
- **Explicit rendering architecture:** A render graph with predictable GPU resource ownership, synchronization, and frame lifetimes, keeping Vulkan-specific implementation details out of the engine core.
- **Integrated development editor:** An extensible editor with a Game View, floating windows, and tools for inspecting and debugging engine systems.

These systems should be designed to cooperate, not merely coexist. For example, 
an ML agent should be able to control a character through the same animation and physics interfaces available to ordinary gameplay code. 
An SDF should be usable by multiple systems without each implementing its own version of distance queries.