using Silk.NET.Vulkan;

namespace Eidolon.Vulkan;

internal enum DescriptorAllocationStrategy
{
    Static,
    PerFrame,
}

internal readonly record struct DescriptorKey
{
    public required string LayoutBindingsSignature { get; init; }
    public required uint SetCount { get; init; }
    public required DescriptorAllocationStrategy AllocationStrategy { get; init; }
    public required string DescriptorTypesSignature { get; init; }

    public static DescriptorKey Create(string layoutBindingsSignature, uint setCount,
        DescriptorAllocationStrategy allocationStrategy, ReadOnlySpan<DescriptorType> descriptorTypes)
    {
        var descriptorTypesSignature = descriptorTypes.Length == 0
            ? string.Empty
            : string.Join('|', descriptorTypes.ToArray().Select(static t => t.ToString()));

        return new DescriptorKey
        {
            LayoutBindingsSignature = layoutBindingsSignature,
            SetCount = setCount,
            AllocationStrategy = allocationStrategy,
            DescriptorTypesSignature = descriptorTypesSignature,
        };
    }
}