using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Eidolon.Vulkan;

internal unsafe class VulkanInstance
{
    [Group("References")]
    private VulkanManager _master;
    private IWindow _window;
    private Instance _instance;
    public Instance Instance => _instance;
    

    [Group("Resource")]
    public Surfaces Surfaces { get; set; }
    private KhrSurface _khrSurface;
    private SurfaceKHR _surfaceKhr;

    
    [Group("Debug")] 
    private ExtDebugUtils DebugUtils;
    private DebugUtilsMessengerEXT DebugMessenger;
    public VulkanInstance(VulkanManager master, IWindow window)
    {
        _master = master;
        _window = window;

        CreateInstance(window);
        CreateSurface(window);
        SetupDebugMessenger();
        
        Surfaces = new Surfaces
        {
            KhrSurface = _khrSurface,
            SurfaceKhr = _surfaceKhr,
        };
    }
    
    private void CreateInstance(IWindow window)
    {
        // Define application info
        ApplicationInfo appInfo = new ApplicationInfo()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Silk.NET Vulkan App"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version12
        };

        // Get available instance layers
        uint availableLayerCount = 0;
       _master.Vk.EnumerateInstanceLayerProperties(ref availableLayerCount, null);
        var availableLayers = new LayerProperties[ availableLayerCount ];
        fixed (LayerProperties* availableLayersPtr = availableLayers)
        {
            _master.Vk.EnumerateInstanceLayerProperties(ref availableLayerCount, availableLayersPtr);
        }

        Console.WriteLine("Available Vulkan layers:");
        foreach (var layer in availableLayers)
        {
            string layerName = Marshal.PtrToStringAnsi((IntPtr)layer.LayerName) ?? "Unknown";
            Console.WriteLine($"  - {layerName}");
        }

        // Check if our desired validation layer exists
        var enabledLayers = new List<string>();
        string desiredValidationLayer = "VK_LAYER_KHRONOS_validation";

        bool hasValidationLayer = availableLayers.Any(layer =>
            Marshal.PtrToStringAnsi((IntPtr)layer.LayerName) == desiredValidationLayer);

        if (hasValidationLayer)
        {
            enabledLayers.Add(desiredValidationLayer);
            Console.WriteLine($"Enabling validation layer: {desiredValidationLayer}");
        }
        else
        {
            Console.WriteLine($"Warning: Validation layer '{desiredValidationLayer}' not available. Running without validation.");
        }

        // Get required extensions - we need both debug utils and surface extensions
        var enabledExtensions = new List<string> { ExtDebugUtils.ExtensionName };


        // Add surface extensions that the window requires
        var windowExtensions = window.VkSurface!.GetRequiredExtensions(out var windowExtCount);
        for (int i = 0; i < (int)windowExtCount; i++)
        {
            string? extension = SilkMarshal.PtrToString((nint)windowExtensions[ i ]);
            if (!string.IsNullOrEmpty(extension))
            {
                enabledExtensions.Add(extension);
            }
        }

        Console.WriteLine("Enabled extensions:");
        foreach (var ext in enabledExtensions)
        {
            Console.WriteLine($"  - {ext}");
        }

        // Convert managed strings to unmanaged pointers
        byte** ppEnabledExtensions = (byte**)SilkMarshal.StringArrayToPtr(enabledExtensions.ToArray());
        byte** ppEnabledLayers = (byte**)SilkMarshal.StringArrayToPtr(enabledLayers.ToArray());

        try
        {
            // Create debug messenger info for instance creation (only if we have the extension)
            DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
            if (enabledExtensions.Contains(ExtDebugUtils.ExtensionName))
            {
                PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
            }

            InstanceCreateInfo createInfo = new()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)enabledExtensions.Count,
                PpEnabledExtensionNames = ppEnabledExtensions,
                EnabledLayerCount = (uint)enabledLayers.Count,
                PpEnabledLayerNames = ppEnabledLayers,
            };

            // Only set PNext if we're actually using debug utils
            if (enabledExtensions.Contains(ExtDebugUtils.ExtensionName))
            {
                createInfo.PNext = &debugCreateInfo;
            }

            Result result = _master.Vk.CreateInstance(ref createInfo, null, out _instance);
            if (result != Result.Success)
            {
                Debug.Log($"Failed to create Vulkan instance! Error: {result}");
                throw new Exception("Failed to create Vulkan instance!");
            }

            Debug.Log("Vulkan instance created successfully!");
        }
        finally
        {
            // Free the unmanaged memory allocated for strings
            SilkMarshal.Free((nint)ppEnabledExtensions);
            SilkMarshal.Free((nint)ppEnabledLayers);
            Marshal.FreeHGlobal((nint)appInfo.PApplicationName);
            Marshal.FreeHGlobal((nint)appInfo.PEngineName);
        }
    }
        
    private void CreateSurface(IWindow window)
    {
        if (window.VkSurface == null)
        {
            throw new Exception("Window was not created with Vulkan Backend!" +
                                "Make sure to use WindowOptions.DefaultVulkan");
        }
			
        //The window can create a Vulkan surface for us
        
        if (!_master.Vk.TryGetInstanceExtension(Instance, out _khrSurface))
        {
            throw new Exception("KHR_surface extension not found!");
        }

        // Create surface using the window's built-in method
        _surfaceKhr = window.VkSurface!.Create<AllocationCallbacks>(Instance.ToHandle(), null).ToSurface();

        if (_surfaceKhr.Handle == (ulong)IntPtr.Zero)
        {
            Debug.Log("Surface creation failed!");
        }
        Debug.Log("Surface creation successful");
    }
    
    
    private void SetupDebugMessenger()
    {
        // Try to get the debug utils extension
        if (!_master.Vk.TryGetInstanceExtension(_instance, out DebugUtils))
        {
            Debug.Log("Debug utils extension not available. Debug messaging disabled.");
            return;
        }

        DebugUtilsMessengerCreateInfoEXT createInfo = new DebugUtilsMessengerCreateInfoEXT();
        PopulateDebugMessengerCreateInfo(ref createInfo);

        // Create the debug messenger
        if (DebugUtils.CreateDebugUtilsMessenger(_instance, in createInfo, null, out DebugMessenger) != Result.Success)
        {
            Debug.Log("Failed to set up debug messenger!");
            
        }
        else
        {
            Debug.Log("Debug messenger set up successfully!");
        }
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