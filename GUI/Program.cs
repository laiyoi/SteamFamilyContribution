using Avalonia;
using System;

namespace SteamContribution;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            Logger.Initialize();
            
            Logger.Info("========================================");
            Logger.Info("=== Steam 家庭库贡献率查询工具 (GUI 版本) ===");
            Logger.Info("========================================");
            Logger.Info($"开始时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            Logger.Info($"工作目录：{Directory.GetCurrentDirectory()}");
            Logger.Info($"程序路径：{AppContext.BaseDirectory}");
            Logger.Info($"操作系统：{Environment.OSVersion}");
            Logger.Info($"CLR 版本：{Environment.Version}");
            Logger.Info($"命令行参数：{string.Join(" ", args)}");
            Logger.Info($"64 位系统：{Environment.Is64BitOperatingSystem}");
            Logger.Info($"处理器数量：{Environment.ProcessorCount}");
            Logger.Info($"内存：{GC.GetTotalMemory(false) / 1024.0 / 1024.0:F2} MB");
            Logger.Info("----------------------------------------");
            
            Logger.Step("1/5", "准备启动 Avalonia GUI...");
            
            Logger.Step("2/5", "配置 Avalonia 应用...");
            var appBuilder = BuildAvaloniaApp();
            Logger.Step("2/5", "✓ Avalonia 配置完成");
            
            Logger.Step("3/5", "初始化应用生命周期...");
            Logger.Step("3/5", "✓ 生命周期初始化完成");
            
            Logger.Step("4/5", "启动桌面应用...");
            Logger.Info("即将创建主窗口...");
            
            appBuilder.StartWithClassicDesktopLifetime(args);
            
            Logger.Step("5/5", "✓ 程序正常退出");
            Logger.Info("----------------------------------------");
            Logger.Info($"结束时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            Logger.Info("========================================");
        }
        catch (Exception ex)
        {
            Logger.Info("----------------------------------------");
            Logger.Error("发生异常！", ex);
            Logger.Info("----------------------------------------");
            Logger.Info($"程序异常退出，时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            Logger.Info("========================================");
            
            Console.WriteLine($"\n错误已记录到日志文件");
            Console.WriteLine($"错误类型：{ex.GetType().Name}");
            Console.WriteLine($"错误信息：{ex.Message}");
            Console.WriteLine("\n按 Enter 键退出...");
            Console.ReadLine();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        Logger.Info("[Avalonia] 开始配置...");
        
        try
        {
            Logger.Debug("[Avalonia] 创建 AppBuilder...");
            var builder = AppBuilder.Configure<App>();
            Logger.Debug("[Avalonia] ✓ AppBuilder 配置完成");
            
            Logger.Debug("[Avalonia] 配置平台检测...");
            builder = builder.UsePlatformDetect();
            Logger.Debug("[Avalonia] ✓ 平台检测已启用");
            
            Logger.Debug("[Avalonia] 配置 Inter 字体...");
            builder = builder.WithInterFont();
            Logger.Debug("[Avalonia] ✓ Inter 字体已加载");
            
            Logger.Debug("[Avalonia] 配置日志输出...");
            builder = builder.LogToTrace();
            Logger.Debug("[Avalonia] ✓ 日志已启用");
            
            Logger.Info("[Avalonia] 配置完成");
            return builder;
        }
        catch (Exception ex)
        {
            Logger.Error("[Avalonia] 配置失败", ex);
            throw;
        }
    }
}