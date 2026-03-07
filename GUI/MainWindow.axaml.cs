using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SteamContribution;

public partial class MainWindow : Window
{
    private MainWindowViewModel _viewModel;
    
    public MainWindow()
    {
        Logger.Debug("[MainWindow] 开始构造函数...");
        try
        {
            Logger.Debug("[MainWindow] 正在初始化组件...");
            InitializeComponent();
            Logger.Debug("[MainWindow] ✓ 组件初始化完成");
            
            Logger.Debug("[MainWindow] 正在创建 ViewModel...");
            _viewModel = new MainWindowViewModel();
            Logger.Debug("[MainWindow] ✓ ViewModel 创建成功");
            
            Logger.Debug("[MainWindow] 正在设置 DataContext...");
            DataContext = _viewModel;
            Logger.Debug("[MainWindow] ✓ DataContext 设置完成");
            
            Logger.Debug("[MainWindow] ✓ 主窗口构造完成");
        }
        catch (Exception ex)
        {
            Logger.Error("[MainWindow] 构造函数失败", ex);
            throw;
        }
    }
    
    private void InitializeComponent()
    {
        Logger.Debug("[MainWindow] 开始加载 XAML...");
        try
        {
            Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
            Logger.Debug("[MainWindow] ✓ XAML 加载完成");
        }
        catch (Exception ex)
        {
            Logger.Error("[MainWindow] XAML 加载失败", ex);
            throw;
        }
    }
}
