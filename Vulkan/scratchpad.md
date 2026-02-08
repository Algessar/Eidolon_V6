
### Goals (in order):

- Render graph
- ImGui editor UI
- GameView in Editor
- Meshes
- 
___
- Floating window support
- Scene loading
- Materials

___
- AssImp integration
- Asset pipeline
- 
___ 
- Textures
- Skybox
- Game engine standalone runtime compile
- 
___

#### Overarching goals:
- SDF rendering
- 3D physics
- ML integration

___
___

### Project structure:

#### EidolonCore
- RenderGraph (data only)
- Asset handles
- Shared math/types

___
#### EidolonEngine
- Builds RenderGraph
- Create meshes/materials (Core handles)
___

#### Vulkan
- Device
- Swapchain
- Frame lifecycle
- ExecuteRenderGraph()
- Silk.NET (internal)
___

