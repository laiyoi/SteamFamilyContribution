namespace SteamContribution;

public class MemberContribution
{
    public ulong SteamId { get; set; }
    public string RoleName { get; set; } = "";
    public double Contribution { get; set; }
    public double UniqueContribution { get; set; }  // 去重后的贡献
    public int OwnedGameCount { get; set; }
}
