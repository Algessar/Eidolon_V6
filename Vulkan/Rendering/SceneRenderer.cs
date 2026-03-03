using Eidolon.Vulkan;

namespace EidolonEngine;

internal class SceneRenderer
{
    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();
    
    // Create and add geometry to scenes
    
    // Build submissions
    
    // PipelineKeys are individual depending on shaders

    public void NewFrame()
    {
        Debug.Log("Running NewFrame in SceneRenderer. Nothing called here yet.", VALIDATION_LAYERS.INFO);
    }

    public void BuildDrawSubmissions()
    {
        
    }
}