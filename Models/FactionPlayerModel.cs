public class FactionPlayerModel
{
    public long PlayerId { get; set; }
    public long FactionId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; } // soft delete
}
