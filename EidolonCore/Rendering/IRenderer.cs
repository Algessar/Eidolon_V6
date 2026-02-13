namespace EidolonCore.Rendering;

public interface IRenderer : IDisposable
{
    //Does this need to be Vulkan side?
    
    public void NewFrame();
    public void Build();

    public void UploadData(); //Sent to FrameHandler


}