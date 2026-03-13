using System.Numerics;
using Eidolon.Vulkan;
using EidolonCore.ECS;
using Silk.NET.Vulkan;

namespace EidolonEngine;

internal class SceneRenderer
{
    private VulkanMaster _master;
    
    private static List<Scene> _scenes;

    private static Scene _currentScene;
    
    private MeshRenderer[] _meshRenderers;
    private PipelineData[] _pipelineData;
     
    public DrawSubmission[] CurrentSubmissions { get; private set; } = Array.Empty<DrawSubmission>();

    public SceneRenderer(VulkanMaster master)
    {
        _master = master;
        
        BuildPipeline();
        
    }
    
    // Create and add geometry to scenes

    // Build submissions

    // PipelineKeys are individual depending on shaders

    public void NewFrame()
    {
        // Debug.Log("Running NewFrame in SceneRenderer. Nothing called here yet.", VALIDATION_LAYERS.INFO);
        // foreach (MeshRenderer m in _meshRenderers)
        // {
        //     m.NewFrame();
        // }

        //
        //
        // for(int i = 0; i < _pipelineData.Length ; i++)
        // {
        //     var submission = BuildDrawSubmissions(_pipelineData[i], _master.DescriptorFactory.GetDescriptorSet(0));
        //     CurrentSubmissions[i] = submission;
        // }
    }

    private DrawSubmission BuildDrawSubmissions(PipelineData pipelineData,  DescriptorSet descriptorSet)
    {
        var submission = new DrawSubmission();
        foreach (MeshRenderer m in _meshRenderers)
        {
             m._drawSubmission = new DrawSubmission 
             {
                PassType = RenderPassType.GameView, //NOTE: ensures that output is rendered in Game View
                PipelineData = pipelineData,
                DescriptorSet = descriptorSet,
                VertexBuffer = default,
                VertexOffset = 0,
                IndexBuffer = default,
                IndexOffset = 0,
                IndexType = IndexType.Uint16,
                VertexCount = (uint)m.Mesh.Vertices.Length,
                IndexCount = (uint)m.Mesh.Indices.Length,
                InstanceCount = 1,
                FirstVertex = 0,
                FirstIndex = 0,
                VertexBase = 0,
                ScissorPolicy = SubmissionScissorPolicy.PassDefault,
                Scissor = default,
                ViewportPolicy = SubmissionViewportPolicy.PassDefault,
                Viewport = default,
                PushConstants = PushConstantPayload.Empty,
                ModelMatrix = Matrix4x4.Identity,
            };
            submission = m._drawSubmission;
             //Isn't the only thing here that needs updating ModelMatrix? 
             // I still don't get why most of this stuff needs per-frame updates.
        }

        return submission;
    }

    private RenderPassKey CreateRenderPassKey()
    {
        return new RenderPassKey
        {
            ColorFormat = Format.R16G16B16A16Sfloat,
            DepthFormat = Format.D32Sfloat,
            HasDepth = true,
            HasAlpha = true,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ColorAttachmentOptimal,
            InitialDepthLayout = ImageLayout.Undefined,
            FinalDepthLayout = ImageLayout.DepthStencilAttachmentOptimal,
        };
    }
    
    private void BuildPipeline()
    {
        var renderPass = _master.RenderPassFactory.CreateRenderPass(CreateRenderPassKey());

        //NOTE: TEMP

        var shader = new Shader();
        
        var pipelineKey = new PipelineKey
        {
            VertexShaderPath = shader.VertexPath,
            FragmentShaderPath = shader.FragPath,
            RenderPass = renderPass,
            Layout = _master.DescriptorFactory.Layout,
            VertexFormat = new VertexFormat
            {
                Stride = 0,
                Attributes = Array.Empty<VertexAttribute>()
            },
            Topology = PrimitiveTopology.TriangleList, //Geometry
            CullMode = CullModeBits.None, //Shader?
            FrontFace = FrontFace.CounterClockwise, //Shader
            // Shader?
            HasDepth = true, 
            DepthTestEnable = true, 
            DepthWriteEnable = false,
            EnableBlending = false,
            BlendState = BlendState.NoBlending,
        };

        // for (int i = 0; i < _pipelineData.Length; i++)
        // {
        //     _pipelineData[i] = _master.PipelineFactory.GetOrCreate(pipelineKey);
        // }

    }

    public static void Register(Component component)
    {
        _currentScene.Add(component);
    }
}



internal class MeshRenderer : Component
{
    
    public Mesh Mesh;
    public DrawSubmission _drawSubmission;

    public override void NewFrame()
    {
        
        
    }
}