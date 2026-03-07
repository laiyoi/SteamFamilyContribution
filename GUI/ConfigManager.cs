using System.Text.Json;

namespace SteamContribution;

public class SteamConfig
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string CountryCode { get; set; } = "CN"; // 默认国家代码为中国
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string GuardData { get; set; } = "";
    // 注意：AuthCode 和 TwoFactorCode 是一次性验证码，不应保存到配置文件
}

public class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "steam_config.json"
    );
    
    // 使用锁避免并发写入
    private static readonly object _configLock = new object();

    /// <summary>
    /// 读取配置文件
    /// </summary>
    public static SteamConfig? LoadConfig()
    {
        lock (_configLock)
        {
            if (!File.Exists(ConfigPath))
            {
                return new SteamConfig(); // 返回新对象而不是 null
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<SteamConfig>(json);
                
                // 直接返回配置对象，不要检查默认值
                return config ?? new SteamConfig();
            }
            catch (Exception ex)
            {
                Logger.Info($"[Config] ✗ 读取配置文件失败：{ex.Message}");
                return new SteamConfig(); // 返回新对象
            }
        }
    }

    /// <summary>
    /// 保存配置文件
    /// </summary>
    public static void SaveConfig(SteamConfig config)
    {
        lock (_configLock)
        {
            try
            {
                // 确保配置文件所在目录存在
                var configDir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Logger.Info($"[Config] ✗ 保存配置文件失败：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 检查配置文件是否存在
    /// </summary>
    public static bool ConfigExists()
    {
        return File.Exists(ConfigPath);
    }

    /// <summary>
    /// 创建示例配置文件
    /// </summary>
    public static void CreateSampleConfig()
    {
        var config = new SteamConfig
        {
            Username = "your_steam_username",
            Password = "your_steam_password",
            CountryCode = "CN"
        };

        SaveConfig(config);
        
        Logger.Info("配置文件已创建，请编辑 steam_config.json 文件填入您的 Steam 账号信息");
    }
}
