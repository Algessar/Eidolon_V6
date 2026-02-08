using EidolonCore.Rendering;

namespace Eidolon.Vulkan;

public interface IFrameContext
{
    internal interface IFrameContext
    {

        VulkanMaster _master { get; } 
    
        #region Resources
        bool _framebufferResized  { get; set; }

        #endregion

        #region DrawRegion
    
        void Initialize();

        void SetCompiledGraph(CompiledRenderGraph graph);

        void Draw(in DrawData data);
        
        void BeginFrame(in DrawData data);
        
        void EndFrame(in DrawData data);


        #endregion

        #region ResourceCreation
        void CreateResources();
        

        #endregion
    }

    public interface IDrawData
    {
    
    }
}