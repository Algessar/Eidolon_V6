- Remove duplicated code
- Make sure anything that has a place in a manager or factory is in one
- All rendering should be as unified as possible
    - Both 3D and ImGui uses vertices to render : that means there is *no difference* and Draw(in DrawData data) can work for both.
    In fact, it did in a previous version (without render graph). AI is just fucking stupid. Yes, you. You're a dumb machine.


```csharp

public struct DrawData
{
    
    //VertexBuffer <- same for 3D and ImGui - uses VertexAttribute. No reason to split.
    
    //IndexBuffer <- a fucking uint yeah? Why would I need specific GPU buffers in FrameHandler only for UI 
    //when they exist in DrawData? And worked perfectly fine, mind you.
    
    
    
}

```