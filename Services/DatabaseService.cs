using System;
using System.Data.SQLite;
using System.IO;
using mamba.TorchDiscordSync.Models;

namespace mamba.TorchDiscordSync.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly SQLiteConnection _connection;

        public DatabaseService(string dbPath)
        {
            _dbPath = dbPath;
            var folder = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var createNew = !File.Exists(_dbPath);
            _connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
            _connection.Open();

            if (createNew)
                CreateTables();
        }

        private void CreateTables()
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Factions(
    FactionID INTEGER PRIMARY KEY,
    Tag TEXT,
    Name TEXT,
    CreatedAt TEXT,
    UpdatedAt TEXT
);
CREATE TABLE IF NOT EXISTS Players(
    SteamID INTEGER PRIMARY KEY,
    OriginalNick TEXT,
    SyncedNick TEXT,
    FactionID INTEGER,
    CreatedAt TEXT,
    UpdatedAt TEXT
);";
                cmd.ExecuteNonQuery();
            }
        }

        public void SaveFaction(FactionModel faction)
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = @"
INSERT OR REPLACE INTO Factions(FactionID, Tag, Name, CreatedAt, UpdatedAt)
VALUES(@FactionID, @Tag, @Name, @CreatedAt, @UpdatedAt);";
                cmd.Parameters.AddWithValue("@FactionID", faction.FactionID);
                cmd.Parameters.AddWithValue("@Tag", faction.Tag);
                cmd.Parameters.AddWithValue("@Name", faction.Name);
                cmd.Parameters.AddWithValue("@CreatedAt", faction.CreatedAt.ToString("o"));
                cmd.Parameters.AddWithValue("@UpdatedAt", faction.UpdatedAt.ToString("o"));
                cmd.ExecuteNonQuery();
            }
        }

        public void SavePlayer(PlayerModel player)
        {
            using (var cmd = new SQLiteCommand(_connection))
            {
                cmd.CommandText = @"
INSERT OR REPLACE INTO Players(SteamID, OriginalNick, SyncedNick, FactionID, CreatedAt, UpdatedAt)
VALUES(@SteamID, @OriginalNick, @SyncedNick, @FactionID, @CreatedAt, @UpdatedAt);";
                cmd.Parameters.AddWithValue("@SteamID", player.SteamID);
                cmd.Parameters.AddWithValue("@OriginalNick", player.OriginalNick);
                cmd.Parameters.AddWithValue("@SyncedNick", player.SyncedNick);
                cmd.Parameters.AddWithValue("@FactionID", player.FactionID);
                cmd.Parameters.AddWithValue("@CreatedAt", player.CreatedAt.ToString("o"));
                cmd.Parameters.AddWithValue("@UpdatedAt", player.UpdatedAt.ToString("o"));
                cmd.ExecuteNonQuery();
            }
        }
    }
}
