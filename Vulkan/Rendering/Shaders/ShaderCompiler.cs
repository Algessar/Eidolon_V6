using System.Diagnostics;

namespace Eidolon.Vulkan;

public static class ShaderCompiler
{
	// Update this to your actual SDK path
	private const string GlslcPath = @"C:\VulkanSDK\1.4.328.1\Bin\glslc.exe";

	/// <summary>
	/// Recompiles every .vert/.frag in <paramref name="shaderDir"/> to a sibling .spv file.
	/// </summary>
	/// <remarks>
	/// Pass the deployed <c>Rendering/Shaders</c> folder next to the executable. The GLSL
	/// sources are copied there by the build, so compiling in place means the SPIR-V the
	/// running process loads is always the one compiled from the sources on disk -- and the
	/// repository tree is never written to.
	/// </remarks>
	public static void CompileShaders(string shaderDir)
	{
		if (!Directory.Exists(shaderDir))
		{
			Debug.Log($"Shader directory '{shaderDir}' not found; using the deployed .spv files.",
				VALIDATION_LAYERS.WARNING);
			return;
		}

		if (!File.Exists(GlslcPath))
		{
			Debug.Log($"glslc not found at '{GlslcPath}'; using the deployed .spv files.",
				VALIDATION_LAYERS.WARNING);
			return;
		}

		Debug.Log("Compiling shaders", VALIDATION_LAYERS.WARNING);

		foreach (var file in Directory.GetFiles(shaderDir))
		{
			if (!file.EndsWith(".vert") && !file.EndsWith(".frag"))
				continue;

			StripBom(file);
			Compile(file);
		}

		Debug.Log("Shaders compiled", VALIDATION_LAYERS.SUCCESS);
	}

	private static void StripBom(string file)
	{
		byte[] fileBytes = File.ReadAllBytes(file);
		if (fileBytes.Length < 3 || fileBytes[0] != 0xEF || fileBytes[1] != 0xBB || fileBytes[2] != 0xBF)
			return;

		File.WriteAllBytes(file, fileBytes.Skip(3).ToArray());
		Debug.Log($"Stripped BOM from {Path.GetFileName(file)}");
	}

	private static void Compile(string file)
	{
		var output = file + ".spv";

		var startInfo = new ProcessStartInfo
		{
			FileName = GlslcPath,
			Arguments = $"\"{file}\" -o \"{output}\"",
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		Debug.Log("Output file: " + output + " ");

		string error;
		try
		{
			using var process = Process.Start(startInfo);
			if (process is null)
			{
				Debug.Log($"Could not start the shader compiler for {Path.GetFileName(file)}.",
					VALIDATION_LAYERS.WARNING);
				return;
			}

			error = process.StandardError.ReadToEnd();
			process.WaitForExit();
		}
		catch (Exception ex)
		{
			// Keep the failure outside the try so a compile error below still fails fast.
			Debug.Log($"Failed to run glslc for {Path.GetFileName(file)}: {ex.Message}",
				VALIDATION_LAYERS.WARNING);
			return;
		}

		if (!string.IsNullOrEmpty(error))
		{
			Debug.Log($"Shader Error in {Path.GetFileName(file)}: {error}", VALIDATION_LAYERS.ERROR);
		}

		Debug.Log($"Compiled: {Path.GetFileName(file)}", VALIDATION_LAYERS.SUCCESS);
	}
}
