using SteamKit2.Internal;
using System.Runtime.Serialization;
using System.Text;

namespace SteamContribution;

/// <summary>
/// 使用 SteamKit2 内置的 Protobuf 类
/// 这些类在 SteamKit2.Internal 命名空间中
/// </summary>

// 简单的包装类以便在其他服务中使用
public class FamilyMember
{
    public ulong SteamId { get; set; }
    public uint Role { get; set; }
    public uint TimeJoined { get; set; }
    public uint CooldownSecondsRemaining { get; set; }
    public string? Nickname { get; set; } // 昵称
    public string? AvatarUrl { get; set; } // 头像 URL
    
    public string RoleName => Role switch
    {
        0 => "Unknown",
        1 => "Adult",
        2 => "Child",
        _ => $"Unknown({Role})"
    };
}

public class SharedApp
{
    public uint AppId { get; set; }
    public ulong OwnerId { get; set; }
    public string Name { get; set; } = "";
    public List<ulong> OwnerSteamIds { get; set; } = new List<ulong>();
}

// GetSharedLibraryApps 请求和响应类
[DataContract]
public class CFamilyGroups_SharedLibraryApps_Request
{
    [DataMember(Order = 1)]
    public ulong steamid { get; set; }
    
    [DataMember(Order = 2)]
    public ulong family_groupid { get; set; }
}

[DataContract]
public class CFamilyGroups_SharedLibraryApps_Response
{
    [DataContract]
    public class AppInfo
    {
        [DataMember(Order = 1)]
        public uint appid { get; set; }
        
        [DataMember(Order = 2)]
        public ulong owner_steamid { get; set; }
    }
    
    [DataMember(Order = 1)]
    public List<AppInfo> apps { get; set; } = new List<AppInfo>();
    
    [DataMember(Order = 2, EmitDefaultValue = false)]
    public uint? status { get; set; }
    
    [DataMember(Order = 3, EmitDefaultValue = false)]
    public string? error_message { get; set; }
    
    [DataMember(Order = 4, EmitDefaultValue = false)]
    public uint? total_count { get; set; }
    
    [DataMember(Order = 5, EmitDefaultValue = false)]
    public ulong? request_steamid { get; set; }
}

public static class SteamFamilyProtos
{
    /// <summary>
    /// 创建 GetFamilyGroupForUser 请求
    /// </summary>
    public static CFamilyGroups_GetFamilyGroupForUser_Request CreateGetFamilyGroupForUserRequest(ulong steamId)
    {
        return new CFamilyGroups_GetFamilyGroupForUser_Request
        {
            steamid = steamId,
        };
    }

    /// <summary>
    /// 创建 GetFamilyGroup 请求
    /// </summary>
    public static CFamilyGroups_GetFamilyGroup_Request CreateGetFamilyGroupRequest(ulong familyGroupId, bool sendRunningApps = false)
    {
        return new CFamilyGroups_GetFamilyGroup_Request
        {
            family_groupid = familyGroupId,
            send_running_apps = sendRunningApps,
        };
    }

    /// <summary>
    /// 创建 GetSharedLibraryApps 请求
    /// </summary>
    public static CFamilyGroups_SharedLibraryApps_Request CreateGetSharedLibraryAppsRequest(ulong steamId, ulong familyGroupId)
    {
        return new CFamilyGroups_SharedLibraryApps_Request
        {
            steamid = steamId,
            family_groupid = familyGroupId,
        };
    }

    /// <summary>
    /// 获取角色名称
    /// </summary>
    public static string GetRoleName(int role)
    {
        return role switch
        {
            0 => "Unknown",
            1 => "Adult",
            2 => "Child",
            _ => $"Unknown({role})"
        };
    }

    /// <summary>
    /// 手动构建 GetSharedLibraryApps 请求
    /// </summary>
    public static string BuildSharedLibraryAppsRequest(
        ulong steamId,
        bool includeOwn = true,
        bool includeExcluded = false,
        bool include_free = false,
        string language = "schinese"
    )
    {
        using var ms = new MemoryStream();

        // field 1: steamid (fixed64)
        WriteTag(ms, 1, 1);
        WriteFixed64(ms, steamId);

        // field 2: include_own
        WriteTag(ms, 2, 0);
        WriteVarint(ms, includeOwn ? 1UL : 0UL);

        // field 3: include_excluded
        WriteTag(ms, 3, 0);
        WriteVarint(ms, includeExcluded ? 1UL : 0UL);

        // field 4: include_free
        WriteTag(ms, 4, 0);
        WriteVarint(ms, include_free ? 1UL : 0UL);

        // field 5: language (length-delimited) - "schinese"
        WriteTag(ms, 5, 2);
        var langBytes = Encoding.UTF8.GetBytes(language);
        WriteVarint(ms, (ulong)langBytes.Length);
        ms.Write(langBytes);

        var protoBytes = ms.ToArray();

        var base64 = Convert.ToBase64String(protoBytes);
        var escaped = Uri.EscapeDataString(base64);

        Console.WriteLine($"[Protobuf] 生成结果:");
        Console.WriteLine($"  Base64: {base64}");
        Console.WriteLine($"  URL编码: {escaped}");
        Console.WriteLine($"  二进制: {BitConverter.ToString(protoBytes).Replace("-", " ")}");
        Console.WriteLine($"  参数: steamId={steamId}, includeOwn={includeOwn}, includeExcluded={includeExcluded}, language={language}");

        return escaped;
    }

    /// <summary>
    /// 写入 fixed64 编码
    /// </summary>
    private static void WriteFixed64(Stream stream, ulong value)
    {
        var bytes = BitConverter.GetBytes(value); // little endian
        stream.Write(bytes, 0, 8);
    }

    /// <summary>
    /// 写入 Protobuf tag
    /// </summary>
    private static void WriteTag(Stream stream, int fieldNumber, int wireType)
    {
        var tag = (ulong)(fieldNumber << 3 | wireType);
        WriteVarint(stream, tag);
    }

    /// <summary>
    /// 写入 varint 编码
    /// </summary>
    private static void WriteVarint(Stream stream, ulong value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value > 0)
            {
                b |= 0x80;
            }
            stream.WriteByte(b);
        } while (value > 0);
    }
}
