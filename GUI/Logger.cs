using System;
using System.IO;

namespace SteamContribution;

public static class Logger
{
    private static string _logFilePath = "";
    private static bool _initialized = false;
    
    public static void Initialize(string? customPath = null)
    {
        if (_initialized) return;
        
        _logFilePath = string.IsNullOrEmpty(customPath) 
            ? Path.Combine(AppContext.BaseDirectory, "log.log")
            : customPath;
        
        try
        {
            if (File.Exists(_logFilePath))
                File.Delete(_logFilePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[警告] 无法删除旧日志文件：{ex.Message}");
        }
        
        _initialized = true;
        Write("========================================");
        Write("=== 日志系统初始化 ===");
        Write($"日志文件路径：{_logFilePath}");
    }
    
    public static void Write(string message, string category = "")
    {
        if (!_initialized) Initialize();
        
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var categoryTag = string.IsNullOrEmpty(category) ? "" : $"[{category}] ";
        var logLine = $"[{timestamp}] {categoryTag}{message}";
        
        System.Diagnostics.Debug.WriteLine(logLine);
        _ = WriteToFileAsync(logLine);
    }
    
    private static async Task WriteToFileAsync(string logLine)
    {
        try
        {
            await Task.Delay(50);
            
            using var fileStream = new FileStream(
                _logFilePath, 
                FileMode.Append, 
                FileAccess.Write, 
                FileShare.ReadWrite,
                bufferSize: 4096);
            
            using var writer = new StreamWriter(fileStream);
            await writer.WriteLineAsync(logLine);
            await writer.FlushAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[文件错误] {ex.Message}");
        }
    }
    
    public static void Error(string message, Exception? ex = null)
    {
        Write($"✗ {message}", "ERROR");
        if (ex != null)
        {
            Write($"类型：{ex.GetType().Name}", "ERROR");
            Write($"消息：{ex.Message}", "ERROR");
            Write($"堆栈：{ex.StackTrace}", "ERROR");
        }
    }
    
    public static void Info(string message) => Write(message, "INFO");
    public static void Warning(string message) => Write(message, "WARN");
    public static void Debug(string message) => Write(message, "DEBUG");
    public static void Step(string step, string message) => Write($"{step} {message}", "STEP");
}
