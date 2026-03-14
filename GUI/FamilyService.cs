using SteamKit2;
using SteamKit2.Internal;

namespace SteamContribution;

public class FamilyMember
{
    public ulong SteamId { get; set; }
    public uint Role { get; set; }
    public uint TimeJoined { get; set; }
    public uint CooldownSecondsRemaining { get; set; }
    public string? Nickname { get; set; }
    public string? AvatarUrl { get; set; }
    
    public string RoleName => Role switch
    {
        0 => "Unknown",
        1 => "Adult",
        2 => "Child",
        _ => $"Unknown({Role})"
    };
}

public class FamilyService
{
    private readonly SteamClientManager _clientManager;

    public FamilyService(SteamClientManager clientManager) => _clientManager = clientManager;

    private Dictionary<ulong, (string Nickname, string AvatarUrl)> GetUserSummaries(List<ulong> steamIds)
    {
        var result = new Dictionary<ulong, (string Nickname, string AvatarUrl)>();

        if (string.IsNullOrEmpty(_clientManager.AccessToken) || steamIds.Count == 0)
            return result;

        try
        {
            Logger.Info($"[FamilyService] 请求 {steamIds.Count} 个用户的详细信息");

            using (dynamic steamUserOAuth = WebAPI.GetInterface("ISteamUserOAuth"))
            {
                steamUserOAuth.Timeout = TimeSpan.FromSeconds(10);
                var steamIdsString = string.Join(",", steamIds);

                KeyValue kvResponse = steamUserOAuth.GetUserSummaries(
                    access_token: _clientManager.AccessToken,
                    steamids: steamIdsString
                );

                var players = kvResponse["players"] ?? kvResponse["response"]?["players"];
                
                if (players != null)
                {
                    foreach (var player in players.Children)
                    {
                        if (player["steamid"] != null && ulong.TryParse(player["steamid"].Value, out var steamId))
                        {
                            result[steamId] = (
                                player["personaname"]?.AsString() ?? "",
                                player["avatarmedium"]?.AsString() ?? ""
                            );
                        }
                    }
                }

                Logger.Info($"[FamilyService] 成功获取 {result.Count} 个用户的详细信息");
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[FamilyService] 获取用户详细信息失败：{ex.Message}");
        }

        return result;
    }

    public (string Name, List<FamilyMember> Members, ulong FamilyGroupId)? GetFullFamilyInfoAsync()
    {
        if (_clientManager.SteamId == null || string.IsNullOrEmpty(_clientManager.AccessToken))
        {
            Logger.Info("[FamilyService] 未登录或无 Access Token");
            return null;
        }

        Logger.Info("[FamilyService] 请求完整家庭组信息");

        try
        {
            using (dynamic familyGroupsService = WebAPI.GetInterface("IFamilyGroupsService"))
            {
                KeyValue kvResponse = familyGroupsService.GetFamilyGroupForUser(
                    access_token: _clientManager.AccessToken,
                    steamid: _clientManager.SteamId.ConvertToUInt64(),
                    include_family_group_response: true
                );

                if (kvResponse["family_groupid"] == null)
                    return null;

                ulong familyGroupId = kvResponse["family_groupid"].AsUnsignedLong();
                var familyGroupKv = kvResponse["family_group"];
                
                if (familyGroupKv == null)
                    return null;

                var name = familyGroupKv["name"].AsString();
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

                var userSummaries = GetUserSummaries(memberSteamIds);
                
                foreach (var member in members)
                {
                    if (userSummaries.TryGetValue(member.SteamId, out var userInfo))
                    {
                        member.Nickname = userInfo.Nickname;
                        member.AvatarUrl = userInfo.AvatarUrl;
                    }
                }

                Logger.Info($"[FamilyService] 获取到家庭组：{name}, 成员数量：{members.Count}");
                return (name, members, familyGroupId);
            }
        }
        catch (Exception ex)
        {
            Logger.Info($"[FamilyService] 获取完整家庭组信息失败：{ex.Message}");
            return null;
        }
    }
}
