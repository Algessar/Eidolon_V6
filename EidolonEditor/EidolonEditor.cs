using Eidolon.Vulkan;
using EidolonCore.Rendering;

namespace Eidolon.Editor;

public class EidolonEditor
{
    public static void Main()
    {
        // ShaderCompiler.CompileShaders(@"G:\Coding\Eidolon_V6\Vulkan\Rendering\Shaders");

        Debug.Log("Starting from EidolonEditor", VALIDATION_LAYERS.INFO);
        var initialGraph = BuildInitialGraph(1920, 1080);
        VulkanHost.Run(initialGraph);
        Debug.Log("EidolonEditor shutting down!", VALIDATION_LAYERS.SUCCESS);
    }

    private static CompiledRenderGraph BuildInitialGraph(uint width, uint height)
    {
        var graph = new RenderGraphBuilder();

        var sceneColor = graph.CreateImage("SceneColor",
            GraphImageDescription.Create(ImageFormat.Rgba16Float,
                FlagImageUsage.ColorAttachment | FlagImageUsage.Sampled));

        var postColor = graph.CreateImage("PostColor",
            GraphImageDescription.Create(ImageFormat.Rgba16Float,
                FlagImageUsage.ColorAttachment | FlagImageUsage.Sampled));

        var backbuffer = graph.ImportImage("Backbuffer",
            GraphImageDescription.Create(ImageFormat.Bgra8Unorm,
                FlagImageUsage.ColorAttachment | FlagImageUsage.Present));

        graph.AddPass("Geometry", RenderPassType.Geometry)
            .Write(sceneColor);
        
        graph.AddPass("PostProcess", RenderPassType.PostProcess)
            .Read(sceneColor)
            .Write(postColor);

        var imgui = new ImGuiRenderer();
        imgui.AddToGraph(graph, sceneColor, backbuffer);
        
        graph.AddPass("Present", RenderPassType.Present)
            .Read(postColor)
            .Write(backbuffer);
        
        
        return graph.Compile(new FrameDescription
        {
            Width = width,
            Height = height,
        });
    }
}