using System.Reflection;
using EidolonCore.ECS;
using EidolonGame;

namespace Eidolon.Tests;

/// <summary>
/// The layering rules, asserted rather than remembered.
/// </summary>
/// <remarks>
/// These check assembly references, so a stray using that drags the renderer into the engine, or the
/// renderer into a game, fails here instead of being discovered halfway through a refactor.
/// </remarks>
public class ArchitectureTests
{
    /// <summary>The engine: ECS, scene components, simulation, assets, physics.</summary>
    private static Assembly Engine => typeof(World).Assembly;

    /// <summary>The game: content and gameplay, built on the engine.</summary>
    private static Assembly Game => typeof(GameModule).Assembly;

    /// <summary>The renderer, which currently also hosts the editor UI.</summary>
    private static Assembly Renderer => typeof(Eidolon.Vulkan.VulkanHost).Assembly;

    private static string[] References(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToArray();

    [Fact]
    public void TheEngineDoesNotDependOnTheRenderer()
    {
        Assert.DoesNotContain("Vulkan", References(Engine));
    }

    [Fact]
    public void TheEngineDoesNotDependOnAnyGame()
    {
        Assert.DoesNotContain("EidolonGame", References(Engine));
    }

    [Fact]
    public void AGameDependsOnTheEngineAndNothingElse()
    {
        var references = References(Game);

        Assert.Contains("EidolonCore", references);
        Assert.DoesNotContain("Vulkan", references);
        Assert.DoesNotContain("EidolonEditor", references);
    }

    [Fact]
    public void TheRendererDoesNotDependOnAnyGame()
    {
        Assert.DoesNotContain("EidolonGame", References(Renderer));
    }

    [Fact]
    public void TheRendererDoesNotDependOnTheEditor()
    {
        // The editor supplies the window and the overlay; the backend must not know it exists,
        // which is what keeps the panels and the renderer in separate assemblies.
        Assert.DoesNotContain("EidolonEditor", References(Renderer));
    }

    [Fact]
    public void ImportingIsAHostChoice()
    {
        // Assimp is a native dependency, so only the host links it: the engine and the renderer stay
        // free of it and can be built and tested without the importer present.
        Assert.DoesNotContain("EidolonAssimp", References(Engine));
        Assert.DoesNotContain("EidolonAssimp", References(Renderer));
    }
}
