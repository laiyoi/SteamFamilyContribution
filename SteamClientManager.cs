using System.Text.Json;
using QRCoder;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using static SteamKit2.SteamUser;

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

        Console.WriteLine("[SteamClient] 正在连接 Steam...");
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
        Console.WriteLine("[SteamClient] 使用 Refresh Token 登录...");
        Console.WriteLine("[SteamClient] Access Token 将用于 Web API 请求");
        
        // 读取保存的用户名
        var username = LoadUsername();
        
        if (string.IsNullOrEmpty(username))
        {
            throw new Exception("未找到保存的用户名，请使用用户名密码登录");
        }
        
        Console.WriteLine($"[SteamClient] 使用用户名：{username}");
        
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

        Console.WriteLine("[SteamClient] 正在连接 Steam...");
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
        Console.WriteLine("[SteamClient] 正在认证...");
        
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
        Console.WriteLine("[SteamClient] 等待认证响应...");
        var pollResponse = await authSession.PollingWaitForResultAsync();

        if (pollResponse == null)
        {
            throw new Exception("认证失败");
        }

        Console.WriteLine($"[SteamClient] ✓ 认证成功！AccountName: {pollResponse.AccountName}");

        // 保存 GuardData
        if (pollResponse.NewGuardData != null)
        {
            _guardData = pollResponse.NewGuardData;
            Console.WriteLine("[SteamClient] ✓ 获取到 GuardData");
            SaveGuardData(_guardData);
        }

        // 保存用户名和 Token
        _username = pollResponse.AccountName;
        _accessToken = pollResponse.AccessToken;
        _refreshToken = pollResponse.RefreshToken;

        Console.WriteLine($"[SteamClient] ✓ 获取到 Access Token");
        Console.WriteLine($"[SteamClient] ✓ 获取到 Refresh Token");

        SaveAccessToken(_accessToken);
        SaveRefreshToken(_refreshToken);
        SaveUsername(_username);

        // 登录 Steam
        Console.WriteLine("[SteamClient] 正在登录 Steam...");
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

        Console.WriteLine("[SteamClient] 正在连接 Steam...");
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
            Console.WriteLine("[SteamClient] ✗ 扫码登录超时或失败");
            return false;
        }

        return true;
    }

    private async void OnConnected(SteamClient.ConnectedCallback callback)
    {
        Console.WriteLine("[SteamClient] ✓ 已连接到 Steam 服务器");
        _isConnected = true;
    }

    private async Task OnConnectedQR(SteamClient.ConnectedCallback callback)
    {
        try
        {
            Console.WriteLine("[SteamClient] ✓ 已连接到 Steam 服务器");
            _isConnected = true;

            // 开始 QR 认证
            Console.WriteLine("[SteamClient] 正在获取二维码...");
            var authSession = await _client.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails());

            // 设置二维码刷新回调
            authSession.ChallengeURLChanged = () =>
            {
                Console.WriteLine();
                Console.WriteLine("[SteamClient] Steam 已刷新二维码链接");
                DrawQRCode(authSession);
            };

            // 显示二维码
            DrawQRCode(authSession);

            // 等待认证响应
            Console.WriteLine("[SteamClient] 等待扫码...");
            var pollResponse = await authSession.PollingWaitForResultAsync();

            if (pollResponse == null)
            {
                Console.WriteLine("[SteamClient] ✗ 扫码失败");
                _isRunning = false;
                return;
            }

            Console.WriteLine($"[SteamClient] ✓ 扫码成功！AccountName: {pollResponse.AccountName}");

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

            Console.WriteLine($"[SteamClient] ✓ 获取到 Access Token");
            Console.WriteLine($"[SteamClient] ✓ 获取到 Refresh Token");

            SaveAccessToken(_accessToken);
            SaveRefreshToken(_refreshToken);
            SaveUsername(_username);

            // 登录 Steam
            Console.WriteLine("[SteamClient] 正在登录 Steam...");
            _steamUser.LogOn(new LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = _refreshToken,
            });
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine("[SteamClient] ✗ 二维码认证被取消");
            _isRunning = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SteamClient] ✗ 二维码认证失败：{ex.Message}");
            _isRunning = false;
        }
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        Console.WriteLine("[SteamClient] ✗ 已断开连接");
        _isConnected = false;
        _isLoggedIn = false;
        _isRunning = false;
    }

    private void OnLoggedOn(LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            Console.WriteLine($"[SteamClient] ✗ 登录失败：{callback.Result} / {callback.ExtendedResult}");
            _isLoggedIn = false;
            _isRunning = false;
            return;
        }

        Console.WriteLine("[SteamClient] ✓ 登录成功！");
        _isLoggedIn = true;
        _steamId = callback.ClientSteamID;
        Console.WriteLine($"[SteamClient] SteamID: {_steamId}");

        // 停止回调循环
        _isRunning = false;
    }

    private void OnLoggedOff(LoggedOffCallback callback)
    {
        Console.WriteLine($"[SteamClient] 已登出：{callback.Result}");
    }

    /// <summary>
    /// 显示二维码
    /// </summary>
    private void DrawQRCode(QrAuthSession authSession)
    {
        Console.WriteLine($"[SteamClient] Challenge URL: {authSession.ChallengeURL}");
        Console.WriteLine();

        try
        {
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(authSession.ChallengeURL, QRCodeGenerator.ECCLevel.L);
            using var qrCode = new AsciiQRCode(qrCodeData);
            var qrCodeAsAsciiArt = qrCode.GetGraphic(1, drawQuietZones: false);

            Console.WriteLine("=== 请使用 Steam 手机应用扫描二维码 ===");
            Console.WriteLine(qrCodeAsAsciiArt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SteamClient] 生成二维码失败：{ex.Message}");
            Console.WriteLine($"[SteamClient] 请手动访问：{authSession.ChallengeURL}");
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

            Console.WriteLine($"[JWT] {name} Payload:");
            Console.WriteLine(formatted);
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[JWT] 解析失败：{ex.Message}");
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
            ConfigManager.SaveConfig(config);
            Console.WriteLine("[SteamClient] ✓ Access Token 已保存到配置文件");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SteamClient] ✗ 保存 Access Token 失败：{ex.Message}");
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
            Console.WriteLine("[SteamClient] ✓ Refresh Token 已保存到配置文件");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SteamClient] ✗ 保存 Refresh Token 失败：{ex.Message}");
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
            Console.WriteLine("[SteamClient] ✓ GuardData 已保存到配置文件");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SteamClient] ✗ 保存 GuardData 失败：{ex.Message}");
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
                Console.WriteLine("[Config] ✓ 已从配置文件读取 GuardData");
                return config.GuardData;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] ✗ 读取 GuardData 失败：{ex.Message}");
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
                Console.WriteLine("[Config] ✓ 已从配置文件读取 Access Token");
                return config.AccessToken;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] ✗ 读取 Access Token 失败：{ex.Message}");
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
                Console.WriteLine("[Config] ✓ 已从配置文件读取 Refresh Token");
                return config.RefreshToken;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] ✗ 读取 Refresh Token 失败：{ex.Message}");
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
            Console.WriteLine("[SteamClient] ✓ 用户名已保存到配置文件");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SteamClient] ✗ 保存用户名失败：{ex.Message}");
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
                Console.WriteLine($"[Config] ✓ 已从配置文件读取用户名：{config.Username}");
                return config.Username;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] ✗ 读取用户名失败：{ex.Message}");
        }

        return null;
    }


}
