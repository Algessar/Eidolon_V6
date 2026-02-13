namespace Eidolon.Vulkan;

public static class RenderGraphUiExtensions
{
    public static void AddImGuiPass(this RenderGraphBuilder builder, ResourceHandle sourceColor, ResourceHandle target)
    {
        builder.AddPass("ImGui", RenderPassType.Ui)
            .Read(sourceColor)
            .Write(target);
    }
}