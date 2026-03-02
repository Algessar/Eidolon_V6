using System.Numerics;
using EidolonCore.Rendering.Interfaces;

namespace Eidolon.Vulkan;

internal struct DrawData
{
    public IRenderTarget? RenderTarget;
    
    public PipelineData PipelineData;
    public DrawSubmission[] Submissions;
}



