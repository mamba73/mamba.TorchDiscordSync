using System;
using System.Collections.Generic;

namespace mamba.TorchDiscordSync.Models
{
    public class FactionModel
    {
        public int FactionID { get; set; }
        public string Tag { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<FactionPlayerModel> Players { get; set; } = new List<FactionPlayerModel>();
    }
}
