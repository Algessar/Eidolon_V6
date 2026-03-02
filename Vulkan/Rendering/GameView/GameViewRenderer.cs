using System.Numerics;
using System.Runtime.InteropServices;
using Eidolon.Vulkan;
using Silk.NET.Vulkan;

namespace EidolonEngine;

internal class GameViewRenderer : IDisposable
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