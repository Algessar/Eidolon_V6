using Silk.NET.Vulkan;
using Buffer = System.Buffer;

namespace Eidolon.Vulkan.Rendering;

internal unsafe class ShaderManager
{
    VulkanMaster _master;
    
    private Dictionary<string, ShaderModule> _cache = new();

    public ShaderManager(VulkanMaster master)
    {
        Debug.Log("Creating ShaderManager", VALIDATION_LAYERS.INFO);
        _master = master;
        
        Debug.Log("ShaderManager created!", VALIDATION_LAYERS.SUCCESS);
    }

    public ShaderModule Load(string fileName)
    {
        if (_cache.TryGetValue(fileName, out var existing))
        {
            return existing;
        }

        if (!fileName.EndsWith(".spv"))
        {
            throw new ArgumentException("Shader must be a .spv file", nameof(fileName));
        }

        string shaderDir = Path.Combine(
            AppContext.BaseDirectory,
            "Rendering",
            "_Shaders");

        string shaderPath = Path.Combine(shaderDir, fileName);

        if (!File.Exists(shaderPath))
        {
            throw new FileNotFoundException(
                $"Shader file not found: {shaderPath}");
        }

        byte[] bytes = File.ReadAllBytes(shaderPath);

        if (bytes.Length % 4 != 0)
        {
            throw new InvalidDataException(
                $"SPIR-V file size is not 4-byte aligned: {fileName}");
        }

        // ---- SAFE SPIR-V HANDLING ----
        uint[] words = new uint[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, words, 0, bytes.Length);

        fixed (uint* pCode = words)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)bytes.Length,
                PCode = pCode
            };

            if (_master.Vk.CreateShaderModule(
                    _master.VulkanDevice.Device,
                    in createInfo,
                    null,
                    out ShaderModule shaderModule) != Result.Success)
            {
                throw new Exception(
                    $"Failed to create shader module: {fileName}");
            }

            _cache[fileName] = shaderModule;
            return shaderModule;
        }
    }
}