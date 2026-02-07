namespace EidolonCore.Rendering;

public interface IRenderer : IDisposable
{
    void Draw();
    void OnRender();
    void OnUpdate();
    void OnClose();
    void OnResize();

    void RegisterWindowCallbacks();
    void CreatePipelineKey();
    void CreateRenderPassKey();
    
    
    void CreateSwapchain();
    
}