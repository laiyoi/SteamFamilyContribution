using System.Text.Json;

namespace SteamContribution;

public class ResultService
{
    private static readonly string ResultsDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "results"
    );

    public static void SaveQueryResult(QueryResult result)
    {
        try
        {
            Directory.CreateDirectory(ResultsDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"query_result_{timestamp}.json";
            var filepath = Path.Combine(ResultsDirectory, filename);

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

    public static QueryResult? LoadQueryResult(string filepath)
    {
        try
        {
            if (!File.Exists(filepath))
                return null;

            var json = File.ReadAllText(filepath);
            return JsonSerializer.Deserialize<QueryResult>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static List<string> GetResultFiles()
    {
        try
        {
            if (!Directory.Exists(ResultsDirectory))
                return new List<string>();

            return Directory.GetFiles(ResultsDirectory, "query_result_*.json")
                .OrderByDescending(f => f)
                .ToList();
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }
}

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

public class MemberResult
{
    public ulong SteamId { get; set; }
    public string RoleName { get; set; } = "";
    public string? Nickname { get; set; }
    public string? AvatarUrl { get; set; }
    public double Contribution { get; set; }
    public double UniqueContribution { get; set; }
    public int OwnedGameCount { get; set; }
    public List<uint> OwnedGameIds { get; set; } = new List<uint>();
}

public class SharedGameResult
{
    public uint AppId { get; set; }
    public string Name { get; set; } = "";
    public ulong OwnerId { get; set; }
    public double Price { get; set; }
    public bool IsFree { get; set; }
}
