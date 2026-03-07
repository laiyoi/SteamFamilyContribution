using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace SteamContribution;

public partial class App : Application
{
    public override void Initialize()
    {
        Logger.Info("[App] 开始初始化...");
        try
        {
            AvaloniaXamlLoader.Load(this);
            Logger.Info("[App] ✓ 初始化完成");
        }
        catch (Exception ex)
        {
            Logger.Error("[App] 初始化失败", ex);
            throw;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Logger.Info("[App] 框架初始化完成事件触发");
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Logger.Debug("[App] 检测到桌面应用生命周期");
                Logger.Debug("[App] 正在创建主窗口...");
                
                var mainWindow = new MainWindow();
                Logger.Debug("[App] ✓ 主窗口创建成功");
                
                desktop.MainWindow = mainWindow;
                Logger.Debug("[App] ✓ 主窗口已设置");
            }
            else
            {
                Logger.Warning($"[App] 未知的生命周期类型：{ApplicationLifetime?.GetType().Name}");
            }

            base.OnFrameworkInitializationCompleted();
            Logger.Info("[App] ✓ 框架初始化完成");
        }
        catch (Exception ex)
        {
            Logger.Error("[App] 框架初始化失败", ex);
            throw;
        }
    }
}
