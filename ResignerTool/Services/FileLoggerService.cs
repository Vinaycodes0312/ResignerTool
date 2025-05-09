namespace ResignerTool.Services;

public class FileLoggerService : ILogService
{
    private readonly string _logPath;
    private readonly object _lockObj = new();
    private readonly Queue<string> _recentLogs;
    private const int MaxRecentLogs = 1000;

    public FileLoggerService()
    {
        _logPath = Path.Combine(Path.GetTempPath(), "resigntool-log.txt");
        _recentLogs = new Queue<string>(MaxRecentLogs);
        LoadRecentLogs();
    }

    private void LoadRecentLogs()
    {
        try
        {
            if (File.Exists(_logPath))
            {
                var lines = File.ReadAllLines(_logPath).TakeLast(MaxRecentLogs);
                foreach (var line in lines)
                {
                    _recentLogs.Enqueue(line);
                    if (_recentLogs.Count > MaxRecentLogs)
                        _recentLogs.Dequeue();
                }
            }
        }
        catch
        {
            // Ignore errors during initialization
        }
    }

    private void WriteLog(string level, string message)
    {
        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        
        lock (_lockObj)
        {
            try
            {
                File.AppendAllText(_logPath, logMessage + Environment.NewLine);
                _recentLogs.Enqueue(logMessage);
                if (_recentLogs.Count > MaxRecentLogs)
                    _recentLogs.Dequeue();
            }
            catch
            {
                // Ignore write errors
            }
        }
    }

    public void Log(string message)
    {
        WriteLog("INFO", message);
    }

    public void LogError(string message, Exception? exception = null)
    {
        var fullMessage = exception != null 
            ? $"{message} - Exception: {exception.Message}\nStackTrace: {exception.StackTrace}"
            : message;
        WriteLog("ERROR", fullMessage);
    }

    public void LogWarning(string message)
    {
        WriteLog("WARN", message);
    }

    public void LogInfo(string message)
    {
        WriteLog("INFO", message);
    }

    public string[] GetRecentLogs(int count = 100)
    {
        lock (_lockObj)
        {
            return _recentLogs.TakeLast(Math.Min(count, _recentLogs.Count)).ToArray();
        }
    }

    public void Clear()
    {
        lock (_lockObj)
        {
            try
            {
                File.WriteAllText(_logPath, string.Empty);
                _recentLogs.Clear();
            }
            catch
            {
                // Ignore clear errors
            }
        }
    }
}
