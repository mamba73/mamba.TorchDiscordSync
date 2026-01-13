using System;
using System.Collections.Generic;

namespace mamba.TorchDiscordSync.Models
{
    public class FactionModel
    {
        public long FactionID { get; set; }
        public string Tag { get; set; }
        public string Name { get; set; }
        public List<PlayerModel> Members { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public FactionModel()
        {
            Members = new List<PlayerModel>();
        }
    }
}
