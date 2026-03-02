using Eidolon.Vulkan;
using EidolonCore.Rendering;
using EidolonEngine;

namespace Eidolon.Editor;

public static class EidolonEditor
{
    public static void Main()
    {
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

        var gameView = graph.CreateImage("GameView", GraphImageDescription.Create(
            ImageFormat.Rgba16Float, //NOTE: Should this not be Rgba32Float?
            FlagImageUsage.ColorAttachment | FlagImageUsage.Sampled));

        var backbuffer = graph.ImportImage("Backbuffer",
            GraphImageDescription.Create(ImageFormat.Bgra8Unorm,
                FlagImageUsage.ColorAttachment | FlagImageUsage.Present));

        graph.AddPass("Geometry", RenderPassType.Geometry)
            .Write(sceneColor);

        graph.AddPass("PostProcess", RenderPassType.PostProcess)
            .Read(sceneColor)
            .Write(postColor);
        
        graph.AddPass("GameView", RenderPassType.GameView) //Codex suggests GameView, which means adding that to RenderPassType
                                                          //and changing PassExecutionKey to account for that.
            .Read(postColor)
            .Write(gameView);

        graph.AddPass("ImGui", RenderPassType.UI).Read(gameView).Write(backbuffer);
        graph.AddPass("Present", RenderPassType.Present)
            .Read(backbuffer);
        
        
        return graph.Compile(new FrameDescription
        {
            Width = width,
            Height = height,
        });
    }
}