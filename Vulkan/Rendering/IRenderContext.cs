namespace Eidolon.Vulkan.Rendering;

internal interface IRenderContext
{

    #region Resources
    bool _framebufferResized  { get; set; }

    #endregion

    #region DrawRegion
    
    void Initialize();

    //NOTE: THIS STRUCTURE MAY CHANGE
    void Draw(in IDrawData data);
        
    void BeginFrame(in IDrawData data);
        
    void EndFrame(in IDrawData data);


    #endregion

    #region ResourceCreation
    void CreateResources(); //Calls from managers!
        

    #endregion
}

public interface IDrawData
{
    
}