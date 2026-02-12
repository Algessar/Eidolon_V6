
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

```csharp
    private void BeginPassRenderPass(CommandBuffer cmd, RenderPass renderPass, bool hasDepth)
    {
        var clearValuesArray = hasDepth ? new ClearValue[2] : new ClearValue[1];
        clearValuesArray[0] = new ClearValue
        {
            Color = new ClearColorValue(0.0f, 0.2f, 0.4f, 1.0f)
        };
    
        if (hasDepth)
        {
            clearValuesArray[1] = new ClearValue
            {
                DepthStencil = new ClearDepthStencilValue(1.0f, 0)
            };
        }
    
        fixed (ClearValue* clearValuesPtr = clearValuesArray)
        {
            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = renderPass,
                Framebuffer = _swapchainHandler.Framebuffers[_currentImageIndex],
                RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainHandler.Extent),
                ClearValueCount = (uint)clearValuesArray.Length,
                PClearValues = clearValuesPtr
            };
    
            _master.Vk.CmdBeginRenderPass(cmd, &renderPassInfo, SubpassContents.Inline);
        }
    }

```