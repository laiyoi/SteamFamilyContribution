using System.Text.Json;

namespace SteamContribution;

public class ResultService
{
    private static readonly string ResultsDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "results"
    );

    /// <summary>
    /// 保存查询结果到文件
    /// </summary>
    public static void SaveQueryResult(QueryResult result)
    {
        try
        {
            // 确保结果目录存在
            Directory.CreateDirectory(ResultsDirectory);

            // 生成文件名，包含时间戳
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"query_result_{timestamp}.json";
            var filepath = Path.Combine(ResultsDirectory, filename);

            // 序列化结果
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(result, options);
            File.WriteAllText(filepath, json);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// 加载查询结果从文件
    /// </summary>
    public static QueryResult? LoadQueryResult(string filepath)
    {
        try
        {
            if (!File.Exists(filepath))
            {
                return null;
            }

            var json = File.ReadAllText(filepath);
            var result = JsonSerializer.Deserialize<QueryResult>(json);

            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 获取所有结果文件列表
    /// </summary>
    public static List<string> GetResultFiles()
    {
        try
        {
            if (!Directory.Exists(ResultsDirectory))
            {
                return new List<string>();
            }

            var files = Directory.GetFiles(ResultsDirectory, "query_result_*.json")
                .OrderByDescending(f => f)
                .ToList();

            return files;
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// 分析已保存的结果
    /// </summary>
    public static void AnalyzeSavedResult(QueryResult result)
    {
        Logger.Info("=== 分析已保存的查询结果 ===");
        Logger.Info($"查询时间：{result.Timestamp}");
        Logger.Info($"家庭组：{result.FamilyGroupName}");
        Logger.Info($"成员数量：{result.FamilyMemberCount}");
        Logger.Info($"总贡献金额：{result.TotalContribution:F2} {result.Currency}");
        Logger.Info($"共享游戏数量：{result.SharedGames.Count}");

        foreach (var member in result.Members.OrderByDescending(m => m.Contribution))
        {
            double ratio = result.TotalContribution > 0 ? (member.Contribution / result.TotalContribution) * 100 : 0;
            Logger.Info($"成员：{member.SteamId} ({member.RoleName})");
            Logger.Info($"  贡献金额：{member.Contribution:F2} {result.Currency}");
            Logger.Info($"  贡献比率：{ratio:F2}%");
            Logger.Info($"  拥有游戏数：{member.OwnedGameCount}");
        }

        Logger.Info("=== 分析完成 ===");
    }
}

// 查询结果类
public class QueryResult
{
    public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string FamilyGroupName { get; set; } = "";
    public int FamilyMemberCount { get; set; }
    public List<MemberResult> Members { get; set; } = new List<MemberResult>();
    public List<SharedGameResult> SharedGames { get; set; } = new List<SharedGameResult>();
    public double TotalContribution { get; set; }
    public string Currency { get; set; } = "CNY";
}

// 成员结果类
public class MemberResult
{
    public ulong SteamId { get; set; }
    public string RoleName { get; set; } = "";
    public string? Nickname { get; set; }
    public string? AvatarUrl { get; set; }
    public double Contribution { get; set; }
    public double UniqueContribution { get; set; }  // 去重后的贡献
    public int OwnedGameCount { get; set; }
    public List<uint> OwnedGameIds { get; set; } = new List<uint>();
}

// 共享游戏结果类
public class SharedGameResult
{
    public uint AppId { get; set; }
    public string Name { get; set; } = "";
    public ulong OwnerId { get; set; }
    public double Price { get; set; }
    public bool IsFree { get; set; }
}
