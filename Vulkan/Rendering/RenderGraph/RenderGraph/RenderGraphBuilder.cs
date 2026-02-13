using System.Diagnostics;

namespace Eidolon.Vulkan;

public sealed class RenderGraphBuilder
{
    private readonly List<PassRecord> _passes = new();
    private readonly Dictionary<uint, ResourceRecord> _resources = new();

    private uint _nextHandle = 1;

    public RenderGraphBuilder()
    {
        Console.WriteLine("Creating RenderGraphBuilder");
        
        Console.WriteLine("RenderGraphBuilder created!");
    }

    public ResourceHandle ImportImage(string name, in GraphImageDescription description)
    {
        return CreateImage(name, description, imported: true);
    }

    public ResourceHandle CreateImage(string name, in GraphImageDescription description)
    {
        return CreateImage(name, description, imported: false);
    }

    private ResourceHandle CreateImage(string name, in GraphImageDescription description, bool imported)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Resource name cannot be null or whitespace", nameof(name));
        }

        var handle = new ResourceHandle { Handle = _nextHandle++ };
        _resources.Add(handle.Handle, new ResourceRecord(handle, name, description, imported));
        return handle;
    }

    public PassBuilder AddPass(string name, RenderPassType type)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Pass name cannot be null or whitespace.", nameof(name));

        }
        var index = _passes.Count;
        _passes.Add(new PassRecord(index, name, type));
        return new PassBuilder(this, index);
    }
    
    internal void RegisterRead(int passIndex, ResourceHandle handle)
    {
        ValidatePassAndResource(passIndex, handle);
        _passes[passIndex].Reads.Add(handle);
    }
    
    internal void RegisterWrite(int passIndex, ResourceHandle handle)
    {
        ValidatePassAndResource(passIndex, handle);
        _passes[passIndex].Writes.Add(handle);
    }

    public CompiledRenderGraph Compile(in FrameDescription frame)
    {
        ValidatePassRecords();

        var deps = BuildDependencies();
        var executionOrder = TopologicalSort(deps);

        var executionIndexByOriginal = new int[_passes.Count];
        for (var i = 0; i < executionOrder.Count; i++)
        {
            executionIndexByOriginal[executionOrder[i]] = i;
        }

        var compiledPasses = new List<CompiledPass>(_passes.Count);
        foreach (var originalIndex in executionOrder)
        {
            var pass = _passes[originalIndex];
            var depIndices = deps[originalIndex]
                .Select(depOriginal => executionIndexByOriginal[depOriginal])
                .Order()
                .ToArray();

            compiledPasses.Add(new CompiledPass(
                ExecutionIndex: executionIndexByOriginal[originalIndex],
                OriginalIndex: pass.Index,
                Name: pass.Name,
                Type: pass.Type,
                Reads: pass.Reads.ToArray(),
                Writes: pass.Writes.ToArray(),
                Dependencies: depIndices));
        }

        var compiledResources = BuildResourceLifetimes(executionIndexByOriginal);

        return new CompiledRenderGraph(frame, compiledResources, compiledPasses);
    }
    
    private List<HashSet<int>> BuildDependencies()
    {
        var dependencies = Enumerable.Range(0, _passes.Count)
            .Select(_ => new HashSet<int>())
            .ToList();

        var lastWriter = new Dictionary<uint, int>();
        var lastReaders = new Dictionary<uint, HashSet<int>>();

        for (var passIndex = 0; passIndex < _passes.Count; passIndex++)
        {
            var pass = _passes[passIndex];
            if (pass is null)
            {
                throw new InvalidOperationException($"Pass at index {passIndex} is null. Ensure AddPass is used for all pass creation.");
            }

            foreach (var read in pass.Reads)
            {
                if (lastWriter.TryGetValue(read.Handle, out var writer))
                {
                    dependencies[passIndex].Add(writer);
                }

                if (!lastReaders.TryGetValue(read.Handle, out var readers))
                {
                    readers = new HashSet<int>();
                    lastReaders.Add(read.Handle, readers);
                }

                readers.Add(passIndex);
            }

            foreach (var write in pass.Writes)
            {
                if (lastWriter.TryGetValue(write.Handle, out var writer))
                {
                    dependencies[passIndex].Add(writer);
                }

                if (lastReaders.TryGetValue(write.Handle, out var readers))
                {
                    foreach (var reader in readers)
                    {
                        dependencies[passIndex].Add(reader);
                    }

                    readers.Clear();
                }

                lastWriter[write.Handle] = passIndex;
            }
        }

        return dependencies;
    }

    private List<CompiledResource> BuildResourceLifetimes(IReadOnlyList<int> executionIndexByOriginal)
    {
        var result = new List<CompiledResource>(_resources.Count);

        foreach (var (_, resource) in _resources.OrderBy(pair => pair.Key))
        {
            var firstUse = int.MaxValue;
            var lastUse = int.MinValue;

            for (var originalPass = 0; originalPass < _passes.Count; originalPass++)
            {
                var pass = _passes[originalPass];
                var used = pass.Reads.Any(r => r.Handle == resource.Handle.Handle)
                           || pass.Writes.Any(w => w.Handle == resource.Handle.Handle);

                if (!used)
                {
                    continue;
                }
                
                var executionPass = executionIndexByOriginal[originalPass];
                
                firstUse = System.Math.Min(firstUse, executionPass);
                lastUse = System.Math.Max(lastUse, executionPass);
            }

            if (firstUse == int.MaxValue)
            {
                firstUse = -1;
                lastUse = -1;
            }
            
            result.Add(new CompiledResource(
                Handle: resource.Handle,
                Name: resource.Name,
                Description: resource.Description,
                Imported: resource.Imported,
                FirstUsePass: firstUse,
                LastUsePass: lastUse));
        }
        return result;
    }

    private List<int> TopologicalSort(IReadOnlyList<HashSet<int>> dependencies)
    {
        var incoming = dependencies.Select(set => set.Count).ToArray();
        var dependents = Enumerable.Range(0, dependencies.Count).Select(_ => new List<int>()).ToArray();

        for (var i = 0; i < dependencies.Count; i++)
        {
            foreach (var dependency  in dependencies[i])
            {
                dependents[dependency ].Add(i);
            }
        }

        var queue = new PriorityQueue<int, int>();
        for (var i = 0; i < incoming.Length; i++)
        {
            if(incoming[i] == 0)
            {
                queue.Enqueue(i, i);
            }
        }
        
        var result = new List<int>(dependencies.Count);

        while (queue.TryDequeue(out var node, out _))
        {
            result.Add(node);
            foreach (var dependent in dependents[node])
            {
                incoming[dependent]--;
                if(incoming[dependent] == 0)
                {
                    queue.Enqueue(dependent, dependent);
                }
            }
        }
        
        if (result.Count != dependencies.Count)
        {
            throw new InvalidOperationException("Render graph contains a dependency cycle.");
        }

        return result;
    }

    private void ValidatePassAndResource(int passIndex, ResourceHandle handle)
    {
        if (passIndex < 0 || passIndex >= _passes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(passIndex));
        }

        if (!_resources.ContainsKey(handle.Handle))
        {
            throw new InvalidOperationException($"Unknown resource handle: {handle.Handle}");
        }
    }
    private void ValidatePassRecords()
    {
        for (var i = 0; i < _passes.Count; i++)
        {
            var pass = _passes[i];
            if (pass is null)
            {
                throw new InvalidOperationException($"Pass at index {i} is null.");
            }

            if (pass.Reads is null || pass.Writes is null)
            {
                throw new InvalidOperationException($"Pass '{pass.Name}' has uninitialized read/write collections.");
            }
        }
    }

    private sealed record ResourceRecord(
        ResourceHandle Handle,
        string Name,
        GraphImageDescription Description,
        bool Imported);

    private sealed class PassRecord
    {
        public int Index { get; }
        public string Name { get; }
        public RenderPassType Type { get; }
        public List<ResourceHandle> Reads { get; }
        public List<ResourceHandle> Writes { get; }

        public PassRecord(int index, string name, RenderPassType type)
        {
            Index = index;
            Name = name;
            Type = type;
            Reads = new List<ResourceHandle>();
            Writes = new List<ResourceHandle>();
        }
    }

}