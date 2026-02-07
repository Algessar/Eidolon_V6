namespace Eidolon.Vulkan;

internal static class Constants
{
    private static readonly uint DEFAULT_FRAMES_IN_FLIGHT = 2;
    
    // Current value (can be changed at runtime)
    public static uint MAX_FRAMES_IN_FLIGHT { get; set; } = DEFAULT_FRAMES_IN_FLIGHT;
    
}