using System;
using Microsoft.Data.Sqlite;
using System.IO;
using SilkroadAIBot.Domain.Entities;
using System.Collections.Generic;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Data
{
    public class DatabaseManager : IDisposable, SilkroadAIBot.Application.Interfaces.ISkillRepository
    {
        public static DatabaseManager Instance { get; private set; } = null!;
        private static readonly object _lock = new object();
        private SqliteConnection _connection;
        private string _dbPath = "bot_data.db";

        private readonly Dictionary<uint, SilkroadAIBot.Domain.Entities.SRSkill> _skillCache = new();
        private readonly Dictionary<string, SilkroadAIBot.Domain.Entities.SRSkill> _skillByNameCache = new(StringComparer.OrdinalIgnoreCase);

        public DatabaseManager()
        {
            Instance = this;
            Initialize();
        }

        public void ChangeDatabase(string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName)) return;
            
            // Clean filename
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                gameName = gameName.Replace(c.ToString(), "");
            }
            string newPath = $"bot_data_{gameName}.db";
            
            lock (_lock)
            {
                if (_dbPath == newPath && _connection != null && _connection.State == System.Data.ConnectionState.Open) return;

                BotLogger.Info("Database", $"Switching database to: {newPath}");
                
                try 
                {
                    if (_connection != null)
                    {
                        _connection.Close();
                        _connection.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    BotLogger.Warn("Database", $"Error closing old connection: {ex.Message}");
                }

                _dbPath = newPath;
                Initialize();
            }
        }

        private void Initialize()
        {
            try 
            {
                _connection = new SqliteConnection($"Data Source={_dbPath}");
                _connection.Open();

                // 1. RefObjCommon (Master table)
                string createRefObjCommon = @"
                    CREATE TABLE IF NOT EXISTS RefObjCommon (
                        ID INTEGER PRIMARY KEY,
                        CodeName TEXT,
                        Name TEXT,
                        TypeID1 INTEGER,
                        TypeID2 INTEGER,
                        TypeID3 INTEGER,
                        TypeID4 INTEGER,
                        IconPath TEXT
                    )";
                Execute(createRefObjCommon);

                // 2. RefSkill
                string createRefSkill = @"
                    CREATE TABLE IF NOT EXISTS RefSkill (
                        ID INTEGER PRIMARY KEY,
                        CodeName TEXT,
                        Name TEXT,
                        Mastery TEXT,
                        Race TEXT,
                        Type TEXT,
                        Level INTEGER,
                        CastTime INTEGER,
                        Cooldown INTEGER,
                        Range INTEGER,
                        IsSelfOnly INTEGER,
                        MPUsage INTEGER,
                        DamageRange TEXT,
                        IconPath TEXT
                    )";
                Execute(createRefSkill);

                // 3. Characters (Bot's local state)
                string createCharactersTable = @"
                    CREATE TABLE IF NOT EXISTS Characters (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT UNIQUE,
                        ModelID INTEGER,
                        Level INTEGER,
                        Exp INTEGER,
                        Gold INTEGER,
                        HP INTEGER,
                        MP INTEGER,
                        X REAL,
                        Y REAL,
                        Z REAL,
                        Region INTEGER,
                        LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";
                Execute(createCharactersTable);

                // 4. Configuration
                Execute("CREATE TABLE IF NOT EXISTS Configuration (Key TEXT PRIMARY KEY, Value TEXT)");

                // 5. Inventory
                string createInventoryTable = @"
                    CREATE TABLE IF NOT EXISTS Inventory (
                        CharId INTEGER,
                        ModelID INTEGER,
                        Slot INTEGER,
                        Amount INTEGER,
                        FOREIGN KEY(CharId) REFERENCES Characters(Id)
                    )";
                Execute(createInventoryTable);

                Console.WriteLine($"[Database] v2.1.1 Schema Initialized ({_dbPath}).");
            }
            catch (Exception ex)
            {
                BotLogger.Error("Database", $"Critical Initialization Error: {ex.Message}");
            }

            LoadSkillCache();
        }

        private void LoadSkillCache()
        {
            lock (_lock)
            {
                _skillCache.Clear();
                _skillByNameCache.Clear();

                if (_connection == null || _connection.State != System.Data.ConnectionState.Open) return;

                try
                {
                    string sql = "SELECT * FROM RefSkill";
                    using var command = new SqliteCommand(sql, _connection);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var skill = new SilkroadAIBot.Domain.Entities.SRSkill
                        {
                            ID = (uint)reader.GetInt64(0),
                            CodeName = reader.GetString(1),
                            Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            MasteryTree = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            Race = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            SkillType = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            Level = (byte)reader.GetInt32(6),
                            CastTime = reader.GetInt32(7),
                            Cooldown = reader.GetInt32(8),
                            Range = reader.GetInt32(9),
                            IsSelfOnly = reader.GetInt32(10) == 1,
                            MPUsage = reader.GetInt32(11),
                            DamageRange = reader.IsDBNull(12) ? "" : reader.GetString(12),
                            IconPath = reader.IsDBNull(13) ? "" : reader.GetString(13)
                        };

                        _skillCache[skill.ID] = skill;
                        _skillByNameCache[skill.CodeName] = skill;
                    }
                    BotLogger.Info("Database", $"Loaded {_skillCache.Count} skills into cache.");
                }
                catch (Exception ex)
                {
                    BotLogger.Error("Database", $"Failed to load skill cache: {ex.Message}");
                }
            }
        }

        #region ISkillRepository
        public SilkroadAIBot.Domain.Entities.SRSkill? GetByID(uint skillID)
        {
            _skillCache.TryGetValue(skillID, out var skill);
            return skill;
        }

        public SilkroadAIBot.Domain.Entities.SRSkill? GetByCodeName(string codeName)
        {
            if (string.IsNullOrEmpty(codeName)) return null;
            _skillByNameCache.TryGetValue(codeName, out var skill);
            return skill;
        }

        public IReadOnlyList<SilkroadAIBot.Domain.Entities.SRSkill> GetByMastery(string masteryTree)
        {
            return _skillCache.Values.Where(s => s.MasteryTree.Equals(masteryTree, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public int Count => _skillCache.Count;
        #endregion

        private void Execute(string sql)
        {
            lock (_lock)
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open) return;
                using (var command = new SqliteCommand(sql, _connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public void SaveModelsBatch(IEnumerable<SRModelInfo> models)
        {
            lock (_lock)
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open) return;
                
                try
                {
                    using (var transaction = _connection.BeginTransaction())
                    {
                        string sql = @"
                            INSERT OR REPLACE INTO RefObjCommon (ID, CodeName, Name, TypeID1, TypeID2, TypeID3, TypeID4, IconPath)
                            VALUES (@id, @codeName, @name, @t1, @t2, @t3, @t4, @icon)";

                        using (var command = new SqliteCommand(sql, _connection, transaction))
                        {
                            var pId = command.Parameters.Add("@id", SqliteType.Integer);
                            var pCode = command.Parameters.Add("@codeName", SqliteType.Text);
                            var pName = command.Parameters.Add("@name", SqliteType.Text);
                            var pT1 = command.Parameters.Add("@t1", SqliteType.Integer);
                            var pT2 = command.Parameters.Add("@t2", SqliteType.Integer);
                            var pT3 = command.Parameters.Add("@t3", SqliteType.Integer);
                            var pT4 = command.Parameters.Add("@t4", SqliteType.Integer);
                            var pIcon = command.Parameters.Add("@icon", SqliteType.Text);

                            foreach (var m in models)
                            {
                                pId.Value = m.ID;
                                pCode.Value = m.CodeName ?? (object)DBNull.Value;
                                pName.Value = m.Name ?? (object)DBNull.Value;
                                pT1.Value = m.TypeID1;
                                pT2.Value = m.TypeID2;
                                pT3.Value = m.TypeID3;
                                pT4.Value = m.TypeID4;
                                pIcon.Value = m.IconPath ?? (object)DBNull.Value;
                                command.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }
                }
                catch (Exception ex)
                {
                    BotLogger.Error("Database", $"Error during Models bulk save: {ex.Message}");
                }
            }
        }

        public void SaveSkillsBatch(IEnumerable<SRSkill> skills)
        {
            lock (_lock)
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open) return;

                try
                {
                    using (var transaction = _connection.BeginTransaction())
                    {
                        string sql = @"
                            INSERT OR REPLACE INTO RefSkill (ID, CodeName, Name, Mastery, Race, Type, Level, CastTime, Cooldown, Range, IsSelfOnly, MPUsage, DamageRange, IconPath)
                            VALUES (@id, @codeName, @name, @mst, @race, @type, @lvl, @cast, @cd, @range, @self, @mp, @dmg, @icon)";

                        using (var command = new SqliteCommand(sql, _connection, transaction))
                        {
                            var pId = command.Parameters.Add("@id", SqliteType.Integer);
                            var pCode = command.Parameters.Add("@codeName", SqliteType.Text);
                            var pName = command.Parameters.Add("@name", SqliteType.Text);
                            var pMst = command.Parameters.Add("@mst", SqliteType.Text);
                            var pRace = command.Parameters.Add("@race", SqliteType.Text);
                            var pType = command.Parameters.Add("@type", SqliteType.Text);
                            var pLvl = command.Parameters.Add("@lvl", SqliteType.Integer);
                            var pCast = command.Parameters.Add("@cast", SqliteType.Integer);
                            var pCd = command.Parameters.Add("@cd", SqliteType.Integer);
                            var pRange = command.Parameters.Add("@range", SqliteType.Integer);
                            var pSelf = command.Parameters.Add("@self", SqliteType.Integer);
                            var pMp = command.Parameters.Add("@mp", SqliteType.Integer);
                            var pDmg = command.Parameters.Add("@dmg", SqliteType.Text);
                            var pIcon = command.Parameters.Add("@icon", SqliteType.Text);

                            foreach (var s in skills)
                            {
                                pId.Value = s.ID;
                                pCode.Value = s.CodeName ?? (object)DBNull.Value;
                                pName.Value = s.Name ?? (object)DBNull.Value;
                                pMst.Value = s.MasteryTree ?? (object)DBNull.Value;
                                pRace.Value = s.Race;
                                pType.Value = s.SkillType ?? "Attack";
                                pLvl.Value = s.Level;
                                pCast.Value = s.CastTime;
                                pCd.Value = s.Cooldown;
                                pRange.Value = s.Range;
                                pSelf.Value = s.IsSelfOnly ? 1 : 0;
                                pMp.Value = s.MPUsage;
                                pDmg.Value = s.DamageRange ?? (object)DBNull.Value;
                                pIcon.Value = s.IconPath ?? (object)DBNull.Value;
                                command.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }
                }
                catch (Exception ex)
                {
                    BotLogger.Error("Database", $"Error during Skills bulk save: {ex.Message}");
                }
            }
        }

        public void SaveConfig(string key, string value)
        {
            lock (_lock)
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open) return;
                string sql = "INSERT OR REPLACE INTO Configuration (Key, Value) VALUES (@key, @value)";
                using (var command = new SqliteCommand(sql, _connection))
                {
                    command.Parameters.AddWithValue("@key", key);
                    command.Parameters.AddWithValue("@value", value);
                    command.ExecuteNonQuery();
                }
            }
        }

        public string GetConfig(string key)
        {
            lock (_lock)
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open) return null;
                string sql = "SELECT Value FROM Configuration WHERE Key = @key";
                using (var command = new SqliteCommand(sql, _connection))
                {
                    command.Parameters.AddWithValue("@key", key);
                    var result = command.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }

        public void UpdateCharacterState(SRCharacter character)
        {
            if (character == null) return;

            lock (_lock)
            {
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open) return;

                string sql = @"
                    INSERT OR REPLACE INTO Characters (Name, ModelID, Level, Exp, Gold, HP, MP, X, Y, Z, Region, LastUpdated)
                    VALUES (@name, @modelId, @level, @exp, @gold, @hp, @mp, @x, @y, @z, @region, CURRENT_TIMESTAMP)";

                using (var command = new SqliteCommand(sql, _connection))
                {
                    command.Parameters.AddWithValue("@name", character.Name ?? "Unknown");
                    command.Parameters.AddWithValue("@modelId", character.ModelID);
                    command.Parameters.AddWithValue("@level", character.Level);
                    command.Parameters.AddWithValue("@exp", (long)character.Exp);
                    command.Parameters.AddWithValue("@gold", (long)character.Gold);
                    command.Parameters.AddWithValue("@hp", character.HP);
                    command.Parameters.AddWithValue("@mp", character.MP);
                    command.Parameters.AddWithValue("@x", character.Position.X);
                    command.Parameters.AddWithValue("@y", character.Position.Y);
                    command.Parameters.AddWithValue("@z", character.Position.Z);
                    command.Parameters.AddWithValue("@region", character.Position.Region);
                    command.ExecuteNonQuery();
                }

                // Get the ID of the character for inventory saving
                long charId = GetCharacterIdInternal(character.Name);
                if (charId != -1)
                {
                    SaveInventoryInternal(charId, character.Inventory);
                }
            }
        }

        public long GetCharacterId(string name)
        {
            lock (_lock)
            {
                return GetCharacterIdInternal(name);
            }
        }

        private long GetCharacterIdInternal(string name)
        {
            if (_connection == null || _connection.State != System.Data.ConnectionState.Open) return -1;
            string sql = "SELECT Id FROM Characters WHERE Name = @name";
            using (var command = new SqliteCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@name", name);
                var result = command.ExecuteScalar();
                return result != null ? (long)result : -1;
            }
        }

        public void SaveInventory(long charId, IEnumerable<SRItem> items)
        {
            lock (_lock)
            {
                SaveInventoryInternal(charId, items);
            }
        }

        private void SaveInventoryInternal(long charId, IEnumerable<SRItem> items)
        {
            if (items == null || _connection == null || _connection.State != System.Data.ConnectionState.Open) return;

            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // Delete old inventory for this character
                    string deleteSql = "DELETE FROM Inventory WHERE CharId = @charId";
                    using (var command = new SqliteCommand(deleteSql, _connection, transaction))
                    {
                        command.Parameters.AddWithValue("@charId", charId);
                        command.ExecuteNonQuery();
                    }

                    // Insert current inventory
                    string insertSql = "INSERT INTO Inventory (CharId, ModelID, Slot, Amount) VALUES (@charId, @modelId, @slot, @amount)";
                    foreach (var item in items)
                    {
                        using (var command = new SqliteCommand(insertSql, _connection, transaction))
                        {
                            command.Parameters.AddWithValue("@charId", charId);
                            command.Parameters.AddWithValue("@modelId", item.ModelID);
                            command.Parameters.AddWithValue("@slot", item.Slot);
                            command.Parameters.AddWithValue("@amount", item.Amount);
                            command.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    BotLogger.Error("Database", $"Failed to save inventory: {ex.Message}");
                    transaction.Rollback();
                }
            }
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}


