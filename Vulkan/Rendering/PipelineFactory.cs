using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal unsafe class PipelineFactory
{
    VulkanMaster _master;
    
    private static readonly byte[] _mainNameArray = "main\0"u8.ToArray();
    private readonly Dictionary<PipelineKey, PipelineData> _cache = new();
    
    public PipelineFactory(VulkanMaster master)
    {
        _master = master;
    }

    public PipelineData GetOrCreate(PipelineKey key)
    {
        if (_cache.TryGetValue(key, out var existing))
        {
            return existing;
        }
        
        var vs = _master.ShaderManager.Load(key.VertexShaderPath);
        var fs = _master.ShaderManager.Load(key.FragmentShaderPath);

        key.Vert = vs;
        key.Frag = fs;

        Debug.Log($"Key VertexShaderPath: {key.VertexShaderPath}, FragmentShaderPath: {key.FragmentShaderPath}");
        Debug.Log($"Key.Vert.Handle {key.Vert.Handle}, Key.Frag.Handle {key.Frag.Handle}");
        Debug.Log($"Shader modules loaded :: vert {vs.Handle}, frag {fs.Handle}");
        fixed (byte* pMain = _mainNameArray)
        {
            
#region Vertex Input
            PipelineShaderStageCreateInfo* stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vs,
                PName = pMain
            };
            stages[1] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fs,
                PName = pMain
            };
            
            //NOTE: For when I want to make Compute Shaders?
            // stages[2] = new PipelineShaderStageCreateInfo
            // {
            //     SType = StructureType.PipelineShaderStageCreateInfo,
            //     Stage = ShaderStageFlags.ComputeBit,
            //     Module = cs.Module,
            //     PName = pMain
            // };
            
            var vertexAttributeCount = (uint)(key.VertexFormat.Attributes?.Length ?? 0);
            var attributeScratchCount = vertexAttributeCount > 0 ? (int)vertexAttributeCount : 1;
            var attributeDescriptions = stackalloc VertexInputAttributeDescription[attributeScratchCount];
            for (var i = 0; i < vertexAttributeCount; i++)
            {
                var attr = key.VertexFormat.Attributes[i];
                attributeDescriptions[i] = new VertexInputAttributeDescription
                {
                    Location = attr.Location,
                    Binding = 0,
                    Format = attr.Format,
                    Offset = attr.Offset,
                };
            }

            VertexInputAttributeDescription* attributeDescriptionsPtr = vertexAttributeCount > 0
                ? attributeDescriptions
                : null;
            
            var bindingDescription = new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = key.VertexFormat.Stride, //(uint)Marshal.SizeOf<key.VertexFormat.Stride>(), // Use full Vertex size
                InputRate = VertexInputRate.Vertex
            };
            
            VertexInputBindingDescription* bindingDescriptionPtr = vertexAttributeCount > 0
                ? &bindingDescription
                : null;

            
            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = vertexAttributeCount > 0 ? 1u : 0u,
                PVertexBindingDescriptions = bindingDescriptionPtr,
                VertexAttributeDescriptionCount = vertexAttributeCount,
                PVertexAttributeDescriptions = attributeDescriptionsPtr
            };
#endregion
            var rasterizer = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                PolygonMode = PolygonMode.Fill,
                CullMode = key.CullMode == CullModeBits.Back ? CullModeFlags.BackBit :
                    key.CullMode == CullModeBits.Front ? CullModeFlags.FrontBit :
                    CullModeFlags.None,
                FrontFace = key.FrontFace,
                LineWidth = 1.0f
            };
            
            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = key.Topology
            };
            var multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.Count1Bit
            };
            
            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                BlendEnable = key.EnableBlending,
                SrcColorBlendFactor = key.BlendState.SrcColorBlendFactor,
                DstColorBlendFactor = key.BlendState.DstColorBlendFactor,
                ColorBlendOp = key.BlendState.ColorBlendOp,
                SrcAlphaBlendFactor = key.BlendState.SrcAlphaBlendFactor,
                DstAlphaBlendFactor = key.BlendState.DstAlphaBlendFactor,
                AlphaBlendOp = key.BlendState.AlphaBlendOp,
                ColorWriteMask = key.BlendState.ColorWriteMask
            };
            var colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment
            };
            
            var depthStencil = new PipelineDepthStencilStateCreateInfo
            {
                
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = key.DepthTestEnable,
                DepthWriteEnable = key.DepthWriteEnable,
                DepthCompareOp = CompareOp.Less,
                DepthBoundsTestEnable = false,
                StencilTestEnable = false,
                
            };
            
            // Dynamic states: viewport & scissor
            DynamicState* dynamicStates = stackalloc DynamicState[2];
            dynamicStates[0] = DynamicState.Viewport;
            dynamicStates[1] = DynamicState.Scissor;
            
            var dynamicStateInfo = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates
            };

            var descriptorSetLayout = key.Layout;

            var pushConstantRange = new PushConstantRange
            {
                StageFlags = ShaderStageFlags.VertexBit,
                Offset = 0,
                Size = (uint)Marshal.SizeOf<System.Numerics.Matrix4x4>()
            };
            Debug.Log($"Push constant range: {pushConstantRange.Size}");
            
            var layoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = &descriptorSetLayout,
                PushConstantRangeCount = 1,
                PPushConstantRanges = &pushConstantRange
            };
            
            if (_master.Vk.CreatePipelineLayout(_master.VulkanDevice.Device, &layoutInfo, null, out PipelineLayout layout) != Result.Success)
                throw new Exception("Failed to create pipeline layout!");

            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
                PViewports = null,
                PScissors = null
            };
            
            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PColorBlendState = &colorBlending,
                PDepthStencilState = &depthStencil,
                Layout = layout,
                RenderPass = key.RenderPass,
                Subpass = 0,
                PViewportState = &viewportState,
                PDynamicState = &dynamicStateInfo
            };
            
            if (_master.Vk.CreateGraphicsPipelines(_master.VulkanDevice.Device, default, 1, in pipelineInfo, null,
                    out Pipeline vkPipeline) != Result.Success)
                throw new Exception("Failed to create graphics pipeline!");

            var result = new PipelineData
            {
                RenderPass =  key.RenderPass,
                VkPipeline = vkPipeline,
                VkLayout = layout,
                DescriptorSetLayout = descriptorSetLayout,
                HasDepth = key.HasDepth,
                
            };
            
            _cache.Add(key, result);
            return result;
        }
    }

    public void Dispose()
    {
        Debug.Log("Disposing PipelineHandler", VALIDATION_LAYERS.WARNING);
        _master.Vk.DeviceWaitIdle(_master.VulkanDevice.Device);
        
        foreach (var pipelineData in _cache.Values)
        {
            if (pipelineData.IsValid)
            {
                if (pipelineData.VkPipeline.Handle != 0)
                {
                    _master.Vk.DestroyPipeline(_master.VulkanDevice.Device, pipelineData.VkPipeline, null);
                }
                if (pipelineData.VkLayout.Handle != 0)
                {
                    _master.Vk.DestroyPipelineLayout(_master.VulkanDevice.Device, pipelineData.VkLayout, null);
                }
            }

        }
        _cache.Clear();
        Debug.Log("PipelineHandler disposed", VALIDATION_LAYERS.SUCCESS);
    }
}

// internal record struct PipelineData
// {
//     public RenderPass RenderPass { get; set; }
//     public Pipeline VkPipeline { get; set; }
//     public PipelineLayout VkLayout { get; set; }
//     public DescriptorSetLayout DescriptorSetLayout { get; set; }
//     
//     public bool HasDepth;
//     
//     public bool IsValid => RenderPass.Handle != 0 && VkPipeline.Handle != 0 && VkLayout.Handle != 0;
//
// }