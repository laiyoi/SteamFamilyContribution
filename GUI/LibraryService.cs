using SteamKit2;
using SteamKit2.Internal;
using System.IO;
using System.Text.Json.Serialization;

namespace SteamContribution;

public class SharedApp
{
    public uint AppId { get; set; }
    public ulong OwnerId { get; set; }
    public string Name { get; set; } = "";
}

public class LibraryService
{
    private readonly SteamClientManager _clientManager;

    public LibraryService(SteamClientManager clientManager) => _clientManager = clientManager;

    public List<SharedApp> GetSharedLibraryAppsAsync(ulong steamId, ulong familyGroupId)
    {
        Logger.Info($"[LibraryService] 获取共享游戏库...");

        if (string.IsNullOrEmpty(_clientManager.AccessToken))
        {
            Logger.Info("[LibraryService] 未获取到 Access Token");
            return new List<SharedApp>();
        }

        try
        {
            using (dynamic familyGroupsService = WebAPI.GetInterface("IFamilyGroupsService"))
            {
                KeyValue kvResponse = familyGroupsService.GetSharedLibraryApps(
                    access_token: _clientManager.AccessToken,
                    family_groupid: familyGroupId,
                    include_Own: true,
                    include_Excluded: false,
                    include_free: false,
                    language: "schinese"
                );

                var sharedApps = new List<SharedApp>();

                foreach (var app in kvResponse["apps"].Children)
                {
                    var appId = app["appid"].AsUnsignedInteger();
                    var appName = app["name"].AsString();

                    if (app["owner_steamids"] != null)
                    {
                        foreach (var ownerSteamId in app["owner_steamids"].Children)
                        {
                            if (ulong.TryParse(ownerSteamId.Value, out var parsedSteamId))
                            {
                                sharedApps.Add(new SharedApp
                                {
                                    AppId = appId,
                                    Name = appName,
                                    OwnerId = parsedSteamId
                                });
                            }
                        }
                    }
                    else
                    {
                        sharedApps.Add(new SharedApp { AppId = appId, Name = appName });
                    }
                }

                Logger.Info($"[LibraryService] 成功获取到 {sharedApps.Count} 个共享游戏");
                return sharedApps;
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[LibraryService] 请求失败：{ex.Message}");
            return new List<SharedApp>();
        }
    }
}
