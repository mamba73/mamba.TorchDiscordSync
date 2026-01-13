public class PlayerModel
{
    public long PlayerId { get; set; }
    public string OriginalName { get; set; } = ""; // Discord nickname prije synca
    public string CurrentName { get; set; } = "";  // [TAG] OriginalName
    public long? SteamId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; } // soft delete, undo
}
