using System.Net.Mime;

namespace SillyDownloader.Logging;

public class Debug
{
    public enum DebugLevel
    {
        Info,
        Warning,
        Success,
        Error
    }
    
    static string logsPath = Path.Join(Paths.DATAPATHS.DATAPATH, "logs");
    static string currentLog = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
    static string fullPath = Path.Join(logsPath, currentLog);

    public static void WriteToLog(string message)
    {
        if (!Directory.Exists(logsPath))
        {
            Directory.CreateDirectory(logsPath);
        }

        if (!File.Exists(fullPath))
        {
            File.WriteAllText(fullPath, message);
        }
        else
        {
            File.AppendAllText(fullPath, message);
        }
    }
    
    public static string BuildLogMessage(DebugLevel level, string message)
    {
        string levelText = "";
        if (level == DebugLevel.Info)
        {
            levelText = "INFO";
        }
        else if (level == DebugLevel.Warning)
        {
            levelText = "WARNING";
        }
        else if (level == DebugLevel.Success)
        {
            levelText = "SUCCESS";
        }
        else if (level == DebugLevel.Error)
        {
            levelText = "ERROR";
        }
        else
        {
            levelText = "UNKNOWN";
        }

        return $"[{levelText}-{DateTime.Now.Month}-{DateTime.Now.Day}-{DateTime.Now.Year}-{DateTime.Now.Hour}:{DateTime.Now.Minute}:{DateTime.Now.Second}] {message}";
    }

    public static void Log(DebugLevel level, string message, bool verbose=false)
    {
        if (verbose && !GLOBAL.argsThing.verbose)
        {
            return;
        }

        string fileLogMessage = BuildLogMessage(level, message);

        if (level == DebugLevel.Info)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
        }
        else if (level == DebugLevel.Warning)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
        } else if (level == DebugLevel.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
        } else if (level == DebugLevel.Error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }
        
        WriteToLog(fileLogMessage);
        Console.WriteLine(fileLogMessage);
        Console.ResetColor();
    }

    public static void Info(string message, bool verbose=false)
    {
        Log(DebugLevel.Info, message, verbose);
    }
    public static void Warning(string message, bool verbose=false)
    {
        Log(DebugLevel.Warning, message, verbose);
    }
    public static void Success(string message, bool verbose=false)
    {
        Log(DebugLevel.Success, message, verbose);
    }
    public static void Error(string message, bool verbose=false)
    {
        Log(DebugLevel.Error, message, verbose);
    }
}