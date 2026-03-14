using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
    private bool _canStartQuery;
    
    private double _totalContribution;
    private double _totalUniqueContribution;
    private int _totalGameCount;
    private int _memberCount;
    private string _currency = "CNY";
    
    private ObservableCollection<MemberContributionItem> _memberContributions = new();
    private CartesianChart? _contributionBarChart;
    private PieChart? _contributionPieChart;
    private MemberContributionItem? _selectedMember;
    private ObservableCollection<GameItem> _memberGames = new();
    private bool _isGameDetailVisible;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
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
    
    public string Username { get => _username; set => SetField(ref _username, value); }
    public string Password { get => _password; set => SetField(ref _password, value); }
    public string LoginStatus { get => _loginStatus; set => SetField(ref _loginStatus, value); }
    public string LogOutput { get => _logOutput; set => SetField(ref _logOutput, value); }
    public string StatusBarMessage { get => _statusBarMessage; set => SetField(ref _statusBarMessage, value); }
    public bool IsProcessing { get => _isProcessing; set => SetField(ref _isProcessing, value); }
    public bool IsResultVisible { get => _isResultVisible; set => SetField(ref _isResultVisible, value); }
    public Bitmap? QRCodeImage { get => _qrCodeImage; set => SetField(ref _qrCodeImage, value); }
    public bool IsQRCodeVisible { get => _isQRCodeVisible; set => SetField(ref _isQRCodeVisible, value); }
    public string SelectedCountry { get => _selectedCountry; set => SetField(ref _selectedCountry, value); }
    public bool CanStartQuery { get => _canStartQuery; set => SetField(ref _canStartQuery, value); }
    public string[] CountryList { get; } = { "CN", "US", "EU", "JP", "KR" };
    public bool SaveResult { get => _saveResult; set => SetField(ref _saveResult, value); }
    
    // 去重模式
    public bool ShowUniqueContribution
    {
        get => _showUniqueContribution;
        set
        {
            if (SetField(ref _showUniqueContribution, value) && MemberContributions.Count > 0)
            {
                CreateCharts();
            }
        }
    }
    
    // 结果显示属性
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
    
    public int TotalGameCount
    {
        get => _totalGameCount;
        set => SetField(ref _totalGameCount, value, nameof(TotalGameCountText));
    }
    
    public int MemberCount
    {
        get => _memberCount;
        set => SetField(ref _memberCount, value, nameof(MemberCountText));
    }
    
    public string Currency
    {
        get => _currency;
        set
        {
            if (SetField(ref _currency, value))
            {
                OnPropertyChanged(nameof(TotalContributionText));
                OnPropertyChanged(nameof(TotalUniqueContributionText));
                foreach (var member in MemberContributions)
                    member.OnPropertyChanged(nameof(member.ContributionText));
            }
        }
    }
    
    public string TotalContributionText => $"{TotalContribution:F2} {Currency}";
    public string TotalUniqueContributionText => $"{TotalUniqueContribution:F2} {Currency}";
    public string TotalGameCountText => TotalGameCount.ToString();
    public string MemberCountText => MemberCount.ToString();
    public string TotalValueDisplayText => $"共享库总价值：{TotalContribution:F2} {Currency} (去重后：{TotalUniqueContribution:F2} {Currency})";
    
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
    
    public MemberContributionItem? SelectedMember
    {
        get => _selectedMember;
        set
        {
            if (SetField(ref _selectedMember, value))
            {
                OnPropertyChanged(nameof(SelectedMember));
                LoadMemberGames();
            }
        }
    }
    
    public ObservableCollection<GameItem> MemberGames
    {
        get => _memberGames;
        set => SetField(ref _memberGames, value);
    }
    
    public bool IsGameDetailVisible
    {
        get => _isGameDetailVisible;
        set => SetField(ref _isGameDetailVisible, value);
    }
    
    private Dictionary<uint, GameInfo> _allGamesMap = new();
    
    private void LoadMemberGames()
    {
        MemberGames.Clear();
        IsGameDetailVisible = false;
        
        if (SelectedMember == null || _allGamesMap.Count == 0)
            return;
        
        IsGameDetailVisible = true;
        
        foreach (var appId in SelectedMember.OwnedGameIds)
        {
            if (_allGamesMap.TryGetValue(appId, out var gameInfo))
            {
                MemberGames.Add(new GameItem
                {
                    AppId = appId,
                    Name = gameInfo.Name,
                    Price = gameInfo.Price,
                    IsFree = gameInfo.IsFree,
                    ImageUrl = $"https://cdn.steamstatic.com/steam/apps/{appId}/header.jpg"
                });
            }
        }
    }
    
    public void SelectMemberCommand(MemberContributionItem? member)
    {
        foreach (var m in MemberContributions)
        {
            m.IsSelected = m == member;
        }
        SelectedMember = member;
    }
    
    // 登录命令
    public async void LoginCommand()
    {
        try
        {
            IsProcessing = true;
            IsResultVisible = false;
            LogOutput = "";
            
            var clientManager = new SteamClientManager();
            bool success = SelectedLoginIndex switch
            {
                0 => await LoginWithUsernamePasswordAsync(clientManager),
                1 => await LoginWithTokenAsync(clientManager),
                2 => await LoginWithQRAsync(clientManager),
                _ => false
            };
            
            if (success)
            {
                LoginStatus = "登录成功！";
                StatusBarMessage = "请点击'开始查询'按钮";
                CanStartQuery = true;
                Logger.Info("✓ 登录成功");
            }
            else
            {
                LoginStatus = "登录失败";
                StatusBarMessage = "登录失败";
                clientManager.Disconnect();
                Logger.Info("✗ 登录失败");
            }
        }
        catch (Exception ex)
        {
            LoginStatus = $"错误：{ex.Message}";
            StatusBarMessage = "发生错误";
            Logger.Info($"✗ 错误：{ex.Message}");
        }
        finally
        {
            IsProcessing = false;
        }
    }
    
    private async Task<bool> LoginWithUsernamePasswordAsync(SteamClientManager clientManager)
    {
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
        {
            LoginStatus = "错误：用户名或密码不能为空";
            return false;
        }
        
        LoginStatus = "正在登录...";
        StatusBarMessage = "登录中...";
        
        var guardData = SteamClientManager.LoadGuardData();
        var success = await clientManager.LoginAsync(Username, Password, guardData);
        
        if (success)
        {
            var config = ConfigManager.LoadConfig() ?? new SteamConfig();
            config.Username = Username;
            config.Password = Password;
            ConfigManager.SaveConfig(config);
            Logger.Info("✓ 用户名和密码已保存");
        }
        
        return success;
    }
    
    private async Task<bool> LoginWithTokenAsync(SteamClientManager clientManager)
    {
        LoginStatus = "正在使用 Token 登录...";
        StatusBarMessage = "Token 登录中...";
        
        var accessToken = SteamClientManager.LoadAccessToken();
        var refreshToken = SteamClientManager.LoadRefreshToken();
        
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            LoginStatus = "未找到保存的 Token";
            return false;
        }
        
        var success = await clientManager.LoginWithAccessTokenAsync(accessToken, refreshToken);
        
        if (success)
        {
            LoginStatus = "Token 登录成功！";
            StatusBarMessage = "请点击'开始查询'按钮";
            Logger.Info("✓ Token 登录成功");
        }
        else
        {
            LoginStatus = "Token 登录失败，请尝试其他方式";
            Logger.Info("✗ Token 登录失败");
        }
        
        return success;
    }
    
    private async Task<bool> LoginWithQRAsync(SteamClientManager clientManager)
    {
        LoginStatus = "正在生成二维码...";
        StatusBarMessage = "生成二维码中...";
        IsQRCodeVisible = false;
        
        try
        {
            clientManager.OnQRCodeGenerated += (qrBitmap) =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        var tempFile = Path.Combine(AppContext.BaseDirectory, "qrcode_temp.png");
                        
                        using (qrBitmap)
                        {
                            try
                            {
                                if (File.Exists(tempFile))
                                    File.Delete(tempFile);
                            }
                            catch { }
                            
                            qrBitmap.Save(tempFile);
                        }
                        
                        await Task.Delay(200);
                        
                        using var fileStream = File.OpenRead(tempFile);
                        QRCodeImage = new Bitmap(fileStream);
                        IsQRCodeVisible = true;
                        LoginStatus = "请使用 Steam 手机应用扫描二维码";
                        
                        await Task.Delay(500);
                        try
                        {
                            if (File.Exists(tempFile))
                                File.Delete(tempFile);
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("[ViewModel] 二维码显示失败", ex);
                        var tempFile = Path.Combine(AppContext.BaseDirectory, $"qrcode_{Guid.NewGuid()}.png");
                        try
                        {
                            using (qrBitmap)
                            {
                                qrBitmap.Save(tempFile);
                            }
                            await Task.Delay(200);
                            using var fileStream = File.OpenRead(tempFile);
                            QRCodeImage = new Bitmap(fileStream);
                            IsQRCodeVisible = true;
                        }
                        finally
                        {
                            try
                            {
                                if (File.Exists(tempFile))
                                    File.Delete(tempFile);
                            }
                            catch { }
                        }
                    }
                });
            };
            var success = await clientManager.LoginWithQRAsync();
            
            if (success)
            {
                LoginStatus = "扫码登录成功！";
                StatusBarMessage = "请点击'开始查询'按钮";
                Logger.Info("✓ 扫码登录成功");
                IsQRCodeVisible = false;
            }
            else
            {
                LoginStatus = "扫码登录失败或已取消";
                Logger.Info("✗ 扫码登录失败");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            LoginStatus = $"错误：{ex.Message}";
            Logger.Info($"✗ 错误：{ex.Message}");
            return false;
        }
    }
    
    // 查询命令
    public async void QueryFamilyCommand()
    {
        try
        {
            if (!CanStartQuery)
            {
                LoginStatus = "请先完成登录";
                return;
            }
            
            var config = ConfigManager.LoadConfig();
            if (string.IsNullOrEmpty(config?.AccessToken))
            {
                LoginStatus = "未获取到 Access Token，请重新登录";
                CanStartQuery = false;
                return;
            }
            
            IsProcessing = true;
            IsResultVisible = false;
            LogOutput = "";
            StatusBarMessage = "查询中...";
            
            Logger.Info("=== 开始查询家庭组信息 ===");
            await RunQueryAsync();
            
            IsProcessing = false;
            IsResultVisible = true;
            StatusBarMessage = "查询完成";
        }
        catch (Exception ex)
        {
            Logger.Error("[ViewModel] 查询失败", ex);
            StatusBarMessage = "查询失败";
            IsProcessing = false;
        }
    }
    
    public void StartQueryCommand() => QueryFamilyCommand();
    
    private async Task RunQueryAsync()
    {
        var config = ConfigManager.LoadConfig();
        if (string.IsNullOrEmpty(config?.AccessToken) || string.IsNullOrEmpty(config?.RefreshToken))
        {
            Logger.Info("✗ 配置文件中未找到 Token，请先登录");
            return;
        }
        
        var clientManager = new SteamClientManager();
        
        try
        {
            Logger.Info("[RunQuery] 使用 Token 登录...");
            var loginSuccess = await clientManager.LoginWithAccessTokenAsync(config.AccessToken, config.RefreshToken);
            
            if (!loginSuccess)
            {
                Logger.Info("[RunQuery] ✗ Token 登录失败");
                CanStartQuery = false;
                return;
            }
            
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
            List<SharedApp> allSharedApps = new();
            
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
            
            _allGamesMap.Clear();
            foreach (var kvp in priceMap)
            {
                _allGamesMap[kvp.Key] = new GameInfo
                {
                    AppId = kvp.Key,
                    Name = kvp.Value.Name,
                    Price = kvp.Value.Price,
                    IsFree = kvp.Value.IsFree
                };
            }
            
            // 计算贡献
            var memberContributions = new List<MemberContribution>();
            double totalContribution = 0;
            double totalUniqueContribution = 0;
            
            foreach (var member in familyInfo.Value.Members)
            {
                var memberOwnedApps = allSharedApps.Where(app => app.OwnerId == member.SteamId).ToList();
                
                double memberContribution = memberOwnedApps
                    .Where(app => priceMap.TryGetValue(app.AppId, out var priceInfo) && !priceInfo.IsFree)
                    .Sum(app => priceMap[app.AppId].Price);
                
                var uniqueMemberAppIds = memberOwnedApps.GroupBy(app => app.AppId).Select(g => g.Key).ToList();
                double memberUniqueContribution = uniqueMemberAppIds
                    .Where(appId => priceMap.TryGetValue(appId, out var priceInfo) && !priceInfo.IsFree)
                    .Sum(appId => priceMap[appId].Price);
                
                totalUniqueContribution += memberUniqueContribution;
                
                memberContributions.Add(new MemberContribution
                {
                    SteamId = member.SteamId,
                    RoleName = member.RoleName,
                    Contribution = memberContribution,
                    UniqueContribution = memberUniqueContribution,
                    OwnedGameCount = memberOwnedApps.Count
                });
                
                totalContribution += memberContribution;
            }
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TotalContribution = totalContribution;
                TotalUniqueContribution = totalUniqueContribution;
                TotalGameCount = allSharedApps.Count;
                MemberCount = familyInfo.Value.Members.Count;
                Currency = CurrencyHelper.GetCurrencyByCountryCode(SelectedCountry);
                
                MemberContributions.Clear();
                foreach (var contrib in memberContributions.OrderByDescending(c => c.Contribution))
                {
                    var member = familyInfo.Value.Members.FirstOrDefault(m => m.SteamId == contrib.SteamId);
                    var memberName = member?.Nickname ?? contrib.SteamId.ToString();
                    double ratio = totalContribution > 0 ? (contrib.Contribution / totalContribution) * 100 : 0;
                    
                    var memberOwnedApps = allSharedApps.Where(app => app.OwnerId == contrib.SteamId).ToList();
                    
                    MemberContributions.Add(new MemberContributionItem
                    {
                        SteamId = contrib.SteamId,
                        Nickname = memberName,
                        RoleName = contrib.RoleName,
                        AvatarUrl = member?.AvatarUrl ?? "",
                        Contribution = contrib.Contribution,
                        UniqueContribution = contrib.UniqueContribution,
                        ContributionRatio = ratio,
                        GameCount = contrib.OwnedGameCount,
                        Currency = Currency,
                        OwnedGameIds = memberOwnedApps.Select(app => app.AppId).ToList()
                    });
                }
                
                CreateCharts();
                Logger.Info("✓ 贡献计算完成");
                Logger.Info($"共享库总价值：{totalContribution:F2} {Currency}");
            });
            
            if (SaveResult)
            {
                SaveQueryResult(familyInfo.Value, memberContributions, allSharedApps, priceMap);
            }
        }
        finally
        {
            clientManager.Disconnect();
            Logger.Info("已断开连接");
        }
    }
    
    private void SaveQueryResult(
        (string Name, List<FamilyMember> Members, ulong FamilyGroupId) familyInfo,
        List<MemberContribution> memberContributions,
        List<SharedApp> allSharedApps,
        Dictionary<uint, AppPriceInfo> priceMap)
    {
        var queryResult = new QueryResult
        {
            FamilyGroupName = familyInfo.Name,
            FamilyMemberCount = familyInfo.Members.Count,
            Currency = Currency,
            TotalContribution = memberContributions.Sum(m => m.Contribution)
        };
        
        foreach (var member in familyInfo.Members)
        {
            var memberOwnedApps = allSharedApps.Where(app => app.OwnerId == member.SteamId).ToList();
            double memberContribution = memberOwnedApps
                .Where(app => priceMap.TryGetValue(app.AppId, out var priceInfo) && !priceInfo.IsFree)
                .Sum(app => priceMap[app.AppId].Price);
            
            var uniqueMemberAppIds = memberOwnedApps.Select(app => app.AppId).Distinct().ToList();
            double memberUniqueContribution = uniqueMemberAppIds
                .Where(appId => priceMap.TryGetValue(appId, out var priceInfo) && !priceInfo.IsFree)
                .Sum(appId => priceMap[appId].Price);
            
            queryResult.Members.Add(new MemberResult
            {
                SteamId = member.SteamId,
                RoleName = member.RoleName,
                Nickname = member.Nickname,
                AvatarUrl = member.AvatarUrl,
                Contribution = memberContribution,
                UniqueContribution = memberUniqueContribution,
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
    
    public async void AnalyzeResultCommand()
    {
        try
        {
            IsProcessing = true;
            var resultFiles = ResultService.GetResultFiles();
            
            if (resultFiles.Count == 0)
            {
                LoginStatus = "没有找到已保存的查询结果";
                IsProcessing = false;
                return;
            }
            
            var latestFile = resultFiles[0];
            var result = ResultService.LoadQueryResult(latestFile);
            
            if (result != null)
            {
                await Task.Run(() =>
                {
                    _allGamesMap.Clear();
                    foreach (var game in result.SharedGames)
                    {
                        _allGamesMap[game.AppId] = new GameInfo
                        {
                            AppId = game.AppId,
                            Name = game.Name,
                            Price = game.Price,
                            IsFree = game.IsFree
                        };
                    }
                    
                    var members = result.Members
                        .OrderByDescending(m => m.Contribution)
                        .Select(member =>
                        {
                            double ratio = result.TotalContribution > 0 ? (member.Contribution / result.TotalContribution) * 100 : 0;
                            
                            return new MemberContributionItem
                            {
                                SteamId = member.SteamId,
                                Nickname = member.Nickname ?? member.SteamId.ToString(),
                                RoleName = member.RoleName,
                                AvatarUrl = member.AvatarUrl ?? "",
                                Contribution = member.Contribution,
                                UniqueContribution = member.UniqueContribution,
                                ContributionRatio = ratio,
                                GameCount = member.OwnedGameCount,
                                Currency = result.Currency,
                                OwnedGameIds = member.OwnedGameIds ?? new List<uint>()
                            };
                        })
                        .ToList();
                    
                    Dispatcher.UIThread.Post(() =>
                    {
                        TotalContribution = result.TotalContribution;
                        TotalUniqueContribution = members.Sum(m => m.UniqueContribution);
                        TotalGameCount = result.SharedGames.Count;
                        MemberCount = result.FamilyMemberCount;
                        Currency = result.Currency;
                        
                        MemberContributions.Clear();
                        foreach (var member in members)
                        {
                            MemberContributions.Add(member);
                        }
                        
                        CreateCharts();
                        
                        IsResultVisible = true;
                        IsProcessing = false;
                        StatusBarMessage = "已加载历史结果";
                        LoginStatus = "已加载历史结果";
                        
                        Logger.Info("✓ 已加载并显示历史结果");
                    });
                });
            }
            else
            {
                Logger.Info("✗ 加载历史结果失败");
                LoginStatus = "加载历史结果失败";
                IsProcessing = false;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("[ViewModel] 分析历史结果失败", ex);
            LoginStatus = $"分析失败：{ex.Message}";
            IsProcessing = false;
        }
    }
    
    public async void RefreshQRCodeCommand()
    {
        try
        {
            IsQRCodeVisible = true;
            LoginStatus = "正在生成二维码...";
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
        if (MemberContributions.Count == 0) return;
        
        var chineseFont = SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Normal)
            ?? SKTypeface.FromFamilyName("SimSun", SKFontStyle.Normal)
            ?? SKTypeface.Default;
        
        var columnSeries = new ColumnSeries<double>
        {
            Values = MemberContributions.Select(m => ShowUniqueContribution ? m.UniqueContribution : m.Contribution).ToList(),
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
                    Labels = MemberContributions.Select(m => m.Nickname).ToList(),
                    Name = "成员",
                    LabelsRotation = 0,
                    LabelsPaint = new SolidColorPaint(new SKColor(160, 160, 160)) { SKTypeface = chineseFont },
                    NamePaint = new SolidColorPaint(new SKColor(255, 255, 255)) { SKTypeface = chineseFont },
                    SeparatorsPaint = new SolidColorPaint(new SKColor(50, 50, 50))
                }
            },
            YAxes = new[]
            {
                new Axis
                {
                    Name = "贡献金额",
                    LabelsPaint = new SolidColorPaint(new SKColor(160, 160, 160)) { SKTypeface = chineseFont },
                    NamePaint = new SolidColorPaint(new SKColor(255, 255, 255)) { SKTypeface = chineseFont },
                    SeparatorsPaint = new SolidColorPaint(new SKColor(50, 50, 50)),
                    MinLimit = 0
                }
            }
        };
        
        var pieSeriesList = MemberContributions.Select(member => new PieSeries<double>
        {
            Values = new[] { ShowUniqueContribution ? member.UniqueContribution : member.Contribution },
            Name = member.Nickname,
            DataLabelsPaint = new SolidColorPaint(SKColors.White),
            DataLabelsSize = 14,
            DataLabelsFormatter = point => $"{point.PrimaryValue:F0}",
            InnerRadius = 0
        }).Cast<ISeries>().ToList();
        
        ContributionPieChart = new PieChart
        {
            Series = pieSeriesList,
            InitialRotation = -90,
            MaxAngle = 360,
            LegendPosition = LiveChartsCore.Measure.LegendPosition.Right
        };
    }
}

public class MemberContributionItem : INotifyPropertyChanged
{
    public ulong SteamId { get; set; }
    public string Nickname { get; set; } = "";
    public string RoleName { get; set; } = "";
    private string _avatarUrl = "";
    private Bitmap? _avatarBitmap;
    private bool _isSelected;
    
    public string AvatarUrl 
    { 
        get => _avatarUrl;
        set
        {
            if (_avatarUrl != value)
            {
                _avatarUrl = value;
                _ = LoadAvatarAsync();
                OnPropertyChanged(nameof(AvatarUrl));
                OnPropertyChanged(nameof(AvatarBitmap));
            }
        }
    }
    
    public Bitmap? AvatarBitmap
    {
        get => _avatarBitmap;
        set
        {
            _avatarBitmap = value;
            OnPropertyChanged(nameof(AvatarBitmap));
        }
    }
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetField(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }
    
    public List<uint> OwnedGameIds { get; set; } = new();
    
    public double Contribution { get; set; }
    public double UniqueContribution { get; set; }
    public double ContributionRatio { get; set; }
    public int GameCount { get; set; }
    public string Currency { get; set; } = "CNY";
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    public string ContributionText => $"{Contribution:F2} {Currency}";
    public string ContributionRatioText => $"{ContributionRatio:F2}%";
    public string GameCountText => $"游戏数量：{GameCount}";
    
    private async Task LoadAvatarAsync()
    {
        if (string.IsNullOrEmpty(_avatarUrl))
            return;
            
        AvatarBitmap = await AvatarLoader.GetAvatarAsync(_avatarUrl);
    }
}

public class GameItem : INotifyPropertyChanged
{
    private string _imageUrl = "";
    private Bitmap? _imageBitmap;
    private bool _isImageLoading;
    
    public uint AppId { get; set; }
    public string Name { get; set; } = "";
    public double Price { get; set; }
    public bool IsFree { get; set; }
    
    public string ImageUrl
    {
        get => _imageUrl;
        set
        {
            if (_imageUrl != value)
            {
                _imageUrl = value;
                OnPropertyChanged(nameof(ImageUrl));
                _ = LoadImageAsync();
            }
        }
    }
    
    public Bitmap? ImageBitmap
    {
        get => _imageBitmap;
        set
        {
            _imageBitmap = value;
            OnPropertyChanged(nameof(ImageBitmap));
        }
    }
    
    public bool IsImageLoading
    {
        get => _isImageLoading;
        set
        {
            _isImageLoading = value;
            OnPropertyChanged(nameof(IsImageLoading));
        }
    }
    
    public string PriceText => IsFree ? "免费" : $"{Price:F2} CNY";
    
    public string StoreUrl => $"https://store.steampowered.com/app/{AppId}";
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    
    private async Task LoadImageAsync()
    {
        if (string.IsNullOrEmpty(_imageUrl))
            return;
        
        IsImageLoading = true;
        
        try
        {
            ImageBitmap = await GameImageLoader.GetImageAsync(_imageUrl);
        }
        finally
        {
            IsImageLoading = false;
        }
    }
}

public class GameInfo
{
    public uint AppId { get; set; }
    public string Name { get; set; } = "";
    public double Price { get; set; }
    public bool IsFree { get; set; }
}
