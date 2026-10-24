using Eidolon.Vulkan;
using EidolonEngine;
using EidolonCore.Assets;
using EidolonCore.ECS;
using EidolonCore.Nexus;
using EidolonCore.Simulation;


namespace Eidolon.Editor;

public static class EidolonEditor
{
    public static void Main()
    {
        Debug.Log("Starting from EidolonEditor", VALIDATION_LAYERS.INFO);

        var world = new World();
        var assets = new AssetLibrary();

        var assetRoot = Path.Combine(AppContext.BaseDirectory, "Assets");
        var discovered = assets.Discover(assetRoot);
        Debug.Log($"Asset library: {discovered} mesh asset(s) under '{assetRoot}'", VALIDATION_LAYERS.INFO);

        DefaultScene.Populate(world, assets);

        // The session owns the authored world and, later, the runtime copy used by play mode.
        var session = new SceneSession(world);
        session.AddSystem(new SpinSystem());
        session.AddSystem(new XpbdWorldSystem());

        var initialGraph = BuildInitialGraph(1920, 1080);
        VulkanHost.Run(session, assets, initialGraph);
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
        
        var gameViewDepth = graph.CreateImage("GameViewDepth", GraphImageDescription.Create(
            ImageFormat.D32Float,
            FlagImageUsage.DepthStencilAttachment));

        var editorColor = graph.CreateImage("EditorView", GraphImageDescription.Create(
            ImageFormat.Rgba16Float,
            FlagImageUsage.ColorAttachment | FlagImageUsage.Sampled));

        var editorDepth = graph.CreateImage("EditorViewDepth", GraphImageDescription.Create(
            ImageFormat.D32Float,
            FlagImageUsage.DepthStencilAttachment));
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
            .Write(gameView)
            .Write(gameViewDepth);
        
        // The editor viewport: the same scene, rendered from the editor camera.
        graph.AddPass("EditorView", RenderPassType.EditorView)
            .Read(postColor)
            .Write(editorColor)
            .Write(editorDepth);

        graph.AddPass("ImGui", RenderPassType.UI).Read(gameView).Read(editorColor).Write(backbuffer);
        graph.AddPass("Present", RenderPassType.Present)
            .Read(backbuffer);
        
        
        return graph.Compile(new FrameDescription
        {
            Width = width,
            Height = height,
        });
    }
}