
using System.Numerics;
using Eidolon.Editor;
using EidolonCore.Math;
using ImGuiNET;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace Eidolon.Vulkan;

// INFO: UI pass = UI submission

internal sealed unsafe class ImGuiRenderer : IDisposable
{
    VulkanMaster _master;
    
    // // Lifetime (created once)
    private PipelineData _pipelineData;
    private DescriptorSetLayout _descriptorSetLayout;
    private DescriptorPool _descriptorPool; // Not sure what this is doing here. Shouldn't this be in DescriptorFactory?
    private DescriptorSet _descriptorSet;
    private Image _fontImage;
    private ImageView _fontImageView;
    private DeviceMemory _fontImageMemory;
    private Sampler _fontSampler;
    
    // Per-frame CPU state

    private readonly UiGeometryUploader _uiGeometryUploader;
    private ImGuiDrawData CurrentDrawData { get; set; } = ImGuiDrawData.Empty;
    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();

    
    public uint LastVertexCount { get; private set; }
    public uint LastIndexCount { get; private set; }
    public int LastCommandListCount { get; private set; }
    
    [Header("Debug")]
    bool _showDemoWindow = true;
    
    [Header("UI and Input")]
    EditorUI _editorUI;
    ImGuiInputManager _inputManager;


    public ImGuiRenderer(VulkanMaster master)
    {
        _master = master;
        _uiGeometryUploader = new UiGeometryUploader(master);
    }
    public void Initialize( RenderPass renderPass)
    {
        
        Debug.Log("Creating ImGuiRenderer", VALIDATION_LAYERS.INFO);
        var io = ImGui.GetIO();
        if (io.Fonts.Fonts.Size == 0)
        {
            io.Fonts.AddFontDefault();
        }
        
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out var width, out var height, out var bytesPerPixel);
        if (pixels == null || width <= 0 || height <= 0 || bytesPerPixel <= 0)
        {
            throw new InvalidOperationException("Failed to load font texture.");
        }
        
        io.Fonts.ClearTexData();

        CreateDescriptorResources();
        CreateFontAtlasTexture(pixels, width, height, bytesPerPixel);
        CreatePipeline(renderPass);

        _editorUI = new EditorUI(_master.GetWindow);
        _inputManager = new ImGuiInputManager(_master.GetWindow);
        
        Debug.Log("ImGuiRenderer initialized", VALIDATION_LAYERS.INFO);
    }
    
    public void NewFrame(float delta, Vector2 windowSize, Vector2 framebufferSize)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = windowSize;

        var safeWindowWidth = MathF.Max(1f, windowSize.X);
        var safeWindowHeight = MathF.Max(1f, windowSize.Y);
        io.DisplayFramebufferScale = new Vector2(
            framebufferSize.X / safeWindowWidth,
            framebufferSize.Y / safeWindowHeight);
        io.DeltaTime = MathF.Max(1f / 1000f, delta);
        _inputManager.UpdateInput(ImGui.GetIO());
        ImGui.NewFrame();
        
        _editorUI.Update();
        BuildUI();
        FinalizeFrame();
        
    }

    private void BuildUI()
    {
        ImGui.Begin("Eidolon / Render Graph");
        ImGui.Text("ImGui is integrated in the frame lifecycle.");
        ImGui.Text($"CmdLists: {LastCommandListCount}, Vtx: {LastVertexCount}, Idx: {LastIndexCount}");
        ImGui.Checkbox("Show ImGui Demo Window", ref _showDemoWindow);
        ImGui.End();

        // if (_showDemoWindow)
        // {
        //     ImGui.ShowDemoWindow(ref _showDemoWindow);
        // }
    }

    private void FinalizeFrame()
    {
        ImGui.Render();
        var drawData = ImGui.GetDrawData();

        LastVertexCount = !drawData.Valid ? 0 : (uint)drawData.TotalVtxCount;
        LastIndexCount = !drawData.Valid ? 0 : (uint)drawData.TotalIdxCount;
        LastCommandListCount = !drawData.Valid ? 0 : drawData.CmdListsCount;
        
        CurrentDrawData = ConvertDrawData(drawData);
    }

    private ImGuiDrawData ConvertDrawData(ImDrawDataPtr drawData)
    {
        if (!drawData.Valid || drawData.CmdListsCount == 0 || drawData.TotalVtxCount <= 0 || drawData.TotalIdxCount <= 0)
        {
            return ImGuiDrawData.Empty;
        }

        var vertices = new ImGuiVertex[drawData.TotalVtxCount];
        var indices = new ushort[drawData.TotalIdxCount];

        var totalCommandCount = 0;
        for (var listIndex = 0; listIndex < drawData.CmdListsCount; listIndex++)
        {
            var cmdList = drawData.CmdLists[listIndex];
            totalCommandCount += cmdList.CmdBuffer.Size;
        }

        var commands = new ImGuiDrawCommand[totalCommandCount];

        var vertexBase = 0;
        var indexBase = 0;
        var commandBase = 0;
        var displaySize = drawData.DisplaySize;
        var safeWidth = MathF.Max(1f, displaySize.X);
        var safeHeight = MathF.Max(1f, displaySize.Y);
        var displayPos = drawData.DisplayPos;

        for (var listIndex = 0; listIndex < drawData.CmdListsCount; listIndex++)
        {
            var cmdList = drawData.CmdLists[listIndex];

            for (var vertexIndex = 0; vertexIndex < cmdList.VtxBuffer.Size; vertexIndex++)
            {
                var vtx = cmdList.VtxBuffer[vertexIndex];
                var normalizedPos = new Vector2((vtx.pos.X - displayPos.X) / safeWidth, (vtx.pos.Y - displayPos.Y) / safeHeight);
                vertices[vertexBase + vertexIndex] = new ImGuiVertex(normalizedPos, vtx.uv, vtx.col);
            }

            for (var index = 0; index < cmdList.IdxBuffer.Size; index++)
            {
                indices[indexBase + index] = cmdList.IdxBuffer[index];
            }

            for (var commandIndex = 0; commandIndex < cmdList.CmdBuffer.Size; commandIndex++)
            {
                var cmd = cmdList.CmdBuffer[commandIndex];
                commands[commandBase + commandIndex] = new ImGuiDrawCommand(
                    cmd.ElemCount,
                    (uint)(indexBase + cmd.IdxOffset),
                    vertexBase + (int)cmd.VtxOffset,
                    new Vector4(cmd.ClipRect.X - displayPos.X, cmd.ClipRect.Y - displayPos.Y, cmd.ClipRect.Z - displayPos.X, cmd.ClipRect.W - displayPos.Y),
                    cmd.TextureId);
            }

            commandBase += cmdList.CmdBuffer.Size;
            vertexBase += cmdList.VtxBuffer.Size;
            indexBase += cmdList.IdxBuffer.Size;
        }

        return new ImGuiDrawData
        {
            Vertices = vertices,
            Indices = indices,
            Commands = commands,
            DisplaySize = drawData.DisplaySize,
            DisplayFramebufferScale = drawData.FramebufferScale,
        };
    }

    //TODO: This is wrong and should be handled by DescriptorFactory
    private void CreateDescriptorResources()
    {
        var layoutBinding = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            StageFlags = ShaderStageFlags.FragmentBit,
        };

        var layoutInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &layoutBinding
        };

        if (_master.Vk.CreateDescriptorSetLayout(_master.VulkanDevice.Device, &layoutInfo, null,
                out _descriptorSetLayout) != Result.Success)
        {
            throw new Exception("Failed to create ImGui descriptor set layout.");
            
        }
        
        var poolSizes = stackalloc DescriptorPoolSize[1];
        poolSizes[0] = new DescriptorPoolSize
        {
            Type = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1
        };

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 1,
            PPoolSizes = poolSizes,
            MaxSets = 1
        };
        
        if (_master.Vk.CreateDescriptorPool(_master.VulkanDevice.Device, &poolInfo, null, out _descriptorPool) != Result.Success)
        {
            throw new Exception("Failed to create ImGui descriptor pool.");
            
        }

        fixed(DescriptorSetLayout* descriptorSetLayoutPtr = &_descriptorSetLayout)
        {
            var descriptorSetAllocateInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = descriptorSetLayoutPtr
            };
            
            if (_master.Vk.AllocateDescriptorSets(_master.VulkanDevice.Device, &descriptorSetAllocateInfo, out _descriptorSet) != Result.Success)
            {
                throw new Exception("Failed to allocate ImGui descriptor set.");
            }
        }
        
        //TODO: upload ImGui font atlas to GPU and write CombinedImageSampler descriptor at binding 0.
    }
    
    private void UpdateFontDescriptorSet()
    {
        var imageInfo = new DescriptorImageInfo
        {
            Sampler = _fontSampler,
            ImageView = _fontImageView,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal
        };

        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _descriptorSet,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &imageInfo
        };
        
        _master.Vk.UpdateDescriptorSets(_master.VulkanDevice.Device, 1, &write, 0, null);
    }

    private void CreatePipeline(RenderPass renderPass)
    {
        var resolvedRenderPass = ResolveRenderPass(renderPass);

        if (renderPass.Handle == 0)
        {
            throw new Exception("ImGuiRenderer received null render pass; cannot create pipeline.");
        }
        var key = new PipelineKey
        {
            VertexShaderPath = "imgui.vert.spv",
            FragmentShaderPath = "imgui.frag.spv",
            RenderPass = resolvedRenderPass,
            Layout = _descriptorSetLayout,
            VertexFormat = new VertexFormat
            {
                Stride = (uint)sizeof(ImGuiVertex),
                Attributes =
                [
                    new VertexAttribute(0, Format.R32G32Sfloat, 0),
                    new VertexAttribute(1, Format.R32G32Sfloat, 8),
                    new VertexAttribute(2, Format.R8G8B8A8Unorm, 16),
                ]
            },
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullModeBits.None,
            FrontFace = FrontFace.CounterClockwise,
            HasDepth = false,
            DepthTestEnable = false,
            DepthWriteEnable = false,
            EnableBlending = true,
            BlendState = BlendState.AlphaBlending,
        };

        // _pipelineKey = key;
        _pipelineData = _master.PipelineFactory.GetOrCreate(key);
        
        if (_pipelineData.RenderPass.Handle == 0)
        {
            throw new Exception("ImGuiRenderer received null render pass; cannot create pipeline.");
        }
    }
    
    //NOTE: This seems wrong to me. Why is it a fallbackKey? Why would I even need that?
    private RenderPass ResolveRenderPass(RenderPass renderPass)
    {
        if (renderPass.Handle != 0)
        {
            Debug.Log($"ImGuiRenderer has a valid RenderPass {renderPass.Handle}; using it.", VALIDATION_LAYERS.WARNING);
            return renderPass;
        }

        var swapchain = _master.SwapchainHandler;
        var fallbackKey = new RenderPassKey
        {
            ColorFormat = swapchain.SwapchainImageFormat,
            DepthFormat = Format.D32Sfloat,
            HasDepth = false,
            HasAlpha = true,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr,
            InitialDepthLayout = ImageLayout.Undefined,
            FinalDepthLayout = ImageLayout.DepthStencilAttachmentOptimal,
        };

        return _master.RenderPassFactory.CreateRenderPass(fallbackKey);
    }
    
    private void CreateFontAtlasTexture(byte* pixels, int width, int height, int bytesPerPixel)
    {
        ulong uploadSize = (ulong)(width * height * bytesPerPixel);

        CreateStagingBuffer(uploadSize, out var stagingBuffer, out var stagingMemory);

        void* mapped;
        _master.Vk.MapMemory(_master.VulkanDevice.Device, stagingMemory, 0, uploadSize, 0, &mapped);
        global::System.Buffer.MemoryCopy(pixels, mapped, uploadSize, uploadSize);
        _master.Vk.UnmapMemory(_master.VulkanDevice.Device, stagingMemory);

        CreateFontImage((uint)width, (uint)height);
        CreateFontImageView();
        CreateFontSampler();

        var cmd = _master.CommandHandler.AllocateTransientCommandBuffer();

        _master.CommandHandler.TransitionImageLayout(cmd, _fontImage, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
        _master.CommandHandler.CopyBufferToImage(cmd, stagingBuffer, _fontImage, (uint)width, (uint)height);
        _master.CommandHandler.TransitionImageLayout(cmd, _fontImage, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

        _master.CommandHandler.EndSubmitAndFreeTransientCommandBuffer(cmd);

        _master.Vk.DestroyBuffer(_master.VulkanDevice.Device, stagingBuffer, null);
        _master.Vk.FreeMemory(_master.VulkanDevice.Device, stagingMemory, null);

        UpdateFontDescriptorSet();
    }

    private void CreateStagingBuffer(ulong size, out Buffer buffer, out DeviceMemory memory)
    {
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = BufferUsageFlags.TransferSrcBit,
            SharingMode = SharingMode.Exclusive
        };

        if (_master.Vk.CreateBuffer(_master.VulkanDevice.Device, &bufferInfo, null, out buffer) != Result.Success)
            throw new Exception("Failed to create ImGui staging buffer.");

        _master.Vk.GetBufferMemoryRequirements(_master.VulkanDevice.Device, buffer, out var memReqs);

        var alloc = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memReqs.Size,
            MemoryTypeIndex = _master.VulkanDevice.FindMemoryType(memReqs.MemoryTypeBits,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
        };

        if (_master.Vk.AllocateMemory(_master.VulkanDevice.Device, &alloc, null, out memory) != Result.Success)
            throw new Exception("Failed to allocate ImGui staging buffer memory.");

        _master.Vk.BindBufferMemory(_master.VulkanDevice.Device, buffer, memory, 0);
    }
    
    private void CreateFontImage(uint width, uint height)
    {
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(width, height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = Format.R8G8B8A8Unorm,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive
        };

        if (_master.Vk.CreateImage(_master.VulkanDevice.Device, &imageInfo, null, out _fontImage) != Result.Success)
            throw new Exception("Failed to create ImGui font image.");

        _master.Vk.GetImageMemoryRequirements(_master.VulkanDevice.Device, _fontImage, out var memReqs);

        var alloc = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memReqs.Size,
            MemoryTypeIndex = _master.VulkanDevice.FindMemoryType(memReqs.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit)
        };

        if (_master.Vk.AllocateMemory(_master.VulkanDevice.Device, &alloc, null, out _fontImageMemory) != Result.Success)
            throw new Exception("Failed to allocate ImGui font image memory.");

        _master.Vk.BindImageMemory(_master.VulkanDevice.Device, _fontImage, _fontImageMemory, 0);
    }
    
    private void CreateFontImageView()
    {
        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _fontImage,
            ViewType = ImageViewType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        if (_master.Vk.CreateImageView(_master.VulkanDevice.Device, &viewInfo, null, out _fontImageView) != Result.Success)
            throw new Exception("Failed to create ImGui font image view.");
    }
    
    private void CreateFontSampler()
    {
        var samplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            MinLod = 0f,
            MaxLod = 0f,
            BorderColor = BorderColor.IntOpaqueWhite,
            UnnormalizedCoordinates = false
        };

        if (_master.Vk.CreateSampler(_master.VulkanDevice.Device, &samplerInfo, null, out _fontSampler) != Result.Success)
            throw new Exception("Failed to create ImGui font sampler.");
    }
    
    public void BuildDrawSubmissions(uint currentFrame, uint maxFramesInFlight)
    {
        if (!CurrentDrawData.HasData)
        {
            CurrentSubmissions = Array.Empty<DrawSubmission>();
            return;
        }

        _uiGeometryUploader.EnsureCurrentFrameUiBuffers(CurrentDrawData, currentFrame, maxFramesInFlight);
        _uiGeometryUploader.UploadCurrentFrameUiData(CurrentDrawData, currentFrame, maxFramesInFlight);

        ref var vertexBuffer = ref _uiGeometryUploader.GetCurrentFrameVertexBuffer(currentFrame);
        ref var indexBuffer = ref _uiGeometryUploader.GetCurrentFrameIndexBuffer(currentFrame);

        var submissions = new List<DrawSubmission>(CurrentDrawData.Commands.Length);
        // var displayWidth = MathF.Max(1f, CurrentDrawData.DisplaySize.X);
        // var displayHeight = MathF.Max(1f, CurrentDrawData.DisplaySize.Y);
        var framebufferScale = CurrentDrawData.DisplayFramebufferScale;
        var displayWidth = MathF.Max(1f, CurrentDrawData.DisplaySize.X * framebufferScale.X);
        var displayHeight = MathF.Max(1f, CurrentDrawData.DisplaySize.Y * framebufferScale.Y);
        
        foreach (var drawCommand in CurrentDrawData.Commands)
        {
            if (drawCommand.ElementCount == 0)
                continue;

            var clipRect = drawCommand.ClipRect;

            var minX = Math.Clamp((int)(clipRect.X * framebufferScale.X), 0, (int)displayWidth);
            var minY = Math.Clamp((int)(clipRect.Y * framebufferScale.Y), 0, (int)displayHeight);
            var maxX = Math.Clamp((int)(clipRect.Z * framebufferScale.X), minX, (int)displayWidth);
            var maxY = Math.Clamp((int)(clipRect.W * framebufferScale.Y), minY, (int)displayHeight);
            if (maxX <= minX || maxY <= minY)
                continue;
            
            // Debug.Log($"[ImGui] scissor: ({minX},{minY})-({maxX},{maxY})  displaySize: {displayWidth}x{displayHeight}", VALIDATION_LAYERS.WARNING);

            submissions.Add(new DrawSubmission
            {
                PassType = RenderPassType.Ui,
                PipelineData = _pipelineData,
                DescriptorSet = _descriptorSet,
                Topology = PrimitiveTopology.TriangleList,
                VertexBuffer = vertexBuffer,
                VertexOffset = 0,
                IndexBuffer = indexBuffer,
                IndexOffset = 0,
                IndexType = IndexType.Uint16,
                VertexCount = (uint)CurrentDrawData.TotalVertexCount,
                IndexCount = drawCommand.ElementCount,
                InstanceCount = 1,
                FirstVertex = 0,
                FirstIndex = drawCommand.FirstIndex,
                VertexBase = drawCommand.VertexOffset,
                ScissorPolicy = SubmissionScissorPolicy.Explicit,
                Scissor = new Rect2D(new Offset2D(minX, minY), new Extent2D((uint)(maxX - minX), (uint)(maxY - minY))),
                ViewportPolicy = SubmissionViewportPolicy.PassDefault,
                Viewport = default,
                PushConstants = PushConstantPayload.Empty,
                
            });
        }

        CurrentSubmissions = submissions.ToArray();
    }

    private Matrix4x4 CalculateImGuiProjection()
    {
        var io = ImGui.GetIO();
        float width = io.DisplaySize.X;
        float height = io.DisplaySize.Y;
        
        // Standard orthographic projection for Vulkan (Y down, depth 0 to 1)
        // Maps pixel coordinates (0,0 at top-left) to Vulkan Normalized Device Coordinates
        // NDC: X = -1 (left), 1 (right). Y = -1 (top), 1 (bottom). Z = 0 (near), 1 (far).
        return new Matrix4x4(
            2.0f / width, 0.0f,           0.0f, 0.0f, //xyzw
            0.0f,         2.0f / height,  0.0f, 0.0f, //xyzw
            0.0f,         0.0f,           1.0f, 0.0f, // Depth range 0->1
            -1.0f,        -1.0f,          0.0f, 1.0f  // Translation
        );
    }
    
    public void Dispose()
    {
        _uiGeometryUploader.Dispose();
        if (_descriptorPool.Handle != 0)
        {
            _master.Vk.DestroyDescriptorPool(_master.VulkanDevice.Device, _descriptorPool, null);
            _descriptorPool = default;
        }

        if (_descriptorSetLayout.Handle != 0)
        {
            _master.Vk.DestroyDescriptorSetLayout(_master.VulkanDevice.Device, _descriptorSetLayout, null);
            _descriptorSetLayout = default;
        }
    }
}