using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;


public static class CsToMarkdownExporter
{
    
    private static readonly string _sourceRoot = @"G:\Coding\ImGui_Test\ImGuiTestProject\Vulkan";
    private static readonly string _outputRoot = @"G:\Coding\Coding_obsidian\Coding_General\ExportedCode";
    private static readonly string _cacheFile =  @"G:\Coding\Coding_obsidian\Coding_General\ExportedCode\.export-cache.json";

    private static Dictionary<string, string> _hashCache = new();

    
    public static void Run()
    {
        LoadCache();
        if (!Directory.Exists(_sourceRoot))
        {
            Console.WriteLine("Source directory does not exist.");
            return;
        }
        

        foreach (string csFilePath in Directory.EnumerateFiles(_sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsIgnoredPath(csFilePath))
                continue;
            ProcessFile(csFilePath);
        }

        CleanupStaleCacheEntries();
        SaveCache();
        Console.WriteLine("Export completed.");
    }
    
    private static bool IsIgnoredPath(string filePath)
    {
        string normalizedPath = filePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        return normalizedPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }
    
    private static void ProcessFile(string csFilePath)
    {
        string relativePath = Path.GetRelativePath(_sourceRoot, csFilePath);
        string currentHash = ComputeHash(csFilePath);

        
        if (_hashCache.TryGetValue(relativePath, out string cachedHash) &&
            cachedHash == currentHash)
        {
            return; // unchanged
        }
        
        ExportFiles(csFilePath, relativePath);
        _hashCache[relativePath] = currentHash;
    }

    private static void ExportFiles(string csFilePath, string relativePath)
    {
        // string relativePath = Path.GetRelativePath(_sourceRoot, csFilePath);
        string outputPath = Path.ChangeExtension(
            Path.Combine(_outputRoot, relativePath),
            ".md"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        string code = File.ReadAllText(csFilePath);

        StringBuilder markdown = new StringBuilder();
        markdown.AppendLine("```cs");
        markdown.AppendLine(code);
        markdown.AppendLine("```");

        File.WriteAllText(outputPath, markdown.ToString(), Encoding.UTF8);
    }
    
    private static string ComputeHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = File.ReadAllBytes(filePath);
        byte[] hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
    
    private static void LoadCache()
    {
        if (!File.Exists(_cacheFile))
            return;

        string json = File.ReadAllText(_cacheFile);
        _hashCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                     ?? new Dictionary<string, string>();
    }

    private static void SaveCache()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile)!);

        string json = JsonSerializer.Serialize(
            _hashCache,
            new JsonSerializerOptions { WriteIndented = true }
        );

        File.WriteAllText(_cacheFile, json, Encoding.UTF8);
    }
    
    private static void CleanupStaleCacheEntries()
    {
        List<string> keysToRemove = new();

        foreach (var entry in _hashCache)
        {
            string absolutePath = Path.Combine(_sourceRoot, entry.Key);

            if (!File.Exists(absolutePath))
            {
                keysToRemove.Add(entry.Key);
            }
        }

        foreach (string key in keysToRemove)
        {
            _hashCache.Remove(key);
        }
    }
}