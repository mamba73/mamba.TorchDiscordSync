using System;
using System.Collections.Generic;

namespace mamba.TorchDiscordSync.Models
{
    public class FactionModel
    {
        public int FactionId { get; set; }
        public string Tag { get; set; }
        public string Name { get; set; }
        public List<ulong> Members { get; set; } = new List<ulong>();

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
