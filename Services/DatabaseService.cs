using System;
using System.Data.SQLite;
using mamba.TorchDiscordSync.Models;

namespace mamba.TorchDiscordSync.Services
{
    public class DatabaseService
    {
        private readonly string _dbFile;
        private SQLiteConnection _connection;

        public DatabaseService(string dbFile)
        {
            _dbFile = dbFile;
            Init();
        }

        // Initialize DB and tables
        private void Init()
        {
            _connection = new SQLiteConnection($"Data Source={_dbFile};Version=3;");
            _connection.Open();

            var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS players (
                    SteamID INTEGER PRIMARY KEY,
                    OriginalNick TEXT,
                    SyncedNick TEXT,
                    CreatedAt TEXT,
                    UpdatedAt TEXT,
                    DeletedAt TEXT
                );
                CREATE TABLE IF NOT EXISTS factions (
                    FactionID INTEGER PRIMARY KEY,
                    Tag TEXT,
                    Name TEXT,
                    CreatedAt TEXT,
                    UpdatedAt TEXT,
                    DeletedAt TEXT
                );
                CREATE TABLE IF NOT EXISTS faction_player (
                    FactionID INTEGER,
                    PlayerSteamID INTEGER,
                    CreatedAt TEXT,
                    UpdatedAt TEXT,
                    DeletedAt TEXT,
                    PRIMARY KEY(FactionID, PlayerSteamID)
                );
            ";
            cmd.ExecuteNonQuery();
        }

        public SQLiteConnection GetConnection() => _connection;
    }
}
