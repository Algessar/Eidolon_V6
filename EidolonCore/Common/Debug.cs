using System.Runtime.CompilerServices;
using System.Timers;
using Timer = System.Timers.Timer;

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
            Console.WriteLine($"{file}({line})[{member}]: {message}");
            
        }

        if (validation == VALIDATION_LAYERS.WARNING)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{file}({line})[{member}]: {message}");
        }

        if (validation == VALIDATION_LAYERS.ERROR)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{file}({line})[{member}]: {message}");
            throw new Exception("Debug Exception");
        }

        if (validation == VALIDATION_LAYERS.INFO)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"{file}({line})[{member}]: {message}");
        }

        Console.ResetColor();
    }
    
    static Timer _debugTimer;
    public static void SetTimer()
    {
        // Second timer interval
        Console.WriteLine($"Setting debug timer, {DateTime.Now}");
        Timer timer = new System.Timers.Timer(5000);
        // Subscribe to event
        timer.Elapsed += OnTimedEvent;
        timer.AutoReset = true;
        timer.Enabled = true;
    }

    private static void OnTimedEvent(Object source, ElapsedEventArgs e)
    {
        Console.WriteLine("Time elapsed since server was started {0:HH:mm:ss.fff}",
            e.SignalTime);
    }
}
