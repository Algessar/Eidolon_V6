using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal unsafe class GameRenderer(VulkanManager vulkanManager) : IRenderer
{
    public void Dispose()
    {
        // TODO release managed resources here
    }

    public Framebuffer Framebuffer { get; }
    public GpuBuffer GpuBuffer { get; }
    public GpuImage GpuImage { get; }
    public SwapchainHandler SwapchainHandler { get; }
    public PipelineFactory PipelineFactory { get; }
    
    public void Initialize()
    {
        throw new NotImplementedException();
    }

    public void FrameSetup()
    {
        throw new NotImplementedException();
    }

    public void FrameCleanup()
    {
        throw new NotImplementedException();
    }

    public void RecreateSwapchain()
    {
        throw new NotImplementedException();
    }
    
    
    private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
        DebugUtilsMessageTypeFlagsEXT messageTypes,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData)
    {
        string message = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage) ?? "Unknown error";
        Console.WriteLine($"[Vulkan Validation] {messageSeverity}: {message}");
        return Vk.False; // The callback returns a VkBool32
    }

    // Helper to configure the debug messenger
    private void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
        createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;
        createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.ValidationBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt;
        createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
    }


}