using System.Runtime.InteropServices;

namespace EidolonCore.Rendering;

public class PassBuilder
{
    private readonly RenderGraphBuilder _graph;
    private readonly int _passIndex;

    internal PassBuilder(RenderGraphBuilder graph, int passIndex)
    {
        _graph = graph;
        _passIndex = passIndex;
    }

    public PassBuilder Read(ResourceHandle resource)
    {
        _graph.RegisterRead(_passIndex, resource);
        return this;
    }

    public PassBuilder Write(ResourceHandle resource)
    {
        _graph.RegisterWrite(_passIndex, resource);
        return this;
    }

    public PassBuilder ReadWrite(ResourceHandle resource)
    {
        _graph.RegisterRead(_passIndex, resource);
        return this;
    }
}