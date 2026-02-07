using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace Eidolon.Vulkan;

internal class VulkanManager
{
    #region Vulkan Core

    public Vk Vk => Vk.GetApi();
    private IWindow _window;
    public VulkanInstance VulkanInstance { get; set; }
    public VulkanDevice VulkanDevice { get; set; }
    
    public Surfaces Surfaces { get; set; }

    #endregion


    #region Managers

    public BufferFactory BufferFactory { get; set; }
    public DescriptorFactory DescriptorFactory { get; set; }
    public RenderPassFactory RenderPassFactory { get; set; }
    public CommandManager CommandManager { get; set; }

    #endregion

    public VulkanManager()
    {
        VulkanInstance = new VulkanInstance(this, _window);
        Surfaces = VulkanInstance.Surfaces;
        
        VulkanDevice = new VulkanDevice(this);
    }

    public void InitializeManagers()
    {
        VulkanInstance = new VulkanInstance(this, _window);
        
        VulkanDevice = new VulkanDevice(this);
        
        
    }
}