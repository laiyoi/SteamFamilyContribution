using System.Text.Json;
using QRCoder;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using static SteamKit2.SteamUser;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SteamContribution;

public class SteamClientManager
{
    private SteamClient? _client;
    private SteamUser? _steamUser;
    private CallbackManager? _callbackManager;
    private bool _isConnected;
    private bool _isLoggedIn;
    private SteamID? _steamId;
    private string? _accessToken;
    private string? _refreshToken;
    private string? _guardData;
    private string? _username;
    private bool _isRunning;

    public bool IsConnected => _isConnected;
    public bool IsLoggedIn => _isLoggedIn;
    public SteamID? SteamId => _steamId;
    public string? AccessToken => _accessToken;
    public string? RefreshToken => _refreshToken;

    /// <summary>
    /// 使用 Access Token 登录
    /// </summary>
    public async Task<bool> LoginWithAccessTokenAsync(string accessToken, string refreshToken)
    {
        _client = new SteamClient();
        _steamUser = _client.GetHandler<SteamUser>()!;
        _callbackManager = new CallbackManager(_client);
        _isRunning = true;
        _accessToken = accessToken;
        _refreshToken = refreshToken;

        // 注册回调
        _callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbackManager.Subscribe<LoggedOnCallback>(OnLoggedOn);
        _callbackManager.Subscribe<LoggedOffCallback>(OnLoggedOff);

        Logger.Info("[SteamClient] 正在连接 Steam...");
        _client.Connect();

        // 等待连接完成
        var connectTimeout = TimeSpan.FromSeconds(15);
        var connectStart = DateTime.Now;

        while (_isConnected == false && (DateTime.Now - connectStart) < connectTimeout)
        {
            _callbackManager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            await Task.Delay(100);
        }

        if (!_isConnected)
        {
            throw new Exception("无法连接到 Steam 服务器");
        }

        // 使用 Refresh Token 登录（Access Token 用于 Web API）
        Logger.Info("[SteamClient] 使用 Refresh Token 登录...");
        Logger.Info("[SteamClient] Access Token 将用于 Web API 请求");
        
        // 读取保存的用户名
        var username = LoadUsername();
        
        if (string.IsNullOrEmpty(username))
        {
            throw new Exception("未找到保存的用户名，请使用用户名密码登录");
        }
        
        Logger.Info($"[SteamClient] 使用用户名：{username}");
        
        _steamUser.LogOn(new LogOnDetails
        {
            Username = username,
            AccessToken = _refreshToken, // 使用 Refresh Token 登录
        });

        // 等待登录完成
        var loginTimeout = TimeSpan.FromSeconds(30);
        var loginStart = DateTime.Now;

        while (!_isLoggedIn && _isRunning && (DateTime.Now - loginStart) < loginTimeout)
        {
            _callbackManager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            await Task.Delay(100);
        }

        return _isLoggedIn;
    }

    /// <summary>
    /// 用户名密码登录
    /// </summary>
    public async Task<bool> LoginAsync(string username, string password, string? guardData = null)
    {
        _client = new SteamClient();
        _steamUser = _client.GetHandler<SteamUser>()!;
        _callbackManager = new CallbackManager(_client);
        _isRunning = true;

        // 注册回调
        _callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbackManager.Subscribe<LoggedOnCallback>(OnLoggedOn);
        _callbackManager.Subscribe<LoggedOffCallback>(OnLoggedOff);

        Logger.Info("[SteamClient] 正在连接 Steam...");
        _client.Connect();

        // 等待连接完成
        var connectTimeout = TimeSpan.FromSeconds(15);
        var connectStart = DateTime.Now;

        while (_isConnected == false && (DateTime.Now - connectStart) < connectTimeout)
        {
            _callbackManager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            await Task.Delay(100);
        }

        if (!_isConnected)
        {
            throw new Exception("无法连接到 Steam 服务器");
        }

        // 开始认证
        Logger.Info("[SteamClient] 正在认证...");
        
        var shouldRememberPassword = false;
        var authSession = await _client.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
        {
            Username = username,
            Password = password,
            IsPersistentSession = shouldRememberPassword,
            GuardData = guardData,
            Authenticator = new UserConsoleAuthenticator(),
        });

        // 等待认证响应
        Logger.Info("[SteamClient] 等待认证响应...");
        var pollResponse = await authSession.PollingWaitForResultAsync();

        if (pollResponse == null)
        {
            throw new Exception("认证失败");
        }

        Logger.Info($"[SteamClient] ✓ 认证成功！AccountName: {pollResponse.AccountName}");

        // 保存 GuardData
        if (pollResponse.NewGuardData != null)
        {
            _guardData = pollResponse.NewGuardData;
            Logger.Info("[SteamClient] ✓ 获取到 GuardData");
            SaveGuardData(_guardData);
        }

        // 保存用户名和 Token
        _username = pollResponse.AccountName;
        _accessToken = pollResponse.AccessToken;
        _refreshToken = pollResponse.RefreshToken;

        Logger.Info($"[SteamClient] ✓ 获取到 Access Token");
        Logger.Info($"[SteamClient] ✓ 获取到 Refresh Token");

        SaveAccessToken(_accessToken);
        SaveRefreshToken(_refreshToken);
        SaveUsername(_username);

        // 登录 Steam
        Logger.Info("[SteamClient] 正在登录 Steam...");
        _steamUser.LogOn(new LogOnDetails
        {
            Username = pollResponse.AccountName,
            AccessToken = _refreshToken,
            ShouldRememberPassword = shouldRememberPassword,
        });

        // 等待登录完成
        var loginTimeout = TimeSpan.FromSeconds(30);
        var loginStart = DateTime.Now;

        while (!_isLoggedIn && _isRunning && (DateTime.Now - loginStart) < loginTimeout)
        {
            _callbackManager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            await Task.Delay(100);
        }

        return _isLoggedIn;
    }

    /// <summary>
    /// 扫码登录
    /// </summary>
    public async Task<bool> LoginWithQRAsync()
    {
        _client = new SteamClient();
        _steamUser = _client.GetHandler<SteamUser>()!;
        _callbackManager = new CallbackManager(_client);
        _isRunning = true;

        // 注册回调
        _callbackManager.Subscribe<SteamClient.ConnectedCallback>(async callback => await OnConnectedQR(callback));
        _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbackManager.Subscribe<LoggedOnCallback>(OnLoggedOn);
        _callbackManager.Subscribe<LoggedOffCallback>(OnLoggedOff);

        Logger.Info("[SteamClient] 正在连接 Steam...");
        _client.Connect();

        // 等待登录完成
        var loginTimeout = TimeSpan.FromMinutes(2); // 给用户足够的时间扫码
        var loginStart = DateTime.Now;

        while (!_isLoggedIn && _isRunning && (DateTime.Now - loginStart) < loginTimeout)
        {
            _callbackManager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            await Task.Delay(100);
        }

        if (!_isLoggedIn)
        {
            Logger.Info("[SteamClient] ✗ 扫码登录超时或失败");
            return false;
        }

        return true;
    }

    private async void OnConnected(SteamClient.ConnectedCallback callback)
    {
        Logger.Info("[SteamClient] ✓ 已连接到 Steam 服务器");
        _isConnected = true;
    }

    private async Task OnConnectedQR(SteamClient.ConnectedCallback callback)
    {
        try
        {
            Logger.Info("[SteamClient] ✓ 已连接到 Steam 服务器");
            _isConnected = true;

            // 开始 QR 认证
            Logger.Info("[SteamClient] 正在获取二维码...");
            var authSession = await _client.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails());

            // 设置二维码刷新回调
            authSession.ChallengeURLChanged = () =>
            {
                Logger.Info("[SteamClient] Steam 已刷新二维码链接");
                DrawQRCode(authSession);
            };

            // 显示二维码
            DrawQRCode(authSession);

            // 等待认证响应
            Logger.Info("[SteamClient] 等待扫码...");
            var pollResponse = await authSession.PollingWaitForResultAsync();

            if (pollResponse == null)
            {
                Logger.Info("[SteamClient] ✗ 扫码失败");
                _isRunning = false;
                return;
            }

            Logger.Info($"[SteamClient] ✓ 扫码成功！AccountName: {pollResponse.AccountName}");

            // 保存 GuardData
            if (pollResponse.NewGuardData != null)
            {
                _guardData = pollResponse.NewGuardData;
                SaveGuardData(_guardData);
            }

            // 保存 Token 和用户名
            _username = pollResponse.AccountName;
            _accessToken = pollResponse.AccessToken;
            _refreshToken = pollResponse.RefreshToken;

            Logger.Info($"[SteamClient] ✓ 获取到 Access Token");
            Logger.Info($"[SteamClient] ✓ 获取到 Refresh Token");

            SaveAccessToken(_accessToken);
            SaveRefreshToken(_refreshToken);
            SaveUsername(_username);

            // 登录 Steam
            Logger.Info("[SteamClient] 正在登录 Steam...");
            _steamUser.LogOn(new LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = _refreshToken,
            });
        }
        catch (TaskCanceledException ex)
        {
            Logger.Info("[SteamClient] ✗ 二维码认证被取消");
            _isRunning = false;
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] ✗ 二维码认证失败：{ex.Message}");
            _isRunning = false;
        }
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        Logger.Info("[SteamClient] ✗ 已断开连接");
        _isConnected = false;
        _isLoggedIn = false;
        _isRunning = false;
    }

    private void OnLoggedOn(LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            Logger.Info($"[SteamClient] ✗ 登录失败：{callback.Result} / {callback.ExtendedResult}");
            _isLoggedIn = false;
            _isRunning = false;
            return;
        }

        Logger.Info("[SteamClient] ✓ 登录成功！");
        _isLoggedIn = true;
        _steamId = callback.ClientSteamID;
        Logger.Info($"[SteamClient] SteamID: {_steamId}");

        // 停止回调循环
        _isRunning = false;
    }

    private void OnLoggedOff(LoggedOffCallback callback)
    {
        Logger.Info($"[SteamClient] 已登出：{callback.Result}");
    }

    /// <summary>
    /// 显示二维码
    /// </summary>
    public event Action<Bitmap>? OnQRCodeGenerated;
    
    private void DrawQRCode(QrAuthSession authSession)
    {
        Logger.Info($"[SteamClient] Challenge URL: {authSession.ChallengeURL}");
        Logger.Info("");

        try
        {
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(authSession.ChallengeURL, QRCodeGenerator.ECCLevel.L);
            
            // 手动生成位图（直接操作像素，确保 PixelFormat 正确）
            var pixelSize = 20;
            var quietZones = 0;
            var size = (qrCodeData.ModuleMatrix.Count + quietZones * 2) * pixelSize;
            
            using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            
            // 使用 SetPixel 方法绘制（更可靠）
            Logger.Info($"[SteamClient] 开始绘制二维码，尺寸：{size}x{size}");
            
            // 填充白色背景
            using var g = System.Drawing.Graphics.FromImage(bitmap);
            g.Clear(Color.White);
            
            // 绘制黑色二维码模块
            using var blackBrush = new SolidBrush(Color.Black);
            for (var x = 0; x < qrCodeData.ModuleMatrix.Count; x++)
            {
                for (var y = 0; y < qrCodeData.ModuleMatrix[x].Count; y++)
                {
                    if (qrCodeData.ModuleMatrix[x][y])
                    {
                        var pixelX = (x + quietZones) * pixelSize;
                        var pixelY = (y + quietZones) * pixelSize;
                        g.FillRectangle(blackBrush, pixelX, pixelY, pixelSize, pixelSize);
                    }
                }
            }
            
            // 强制刷新
            g.Flush();
            
            Logger.Info($"[SteamClient] ✓ 二维码绘制完成，PixelFormat: {bitmap.PixelFormat}");
            
            Logger.Info($"[SteamClient] Bitmap 尺寸：{bitmap.Width}x{bitmap.Height}");
            Logger.Info($"[SteamClient] 最终 PixelFormat: {bitmap.PixelFormat}");
            
            // 克隆 Bitmap 以确保事件处理时不会被释放
            Logger.Info("[SteamClient] 正在克隆 Bitmap...");
            var clonedBitmap = new Bitmap(bitmap);
            Logger.Info($"[SteamClient] ✓ 已克隆，PixelFormat: {clonedBitmap.PixelFormat}");
            
            // 触发事件通知 ViewModel 更新 UI（使用克隆的 Bitmap）
            OnQRCodeGenerated?.Invoke(clonedBitmap);
            
            // 注意：不调用 clonedBitmap.Dispose()，让 ViewModel 负责释放
            Logger.Info("=== 请使用 Steam 手机应用扫描二维码 ===");
            Logger.Info("[SteamClient] ✓ 二维码已生成并发送到 UI");
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] 生成二维码失败：{ex.Message}");
            Logger.Info($"[SteamClient] 请手动访问：{authSession.ChallengeURL}");
        }
    }

    /// <summary>
    /// 解析 JWT Token
    /// </summary>
    private void ParseJsonWebToken(string token, string name)
    {
        try
        {
            var tokenComponents = token.Split('.');
            var base64 = tokenComponents[1].Replace('-', '+').Replace('_', '/');

            if (base64.Length % 4 != 0)
            {
                base64 += new string('=', 4 - base64.Length % 4);
            }

            var payloadBytes = Convert.FromBase64String(base64);
            var payload = JsonDocument.Parse(payloadBytes);
            var formatted = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

            Logger.Info($"[JWT] {name} Payload:");
            Logger.Info(formatted);
            Logger.Info("");
        }
        catch (Exception ex)
        {
            Logger.Info($"[JWT] 解析失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 保存 Access Token
    /// </summary>
    private void SaveAccessToken(string token)
    {
        try
        {
            var config = ConfigManager.LoadConfig() ?? new SteamConfig();
            config.AccessToken = token;
            Logger.Info(token);
            ConfigManager.SaveConfig(config);
            Logger.Info("[SteamClient] ✓ Access Token 已保存到配置文件");
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] ✗ 保存 Access Token 失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 保存 Refresh Token
    /// </summary>
    private void SaveRefreshToken(string token)
    {
        try
        {
            var config = ConfigManager.LoadConfig() ?? new SteamConfig();
            config.RefreshToken = token;
            ConfigManager.SaveConfig(config);
            Logger.Info("[SteamClient] ✓ Refresh Token 已保存到配置文件");
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] ✗ 保存 Refresh Token 失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 保存 GuardData
    /// </summary>
    private void SaveGuardData(string data)
    {
        try
        {
            var config = ConfigManager.LoadConfig() ?? new SteamConfig();
            config.GuardData = data;
            ConfigManager.SaveConfig(config);
            Logger.Info("[SteamClient] ✓ GuardData 已保存到配置文件");
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] ✗ 保存 GuardData 失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 读取 GuardData
    /// </summary>
    public static string? LoadGuardData()
    {
        try
        {
            var config = ConfigManager.LoadConfig();
            if (config != null && !string.IsNullOrEmpty(config.GuardData))
            {
                Logger.Info("[Config] ✓ 已从配置文件读取 GuardData");
                return config.GuardData;
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[Config] ✗ 读取 GuardData 失败：{ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        _client?.Disconnect();

        while (_isConnected)
        {
            _callbackManager?.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            Task.Delay(100).Wait();
        }
    }

    /// <summary>
    /// 读取 Access Token
    /// </summary>
    public static string? LoadAccessToken()
    {
        try
        {
            var config = ConfigManager.LoadConfig();
            if (config != null && !string.IsNullOrEmpty(config.AccessToken))
            {
                Logger.Info("[Config] ✓ 已从配置文件读取 Access Token");
                return config.AccessToken;
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[Config] ✗ 读取 Access Token 失败：{ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 读取 Refresh Token
    /// </summary>
    public static string? LoadRefreshToken()
    {
        try
        {
            var config = ConfigManager.LoadConfig();
            if (config != null && !string.IsNullOrEmpty(config.RefreshToken))
            {
                Logger.Info("[Config] ✓ 已从配置文件读取 Refresh Token");
                return config.RefreshToken;
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[Config] ✗ 读取 Refresh Token 失败：{ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 保存用户名
    /// </summary>
    private void SaveUsername(string username)
    {
        try
        {
            var config = ConfigManager.LoadConfig() ?? new SteamConfig();
            config.Username = username;
            ConfigManager.SaveConfig(config);
            Logger.Info("[SteamClient] ✓ 用户名已保存到配置文件");
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] ✗ 保存用户名失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 读取用户名
    /// </summary>
    public static string? LoadUsername()
    {
        try
        {
            var config = ConfigManager.LoadConfig();
            if (config != null && !string.IsNullOrEmpty(config.Username))
            {
                Logger.Info($"[Config] ✓ 已从配置文件读取用户名：{config.Username}");
                return config.Username;
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[Config] ✗ 读取用户名失败：{ex.Message}");
        }

        return null;
    }


}
