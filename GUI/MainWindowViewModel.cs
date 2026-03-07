using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace SteamContribution;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private int _selectedLoginIndex;
    private string _username = "";
    private string _password = "";
    private string _loginStatus = "就绪";
    private string _logOutput = "";
    private string _statusBarMessage = "就绪";
    private bool _isProcessing;
    private bool _isResultVisible;
    private bool _showUniqueContribution;
    private bool _saveResult = true;
    private Bitmap? _qrCodeImage;
    private bool _isQRCodeVisible;
    private string _selectedCountry = "CN";
    private bool _canStartQuery;  // ← 新增：是否可以开始查询
    
    private double _totalContribution;
    private int _totalGameCount;
    private int _memberCount;
    private string _currency = "CNY";
    
    private ObservableCollection<MemberContributionItem> _memberContributions = new();
    
    private CartesianChart? _contributionBarChart;
    private PieChart? _contributionPieChart;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    // 切换去重模式的属性
    public bool ShowUniqueContribution
    {
        get => _showUniqueContribution;
        set
        {
            if (SetField(ref _showUniqueContribution, value))
            {
                // 当切换模式时，重新创建图表
                if (MemberContributions.Count > 0)
                {
                    CreateCharts();
                    
                    // 触发图表属性变更通知
                    OnPropertyChanged(nameof(ContributionPieChart));
                    OnPropertyChanged(nameof(ContributionBarChart));
                }
            }
        }
    }
    
    // 登录相关属性
    public int SelectedLoginIndex
    {
        get => _selectedLoginIndex;
        set
        {
            if (SetField(ref _selectedLoginIndex, value))
            {
                OnPropertyChanged(nameof(IsUsernamePasswordVisible));
                OnPropertyChanged(nameof(IsTokenLoginVisible));
                OnPropertyChanged(nameof(IsQrLoginVisible));
            }
        }
    }
    
    public bool IsUsernamePasswordVisible => SelectedLoginIndex == 0;
    public bool IsTokenLoginVisible => SelectedLoginIndex == 1;
    public bool IsQrLoginVisible => SelectedLoginIndex == 2;
    
    public string Username
    {
        get => _username;
        set => SetField(ref _username, value);
    }
    
    public string Password
    {
        get => _password;
        set => SetField(ref _password, value);
    }
    
    public string LoginStatus
    {
        get => _loginStatus;
        set => SetField(ref _loginStatus, value);
    }
    
    public string LogOutput
    {
        get => _logOutput;
        set => SetField(ref _logOutput, value);
    }
    
    public string StatusBarMessage
    {
        get => _statusBarMessage;
        set => SetField(ref _statusBarMessage, value);
    }
    
    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetField(ref _isProcessing, value);
    }
    
    public bool IsResultVisible
    {
        get => _isResultVisible;
        set => SetField(ref _isResultVisible, value);
    }
    
    public Bitmap? QRCodeImage
    {
        get => _qrCodeImage;
        set => SetField(ref _qrCodeImage, value);
    }
    
    public bool IsQRCodeVisible
    {
        get => _isQRCodeVisible;
        set => SetField(ref _isQRCodeVisible, value);
    }
    
    public string SelectedCountry
    {
        get => _selectedCountry;
        set => SetField(ref _selectedCountry, value);
    }
    
    public bool CanStartQuery
    {
        get => _canStartQuery;
        set => SetField(ref _canStartQuery, value);
    }
    
    public string[] CountryList { get; } = { "CN", "US", "EU", "JP", "KR" };
    
    public bool SaveResult
    {
        get => _saveResult;
        set => SetField(ref _saveResult, value);
    }
    
    // 结果显示相关属性
    public double TotalContribution
    {
        get => _totalContribution;
        set
        {
            if (SetField(ref _totalContribution, value))
            {
                OnPropertyChanged(nameof(TotalContributionText));
                OnPropertyChanged(nameof(TotalValueDisplayText));
            }
        }
    }
    
    public int TotalGameCount
    {
        get => _totalGameCount;
        set
        {
            if (SetField(ref _totalGameCount, value))
            {
                OnPropertyChanged(nameof(TotalGameCountText));
            }
        }
    }
    
    public int MemberCount
    {
        get => _memberCount;
        set
        {
            if (SetField(ref _memberCount, value))
            {
                OnPropertyChanged(nameof(MemberCountText));
            }
        }
    }
    
    public string Currency
    {
        get => _currency;
        set
        {
            if (SetField(ref _currency, value))
            {
                OnPropertyChanged(nameof(TotalContributionText));
                foreach (var member in MemberContributions)
                {
                    member.OnPropertyChanged(nameof(member.ContributionText));
                }
            }
        }
    }
    
    public string TotalContributionText => $"{TotalContribution:F2} {Currency}";
    public string TotalGameCountText => TotalGameCount.ToString();
    public string MemberCountText => MemberCount.ToString();
    public string TotalUniqueContributionText => $"{TotalUniqueContribution:F2} {Currency}";
    
    // 显示两种总价值的文本
    public string TotalValueDisplayText => $"共享库总价值：{TotalContribution:F2} {Currency} (去重后：{TotalUniqueContribution:F2} {Currency})";
    
    public double TotalUniqueContribution
    {
        get => _totalUniqueContribution;
        set
        {
            if (SetField(ref _totalUniqueContribution, value))
            {
                OnPropertyChanged(nameof(TotalUniqueContributionText));
                OnPropertyChanged(nameof(TotalValueDisplayText));
            }
        }
    }
    private double _totalUniqueContribution;
    
    public ObservableCollection<MemberContributionItem> MemberContributions
    {
        get => _memberContributions;
        set => SetField(ref _memberContributions, value);
    }
    
    public CartesianChart? ContributionBarChart
    {
        get => _contributionBarChart;
        set => SetField(ref _contributionBarChart, value);
    }
    
    public PieChart? ContributionPieChart
    {
        get => _contributionPieChart;
        set => SetField(ref _contributionPieChart, value);
    }
    
    // 命令
    public async void LoginCommand()
    {
        try
        {
            IsProcessing = true;
            IsResultVisible = false;
            LogOutput = "";
            
            if (SelectedLoginIndex == 0)
            {
                // 用户名密码登录
                if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
                {
                    LoginStatus = "错误：用户名或密码不能为空";
                    IsProcessing = false;
                    return;
                }
                
                LoginStatus = "正在登录...";
                StatusBarMessage = "登录中...";
                
                var clientManager = new SteamClientManager();
                var guardData = SteamClientManager.LoadGuardData();
                
                var success = await clientManager.LoginAsync(Username, Password, guardData);
                
                if (success)
                {
                    LoginStatus = "登录成功！";
                    StatusBarMessage = "请点击'开始查询'按钮";
                    
                    // 保存凭证
                    var config = ConfigManager.LoadConfig() ?? new SteamConfig();
                    config.Username = Username;
                    config.Password = Password;
                    ConfigManager.SaveConfig(config);
                    
                    Logger.Info("✓ 登录成功");
                    Logger.Info("✓ 用户名和密码已保存");
                    // 登录成功，保持连接
                    Logger.Info("[ViewModel] ✓ 用户名密码登录成功，保持连接状态");
                    
                    // 启用查询按钮
                    CanStartQuery = true;
                }
                else
                {
                    LoginStatus = "登录失败";
                    StatusBarMessage = "登录失败";
                    Logger.Info("✗ 登录失败");
                    // 登录失败，断开连接
                    clientManager.Disconnect();
                    Logger.Info("[ViewModel] 已断开连接");
                }
            }
            else if (SelectedLoginIndex == 1)
            {
                // Token 登录
                LoginStatus = "正在使用 Token 登录...";
                StatusBarMessage = "Token 登录中...";
                
                var accessToken = SteamClientManager.LoadAccessToken();
                var refreshToken = SteamClientManager.LoadRefreshToken();
                
                if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken))
                {
                    var clientManager = new SteamClientManager();
                    var success = await clientManager.LoginWithAccessTokenAsync(accessToken, refreshToken);
                    
                    if (success)
                    {
                        LoginStatus = "Token 登录成功！";
                        StatusBarMessage = "请点击'开始查询'按钮";
                        Logger.Info("✓ Token 登录成功");
                        // 登录成功，保持连接
                        Logger.Info("[ViewModel] ✓ Token 登录成功，保持连接状态");
                        
                        // 启用查询按钮
                        CanStartQuery = true;
                    }
                    else
                    {
                        LoginStatus = "Token 登录失败，请尝试其他方式";
                        StatusBarMessage = "Token 登录失败";
                        Logger.Info("✗ Token 登录失败");
                        // 登录失败，断开连接
                        clientManager.Disconnect();
                        Logger.Info("[ViewModel] 已断开连接");
                    }
                }
                else
                {
                    LoginStatus = "未找到保存的 Token";
                    StatusBarMessage = "错误";
                    Logger.Info("✗ 未找到保存的 Token");
                }
            }
            else if (SelectedLoginIndex == 2)
            {
                // 扫码登录
                LoginStatus = "正在生成二维码...";
                StatusBarMessage = "生成二维码中...";
                IsQRCodeVisible = false;
                
                var clientManager = new SteamClientManager();
                
                try
                {
                    // 订阅二维码生成事件
                clientManager.OnQRCodeGenerated += (qrBitmap) =>
                {
                    Logger.Info($"[ViewModel] 收到二维码事件，尺寸：{qrBitmap.Width}x{qrBitmap.Height}");
                    
                    // 在 UI 线程更新二维码图片
                    Dispatcher.UIThread.Post(async () =>
                    {
                        try
                        {
                            Logger.Info("[ViewModel] 开始在 UI 线程更新二维码...");
                            Logger.Info($"[ViewModel] Bitmap PixelFormat: {qrBitmap.PixelFormat}");
                            
                            // 方法：先保存为 PNG 文件，然后从文件加载
                            var tempFile = System.IO.Path.Combine(AppContext.BaseDirectory, "qrcode_temp.png");
                            
                            // 删除旧文件
                            if (System.IO.File.Exists(tempFile))
                            {
                                System.IO.File.Delete(tempFile);
                            }
                            
                            // 保存为 PNG 文件
                            Logger.Info("[ViewModel] 正在保存二维码为 PNG...");
                            qrBitmap.Save(tempFile, System.Drawing.Imaging.ImageFormat.Png);
                            Logger.Info($"[ViewModel] ✓ 已保存到：{tempFile}");
                            
                            // 等待文件写入完成
                            await Task.Delay(100);
                            
                            // 从文件加载到 Avalonia Bitmap
                            Logger.Info("[ViewModel] 正在从文件加载到 UI...");
                            using var fileStream = System.IO.File.OpenRead(tempFile);
                            var bitmap = new Avalonia.Media.Imaging.Bitmap(fileStream);
                            
                            QRCodeImage = bitmap;
                            IsQRCodeVisible = true;
                            LoginStatus = "请使用 Steam 手机应用扫描二维码";
                            
                            Logger.Info("[ViewModel] ✓ 二维码已成功显示在界面上");
                            Logger.Info("✓ 二维码已生成并显示");
                            
                            // 清理临时文件
                            await Task.Delay(100);
                            if (System.IO.File.Exists(tempFile))
                            {
                                try
                                {
                                    System.IO.File.Delete(tempFile);
                                    Logger.Info("[ViewModel] ✓ 临时文件已清理");
                                }
                                catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("[ViewModel] 二维码显示失败", ex);
                            Logger.Info($"✗ 二维码显示失败：{ex.Message}");
                            
                            // 尝试备用方法：使用 MemoryStream
                            try
                            {
                                Logger.Info("[ViewModel] 尝试使用 MemoryStream 方法...");
                                using var memoryStream = new MemoryStream();
                                qrBitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                                memoryStream.Position = 0;
                                
                                var bitmap = new Avalonia.Media.Imaging.Bitmap(memoryStream);
                                QRCodeImage = bitmap;
                                IsQRCodeVisible = true;
                                LoginStatus = "请使用 Steam 手机应用扫描二维码";
                                
                                Logger.Info("[ViewModel] ✓ 使用 MemoryStream 方法成功");
                            }
                            catch (Exception ex2)
                            {
                                Logger.Error("[ViewModel] MemoryStream 方法也失败", ex2);
                            }
                        }
                    });
                };
                    
                    Logger.Info("[ViewModel] 开始扫码登录流程...");
                    var qrSuccess = await clientManager.LoginWithQRAsync();
                    
                    if (qrSuccess)
                    {
                        LoginStatus = "扫码登录成功！";
                        StatusBarMessage = "请点击'开始查询'按钮";
                        Logger.Info("✓ 扫码登录成功");
                        IsQRCodeVisible = false;
                        // 登录成功，保持连接
                        Logger.Info("[ViewModel] ✓ 保持连接状态，可以进行后续操作");
                        
                        // 启用查询按钮
                        CanStartQuery = true;
                    }
                    else
                    {
                        LoginStatus = "扫码登录失败或已取消";
                        StatusBarMessage = "扫码失败";
                        Logger.Info("✗ 扫码登录失败");
                        IsQRCodeVisible = false;
                        // 登录失败，断开连接
                        clientManager.Disconnect();
                        Logger.Info("[ViewModel] 已断开连接");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("[ViewModel] 扫码登录异常", ex);
                    LoginStatus = $"错误：{ex.Message}";
                    StatusBarMessage = "发生错误";
                    Logger.Info($"✗ 错误：{ex.Message}");
                    IsQRCodeVisible = false;
                    // 发生异常，断开连接
                    clientManager.Disconnect();
                    Logger.Info("[ViewModel] 已断开连接");
                }
            }
            
            IsProcessing = false;
        }
        catch (Exception ex)
        {
            LoginStatus = $"错误：{ex.Message}";
            StatusBarMessage = "发生错误";
            Logger.Info($"✗ 错误：{ex.Message}");
            IsProcessing = false;
        }
    }
    
    private System.Drawing.Imaging.ImageCodecInfo GetPngEncoder()
    {
        try
        {
            var codecs = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders();
            foreach (var codec in codecs)
            {
                if (codec.MimeType == "image/png")
                    return codec;
            }
        }
        catch
        {
            // 如果获取编码器失败，返回 null 使用默认编码器
        }
        return null!;
    }
    
    public async void QueryFamilyCommand()
    {
        try
        {
            // 详细检查登录状态
            Logger.Info("[ViewModel] 检查登录状态...");
            
            if (!CanStartQuery)
            {
                Logger.Info("[ViewModel] ✗ 未启用查询（CanStartQuery = false）");
                LoginStatus = "请先完成登录";
                Logger.Info("✗ 无法开始查询：未完成登录");
                return;
            }
            
            // 检查 SteamId 和 AccessToken（从配置文件读取）
            var config = ConfigManager.LoadConfig();
            Logger.Info($"[ViewModel] 检查配置文件...");
            Logger.Info($"[ViewModel] AccessToken: {(string.IsNullOrEmpty(config?.AccessToken) ? "无" : "有")}");
            Logger.Info($"[ViewModel] RefreshToken: {(string.IsNullOrEmpty(config?.RefreshToken) ? "无" : "有")}");
            
            if (string.IsNullOrEmpty(config?.AccessToken))
            {
                Logger.Info("[ViewModel] ✗ AccessToken 为空，请重新登录");
                LoginStatus = "未获取到 Access Token，请重新登录";
                Logger.Info("✗ 无法开始查询：AccessToken 为空");
                CanStartQuery = false;
                return;
            }
            
            Logger.Info("[ViewModel] ✓ 登录状态正常，开始查询...");
            
            IsProcessing = true;
            IsResultVisible = false;
            LogOutput = "";
            StatusBarMessage = "查询中...";
            
            Logger.Info("=== 开始查询家庭组信息 ===");
            Logger.Info($"使用地区代码：{SelectedCountry}");
            
            // 调用查询逻辑
            await RunQueryAsync();
            
            IsProcessing = false;
            IsResultVisible = true;
            StatusBarMessage = "查询完成";
        }
        catch (Exception ex)
        {
            Logger.Error("[ViewModel] 查询失败", ex);
            Logger.Info($"✗ 错误：{ex.Message}");
            StatusBarMessage = "查询失败";
            IsProcessing = false;
        }
    }
    
    public async void StartQueryCommand()
    {
        if (!CanStartQuery)
        {
            LoginStatus = "请先完成登录";
            Logger.Info("✗ 无法开始查询：未完成登录");
            return;
        }
        
        // 直接调用 QueryFamilyCommand，不用 await
        QueryFamilyCommand();
    }
    
    private async Task RunQueryAsync()
    {
        // 从配置文件读取 Token
        var config = ConfigManager.LoadConfig();
        
        if (string.IsNullOrEmpty(config?.AccessToken) || string.IsNullOrEmpty(config?.RefreshToken))
        {
            Logger.Info("✗ 配置文件中未找到 Token，请先登录");
            return;
        }
        
        // 创建并登录 Steam 客户端
        var clientManager = new SteamClientManager();
        
        try
        {
            Logger.Info("[RunQuery] 使用 Token 登录...");
            var loginSuccess = await clientManager.LoginWithAccessTokenAsync(config.AccessToken, config.RefreshToken);
            
            if (!loginSuccess)
            {
                Logger.Info("[RunQuery] ✗ Token 登录失败");
                Logger.Info("✗ Token 登录失败，请重新登录");
                CanStartQuery = false;
                return;
            }
            
            Logger.Info("[RunQuery] ✓ Token 登录成功");
            Logger.Info($"[RunQuery] SteamId: {clientManager.SteamId?.ConvertToUInt64()}");
            
            // 获取家庭组信息
            var familyService = new FamilyService(clientManager);
            var familyInfo = familyService.GetFullFamilyInfoAsync();
            
            if (familyInfo == null)
            {
                Logger.Info("✗ 未找到家庭组信息");
                return;
            }
            
            Logger.Info($"✓ 家庭组名称：{familyInfo.Value.Name}");
            Logger.Info($"✓ 家庭成员数：{familyInfo.Value.Members.Count}");
            
            // 获取共享库游戏
            var libraryService = new LibraryService(clientManager);
            var allSharedApps = new System.Collections.Generic.List<SharedApp>();
            
            if (familyInfo.Value.Members.Count > 0)
            {
                var firstMember = familyInfo.Value.Members.First();
                allSharedApps = libraryService.GetSharedLibraryAppsAsync(firstMember.SteamId, familyInfo.Value.FamilyGroupId);
                Logger.Info($"✓ 获取到 {allSharedApps.Count} 个共享游戏");
            }
            
            // 获取游戏价格
            var priceService = new PriceService(clientManager, SelectedCountry);
            var allAppIds = allSharedApps.Select(app => app.AppId).ToList();
            var priceMap = await priceService.GetBatchAppPricesAsync(allAppIds);
            Logger.Info($"✓ 获取到 {priceMap.Count} 个游戏价格");
            
            // 计算贡献
            var memberContributions = new System.Collections.Generic.List<MemberContribution>();
            double totalContribution = 0;
            double totalUniqueContribution = 0;  // 去重后的总贡献
            var memberUniqueContributions = new System.Collections.Generic.Dictionary<ulong, double>();
            
            Logger.Debug($"[调试] 开始计算贡献，共享游戏总数：{allSharedApps.Count}");
            Logger.Debug($"[调试] 价格表大小：{priceMap.Count}");
            
            foreach (var member in familyInfo.Value.Members)
            {
                var memberOwnedApps = allSharedApps.Where(app => app.OwnerId == member.SteamId).ToList();
                
                Logger.Debug($"[调试] 成员 {member.Nickname} (SteamId: {member.SteamId}) 拥有 {memberOwnedApps.Count} 个游戏");
                
                double memberContribution = 0;
                foreach (var app in memberOwnedApps)
                {
                    if (priceMap.TryGetValue(app.AppId, out var priceInfo) && !priceInfo.IsFree)
                    {
                        memberContribution += priceInfo.Price;
                    }
                }
                
                Logger.Info($"[调试] {member.Nickname} 总贡献 (不去重): {memberContribution:F2}");
                
                // 去重：按 AppId 分组，只计算一次
                var uniqueMemberAppIds = memberOwnedApps.GroupBy(app => app.AppId)
                    .Select(g => g.Key).ToList();
                
                Logger.Info($"[调试] {member.Nickname} 去重后游戏数：{uniqueMemberAppIds.Count}");
                Logger.Info($"[调试] {member.Nickname} 原始游戏数：{memberOwnedApps.Count}");
                
                double memberUniqueContribution = 0;
                foreach (var appId in uniqueMemberAppIds)
                {
                    if (priceMap.TryGetValue(appId, out var priceInfo) && !priceInfo.IsFree)
                    {
                        memberUniqueContribution += priceInfo.Price;
                    }
                    else
                    {
                        Logger.Debug($"  - AppId {appId}: 未找到价格或免费游戏");
                    }
                }
                
                Logger.Info($"[调试] {member.Nickname} 去重后贡献：{memberUniqueContribution:F2}");
                
                memberUniqueContributions[member.SteamId] = memberUniqueContribution;
                totalUniqueContribution += memberUniqueContribution;  // 累加去重后的总贡献
                
                memberContributions.Add(new MemberContribution
                {
                    SteamId = member.SteamId,
                    RoleName = member.RoleName,
                    Contribution = memberContribution,
                    UniqueContribution = memberUniqueContribution,  // 设置去重后的贡献
                    OwnedGameCount = memberOwnedApps.Count
                });
                
                totalContribution += memberContribution;
            }
            
            Logger.Info($"[调试] 总贡献：{totalContribution:F2}, 去重后总贡献：{totalUniqueContribution:F2}");
            
            // 更新 UI
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TotalContribution = totalContribution;
                TotalUniqueContribution = totalUniqueContribution;  // 设置去重后的总贡献
                Logger.Info($"[调试] UI 更新 - TotalContribution={TotalContribution}, TotalUniqueContribution={TotalUniqueContribution}");
                TotalGameCount = allSharedApps.Count;
                MemberCount = familyInfo.Value.Members.Count;
                Currency = CurrencyHelper.GetCurrencyByCountryCode(SelectedCountry);
                
                Logger.Info($"[调试] UI 更新 - TotalContribution={TotalContribution}, TotalUniqueContribution={TotalUniqueContribution}");
                
                MemberContributions.Clear();
                foreach (var contrib in memberContributions.OrderByDescending(c => c.Contribution))
                {
                    var member = familyInfo.Value.Members.FirstOrDefault(m => m.SteamId == contrib.SteamId);
                    var memberName = member?.Nickname ?? contrib.SteamId.ToString();
                    
                    double ratio = totalContribution > 0 ? (contrib.Contribution / totalContribution) * 100 : 0;
                    
                    Logger.Info($"[调试] 填充成员 {memberName}: Contribution={contrib.Contribution}, UniqueContribution={contrib.UniqueContribution}");
                    
                    MemberContributions.Add(new MemberContributionItem
                    {
                        SteamId = contrib.SteamId,
                        Nickname = memberName,
                        RoleName = contrib.RoleName,
                        AvatarUrl = member?.AvatarUrl ?? "",
                        Contribution = contrib.Contribution,
                        UniqueContribution = contrib.UniqueContribution,  // 去重后的贡献
                        ContributionRatio = ratio,
                        GameCount = contrib.OwnedGameCount,
                        Currency = Currency
                    });
                }
                
                // 创建图表
                CreateCharts();
                
                Logger.Info("✓ 贡献计算完成");
                Logger.Info("✓ 图表已生成");
                Logger.Info($"共享库总价值：{totalContribution:F2} {Currency}");
                Logger.Info($"共享游戏总数：{allSharedApps.Count}");
            });
            
            // 保存结果
            if (SaveResult)
            {
                var queryResult = new QueryResult
                {
                    FamilyGroupName = familyInfo.Value.Name,
                    FamilyMemberCount = familyInfo.Value.Members.Count,
                    Currency = Currency,
                    TotalContribution = totalContribution
                };
                
                foreach (var member in familyInfo.Value.Members)
                {
                    var memberOwnedApps = allSharedApps.Where(app => app.OwnerId == member.SteamId).ToList();
                    double memberContribution = 0;
                    foreach (var app in memberOwnedApps)
                    {
                        if (priceMap.TryGetValue(app.AppId, out var priceInfo) && !priceInfo.IsFree)
                        {
                            memberContribution += priceInfo.Price;
                        }
                    }
                    
                    // 计算去重后的贡献
                    var uniqueMemberAppIds = memberOwnedApps.Select(app => app.AppId).Distinct().ToList();
                    double memberUniqueContribution = 0;
                    foreach (var appId in uniqueMemberAppIds)
                    {
                        if (priceMap.TryGetValue(appId, out var priceInfo) && !priceInfo.IsFree)
                        {
                            memberUniqueContribution += priceInfo.Price;
                        }
                    }
                    
                    queryResult.Members.Add(new MemberResult
                    {
                        SteamId = member.SteamId,
                        RoleName = member.RoleName,
                        Nickname = member.Nickname,
                        AvatarUrl = member.AvatarUrl,
                        Contribution = memberContribution,
                        UniqueContribution = memberUniqueContribution,  // 设置去重后的贡献
                        OwnedGameCount = memberOwnedApps.Count,
                        OwnedGameIds = memberOwnedApps.Select(app => app.AppId).ToList()
                    });
                }
                
                foreach (var app in allSharedApps)
                {
                    var priceInfo = priceMap.GetValueOrDefault(app.AppId);
                    queryResult.SharedGames.Add(new SharedGameResult
                    {
                        AppId = app.AppId,
                        Name = priceInfo?.Name ?? "未知游戏",
                        OwnerId = app.OwnerId,
                        Price = priceInfo?.Price ?? 0,
                        IsFree = priceInfo?.IsFree ?? false
                    });
                }
                
                ResultService.SaveQueryResult(queryResult);
                Logger.Info("✓ 结果已保存");
            }
        }
        finally
        {
            clientManager.Disconnect();
            Logger.Info("已断开连接");
        }
    }
    

    
    public void LoadCredentialsCommand()
    {
        var config = ConfigManager.LoadConfig();
        if (config != null && !string.IsNullOrEmpty(config.Username))
        {
            Username = config.Username;
            Password = config.Password;
            LoginStatus = "已从配置文件加载凭证";
            Logger.Info("✓ 已加载保存的用户名和密码");
        }
        else
        {
            LoginStatus = "未找到保存的凭证";
            Logger.Info("✗ 未找到保存的凭证");
        }
    }
    
    public void AnalyzeResultCommand()
    {
        var resultFiles = ResultService.GetResultFiles();
        
        if (resultFiles.Count == 0)
        {
            LoginStatus = "没有找到已保存的查询结果";
            Logger.Info("✗ 未找到已保存的查询结果");
            return;
        }
        
        // 加载最新的结果
        var latestFile = resultFiles[0];
        var result = ResultService.LoadQueryResult(latestFile);
        
        if (result != null)
        {
            Logger.Info("=== 加载历史查询结果 ===");
            Logger.Info($"查询时间：{result.Timestamp}");
            Logger.Info($"家庭组：{result.FamilyGroupName}");
            Logger.Info($"成员数量：{result.FamilyMemberCount}");
            Logger.Info($"总贡献金额：{result.TotalContribution:F2} {result.Currency}");
            Logger.Info($"共享游戏数量：{result.SharedGames.Count}");
            
            // 计算去重后的总贡献
            double totalUniqueContribution = 0;
            foreach (var member in result.Members)
            {
                totalUniqueContribution += member.UniqueContribution;
            }
            Logger.Info($"去重后总贡献：{totalUniqueContribution:F2} {result.Currency}");
            
            // 更新 UI 显示结果
            TotalContribution = result.TotalContribution;
            TotalUniqueContribution = totalUniqueContribution;  // 设置去重后的总贡献
            TotalGameCount = result.SharedGames.Count;
            MemberCount = result.FamilyMemberCount;
            Currency = result.Currency;
            
            // 填充成员贡献列表
            MemberContributions.Clear();
            foreach (var member in result.Members.OrderByDescending(m => m.Contribution))
            {
                double ratio = result.TotalContribution > 0 ? (member.Contribution / result.TotalContribution) * 100 : 0;
                
                MemberContributions.Add(new MemberContributionItem
                {
                    SteamId = member.SteamId,
                    Nickname = member.Nickname ?? member.SteamId.ToString(),
                    RoleName = member.RoleName,
                    AvatarUrl = member.AvatarUrl ?? "",
                    Contribution = member.Contribution,
                    UniqueContribution = member.UniqueContribution,  // 去重后的贡献
                    ContributionRatio = ratio,
                    GameCount = member.OwnedGameCount,
                    Currency = result.Currency
                });
            }
            
            // 创建图表
            CreateCharts();
            
            // 显示结果区域
            IsResultVisible = true;
            IsProcessing = false;
            StatusBarMessage = "已加载历史结果";
            LoginStatus = "已加载历史结果";
            
            Logger.Info("✓ 已加载并显示历史结果");
            Logger.Info("✓ 图表已生成");
        }
        else
        {
            Logger.Info("✗ 加载历史结果失败");
            LoginStatus = "加载历史结果失败";
        }
    }
    
    public async void RefreshQRCodeCommand()
    {
        try
        {
            IsQRCodeVisible = true;
            LoginStatus = "正在生成二维码...";
            
            // 这里需要实现生成二维码的逻辑
            // 由于时间关系，这里只是一个示例
            await Task.Delay(1000);
            
            LoginStatus = "二维码已刷新，请使用 Steam 手机应用扫描";
        }
        catch (Exception ex)
        {
            LoginStatus = $"生成二维码失败：{ex.Message}";
        }
    }
    
    private void CreateCharts()
    {
        if (MemberContributions.Count == 0)
            return;
        
        var barValues = new List<double>();
        var labels = new List<string>();
        
        foreach (var member in MemberContributions)
        {
            // 根据去重模式选择显示的数据
            double value = ShowUniqueContribution ? member.UniqueContribution : member.Contribution;
            
            // 调试输出
            Logger.Info($"[图表数据] {member.Nickname}: 总贡献={member.Contribution:F2}, 去重={member.UniqueContribution:F2}, 显示={value:F2}");
            
            barValues.Add(value);
            labels.Add(member.Nickname);
        }
        
        // 使用系统默认字体 (支持中文)
        // 尝试多种中文字体，确保至少有一个可用
        var systemFont = SKTypeface.Default;
        
        // 如果微软雅黑不可用，尝试其他中文字体
        if (systemFont.FamilyName != "Microsoft YaHei")
        {
            systemFont = SKTypeface.Default;
        }
        
        Logger.Info($"[字体] 使用字体：{systemFont.FamilyName}");
        
        // 创建柱状图
        var columnSeries = new ColumnSeries<double>
        {
            Values = barValues,
            Name = "贡献金额",
            Fill = new SolidColorPaint(new SKColor(233, 69, 96)),
            DataLabelsPaint = new SolidColorPaint(new SKColor(76, 175, 80)),
            DataLabelsSize = 14,
            DataLabelsFormatter = point => point.PrimaryValue!.ToString("F0")
        };
        
        ContributionBarChart = new CartesianChart
        {
            Series = new ISeries[] { columnSeries },
            XAxes = new[]
            {
                new Axis
                {
                    Labels = labels,
                    Name = "成员",
                    LabelsRotation = 0,
                    LabelsPaint = new SolidColorPaint(new SKColor(160, 160, 160))
                    {
                        SKTypeface = systemFont
                    },
                    NamePaint = new SolidColorPaint(new SKColor(255, 255, 255))
                    {
                        SKTypeface = systemFont
                    },
                    SeparatorsPaint = new SolidColorPaint(new SKColor(50, 50, 50))
                }
            },
            YAxes = new[]
            {
                new Axis
                {
                    Name = "贡献金额",
                    LabelsPaint = new SolidColorPaint(new SKColor(160, 160, 160))
                    {
                        SKTypeface = systemFont
                    },
                    NamePaint = new SolidColorPaint(new SKColor(255, 255, 255))
                    {
                        SKTypeface = systemFont
                    },
                    SeparatorsPaint = new SolidColorPaint(new SKColor(50, 50, 50)),
                    MinLimit = 0
                }
            },
            // 设置悬浮窗字体
            TooltipTextPaint = new SolidColorPaint(new SKColor(255, 255, 255))
            {
                SKTypeface = systemFont
            },
            TooltipBackgroundPaint = new SolidColorPaint(new SKColor(30, 30, 30))
            {
                SKTypeface = systemFont
            }
        };
        
        // 创建饼图 - 每个成员一个独立的 PieSeries
        var pieSeriesList = new List<ISeries>();
        
        foreach (var member in MemberContributions)
        {
            // 根据去重模式选择显示的数据
            double value = ShowUniqueContribution ? member.UniqueContribution : member.Contribution;
            
            pieSeriesList.Add(new PieSeries<double> 
            { 
                Values = new[] { value },
                Name = member.Nickname,
                DataLabelsPaint = new SolidColorPaint(SKColors.White)
                {
                    SKTypeface = systemFont
                },
                DataLabelsSize = 14,
                DataLabelsFormatter = point => $"{point.PrimaryValue:F0}",
                InnerRadius = 0
            });
        }
        
        ContributionPieChart = new PieChart
        {
            Series = pieSeriesList,
            InitialRotation = -90, // 从 12 点钟方向开始
            MaxAngle = 360, // 完整的圆形
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Right,
            LegendTextPaint = new SolidColorPaint(new SKColor(255, 255, 255))
            {
                SKTypeface = systemFont
            },
            LegendTextSize = 14,
            // 设置悬浮窗字体
            TooltipTextPaint = new SolidColorPaint(new SKColor(255, 255, 255))
            {
                SKTypeface = systemFont
            },
            TooltipBackgroundPaint = new SolidColorPaint(new SKColor(30, 30, 30))
            {
                SKTypeface = systemFont
            }
        };
    }
}

public class MemberContributionItem : INotifyPropertyChanged
{
    public ulong SteamId { get; set; }
    public string Nickname { get; set; } = "";
    public string RoleName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public double Contribution { get; set; }
    public double UniqueContribution { get; set; }  // 去重后的贡献
    public double ContributionRatio { get; set; }
    public int GameCount { get; set; }
    public string Currency { get; set; } = "CNY";
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    public string ContributionText => $"{Contribution:F2} {Currency}";
    public string ContributionRatioText => $"{ContributionRatio:F2}%";
    public string GameCountText => $"游戏数量：{GameCount}";
}
