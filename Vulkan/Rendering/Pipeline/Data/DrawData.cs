using System.Numerics;
using EidolonCore.Rendering.Interfaces;

namespace Eidolon.Vulkan;

internal struct DrawData
{
    public PipelineData PipelineData;
    public IRenderTarget? RenderTarget;
    public DrawSubmission[] Submissions;
    
    public Matrix4x4? ModelMatrix;
}



