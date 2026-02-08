# Render Graph (Core data model)

This folder contains a backend-agnostic render graph description and compiler.

## Main types

- `RenderGraphBuilder`: owns graph resources and pass declarations.
- `PassBuilder`: fluent pass setup (`Read`, `Write`, `ReadWrite`).
- `CompiledRenderGraph`: result of dependency analysis + pass ordering + resource lifetimes.

## Example

```csharp
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

graph.AddPass("Present", RenderPassType.Present)
    .Read(postColor)
    .Write(backbuffer);

var compiled = graph.Compile(new FrameDescription { Width = 1920, Height = 1080 });
```

The Vulkan backend can consume `compiled.Passes` and `compiled.Resources` to create barriers,
allocate transient resources, and execute in order.