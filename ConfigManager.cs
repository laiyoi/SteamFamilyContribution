using System.Text.Json;

namespace SteamContribution;

public class SteamConfig
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string CountryCode { get; set; } = "CN"; // 默认国家代码为中国
    // 注意：AuthCode 和 TwoFactorCode 是一次性验证码，不应保存到配置文件
}

public class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "steam_config.json"
    );

    /// <summary>
    /// 读取配置文件
    /// </summary>
    public static SteamConfig? LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<SteamConfig>(json);
            
            if (config != null && !string.IsNullOrEmpty(config.Username) && config.Username != "your_steam_username")
            {
                return config;
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    /// <summary>
    /// 保存配置文件
    /// </summary>
    public static void SaveConfig(SteamConfig config)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception)
        {
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
            ApiKey = "your_steam_web_api_key",
            CountryCode = "CN"
        };

        SaveConfig(config);
        
        Console.WriteLine();
        Console.WriteLine("配置文件已创建，请编辑 steam_config.json 文件填入您的 Steam 账号信息");
        Console.WriteLine();
    }
}
