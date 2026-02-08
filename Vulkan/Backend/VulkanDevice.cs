using System.ComponentModel;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

//This will now be instantiated after CommandManager?
internal unsafe class VulkanDevice : IDisposable
{
	[Header("References")] 
	private VulkanMaster _master;
	
	private Device _device;
	private PhysicalDevice _physicalDevice;
	
	public Device Device => _device;
	public PhysicalDevice PhysicalDevice => _physicalDevice;

    [Header("Resources")]
    private Queue _graphicsQueue;
    private Queue _presentQueue;
    public Queue GraphicsQueue  => _graphicsQueue;
    public Queue PresentQueue  => _presentQueue;
    
    private CommandPool _transientCommandPool;
    
    public uint GraphicsQueueFamily { get; set; }

    public VulkanDevice(VulkanMaster master)
    {
	    _master = master;
	    
	    Debug.Log("Creating Vulkan Device", VALIDATION_LAYERS.WARNING);
	    
	    PickPhysicalDevice();
	    CreateLogicalDevice();
		CreateTransientCommandPool(GraphicsQueueFamily);
	    
	    Debug.Log("Device successfully created!", VALIDATION_LAYERS.SUCCESS);
    }
    
    private void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        _master.Vk.EnumeratePhysicalDevices(_master.VulkanInstance.Instance, ref deviceCount, null);

        if (deviceCount == 0)
            throw new Exception("Failed to find GPUs with Vulkan support!");

        var devices = new PhysicalDevice[ deviceCount ];

        fixed (PhysicalDevice* devicesPtr = devices)
        {
            _master.Vk.EnumeratePhysicalDevices(_master.VulkanInstance.Instance, ref deviceCount, devicesPtr);
        }

        foreach (var device in devices)
        {
            if (IsDeviceSuitable(device))
            {
                _physicalDevice = device;
                break;
            }
        }

        if (PhysicalDevice.Handle == 0)
        {
            throw new Exception("Failed to find a suitable GPU!");
        }
    }
    
    private bool IsDeviceSuitable(PhysicalDevice device)
    {
        _master.Vk.GetPhysicalDeviceProperties(device, out var properties);

        // Check for basic graphics support
        if (properties.DeviceType == PhysicalDeviceType.Cpu ||
            properties.DeviceType == PhysicalDeviceType.Other)
            return false;

        // Check for queue family that supports graphics
        if (!FindQueueFamilies(device).HasGraphicsFamily)
            return false;

        // Check for surface support
        if (!CheckSurfaceSupport(device))
            return false;

        return true;
    }
    private bool CheckSurfaceSupport(PhysicalDevice device)
    {
	    // Get the queue families
	    uint queueFamilyCount = 0;
	    _master.Vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

	    var queueFamilies = new QueueFamilyProperties[ queueFamilyCount ];
	    fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
	    {
		    _master.Vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);
	    }

	    // Check if any queue family supports presentation to our surface
	    for (uint i = 0; i < queueFamilies.Length; i++)
	    {
		    if (_master.Surfaces.KhrSurface.GetPhysicalDeviceSurfaceSupport(device, i, _master.SurfaceKhr, out var supported) == Result.Success && supported)
		    {
			    return true;
		    }
	    }

	    return false;
    }

    
    private void CreateLogicalDevice()
		{
			var indices = FindQueueFamilies(PhysicalDevice);

			if (!indices.IsComplete)
				throw new Exception("Device doesn't support required queue families!");

			// We might need separate queues for graphics and presentation
			var uniqueQueueFamilies = new HashSet<uint> { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };

			var queueCreateInfos = new List<DeviceQueueCreateInfo>();
			float queuePriority = 1.0f;

			foreach (var queueFamily in uniqueQueueFamilies)
			{
				var queueCreateInfo = new DeviceQueueCreateInfo
				{
					SType = StructureType.DeviceQueueCreateInfo,
					QueueFamilyIndex = queueFamily,
					QueueCount = 1,
					PQueuePriorities = (float*)Marshal.AllocHGlobal(sizeof(float))
				};
				*queueCreateInfo.PQueuePriorities = queuePriority;
				queueCreateInfos.Add(queueCreateInfo);
			}

			try
			{
				PhysicalDeviceFeatures deviceFeatures = new PhysicalDeviceFeatures();

				// Enable swapchain extension
				var enabledExtensions = new[] { "VK_KHR_swapchain" };
				byte** ppEnabledExtensions = (byte**)SilkMarshal.StringArrayToPtr(enabledExtensions);

				// FIXED: Instead of MarshalArrayToPtr, we manually create the array in unmanaged memory
				DeviceQueueCreateInfo* queueCreateInfosArray = (DeviceQueueCreateInfo*)SilkMarshal.Allocate(sizeof(DeviceQueueCreateInfo) * queueCreateInfos.Count);
				for (int i = 0; i < queueCreateInfos.Count; i++)
				{
					queueCreateInfosArray[ i ] = queueCreateInfos[ i ];
				}

				DeviceCreateInfo createInfo = new DeviceCreateInfo
				{
					SType = StructureType.DeviceCreateInfo,
					QueueCreateInfoCount = (uint)queueCreateInfos.Count,
					PQueueCreateInfos = queueCreateInfosArray,  // Use our manually created array
					PEnabledFeatures = &deviceFeatures,
					EnabledExtensionCount = (uint)enabledExtensions.Length,
					PpEnabledExtensionNames = ppEnabledExtensions,
					EnabledLayerCount = 0
				};

				if (_master.Vk.CreateDevice(_physicalDevice, in createInfo, null, out _device) != Result.Success)
				{
					throw new Exception("Failed to create logical device!");
				}

				// Get both graphics and presentation queues
				_master.Vk.GetDeviceQueue(_device, indices.GraphicsFamily.Value, 0, out _graphicsQueue);
				_master.Vk.GetDeviceQueue(_device, indices.PresentFamily.Value, 0, out _presentQueue);

				SilkMarshal.Free((nint)ppEnabledExtensions);
				SilkMarshal.Free((nint)queueCreateInfosArray);  // Free our manually created array
			}
			finally
			{
				// Clean up allocated memory for queue priorities
				foreach (var queueCreateInfo in queueCreateInfos)
				{
					Marshal.FreeHGlobal((nint)queueCreateInfo.PQueuePriorities);
				}
			}
		}


		private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
		{
			var indices = new QueueFamilyIndices();

			uint queueFamilyCount = 0;
			_master.Vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

			var queueFamilies = new QueueFamilyProperties[ queueFamilyCount ];
			fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
			{
				_master.Vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);
			}

			// Find graphics queue family
			for (uint i = 0; i < queueFamilies.Length; i++)
			{
				if (queueFamilies[ i ].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
				{
					indices.GraphicsFamily = i;
				}

				// Check for presentation support
				if (_master.KhrSurface.GetPhysicalDeviceSurfaceSupport(device, i, _master.SurfaceKhr, out var supported) == Result.Success && supported)
				{
					indices.PresentFamily = i;
				}

				if (indices.IsComplete) break;
			}

			return indices;
		}
		
		public uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
		{
			_master.Vk.GetPhysicalDeviceMemoryProperties(PhysicalDevice, out var memoryProperties);
			
			for (uint i = 0; i < memoryProperties.MemoryTypeCount; i++)
			{
				bool supported = (typeFilter & (1u << (int)i)) != 0;
				bool hasFlags = (memoryProperties.MemoryTypes[ (int)i ].PropertyFlags & properties) == properties;

				if (supported && hasFlags)
					return i;
			}

			throw new Exception("Failed to find suitable memory type.");
		}
		
		public struct QueueFamilyIndices
		{
			public uint? GraphicsFamily { get; set; }
			public uint? PresentFamily { get; set; }

			public bool HasGraphicsFamily => GraphicsFamily.HasValue;
			public bool HasPresentFamily => PresentFamily.HasValue;
			public bool IsComplete => HasGraphicsFamily && HasPresentFamily;
		}
		
		public void CreateTransientCommandPool(uint queueFamilyIndex)
		{
			Console.WriteLine($"Creating transient command pool for queue family {queueFamilyIndex}");

			var poolInfo = new CommandPoolCreateInfo
			{
				SType = StructureType.CommandPoolCreateInfo,
				QueueFamilyIndex = queueFamilyIndex,
				Flags = CommandPoolCreateFlags.TransientBit | CommandPoolCreateFlags.ResetCommandBufferBit
			};

			Result result = _master.Vk.CreateCommandPool(_device, in poolInfo, null, out _transientCommandPool);
			if (result != Result.Success)
			{
				Console.WriteLine($"FAILED to create transient command pool: {result}");
				throw new Exception($"Failed to create transient command pool: {result}");
			}

			Console.WriteLine($"Transient command pool created: handle = {_transientCommandPool.Handle}");
		}

		public void Dispose()
		{
			_master.Vk.DeviceWaitIdle(_device);
			
			if (_device.Handle != 0)
			{
				_master.Vk.DestroyDevice(_device, null);
			}
			
			
		}
}