namespace EidolonCore.Rendering;

public interface IRenderer : IDisposable
{

    void Initialize();
    public void FrameSetup();

    void Draw();
    
    void OnRender();
    
    void OnUpdate();
    
    void OnClose();
    
    void OnResize();

    void RegisterWindowCallbacks();
    
    void CreatePipelineKey();
    
    void CreateRenderPassKey();

    void CreateImageKey();
    
    void RecreateSwapchain();
    public void FrameCleanup();
    
}