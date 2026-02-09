using System.Diagnostics;

namespace Eidolon.Vulkan;

public static class ShaderCompiler
{
	public static void CompileShaders(string shaderDir)
	{
		Debug.Log("Compiling shaders", VALIDATION_LAYERS.WARNING);
		// Update this to your actual SDK path
		string glslcPath = @"C:\VulkanSDK\1.4.328.1\Bin\glslc.exe"; 

		if (!Directory.Exists(shaderDir))
		{
			Debug.Log("Did not get past path check",  VALIDATION_LAYERS.ERROR);
			return;
		}
		
		var files = Directory.GetFiles(shaderDir);
		foreach (var file in files)
		{
			if (file.EndsWith(".vert") || file.EndsWith(".frag"))
			{
				// --- Strip the BOM if it exists ---
				byte[] fileBytes = File.ReadAllBytes(file);
				if (fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF)
				{
					// Re-save without the BOM
					File.WriteAllBytes(file, fileBytes.Skip(3).ToArray());
					Console.WriteLine($"Stripped BOM from {Path.GetFileName(file)}");
				}
					
				string output = file + ".spv";
                    
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					FileName = glslcPath,
					Arguments = $"\"{file}\" -o \"{output}\"",
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				// Debug: Read the first 5 characters and print their hex values
				byte[] firstBytes = File.ReadAllBytes(file).Take(5).ToArray();
				Debug.Log($"Hex header of {Path.GetFileName(file)}: {BitConverter.ToString(firstBytes)}");
				Debug.Log("Output file: " + output + " ");
				using var process = Process.Start(startInfo);
				string error = process.StandardError.ReadToEnd();
				process.WaitForExit();

				if (!string.IsNullOrEmpty(error))
				{
					Debug.Log($"Shader Error in {Path.GetFileName(file)}: {error}", VALIDATION_LAYERS.ERROR);
				}
				else
				{
					Debug.Log($"Compiled: {Path.GetFileName(file)}", VALIDATION_LAYERS.SUCCESS);
				}
			}
		}
		Debug.Log("Shaders compiled", VALIDATION_LAYERS.SUCCESS);
	}
}