namespace Eidolon.Vulkan;

public class Shader
{
    // String path to shader
    public string VertexPath = "default_shader.vert.spv";
    public string FragPath = "default_shader.frag.spv";
   
    
    private void SetPath(string vertPath, string fragPath)
    {
        //Check if the paths are default
        
        //if not, join strings

        vertPath += ".vert.spv";
        fragPath += ".frag.spv";
    }
}