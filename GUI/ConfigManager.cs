using System.Text.Json;

namespace SteamContribution;

public class SteamConfig
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string CountryCode { get; set; } = "CN";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string GuardData { get; set; } = "";
}

public class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "steam_config.json"
    );
    
    private static readonly object _configLock = new object();

    public static SteamConfig? LoadConfig()
    {
        lock (_configLock)
        {
            if (!File.Exists(ConfigPath))
                return new SteamConfig();

            try
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<SteamConfig>(json) ?? new SteamConfig();
            }
            catch (Exception ex)
            {
                Logger.Info($"[Config] ✗ 读取配置文件失败：{ex.Message}");
                return new SteamConfig();
            }
        }
    }

    public static void SaveConfig(SteamConfig config)
    {
        lock (_configLock)
        {
            try
            {
                var configDir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                    Directory.CreateDirectory(configDir);

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

    public static bool ConfigExists() => File.Exists(ConfigPath);
}
