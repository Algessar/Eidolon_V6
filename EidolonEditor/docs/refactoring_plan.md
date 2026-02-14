- Remove duplicated code
- Make sure anything that has a place in a manager or factory is in one
- All rendering should be as unified as possible
    - Both 3D and ImGui uses vertices to render : that means there is *no difference* and Draw(in DrawData data) can work for both.
    In fact, it did in a previous version (without render graph). AI is just fucking stupid. Yes, you. You're a dumb machine.
- 

```csharp

public struct DrawData
{    
    public PipelineData PipelineData;
  
    public IRenderTarget? RenderTarget;
  
    public GpuBuffer VertexBuffer; //  <- same for 3D and ImGui. It's just a wrapper for Buffer, flags and memory 
    public GpuBuffer IndexBuffer;  // <- same type 
    public uint VertexCount; // If ElementCount is a uint, and VertexCount is also a uint...
    public uint IndexCount;
    public bool HasIndices;
    public IndexType IndexType;
    public Matrix4x4? ModelMatrix;
    public int[] VertexOffsets { get; set; } // <- for ImGui, this could just be vbOffset = VertexOffsets[0] no?
    public int[] IndexOffsets { get; set; }
    
    public ImGuiDrawData ImGuiDrawData;
    
    //These two are stupid. There is already PipelineData. I can add DescriptorSet as a general field no?
    public PipelineData UiPipelineData; 
    public DescriptorSet UiDescriptorSet;
    //NOTE: I will keep it like this for now and refactor later.
}

```

```csharp
internal readonly record struct ImGuiDrawCommand(
    uint ElementCount, // <- needed for ImGui
    uint FirstIndex, // <- I guess needed?
    int VertexOffset, //<- Exists in DrawData
    Vector4 ClipRect, //<- fine I guess? Could be in DrawData?

//Which means that there are two things in this struct that is ImGui specific. Worth it?
   
```

```csharp
internal sealed class ImGuiDrawData
{
    public static readonly ImGuiDrawData Empty = new();

    public ImGuiVertex[] Vertices { get; init; } = Array.Empty<ImGuiVertex>(); //ImGuiVertex is superflous. 
    public ushort[] Indices { get; init; } = Array.Empty<ushort>(); 
    public ImGuiDrawCommand[] Commands { get; init; } = Array.Empty<ImGuiDrawCommand>();
    public Vector2 DisplaySize { get; init; }

    public int TotalVertexCount => Vertices.Length;
    public int TotalIndexCount => Indices.Length;
    public bool HasData => Vertices.Length > 0 && Indices.Length > 0 && Commands.Length > 0;
}
```