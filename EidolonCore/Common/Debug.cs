using System.Runtime.CompilerServices;

public enum VALIDATION_LAYERS
{
    SUCCESS,
    WARNING,
    ERROR,
    INFO
}
public static class Debug
{
    public static void Log(string message,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
    {
        Console.WriteLine($"{file}({line})[{member}]: {message}");
    }
    
    public static void Log(string message, VALIDATION_LAYERS validation,
        bool shouldLog = true,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "" )
    {
        if (!shouldLog) return;
        
        if (validation == VALIDATION_LAYERS.SUCCESS)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{DateTime.Now}]{file}({line})[{member}]: {message}");
            
        }

        if (validation == VALIDATION_LAYERS.WARNING)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{DateTime.Now}]{file}({line})[{member}]: {message}");
        }

        if (validation == VALIDATION_LAYERS.ERROR)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now}]{file}({line})[{member}]: {message}");
            throw new Exception("Debug Exception");
        }

        if (validation == VALIDATION_LAYERS.INFO)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[{DateTime.Now}]{file}({line})[{member}]: {message}");
        }

        Console.ResetColor();
    }
}
