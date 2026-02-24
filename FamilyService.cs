using SteamKit2;
using SteamKit2.Internal;

namespace SteamContribution;

public class FamilyService
{
    private readonly SteamClientManager _clientManager;

    public FamilyService(SteamClientManager clientManager)
    {
        _clientManager = clientManager;
    }



    /// <summary>
    /// 使用 ISteamUserOAuth/GetUserSummaries 获取用户昵称和头像
    /// </summary>
    /// <param name="steamIds">Steam ID 列表</param>
    /// <returns>Steam ID 到用户信息的映射</returns>
    private Dictionary<ulong, (string Nickname, string AvatarUrl)> GetUserSummaries(List<ulong> steamIds)
    {
        var result = new Dictionary<ulong, (string Nickname, string AvatarUrl)>();

        if (string.IsNullOrEmpty(_clientManager.AccessToken) || steamIds.Count == 0)
        {
            return result;
        }

        try
        {
            Console.WriteLine($"[FamilyService] 请求 {steamIds.Count} 个用户的详细信息");

            // 使用 WebAPI.GetInterface 获取 ISteamUserOAuth 接口
            using (dynamic steamUserOAuth = WebAPI.GetInterface("ISteamUserOAuth"))
            {
                // 设置超时
                steamUserOAuth.Timeout = TimeSpan.FromSeconds(10);

                // 构建逗号分隔的 Steam ID 列表
                var steamIdsString = string.Join(",", steamIds);

                // 调用 GetUserSummaries 方法
                KeyValue kvResponse = steamUserOAuth.GetUserSummaries(
                    access_token: _clientManager.AccessToken,
                    steamids: steamIdsString
                    );

                // 解析响应
                // 首先尝试直接访问 players（根据用户提供的响应格式）
                if (kvResponse["players"] != null)
                {
                    foreach (var player in kvResponse["players"].Children)
                    {
                        if (player["steamid"] != null && ulong.TryParse(player["steamid"].Value, out var steamId))
                        {
                            var nickname = player["personaname"]?.AsString() ?? "";
                            var avatarUrl = player["avatarmedium"]?.AsString() ?? "";
                            
                            result[steamId] = (nickname, avatarUrl);
                        }
                    }
                }
                // 然后尝试通过 response.players 访问（兼容其他可能的响应格式）
                else if (kvResponse["response"] != null && kvResponse["response"]["players"] != null)
                {
                    foreach (var player in kvResponse["response"]["players"].Children)
                    {
                        if (player["steamid"] != null && ulong.TryParse(player["steamid"].Value, out var steamId))
                        {
                            var nickname = player["personaname"]?.AsString() ?? "";
                            var avatarUrl = player["avatarmedium"]?.AsString() ?? "";
                            
                            result[steamId] = (nickname, avatarUrl);
                        }
                    }
                }

                Console.WriteLine($"[FamilyService] 成功获取 {result.Count} 个用户的详细信息");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FamilyService] 获取用户详细信息失败：{ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 存储家庭组 ID，避免重复调用 API
    /// </summary>
    private ulong? _cachedFamilyGroupId = null;

    /// <summary>
    /// 获取完整的家庭组信息（包括成员列表和家庭组 ID）
    /// </summary>
    public (string Name, List<FamilyMember> Members, ulong FamilyGroupId)? GetFullFamilyInfoAsync()
    {
        if (_clientManager.SteamId == null)
        {
            Console.WriteLine("[FamilyService] 未登录，无法获取家庭组信息");
            return null;
        }

        if (string.IsNullOrEmpty(_clientManager.AccessToken))
        {
            Console.WriteLine("[FamilyService] 未获取到 Access Token");
            return null;
        }

        Console.WriteLine("[FamilyService] 请求完整家庭组信息");
        Console.WriteLine($"[FamilyService] SteamID: {_clientManager.SteamId.ConvertToUInt64()}");

        try
        {
            // 使用 WebAPI.GetInterface 获取 IFamilyGroupsService 接口
            using (dynamic familyGroupsService = WebAPI.GetInterface("IFamilyGroupsService"))
            {
                // 调用 GetFamilyGroupForUser 方法，包含完整的家庭组信息
                KeyValue kvResponse = familyGroupsService.GetFamilyGroupForUser(
                    access_token: _clientManager.AccessToken,
                    steamid: _clientManager.SteamId.ConvertToUInt64(),
                    include_family_group_response: true);

                // 尝试获取家庭组 ID
                ulong familyGroupId = 0;
                if (kvResponse["family_groupid"] != null)
                {
                    familyGroupId = kvResponse["family_groupid"].AsUnsignedLong();
                    _cachedFamilyGroupId = familyGroupId;
                }

                // 尝试获取 family_group
                var familyGroupKv = kvResponse["family_group"];
                if (familyGroupKv != null && familyGroupId > 0)
                {
                    // 尝试获取家庭组名称
                    var name = familyGroupKv["name"].AsString();

                    // 尝试获取成员列表
                    var members = new List<FamilyMember>();
                    var memberSteamIds = new List<ulong>();
                    
                    foreach (var member in familyGroupKv["members"].Children)
                    {
                        var familyMember = new FamilyMember
                        {
                            SteamId = member["steamid"].AsUnsignedLong(),
                            Role = (uint)member["role"].AsInteger(),
                            TimeJoined = member["time_joined"].AsUnsignedInteger(),
                            CooldownSecondsRemaining = member["cooldown_seconds_remaining"].AsUnsignedInteger()
                        };
                        members.Add(familyMember);
                        memberSteamIds.Add(familyMember.SteamId);
                    }

                    // 获取成员的昵称和头像
                    var userSummaries = GetUserSummaries(memberSteamIds);
                    
                    // 填充成员的昵称和头像
                    foreach (var member in members)
                    {
                        if (userSummaries.TryGetValue(member.SteamId, out var userInfo))
                        {
                            member.Nickname = userInfo.Nickname;
                            member.AvatarUrl = userInfo.AvatarUrl;
                        }
                    }

                    Console.WriteLine($"[FamilyService] 获取到家庭组：{name}");
                    Console.WriteLine($"[FamilyService] 成员数量：{members.Count}");
                    Console.WriteLine($"[FamilyService] 家庭组 ID：{familyGroupId}");
                    
                    // 显示成员详细信息
                    foreach (var member in members)
                    {
                        Console.WriteLine($"[FamilyService]   成员：{member.SteamId}, 昵称：{member.Nickname ?? "未知"}, 角色：{member.RoleName}");
                    }
                    
                    return (name, members, familyGroupId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FamilyService] 获取完整家庭组信息失败：{ex.Message}");
        }

        Console.WriteLine("[FamilyService] 响应中无家庭组信息");
        return null;
    }

}
