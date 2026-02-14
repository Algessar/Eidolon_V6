using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

//TODO: rename to GraphBarrierHandler
internal sealed unsafe class GraphBarrierPlanner
{
    private readonly VulkanMaster _master;
    private readonly GraphResourceRuntimeManager _runtimeManager;
    private readonly bool _logRenderGraph;

    public GraphBarrierPlanner(VulkanMaster master, GraphResourceRuntimeManager runtimeManager, bool logRenderGraph)
    {
        _master = master;
        _runtimeManager = runtimeManager;
        _logRenderGraph = logRenderGraph;
    }

    public void TransitionLayouts(CommandBuffer cmd, in CompiledPass pass)
    {
        foreach (var read in pass.Reads)
        {
            if (!_runtimeManager.TryGetResource(read.Handle, out var resource))
                continue;

            if (!_runtimeManager.TryGetRuntime(read.Handle, out var runtime))
                continue;

            if (runtime.Imported)
                continue;

            EmitShaderReadBarrier(cmd, resource, ref runtime);
            _runtimeManager.SetRuntime(read.Handle, runtime);
        }

        foreach (var write in pass.Writes)
        {
            if (!_runtimeManager.TryGetResource(write.Handle, out var resource))
                continue;

            if (!_runtimeManager.TryGetRuntime(write.Handle, out var runtime))
                continue;

            if (runtime.Imported)
            {
                if (pass.Type == RenderPassType.Present)
                    EmitPresentBarrier(cmd, resource, ref runtime);
                else
                    EmitImportedColorAttachmentBarrier(cmd, resource, ref runtime);
            }
            else
            {
                EmitColorAttachmentBarrier(cmd, resource, ref runtime);
            }

            _runtimeManager.SetRuntime(write.Handle, runtime);
        }
    }

    private void EmitColorAttachmentBarrier(CommandBuffer cmd, in CompiledResource resource, ref GraphImageRuntime runtime)
    {
        if (runtime.CurrentLayout == ImageLayout.PresentSrcKhr)
            return;

        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = runtime.CurrentLayout,
            NewLayout = ImageLayout.ColorAttachmentOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            SrcAccessMask = runtime.CurrentLayout == ImageLayout.ColorAttachmentOptimal ? AccessFlags.ColorAttachmentWriteBit : 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
            Image = runtime.Image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = GraphResourceRuntimeManager.ResolveAspectFlags(resource.Description.Usage, resource.Description.Format),
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        _master.Vk.CmdPipelineBarrier(
            cmd,
            runtime.CurrentLayout == ImageLayout.ColorAttachmentOptimal
                ? PipelineStageFlags.ColorAttachmentOutputBit
                : PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier);

        if (_logRenderGraph)
            Debug.Log($"[RG] Barrier: {resource.Name} {barrier.OldLayout} -> ColorAttachmentOptimal");

        runtime.CurrentLayout = ImageLayout.ColorAttachmentOptimal;
    }

    private void EmitShaderReadBarrier(CommandBuffer cmd, in CompiledResource resource, ref GraphImageRuntime runtime)
    {
        if (runtime.CurrentLayout != ImageLayout.ColorAttachmentOptimal)
            return;

        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = runtime.CurrentLayout,
            NewLayout = ImageLayout.ShaderReadOnlyOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = AccessFlags.ShaderReadBit,
            Image = runtime.Image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = GraphResourceRuntimeManager.ResolveAspectFlags(resource.Description.Usage, resource.Description.Format),
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        _master.Vk.CmdPipelineBarrier(
            cmd,
            PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.FragmentShaderBit,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier);

        if (_logRenderGraph)
            Debug.Log($"[RG] Barrier: {resource.Name} ColorAttachmentOptimal -> ShaderReadOnlyOptimal");

        runtime.CurrentLayout = ImageLayout.ShaderReadOnlyOptimal;
    }

    private void EmitPresentBarrier(CommandBuffer cmd, in CompiledResource resource, ref GraphImageRuntime runtime)
    {
        if (runtime.CurrentLayout == ImageLayout.PresentSrcKhr)
            return;

        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = runtime.CurrentLayout,
            NewLayout = ImageLayout.PresentSrcKhr,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            SrcAccessMask = runtime.CurrentLayout == ImageLayout.ColorAttachmentOptimal ? AccessFlags.ColorAttachmentWriteBit : 0,
            DstAccessMask = 0,
            Image = runtime.Image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        _master.Vk.CmdPipelineBarrier(
            cmd,
            runtime.CurrentLayout == ImageLayout.ColorAttachmentOptimal
                ? PipelineStageFlags.ColorAttachmentOutputBit
                : PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.AllCommandsBit,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier);

        if (_logRenderGraph)
            Debug.Log($"[RG] Barrier: {resource.Name} ColorAttachmentOptimal -> PresentSrcKHR");

        runtime.CurrentLayout = ImageLayout.PresentSrcKhr;
    }

    private void EmitImportedColorAttachmentBarrier(CommandBuffer cmd, in CompiledResource resource, ref GraphImageRuntime runtime)
    {
        if (runtime.CurrentLayout == ImageLayout.ColorAttachmentOptimal)
            return;

        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = runtime.CurrentLayout,
            NewLayout = ImageLayout.ColorAttachmentOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            SrcAccessMask = runtime.CurrentLayout == ImageLayout.PresentSrcKhr ? AccessFlags.MemoryReadBit : 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
            Image = runtime.Image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        _master.Vk.CmdPipelineBarrier(
            cmd,
            runtime.CurrentLayout == ImageLayout.PresentSrcKhr
                ? PipelineStageFlags.ColorAttachmentOutputBit
                : PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier);

        if (_logRenderGraph)
            Debug.Log($"[RG] Barrier: {resource.Name} {barrier.OldLayout} -> ColorAttachmentOptimal (imported)");

        runtime.CurrentLayout = ImageLayout.ColorAttachmentOptimal;
    }
}