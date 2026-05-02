using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Domain.Entities;
using SRO.PK2API;
using SilkroadAIBot.Core.Configuration;

namespace SilkroadAIBot.Data
{
    public class DataManager
    {
        public static DataManager Instance { get; private set; } = null!;

        public DataManager()
        {
            Instance = this;
        }

        private Pk2Stream? _mediaPk2;
        private Pk2Stream? _dataPk2;
        private Pk2Stream? _mapPk2;
        
        public SilkroadAIBot.Data.Readers.NavmeshReader? Navmesh { get; private set; }
        private DatabaseManager _db = null!;
        
        // Using SRModelInfo from SilkroadAIBot.Domain.Entities instead of local struct
        private Dictionary<uint, SRModelInfo> _modelInfo = new Dictionary<uint, SRModelInfo>();
        private Dictionary<string, string> _codeNameToName = new Dictionary<string, string>();
        private Dictionary<uint, string> _modelNames = new Dictionary<uint, string>();
        private Dictionary<uint, SRSkill> _skills = new Dictionary<uint, SRSkill>();
        public SilkroadAIBot.Bot.Navigation.AStarPathfinder Pathfinder { get; private set; } = new();

        private string? _extractedDataPath;
        private bool _isDataLoading = false;
        private readonly HashSet<string> _loadedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        public event Action<int, string>? OnProgress;

        public IEnumerable<SRSkill> GetAllSkills() => _skills.Values;

        private void ReportProgress(int percent, string message)
        {
            BotLogger.Info("DataManager", $"[{percent}%] {message}");
            OnProgress?.Invoke(percent, message);
        }

        public bool Initialize(string sroPath, string? extractedDataPath = null)
        {
            _extractedDataPath = extractedDataPath;
            if (string.IsNullOrWhiteSpace(sroPath) && string.IsNullOrWhiteSpace(extractedDataPath))
            {
                BotLogger.Error("[DataManager] Both SRO Path and Extracted Path are empty.");
                return false;
            }

            // Check if sroPath is actually an extracted folder (has Media subdirectory)
            if (!string.IsNullOrWhiteSpace(sroPath) && Directory.Exists(Path.Combine(sroPath, "Media")))
            {
                if (string.IsNullOrEmpty(_extractedDataPath))
                {
                    _extractedDataPath = sroPath;
                    BotLogger.Info($"[DataManager] SRO Path appears to be an extracted folder structure. Using as ExtractedDataPath.");
                }
            }

            string mediaPath = Path.Combine(sroPath, "Media.pk2");
            string mapPath = Path.Combine(sroPath, "Map.pk2");
            string dataPath = Path.Combine(sroPath, "Data.pk2");

            try 
            {
                // Try PK2 first
                if (File.Exists(mediaPath))
                {
                    BotLogger.Info("[DataManager] Attempting to load Media.pk2...");
                    _mediaPk2 = new Pk2Stream(mediaPath, "169841", true);
                    BotLogger.Info($"[DataManager] Media.pk2 loaded and indexed ({_mediaPk2.Files.Count} files).");
                }

                if (File.Exists(dataPath))
                {
                    BotLogger.Info("[DataManager] Attempting to load Data.pk2...");
                    _dataPk2 = new Pk2Stream(dataPath, "169841", true);
                    BotLogger.Info($"[DataManager] Data.pk2 loaded and indexed ({_dataPk2.Files.Count} files).");
                }

                if (File.Exists(mapPath))
                {
                    BotLogger.Info("[DataManager] Attempting to load Map.pk2...");
                    _mapPk2 = new Pk2Stream(mapPath, "169841", true);
                    Navmesh = new SilkroadAIBot.Data.Readers.NavmeshReader(_mapPk2);
                    BotLogger.Info($"[DataManager] Map.pk2 loaded and NavmeshReader initialized.");
                }

                // If PK2s are missing, we'll fallback to extracted files during load methods
                if (_mediaPk2 == null && !string.IsNullOrEmpty(_extractedDataPath))
                    BotLogger.Warn("[DataManager] PK2 files missing or inaccessible. Falling back to extracted data folder.");

                return true;
            }
            catch (Exception ex)
            {
                BotLogger.Error($"[DataManager] PK2 Load Critical Error: {ex.Message}");
                return false; 
            }
        }

        public bool AutoDiscoverServerConfig(out string ip, out int port)
        {
            ip = "";
            port = 15779;

            if (_mediaPk2 == null && string.IsNullOrEmpty(_extractedDataPath)) return false;

            try
            {
                // 1. Division Info
                byte[]? divData = null;
                if (_mediaPk2 != null) divData = _mediaPk2.GetFile("divisioninfo.txt")?.GetContent();
                else if (!string.IsNullOrEmpty(_extractedDataPath))
                {
                    string p = Path.Combine(_extractedDataPath, "Media", "divisioninfo.txt");
                    if (File.Exists(p)) divData = File.ReadAllBytes(p);
                }

                if (divData != null)
                {
                    using (var reader = new BinaryReader(new MemoryStream(divData)))
                    {
                        reader.ReadByte(); 
                        byte count = reader.ReadByte();
                        for (int i = 0; i < count; i++)
                        {
                            int nameLen = reader.ReadInt32();
                            string divName = Encoding.ASCII.GetString(reader.ReadBytes(nameLen));
                            reader.ReadByte(); 
                            
                            byte divCount = reader.ReadByte();
                            for (int j = 0; j < divCount; j++)
                            {
                                int len = reader.ReadInt32();
                                if (len > 0)
                                {
                                    string fetchedIp = Encoding.ASCII.GetString(reader.ReadBytes(len));
                                    reader.ReadByte(); 
                                    
                                    if (string.IsNullOrEmpty(ip) && !string.IsNullOrEmpty(fetchedIp))
                                    {
                                        ip = fetchedIp.TrimEnd('\0');
                                    }
                                }
                            }
                        }
                    }
                }

                // 2. Gateway Port
                byte[]? portData = null;
                if (_mediaPk2 != null) portData = _mediaPk2.GetFile("gateport.txt")?.GetContent();
                else if (!string.IsNullOrEmpty(_extractedDataPath))
                {
                    string p = Path.Combine(_extractedDataPath, "Media", "gateport.txt");
                    if (File.Exists(p)) portData = File.ReadAllBytes(p);
                }

                if (portData != null)
                {
                    string portStr = Encoding.ASCII.GetString(portData).Trim();
                    if (int.TryParse(portStr, out int p)) port = p;
                }

                if (!string.IsNullOrEmpty(ip))
                {
                    BotLogger.Info($"[DataManager] Discovered Server Address: {ip}:{port}");
                    
                    try
                    {
                        if (!System.Net.IPAddress.TryParse(ip, out _))
                        {
                            BotLogger.Info($"[DataManager] Resolving hostname '{ip}'...");
                            var addresses = System.Net.Dns.GetHostAddresses(ip);
                            if (addresses.Length > 0)
                            {
                                string numericIp = addresses[0].ToString();
                                BotLogger.Info($"[DataManager] DNS Resolved: {ip} -> {numericIp}");
                                ip = numericIp;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        BotLogger.Error($"[DataManager] DNS Resolution failed for {ip}: {ex.Message}");
                    }

                    BotLogger.Info($"[DataManager] Final Target IP: '{ip}'");
                    
                    // Update global config with the discovered IP and Port
                    ConfigManager.Config.OriginalServerIp = ip;
                    ConfigManager.Config.LastServerPort = port;
                    ConfigManager.Save();
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                BotLogger.Error($"[DataManager] Error during server discovery: {ex.Message}");
                return false;
            }
        }

        public void SetDatabase(DatabaseManager db)
        {
            _db = db;
        }

        public void ExtractAndSaveToDatabase()
        {
            if (_isDataLoading) 
            {
                BotLogger.Warn("[DataManager] Data loading already in progress. Skipping redundant request.");
                return;
            }

            _isDataLoading = true;
            _loadedFiles.Clear();
            try
            {
                ReportProgress(5, "Starting Data Extraction...");
                LoadTextData();
                
                ReportProgress(25, "Loading Character Data...");
                LoadCharacterData();
                
                ReportProgress(45, "Loading Item Data...");
                LoadItemData();
                
                ReportProgress(65, "Loading Skill Data...");
                LoadSkillData();

                if (_db != null)
                {
                    ReportProgress(85, "Synchronizing with Database...");
                    _db.SaveModelsBatch(_modelInfo.Values);
                    _db.SaveSkillsBatch(_skills.Values);
                    ReportProgress(100, "Data Loaded Successfully.");
                }
                
                BuildModelNameCache();
            }
            catch (Exception ex)
            {
                BotLogger.Error($"[DataManager] CRITICAL ERROR during data loading: {ex.Message}\n{ex.StackTrace}");
                ReportProgress(0, "Error: Data Load Failed");
            }
            finally
            {
                _isDataLoading = false;
            }
        }

        private void BuildModelNameCache()
        {
            _modelNames.Clear();
            foreach (var info in _modelInfo.Values)
            {
                _modelNames[info.ID] = info.Name;
            }
        }
        public string GetTranslation(string codeName)
        {
            if (string.IsNullOrEmpty(codeName)) return "";
            if (_codeNameToName.TryGetValue(codeName, out string name)) return name;
            return codeName;
        }

        public string GetModelName(uint modelID)
        {
            if (_modelNames.TryGetValue(modelID, out var name))
                return name;
            return $"Unknown[{modelID}]";
        }

        public SRModelInfo? GetCommonInfo(uint modelID)
        {
            if (_modelInfo.TryGetValue(modelID, out var info))
                return info;
            return null;
        }

        public SRModelInfo? GetItem(uint id) => GetCommonInfo(id);
        
        private void LoadTextData()
        {
            // Try multiple text data files for comprehensive translation
            // Supports naming variations found in different SRO versions
            string[] textFiles = { 
                "server_dep\\silkroad\\textdata\\textdata_object.txt",
                "server_dep\\silkroad\\textdata\\textdata_skill.txt",
                "server_dep\\silkroad\\textdata\\textdata_equip&skill.txt",
                "server_dep\\silkroad\\textdata\\textdata_item.txt",
                "server_dep\\silkroad\\textdata\\textdata_quest.txt"
            };

            foreach (var file in textFiles)
            {
                byte[]? fileData = null;
                if (_mediaPk2 != null)
                {
                    string normalized = file.Replace('/', '\\').ToLowerInvariant();
                    if (_loadedFiles.Contains(normalized)) continue;
                    
                    fileData = _mediaPk2.GetFile(file)?.GetContent();
                    if (fileData == null) fileData = _mediaPk2.GetFile(file.Replace("\\", "/"))?.GetContent();
                    
                    if (fileData != null) _loadedFiles.Add(normalized);
                }
                else if (!string.IsNullOrEmpty(_extractedDataPath))
                {
                    string fullPath = Path.Combine(_extractedDataPath, "Media", file);
                    if (File.Exists(fullPath)) fileData = File.ReadAllBytes(fullPath);
                }

                if (fileData != null)
                {
                    using (var reader = new StreamReader(new MemoryStream(fileData), Encoding.Unicode))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            var parts = line.Split('\t');
                            if (parts.Length >= 3)
                            {
                                string codeName = parts[1];
                                string realName = parts[2];
                                _codeNameToName[codeName] = realName;
                            }
                        }
                    }
                }
            }
            BotLogger.Info("[DataManager]", $"Loaded {_codeNameToName.Count} translation entries.");
        }
        
        private void LoadCharacterData()
        {
            if (_dataPk2 == null && _mediaPk2 == null && string.IsNullOrEmpty(_extractedDataPath)) return;

            // Search in both Data and Media PK2s as private servers often move these files
            var pk2s = new[] { _dataPk2, _mediaPk2 };
            foreach (var pk2 in pk2s)
            {
                if (pk2 == null) continue;
                
                BotLogger.Debug("[DataManager]", $"Scanning PK2 for character data...");
                foreach (var filePath in pk2.Files.Keys)
                {
                    if (filePath.Contains("characterdata_") && filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        string normalized = filePath.Replace('/', '\\').ToLowerInvariant();
                        if (!_loadedFiles.Add(normalized)) continue;

                        BotLogger.Debug("[DataManager]", $"Reading PK2 character data: {filePath}");
                        byte[]? data = pk2.GetFile(filePath)?.GetContent();
                        if (data != null) ProcessCharacterData(data);
                    }
                }
            }

            if (!string.IsNullOrEmpty(_extractedDataPath))
            {
                // In this specific extraction, data files are in Media/server_dep/silkroad/textdata
                string textDataDir = Path.Combine(_extractedDataPath, "Media", "server_dep", "silkroad", "textdata");
                if (Directory.Exists(textDataDir))
                {
                    foreach (var file in Directory.GetFiles(textDataDir, "characterdata_*.txt"))
                    {
                        ProcessCharacterData(File.ReadAllBytes(file));
                    }
                }
                
                // Fallback to Data/data if missing from Media
                string dataDir = Path.Combine(_extractedDataPath, "Data", "data");
                if (Directory.Exists(dataDir))
                {
                    foreach (var file in Directory.GetFiles(dataDir, "characterdata_*.txt"))
                    {
                        ProcessCharacterData(File.ReadAllBytes(file));
                    }
                }
            }
            BotLogger.Info("[DataManager]", $"Loaded {_modelInfo.Count} character data entries.");
        }

        private void LoadItemData()
        {
            if (_dataPk2 == null && _mediaPk2 == null && string.IsNullOrEmpty(_extractedDataPath)) return;

            var pk2s = new[] { _dataPk2, _mediaPk2 };
            foreach (var pk2 in pk2s)
            {
                if (pk2 == null) continue;

                BotLogger.Debug("[DataManager]", $"Scanning PK2 for item data...");
                foreach (var filePath in pk2.Files.Keys)
                {
                    if (filePath.Contains("itemdata_") && filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        string normalized = filePath.Replace('/', '\\').ToLowerInvariant();
                        if (!_loadedFiles.Add(normalized)) continue;

                        BotLogger.Debug("[DataManager]", $"Reading PK2 item data: {filePath}");
                        byte[]? data = pk2.GetFile(filePath)?.GetContent();
                        if (data != null) ProcessItemData(data);
                    }
                }
            }

            if (!string.IsNullOrEmpty(_extractedDataPath))
            {
                string textDataDir = Path.Combine(_extractedDataPath, "Media", "server_dep", "silkroad", "textdata");
                if (Directory.Exists(textDataDir))
                {
                    var files = Directory.GetFiles(textDataDir, "itemdata_*.txt");
                    BotLogger.Info("[DataManager]", $"Found {files.Length} item data files in extracted path.");
                    foreach (var file in files)
                    {
                        BotLogger.Debug("[DataManager]", $"Processing item file: {Path.GetFileName(file)}");
                        ProcessItemData(File.ReadAllBytes(file));
                    }
                }
            }
            BotLogger.Info("[DataManager]", $"Total model entries loaded: {_modelInfo.Count}");
        }

        private void ProcessItemData(byte[] data)
        {
            try
            {
                using (var reader = GetRobustReader(data))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;
                        var parts = line.Split('\t');
                        
                        if (parts.Length >= 14) 
                        {
                            if (byte.TryParse(parts[0], out byte svc) && svc == 1)
                            {
                                if (uint.TryParse(parts[1], out uint id))
                                {
                                    byte t1 = 0, t2 = 0, t3 = 0, t4 = 0;
                                    if (parts.Length > 10) byte.TryParse(parts[10], out t1);
                                    if (parts.Length > 11) byte.TryParse(parts[11], out t2);
                                    if (parts.Length > 12) byte.TryParse(parts[12], out t3);
                                    if (parts.Length > 13) byte.TryParse(parts[13], out t4);

                                    var info = new SRModelInfo
                                    {
                                        ID = id,
                                        CodeName = parts[2],
                                        Name = GetTranslation(parts[2]),
                                        TypeID1 = t1,
                                        TypeID2 = t2,
                                        TypeID3 = t3,
                                        TypeID4 = t4
                                    };

                                    _modelInfo[id] = info;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BotLogger.Debug("[DataManager]", $"Warning: Error processing item data segment: {ex.Message}");
            }
        }

        private StreamReader GetRobustReader(byte[] data)
        {
            // Detect BOM
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return new StreamReader(new MemoryStream(data), Encoding.Unicode);
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return new StreamReader(new MemoryStream(data), Encoding.UTF8);

            // Heuristic for UTF-16 LE without BOM
            int zeroCount = 0;
            int checkLen = Math.Min(data.Length, 100);
            for (int i = 1; i < checkLen; i += 2) if (data[i] == 0) zeroCount++;
            
            if (zeroCount > (checkLen / 4)) 
                return new StreamReader(new MemoryStream(data), Encoding.Unicode);

            return new StreamReader(new MemoryStream(data), Encoding.UTF8);
        }

        private void ProcessCharacterData(byte[] data)
        {
            try
            {
                using (var reader = GetRobustReader(data))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//")) continue;
                        var parts = line.Split('\t');
                        
                        if (parts.Length >= 14) 
                        {
                            if (byte.TryParse(parts[0], out byte svc) && svc == 1)
                            {
                                if (uint.TryParse(parts[1], out uint id))
                                {
                                    byte t1 = 0, t2 = 0, t3 = 0, t4 = 0;
                                    if (parts.Length > 10) byte.TryParse(parts[10], out t1);
                                    if (parts.Length > 11) byte.TryParse(parts[11], out t2);
                                    if (parts.Length > 12) byte.TryParse(parts[12], out t3);
                                    if (parts.Length > 13) byte.TryParse(parts[13], out t4);

                                    var info = new SRModelInfo
                                    {
                                        ID = id,
                                        CodeName = parts[2],
                                        Name = GetTranslation(parts[2]),
                                        TypeID1 = t1,
                                        TypeID2 = t2,
                                        TypeID3 = t3,
                                        TypeID4 = t4
                                    };

                                    _modelInfo[id] = info;
                                }
                            }
                        }
                        else if (parts.Length >= 13 && uint.TryParse(parts[0], out uint oldId))
                        {
                            byte t1 = 0, t2 = 0, t3 = 0, t4 = 0;
                            if (parts.Length > 9) byte.TryParse(parts[9], out t1);
                            if (parts.Length > 10) byte.TryParse(parts[10], out t2);
                            if (parts.Length > 11) byte.TryParse(parts[11], out t3);
                            if (parts.Length > 12) byte.TryParse(parts[12], out t4);

                            var info = new SRModelInfo
                            {
                                ID = oldId,
                                CodeName = parts[1],
                                Name = GetTranslation(parts[1]),
                                TypeID1 = t1,
                                TypeID2 = t2,
                                TypeID3 = t3,
                                TypeID4 = t4
                            };

                            _modelInfo[oldId] = info;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BotLogger.Debug("[DataManager]", $"Warning: Error processing character data segment: {ex.Message}");
            }
        }
        
        private void LoadSkillData()
        {
            if (_dataPk2 == null && _mediaPk2 == null && string.IsNullOrEmpty(_extractedDataPath)) return;

            var pk2s = new[] { _dataPk2, _mediaPk2 };
            foreach (var pk2 in pk2s)
            {
                if (pk2 == null) continue;

                foreach (var filePath in pk2.Files.Keys)
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName.StartsWith("skilldata_") && fileName.EndsWith(".txt"))
                    {
                        byte[]? data = pk2.GetFile(filePath)?.GetContent();
                        if (data != null) ProcessSkillData(data);
                    }
                }
            }

            if (!string.IsNullOrEmpty(_extractedDataPath))
            {
                string textDataDir = Path.Combine(_extractedDataPath, "Media", "server_dep", "silkroad", "textdata");
                if (Directory.Exists(textDataDir))
                {
                    foreach (var file in Directory.GetFiles(textDataDir, "skilldata_*.txt"))
                    {
                        if (file.EndsWith("enc.txt")) continue; // Skip encrypted if raw exists
                        ProcessSkillData(File.ReadAllBytes(file));
                    }
                }

                string dataDir = Path.Combine(_extractedDataPath, "Data", "data");
                if (Directory.Exists(dataDir))
                {
                    foreach (var file in Directory.GetFiles(dataDir, "skilldata_*.txt"))
                    {
                        if (file.EndsWith("enc.txt")) continue;
                        ProcessSkillData(File.ReadAllBytes(file));
                    }
                }
            }
        }

        private void ProcessSkillData(byte[] data)
        {
            try
            {
                using (var reader = GetRobustReader(data))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

                        var parts = line.Split('\t');
                        if (parts.Length < 63) continue;

                        if (byte.TryParse(parts[0], out byte service) && service == 1)
                        {
                            if (uint.TryParse(parts[1], out uint id))
                            {
                                string codeName = parts[3];
                                string mastery = parts[6];
                                
                                byte lvl = 0; if (parts.Length > 7) byte.TryParse(parts[7], out lvl);
                                int cast = 0; if (parts.Length > 12) int.TryParse(parts[12], out cast);
                                int cd = 0; if (parts.Length > 14) int.TryParse(parts[14], out cd);
                                int range = 0; if (parts.Length > 21) int.TryParse(parts[21], out range);
                                int mp = 0; if (parts.Length > 53) int.TryParse(parts[53], out mp);
                                
                                bool selfOnly = parts.Length > 26 && parts[26] == "1";
                                string type = "Attack";
                                if (codeName.Contains("_PASSIVE")) type = "Passive";
                                else if (selfOnly && range == 0) type = "Buff";

                                var skill = new SRSkill
                                {
                                    ID = id,
                                    CodeName = codeName,
                                    MasteryTree = mastery,
                                    Level = lvl,
                                    CastTime = cast,
                                    Cooldown = cd,
                                    Range = range,
                                    IsSelfOnly = selfOnly,
                                    MPUsage = mp,
                                    IconPath = parts.Length > 61 ? parts[61] : "",
                                    DamageRange = (parts.Length > 55) ? (parts[54] + " - " + parts[55]) : "",
                                    Race = (codeName.Contains("_CH_") || mastery.Contains("_CH_")) ? "CH" : "EU",
                                    SkillType = type,
                                    Name = (parts.Length > 62 && _codeNameToName.TryGetValue(parts[62], out string realName)) ? realName : codeName
                                };

                                _skills[id] = skill;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BotLogger.Debug("[DataManager]", $"Warning: Error processing skill data segment: {ex.Message}");
            }
        }

        public System.Drawing.Bitmap? GetSkillIcon(string iconPath)
        {
            if (_mediaPk2 == null || string.IsNullOrEmpty(iconPath)) return null;

            try
            {
                string path = iconPath.Replace("/", "\\");
                var file = _mediaPk2.GetFile(path);
                if (file == null) return null;

                byte[] data = file.GetContent();
                if (data == null || data.Length < 12) return null;

                // DDJ Decoder: Skip 12-byte header
                // Silkroad DDJ format is often 12 bytes of header then DDS or raw image data
                // For simplicity, we skip 12 and try to load with GDI+
                using (var ms = new MemoryStream(data, 12, data.Length - 12))
                {
                    try { return new System.Drawing.Bitmap(ms); }
                    catch { 
                        // If standard GDI fails, it's likely a compressed DDS
                        // In a real bot we'd use a DDS library, but for now we fallback
                        return null; 
                    }
                }
            }
            catch { return null; }
        }

        public SRSkill GetSkill(uint id)
        {
            if (_skills.TryGetValue(id, out var skill))
                return skill;
            return null;
        }

        public string? GetCodeName(uint id)
        {
            if (_modelInfo.TryGetValue(id, out var info))
                return info.CodeName;
            return null;
        }

        public Pk2Stream? GetPk2File(string path, string pk2Name)
        {
            if (pk2Name.Equals("Media.pk2", StringComparison.OrdinalIgnoreCase)) return _mediaPk2;
            if (pk2Name.Equals("Data.pk2", StringComparison.OrdinalIgnoreCase)) return _dataPk2;
            if (pk2Name.Equals("Map.pk2", StringComparison.OrdinalIgnoreCase)) return _mapPk2;
            return null;
        }
    }
}


