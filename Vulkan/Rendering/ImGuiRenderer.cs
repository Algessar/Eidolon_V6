
using System.Numerics;
using EidolonCore.Math;
using ImGuiNET;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

public sealed class ImGuiRenderer : IDisposable
{
    // // Lifetime (created once)
    // private PipelineData _pipeline;
    // private DescriptorSet _fontSet;
    // private GpuImage _fontImage; // From ImageData?
    //
    // // Per-frame CPU state
    // private DrawData _drawData; // Holds Vertex/IndexBuffers

    
    public int LastVertexCount { get; private set; }
    public int LastIndexCount { get; private set; }
    public int LastCommandListCount { get; private set; }
    
    [Header("Debug")]
    bool _showDemoWindow = true;

    public unsafe void Initialize()
    {
        
        Debug.Log("Creating ImGuiRenderer", VALIDATION_LAYERS.INFO);
        var io = ImGui.GetIO();
        if (io.Fonts.Fonts.Size == 0)
        {
            io.Fonts.AddFontDefault();
        }
        
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out var width, out var height, out var bytesPerPixel);
        if (pixels == null || width <= 0 || height <= 0 || bytesPerPixel <= 0)
        {
            throw new InvalidOperationException("Failed to load font texture.");
        }
        
        io.Fonts.ClearTexData();
        
        Debug.Log("ImGuiRenderer initialized", VALIDATION_LAYERS.INFO);
    }
    
    public void NewFrame(float delta, Vector2 size)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = size;
        io.DeltaTime = Mathf.Max(1f / 1000f, delta);
        ImGui.NewFrame();
    }

    public void BuildUI()
    {
        ImGui.Begin("Eidolon / Render Graph");
        ImGui.Text("ImGui is integrated in the render graph frame lifecycle!");
        
        ImGui.Text($"CmdListst count: {LastCommandListCount}, Vtx: {LastVertexCount}, Idx: {LastIndexCount}");
        ImGui.Checkbox("Show Demo Window", ref _showDemoWindow);
        ImGui.End();
        
        if(_showDemoWindow)
        {
            ImGui.ShowDemoWindow(ref _showDemoWindow);
        }
    }
    public void FinalizeFrame()
    {
        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        LastVertexCount = !drawData.Valid ? 0 : drawData.TotalVtxCount;
        LastIndexCount = !drawData.Valid ? 0 :drawData.TotalIdxCount;
        LastCommandListCount =!drawData.Valid ? 0 : drawData.CmdListsCount;
    }
    
    public void AddToGraph(RenderGraphBuilder builder, ResourceHandle sourceColor, ResourceHandle target)
    {
        builder.AddPass("Imgui", RenderPassType.Ui)
            .Read(sourceColor)
            .Write(target);
    }
    
    public void Upload()
    {
        
    }



    public void Dispose()
    {
        
    }


}

// internal struct ImGuiDrawData
// {
//     public GpuBuffer VertexBuffer;
//     public GpuBuffer IndexBuffer;
//     
//     public uint VertexCount;
//     public uint IndexCount;
//     
//     public bool HasIndices;
//     
//     
// }

