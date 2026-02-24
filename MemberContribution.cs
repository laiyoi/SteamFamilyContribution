namespace SteamContribution;

public class MemberContribution
{
    public ulong SteamId { get; set; }
    public string RoleName { get; set; } = "";
    public double Contribution { get; set; }
    public int OwnedGameCount { get; set; }
}
