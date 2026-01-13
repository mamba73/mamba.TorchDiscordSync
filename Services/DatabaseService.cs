using System;
using System.Data.SQLite;
using System.IO;
using mamba.TorchDiscordSync.Models;

namespace mamba.TorchDiscordSync.Services
{
    // Handles SQLite database operations
    public class DatabaseService
    {
        private string _dbPath;
        private SQLiteConnection _connection;

        public DatabaseService(string dbPath)
        {
            _dbPath = dbPath;
            Initialize();
        }

        private void Initialize()
        {
            bool createNew = !File.Exists(_dbPath);
            _connection = new SQLiteConnection("Data Source=" + _dbPath);
            _connection.Open();

            if (createNew)
            {
                var cmd = _connection.CreateCommand();
                cmd.CommandText =
                    @"CREATE TABLE IF NOT EXISTS Factions (
                        FactionID INTEGER PRIMARY KEY,
                        Tag TEXT,
                        Name TEXT,
                        CreatedAt TEXT,
                        UpdatedAt TEXT
                    );
                      CREATE TABLE IF NOT EXISTS Players (
                        SteamID INTEGER PRIMARY KEY,
                        OriginalNick TEXT,
                        SyncedNick TEXT,
                        CreatedAt TEXT,
                        UpdatedAt TEXT
                      );";
                cmd.ExecuteNonQuery();
            }
        }

        public void SaveFaction(FactionModel faction)
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO Factions (FactionID, Tag, Name, CreatedAt, UpdatedAt) 
                                VALUES (@id,@tag,@name,@created,@updated)";
            cmd.Parameters.AddWithValue("@id", faction.FactionID);
            cmd.Parameters.AddWithValue("@tag", faction.Tag);
            cmd.Parameters.AddWithValue("@name", faction.Name);
            cmd.Parameters.AddWithValue("@created", faction.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updated", faction.UpdatedAt.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public void SavePlayer(PlayerModel player)
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO Players (SteamID, OriginalNick, SyncedNick, CreatedAt, UpdatedAt)
                                VALUES (@sid,@orig,@synced,@created,@updated)";
            cmd.Parameters.AddWithValue("@sid", player.SteamID);
            cmd.Parameters.AddWithValue("@orig", player.OriginalNick);
            cmd.Parameters.AddWithValue("@synced", player.SyncedNick);
            cmd.Parameters.AddWithValue("@created", player.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updated", player.UpdatedAt.ToString("o"));
            cmd.ExecuteNonQuery();
        }
    }
}
