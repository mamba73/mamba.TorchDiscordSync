public class FactionModel
{
    public long FactionId { get; set; }
    public string Tag { get; set; } = "";
    public string Name { get; set; } = "";
    // Leader SteamID još uvijek može postojati, ali nije potreban za Discord
    public long? LeaderSteamId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; } // optional soft delete
}
