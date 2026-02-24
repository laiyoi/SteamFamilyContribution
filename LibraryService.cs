using SteamKit2;
using SteamKit2.Internal;
using System;
using System.IO;
using System.Text.Json.Serialization;

namespace SteamContribution;

public class LibraryService
{
    private readonly SteamClientManager _clientManager;

    public LibraryService(SteamClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    /// <summary>
    /// 获取家庭成员共享的游戏列表
    /// 逻辑：GetSharedLibraryApps 获取共享库全部游戏，再查询每个人的游戏
    /// </summary>
    public List<SharedApp> GetSharedLibraryAppsAsync(ulong steamId, ulong familyGroupId)
    {
        Console.WriteLine($"[LibraryService] 获取共享游戏库...");
        Console.WriteLine($"[LibraryService] SteamID: {steamId}");
        Console.WriteLine($"[LibraryService] 家庭组 ID: {familyGroupId}");

        if (string.IsNullOrEmpty(_clientManager.AccessToken))
        {
            Console.WriteLine("[LibraryService] 未获取到 Access Token");
            return new List<SharedApp>();
        }

        try
        {
            // 使用 WebAPI.GetInterface 获取 IFamilyGroupsService 接口
            using (dynamic familyGroupsService = WebAPI.GetInterface( "IFamilyGroupsService" ))
            {

                // 调用 GetSharedLibraryApps 方法
                KeyValue kvResponse = familyGroupsService.GetSharedLibraryApps(
                    access_token: _clientManager.AccessToken,
                    family_groupid: familyGroupId,
                    include_Own: true,
                    include_Excluded: false,
                    include_free: false,
                    language: "schinese"
                );

                // 转换为 SharedApp 列表
                var sharedApps = new List<SharedApp>();

                // 尝试获取应用列表
                foreach (var app in kvResponse["apps"].Children)
                {
                    var appId = app["appid"].AsUnsignedInteger();
                    var appName = app["name"].AsString(); // 从响应中获取游戏名称

                    // 获取所有者 Steam ID 列表
                    if (app["owner_steamids"] != null)
                    {
                        foreach (var ownerSteamId in app["owner_steamids"].Children)
                        {
                            if (ulong.TryParse(ownerSteamId.Value, out var parsedSteamId))
                            {
                                // 为每个所有者创建一个单独的 SharedApp 对象，实现重复计算
                                var sharedApp = new SharedApp
                                {
                                    AppId = appId,
                                    Name = appName,
                                    OwnerId = parsedSteamId // 设置当前所有者为 OwnerId
                                };

                                // 添加到共享游戏列表
                                sharedApps.Add(sharedApp);
                            }
                        }
                    }
                    else
                    {
                        // 如果没有所有者信息，创建一个默认的 SharedApp 对象
                        var sharedApp = new SharedApp
                        {
                            AppId = appId,
                            Name = appName
                        };
                        sharedApps.Add(sharedApp);
                    }
                }

                // 保存解码后的纯文本 response 数据
                SaveResponseToFile(kvResponse, sharedApps);

                Console.WriteLine($"[LibraryService] 成功获取到 {sharedApps.Count} 个共享游戏");
                
                return sharedApps;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LibraryService] 请求失败：{ex.Message}");
            return new List<SharedApp>();
        }
    }
    
    /// <summary>
    /// 保存解码后的 response 数据到 JSON 文件
    /// </summary>
    /// <param name="kvResponse">KeyValue 响应对象</param>
    /// <param name="sharedApps">解析后的共享游戏列表</param>
    private void SaveResponseToFile(KeyValue kvResponse, List<SharedApp> sharedApps)
    {
        try
        {
            // 创建保存目录
            var saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ResponseData");
            Directory.CreateDirectory(saveDir);
            
            // 生成文件名（包含时间戳）
            var fileName = $"response_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(saveDir, fileName);
            
            // 构建完整的响应信息对象
            var responseInfo = new
            {
                // 响应元数据
                metadata = new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    app_count = sharedApps.Count
                },
                // 游戏详细信息
                apps = sharedApps.Select(app => new
                {
                    appid = app.AppId,
                    owner_steamid = app.OwnerId,
                    name = app.Name
                }).ToList(),
                // 原始响应字符串
                original_response = kvResponse.ToString()
            };
            
            // 序列化到 JSON
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true, // 格式化输出
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(responseInfo, jsonOptions);
            
            // 写入文件
            File.WriteAllText(filePath, jsonContent);
            
            Console.WriteLine($"[LibraryService] 响应数据已保存到：{filePath}");
            Console.WriteLine($"[LibraryService] 保存了 {sharedApps.Count} 个游戏的详细信息");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LibraryService] 保存响应数据失败：{ex.Message}");
        }
    }
}