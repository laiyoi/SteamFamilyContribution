using System;
using System.IO;

namespace SteamContribution;

/// <summary>
/// 全局日志记录器
/// </summary>
public static class Logger
{
    private static string _logFilePath = "";
    private static bool _initialized = false;
    
    /// <summary>
    /// 初始化日志系统
    /// </summary>
    public static void Initialize(string? customPath = null)
    {
        if (_initialized) return;
        
        _logFilePath = string.IsNullOrEmpty(customPath) 
            ? Path.Combine(AppContext.BaseDirectory, "log.log")
            : customPath;
        
        // 清空之前的日志文件
        try
        {
            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[警告] 无法删除旧日志文件：{ex.Message}");
        }
        
        _initialized = true;
        Write("========================================");
        Write("=== 日志系统初始化 ===");
        Write($"日志文件路径：{_logFilePath}");
        Write($"初始化时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
    }
    
    /// <summary>
    /// 写入日志
    /// </summary>
    public static void Write(string message, string category = "")
    {
        if (!_initialized) Initialize();
        
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var categoryTag = string.IsNullOrEmpty(category) ? "" : $"[{category}] ";
        var logLine = $"[{timestamp}] {categoryTag}{message}";
        
        // 主要输出到调试控制台（F5 调试时可以看到）
        System.Diagnostics.Debug.WriteLine(logLine);
        
        // 次要输出到文件（异步，不阻塞）
        Task.Run(() => WriteToFileAsync(logLine));
    }
    
    private static async Task WriteToFileAsync(string logLine)
    {
        try
        {
            // 使用异步写入，避免阻塞主线程
            await Task.Delay(50); // 短暂延迟，避免文件锁定
            
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
            // 文件写入失败不影响调试输出
            System.Diagnostics.Debug.WriteLine($"[文件错误] {ex.Message}");
        }
    }
    
    /// <summary>
    /// 写入错误日志
    /// </summary>
    public static void Error(string message, Exception? ex = null)
    {
        Write($"✗ {message}", "ERROR");
        if (ex != null)
        {
            Write($"类型：{ex.GetType().Name}", "ERROR");
            Write($"消息：{ex.Message}", "ERROR");
            Write($"堆栈：{ex.StackTrace}", "ERROR");
            if (ex.InnerException != null)
            {
                Write($"内部异常：{ex.InnerException.Message}", "ERROR");
            }
        }
    }
    
    /// <summary>
    /// 写入信息日志
    /// </summary>
    public static void Info(string message)
    {
        Write(message, "INFO");
    }
    
    /// <summary>
    /// 写入警告日志
    /// </summary>
    public static void Warning(string message)
    {
        Write(message, "WARN");
    }
    
    /// <summary>
    /// 写入调试日志
    /// </summary>
    public static void Debug(string message)
    {
        Write(message, "DEBUG");
    }
    
    /// <summary>
    /// 写入步骤日志
    /// </summary>
    public static void Step(string step, string message)
    {
        Write($"{step} {message}", "STEP");
    }
}
