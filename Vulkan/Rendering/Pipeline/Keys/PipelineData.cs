using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;
/// <summary>
/// This is the collection of data passed out from PipelineManager.
/// It is later used in RenderContext, Vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, pipeline.VkPipeline);
/// </summary>
internal record struct PipelineData
{
    public RenderPass RenderPass { get; set; }
    public Silk.NET.Vulkan.Pipeline VkPipeline { get; set; }
    public PipelineLayout VkLayout { get; set; }
    public DescriptorSetLayout DescriptorSetLayout { get; set; }
    
    public bool HasDepth;
    
    public bool IsValid => RenderPass.Handle != 0 && VkPipeline.Handle != 0 && VkLayout.Handle != 0;


}