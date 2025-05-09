namespace ResignerTool.Services;

public interface ILogService
{
    
    void Log(string message);
    void LogError(string message, Exception? exception = null);
    void LogWarning(string message);
    void LogInfo(string message);
    string[] GetRecentLogs(int count = 100);
    void Clear();
}