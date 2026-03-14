using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
    private bool _isRunning;

    public bool IsConnected => _isConnected;
    public bool IsLoggedIn => _isLoggedIn;
    public SteamID? SteamId => _steamId;
    public string? AccessToken => _accessToken;

    public event Action<Bitmap>? OnQRCodeGenerated;

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

        _callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbackManager.Subscribe<LoggedOnCallback>(OnLoggedOn);
        _callbackManager.Subscribe<LoggedOffCallback>(OnLoggedOff);

        Logger.Info("[SteamClient] 正在连接 Steam...");
        _client.Connect();

        await WaitForConditionAsync(() => _isConnected, TimeSpan.FromSeconds(15));

        if (!_isConnected)
            throw new Exception("无法连接到 Steam 服务器");

        var username = LoadUsername();
        if (string.IsNullOrEmpty(username))
            throw new Exception("未找到保存的用户名，请使用用户名密码登录");

        Logger.Info($"[SteamClient] 使用用户名：{username}");
        
        _steamUser.LogOn(new LogOnDetails
        {
            Username = username,
            AccessToken = _refreshToken,
        });

        await WaitForConditionAsync(() => _isLoggedIn, TimeSpan.FromSeconds(30));

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

        _callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbackManager.Subscribe<LoggedOnCallback>(OnLoggedOn);
        _callbackManager.Subscribe<LoggedOffCallback>(OnLoggedOff);

        Logger.Info("[SteamClient] 正在连接 Steam...");
        _client.Connect();

        await WaitForConditionAsync(() => _isConnected, TimeSpan.FromSeconds(15));

        if (!_isConnected)
            throw new Exception("无法连接到 Steam 服务器");

        Logger.Info("[SteamClient] 正在认证...");
        
        var authSession = await _client.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails
        {
            Username = username,
            Password = password,
            IsPersistentSession = false,
            GuardData = guardData,
            Authenticator = new UserConsoleAuthenticator(),
        });

        Logger.Info("[SteamClient] 等待认证响应...");
        var pollResponse = await authSession.PollingWaitForResultAsync();

        if (pollResponse == null)
            throw new Exception("认证失败");

        Logger.Info($"[SteamClient] ✓ 认证成功！AccountName: {pollResponse.AccountName}");

        if (pollResponse.NewGuardData != null)
        {
            _guardData = pollResponse.NewGuardData;
            SaveGuardData(_guardData);
        }

        _accessToken = pollResponse.AccessToken;
        _refreshToken = pollResponse.RefreshToken;

        SaveAccessToken(_accessToken);
        SaveRefreshToken(_refreshToken);
        SaveUsername(pollResponse.AccountName);

        Logger.Info("[SteamClient] 正在登录 Steam...");
        _steamUser.LogOn(new LogOnDetails
        {
            Username = pollResponse.AccountName,
            AccessToken = _refreshToken,
        });

        await WaitForConditionAsync(() => _isLoggedIn, TimeSpan.FromSeconds(30));

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

        _callbackManager.Subscribe<SteamClient.ConnectedCallback>(async callback => await OnConnectedQR(callback));
        _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbackManager.Subscribe<LoggedOnCallback>(OnLoggedOn);
        _callbackManager.Subscribe<LoggedOffCallback>(OnLoggedOff);

        Logger.Info("[SteamClient] 正在连接 Steam...");
        _client.Connect();

        await WaitForConditionAsync(() => _isLoggedIn, TimeSpan.FromMinutes(2));

        return _isLoggedIn;
    }

    private async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.Now;
        while (!condition() && _isRunning && (DateTime.Now - start) < timeout)
        {
            _callbackManager?.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            await Task.Delay(100);
        }
    }

    private void OnConnected(SteamClient.ConnectedCallback callback)
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

            Logger.Info("[SteamClient] 正在获取二维码...");
            var authSession = await _client.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails());

            authSession.ChallengeURLChanged = () =>
            {
                Logger.Info("[SteamClient] Steam 已刷新二维码链接");
                DrawQRCode(authSession);
            };

            DrawQRCode(authSession);

            Logger.Info("[SteamClient] 等待扫码...");
            var pollResponse = await authSession.PollingWaitForResultAsync();

            if (pollResponse == null)
            {
                Logger.Info("[SteamClient] ✗ 扫码失败");
                _isRunning = false;
                return;
            }

            Logger.Info($"[SteamClient] ✓ 扫码成功！AccountName: {pollResponse.AccountName}");

            if (pollResponse.NewGuardData != null)
            {
                _guardData = pollResponse.NewGuardData;
                SaveGuardData(_guardData);
            }

            _accessToken = pollResponse.AccessToken;
            _refreshToken = pollResponse.RefreshToken;

            SaveAccessToken(_accessToken);
            SaveRefreshToken(_refreshToken);
            SaveUsername(pollResponse.AccountName);

            Logger.Info("[SteamClient] 正在登录 Steam...");
            _steamUser.LogOn(new LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = _refreshToken,
            });
        }
        catch (TaskCanceledException)
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
            Logger.Info($"[SteamClient] ✗ 登录失败：{callback.Result}");
            _isLoggedIn = false;
            _isRunning = false;
            return;
        }

        Logger.Info("[SteamClient] ✓ 登录成功！");
        _isLoggedIn = true;
        _steamId = callback.ClientSteamID;
        Logger.Info($"[SteamClient] SteamID: {_steamId}");
        _isRunning = false;
    }

    private void OnLoggedOff(LoggedOffCallback callback)
    {
        Logger.Info($"[SteamClient] 已登出：{callback.Result}");
    }

    private void DrawQRCode(QrAuthSession authSession)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(authSession.ChallengeURL, QRCodeGenerator.ECCLevel.L);
            
            var pixelSize = 20;
            var quietZones = 0;
            var size = (qrCodeData.ModuleMatrix.Count + quietZones * 2) * pixelSize;
            
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.White);
            
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
            
            g.Flush();
            Logger.Info($"[SteamClient] ✓ 二维码绘制完成");
            
            var clonedBitmap = new Bitmap(bitmap);
            OnQRCodeGenerated?.Invoke(clonedBitmap);
            
            Logger.Info("=== 请使用 Steam 手机应用扫描二维码 ===");
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] 生成二维码失败：{ex.Message}");
        }
    }

    private void SaveAccessToken(string token)
    {
        try
        {
            var config = ConfigManager.LoadConfig() ?? new SteamConfig();
            config.AccessToken = token;
            ConfigManager.SaveConfig(config);
            Logger.Info("[SteamClient] ✓ Access Token 已保存");
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] ✗ 保存 Access Token 失败：{ex.Message}");
        }
    }

    private void SaveRefreshToken(string token)
    {
        try
        {
            var config = ConfigManager.LoadConfig() ?? new SteamConfig();
            config.RefreshToken = token;
            ConfigManager.SaveConfig(config);
            Logger.Info("[SteamClient] ✓ Refresh Token 已保存");
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] ✗ 保存 Refresh Token 失败：{ex.Message}");
        }
    }

    private void SaveGuardData(string data)
    {
        try
        {
            var config = ConfigManager.LoadConfig() ?? new SteamConfig();
            config.GuardData = data;
            ConfigManager.SaveConfig(config);
            Logger.Info("[SteamClient] ✓ GuardData 已保存");
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] ✗ 保存 GuardData 失败：{ex.Message}");
        }
    }

    public static string? LoadGuardData()
    {
        try
        {
            var config = ConfigManager.LoadConfig();
            if (config != null && !string.IsNullOrEmpty(config.GuardData))
            {
                Logger.Info("[Config] ✓ 已读取 GuardData");
                return config.GuardData;
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[Config] ✗ 读取 GuardData 失败：{ex.Message}");
        }
        return null;
    }

    public static string? LoadAccessToken()
    {
        try
        {
            var config = ConfigManager.LoadConfig();
            if (config != null && !string.IsNullOrEmpty(config.AccessToken))
            {
                Logger.Info("[Config] ✓ 已读取 Access Token");
                return config.AccessToken;
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[Config] ✗ 读取 Access Token 失败：{ex.Message}");
        }
        return null;
    }

    public static string? LoadRefreshToken()
    {
        try
        {
            var config = ConfigManager.LoadConfig();
            if (config != null && !string.IsNullOrEmpty(config.RefreshToken))
            {
                Logger.Info("[Config] ✓ 已读取 Refresh Token");
                return config.RefreshToken;
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[Config] ✗ 读取 Refresh Token 失败：{ex.Message}");
        }
        return null;
    }

    private void SaveUsername(string username)
    {
        try
        {
            var config = ConfigManager.LoadConfig() ?? new SteamConfig();
            config.Username = username;
            ConfigManager.SaveConfig(config);
            Logger.Info("[SteamClient] ✓ 用户名已保存");
        }
        catch (Exception ex)
        {
            Logger.Info($"[SteamClient] ✗ 保存用户名失败：{ex.Message}");
        }
    }

    public static string? LoadUsername()
    {
        try
        {
            var config = ConfigManager.LoadConfig();
            if (config != null && !string.IsNullOrEmpty(config.Username))
            {
                Logger.Info($"[Config] ✓ 已读取用户名：{config.Username}");
                return config.Username;
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[Config] ✗ 读取用户名失败：{ex.Message}");
        }
        return null;
    }

    public void Disconnect()
    {
        _client?.Disconnect();

        while (_isConnected)
        {
            _callbackManager?.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            Task.Delay(100).Wait();
        }
    }

    private string? _guardData;
}
