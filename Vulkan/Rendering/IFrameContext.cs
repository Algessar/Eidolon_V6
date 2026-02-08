namespace Eidolon.Vulkan.Rendering;

internal interface IFrameContext
{

    VulkanMaster _master { get; } 
    
    #region Resources
    bool _framebufferResized  { get; set; }

    #endregion

    #region DrawRegion
    
    void Initialize();

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