using System.Numerics;
using System.Runtime.InteropServices;
using Eidolon.Vulkan;
using Silk.NET.Vulkan;

namespace EidolonEngine;

/// <summary>
/// Produces game-view draw submissions. Render-pass/framebuffer ownership stays in render-graph execution.
/// </summary>
internal sealed class GameViewRenderer
{
    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();
    public void NewFrame()
    {
        CurrentSubmissions = Array.Empty<DrawSubmission>();
        Debug.Log("Running NewFrame in GameViewRenderer", VALIDATION_LAYERS.INFO);
    }

    public void SetSubmissions(params DrawSubmission[] submissions)
    {
        CurrentSubmissions = submissions ?? Array.Empty<DrawSubmission>();
    }

    

    public void Dispose()
    {
        // TODO release managed resources here
    }
}