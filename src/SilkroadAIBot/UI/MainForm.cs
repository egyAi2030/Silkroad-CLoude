using System;
using System.Windows.Forms;
using System.Threading.Tasks;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Application.Bot;
using SilkroadAIBot.Application.Bot.Bundles;
using SilkroadAIBot.Domain.Enums;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Data;
using SilkroadAIBot.Core.Helpers;
using System.IO;
using SilkroadAIBot.UI.Controls;
using SilkroadAIBot.Core.Configuration;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using SilkroadAIBot.Core.Settings;
using SilkroadAIBot.Core.Memory;
using SilkroadAIBot.Infrastructure.Networking;
using System.Linq;
using System.Collections.Immutable;
using System.Drawing.Drawing2D;
using SilkroadAIBot.Proxy;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Infrastructure.Networking.Mcp;
// Visual layout methods live in MainForm_Layout.cs (partial class)

namespace SilkroadAIBot.UI
{
    public partial class MainForm : Form
    {
        private string _botVersion = "v4.1.0";
        // Sidebar & Content
        private Panel _viewRadar = null!;
        private DataGridView _dgvRadar = null!;
        private System.Windows.Forms.Timer _radarTimer = null!;
        private Label _lblRadarCount = null!;
        private Panel _sidebar = null!, _contentPanel = null!, _pnlBottomLog = null!, _pnlTopStatus = null!;
        private Dictionary<string, Panel> _views = new Dictionary<string, Panel>();
        private Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();
        
        // Views
        private Panel _viewDashboard = null!, _viewMap = null!, _viewSniffer = null!, _viewSettings = null!, _viewModules = null!;
        private PacketSnifferControl _sniffer = null!;
        private MapControl _map = null!;
        private RichTextBox _rtbLogs = null!, _rtbRadarLog = null!;
        private Button _btnStartBot = null!, _btnStopBot = null!;

        // Statistics Labels (Top Bar)
        private Label _lblTopLv = null!, _lblTopName = null!, _lblTopExp = null!, _lblTopGold = null!, _lblTopPos = null!, _lblLoadProgress = null!;
        // Statistics Labels (Dashboard)
        private Label _lblDashLv = null!, _lblDashName = null!, _lblDashExp = null!, _lblDashGold = null!, _lblDashPos = null!;
        private Label _lblSessionTime = null!, _lblSessionXp = null!, _lblSessionSp = null!, _lblSessionKills = null!;

        // -----------------------------------------
        // View 1: Setup & Login
        // -----------------------------------------
        private TextBox _txtSroPath = null!, _txtManualIp = null!;
        private Button? _btnSelectPath;
        private Button? _btnLoadData;
        private CheckBox _chkProxyMode = null!, _chkNoDc = null!;
        private Button _btnStartGame = null!, _btnClientless = null!;
        private TextBox _txtUsername = null!, _txtPassword = null!;

        // -----------------------------------------
        // View 3: Modules Config
        // -----------------------------------------
        private TabControl _tcModules = null!;
        private BotSettings _settings = BotSettings.Instance;

        // -----------------------------------------
        // View: Skills & Combat
        // -----------------------------------------
        private ListView _lvMasteries = null!;
        private FlowLayoutPanel _flpSkillGrid = null!;
        private FlowLayoutPanel _flpAttackSequence = null!;
        private Panel _pnlSkillDetail = null!;
        private Label _lblSkillName = null!, _lblSkillStats = null!;
        private ProgressBar _pbMasteryExp = null!;
        private Label _lblSP = null!;

        // -----------------------------------------
        // View: Hunting Area (in Movement tab)
        // -----------------------------------------
        private Label _lblHuntCenter = null!;
        private NumericUpDown _nudHuntRadius = null!;
        private Button _btnSetHuntCenter = null!;

        // Logic
        private readonly Application.Interfaces.IActionLogger _logger;
        private readonly Application.Interfaces.IWorldStateRepository _worldState;
        private readonly Application.Interfaces.IEntityRepository _entityRepo;
        private DatabaseManager _db = null!;
        private DataManager _dataManager = null!;
        private IPCManager _ipc = null!;
        private McpServer _mcp = null!;
        // PacketDispatcher is owned by ProxyContext. MainForm subscribes to its static event.
        private PacketParser _packetParser = null!;
        private WorldStateAnalyzer _worldStateAnalyzer = null!;
        private SkillController _skillController = null!;
        private IBotController _botController = null!;
        private IPacketSender _packetSender = null!;
        private ProxyManager _proxy = null!;
        private System.Diagnostics.Process? _sroProcess;

        public MainForm(
            Application.Interfaces.IWorldStateRepository worldState, 
            Application.Interfaces.IEntityRepository entityRepo, 
            DatabaseManager db,
            Application.Interfaces.IActionLogger logger,
            IBotController botController,
            IPacketSender packetSender,
            McpServer mcp,
            PacketParser packetParser,
            WorldStateAnalyzer worldStateAnalyzer,
            SkillController skillController,
            ProxyManager proxy)
        {
            try 
            {
                this.DoubleBuffered = true;
                _botVersion = "v4.1.1";
                
                // Initialize engines
                _db = db;
                _worldState = worldState;
                _entityRepo = entityRepo;
                _logger = logger;
                _botController = botController;
                _packetSender = packetSender;
                _mcp = mcp;
                _packetParser = packetParser;
                _worldStateAnalyzer = worldStateAnalyzer;
                _skillController = skillController;
                _proxy = proxy;
                _dataManager = new DataManager();

                // Setup UI
                InitializeLayout();
                
                // Redirect Logging
                BotLogger.OnLogMessage += (msg, level) => SafeInvoke(() => {
                    AppendColoredLog(msg + Environment.NewLine, level);
                    if (msg.Contains("[Radar]") || msg.Contains("[Entity]"))
                        AppendRadarLog(msg + Environment.NewLine);
                    
                    // Forward to ActionLogger if it's an "action" level log
                    if (level == BotLogger.LogLevel.INFO) _logger.Log(msg);
                });

                _logger.OnLogAdded += (entry) => SafeInvoke(() => {
                    // This is for logs coming from the AI/Bot bundles directly via the interface
                    // They are already prefixed and formatted by ActionLogger
                    AppendColoredLog(entry + Environment.NewLine, BotLogger.LogLevel.INFO);
                });
                
                // Track Data Loading Progress
                _dataManager.OnProgress += (percent, msg) => SafeInvoke(() => {
                    if (_lblLoadProgress != null && !_lblLoadProgress.IsDisposed)
                    {
                        _lblLoadProgress.Text = $"[{percent}%] {msg}";
                        _lblLoadProgress.ForeColor = percent >= 100 ? Color.LimeGreen : ThemeColors.TextMuted;
                    }
                });

                RedirectConsole();
            }
            catch (Exception ex)
            {
                SilkroadAIBot.Core.Helpers.CrashReporter.Report(ex, "MainForm Constructor");
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BotLogger.Info("System", "Application Loaded. Starting components...");
            InitializeBotComponents();
        }

        // InitializeLayout, AddSidebarNav, CreateSidebarButton, ShowView,
        // BuildPersistentStatusPanel, CreateFlatButton, CreateBotButton,
        // GroupBox_Paint, CreateSidebarIcon — all in MainForm_Layout.cs

        private struct NavigationTarget { public Panel View; public int TabIndex; }

        private Panel CreateViewPanel(string title)
        {
            Panel p = new Panel { Dock = DockStyle.Fill, Visible = false };
            _contentPanel.Controls.Add(p);
            _views[title] = p;
            return p;
        }


        // ShowView → MainForm_Layout.cs

        // BuildPersistentStatusPanel → MainForm_Layout.cs


        // CreateSidebarIcon → MainForm_Layout.cs

        private void SetupDashboard(Panel page)
        {
            _rtbLogs = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 10, 15), ForeColor = Color.LightGray, BorderStyle = BorderStyle.None, ReadOnly = true, Font = new Font("Consolas", 9F) };
            page.Controls.Add(_rtbLogs);
            
            var lblTitle = new Label { Text = "SYSTEM LOGS", Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = ThemeColors.PrimaryAccent, Padding = new Padding(5, 0, 0, 0) };
            page.Controls.Add(lblTitle);
        }

        private void SetupRadarView(Panel parent)
        {
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(20, 20, 25), Padding = new Padding(10, 5, 10, 5) };
            _lblRadarCount = new Label { Text = "Nearby Entities: 0", ForeColor = ThemeColors.PrimaryAccent, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, Location = new Point(10, 10) };
            pnlHeader.Controls.Add(_lblRadarCount);
            
            var btnDump = new Button { Text = "Dump Entities to Log", ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 50), FlatStyle = FlatStyle.Flat, Size = new Size(150, 25), Location = new Point(200, 7) };
            btnDump.FlatAppearance.BorderSize = 0;
            btnDump.Click += (s, e) => DumpRadarToLog();
            pnlHeader.Controls.Add(btnDump);

            parent.Controls.Add(pnlHeader);

            _dgvRadar = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = ThemeColors.Background,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                EnableHeadersVisualStyles = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            _dgvRadar.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 40);
            _dgvRadar.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvRadar.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            _dgvRadar.DefaultCellStyle.BackColor = ThemeColors.Background;
            _dgvRadar.DefaultCellStyle.SelectionBackColor = ThemeColors.PrimaryAccent;
            _dgvRadar.GridColor = Color.FromArgb(40, 40, 50);

            _dgvRadar.Columns.Add("Type", "Type");
            _dgvRadar.Columns.Add("Name", "Name");
            _dgvRadar.Columns.Add("Dist", "Distance");
            _dgvRadar.Columns.Add("HP", "HP %");
            _dgvRadar.Columns.Add("ID", "Unique ID");

            _dgvRadar.Columns["Dist"].Width = 60;
            _dgvRadar.Columns["HP"].Width = 60;
            _dgvRadar.Columns["Type"].Width = 80;

            parent.Controls.Add(_dgvRadar);
            _dgvRadar.BringToFront();

            Splitter splitRadar = new Splitter { Dock = DockStyle.Bottom, Height = 3 };
            parent.Controls.Add(splitRadar);

            _rtbRadarLog = new RichTextBox { 
                Dock = DockStyle.Bottom, 
                Height = 150, 
                BackColor = Color.FromArgb(10, 10, 15), 
                ForeColor = Color.LimeGreen, 
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Font = new Font("Consolas", 8F)
            };
            parent.Controls.Add(_rtbRadarLog);

            _radarTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _radarTimer.Tick += (s, e) => { if (_viewRadar.Visible) UpdateRadar(); };
            _radarTimer.Start();
        }

        private HashSet<uint> _loggedRadarEntities = new HashSet<uint>();

        private void DumpRadarToLog()
        {
            if (_worldState == null || _worldState.Character == null) return;
            var entities = _worldState.Entities.Values.ToList();
            BotLogger.Info("Radar", $"=== DUMPING {entities.Count} ENTITIES TO LOG ===");
            foreach (var e in entities)
            {
                string type = e.EntityType.ToString();
                if (e is SRMob mob) type = mob.Rarity == MobRarity.Unique ? "UNIQUE" : "MOB";
                else if (e is SRPlayer) type = "PLAYER";
                else if (e is SRNpc) type = "NPC";
                else if (e is SRGroundItem) type = "DROP";
                
                BotLogger.Info("Radar", $"[Dump] UID: 0x{e.UniqueID:X} | ModelID: {e.ModelID} | Type: {type} | Name: '{e.Name}' | Pos: {e.Position.Region} ({e.Position.X}, {e.Position.Y}, {e.Position.Z})");
            }
            BotLogger.Info("Radar", $"===============================================");
        }

        private void UpdateRadar()
        {
            if (_worldState == null || _worldState.Character == null) return;

            var entities = _worldState.Entities.Values.OrderBy(e => e.Position.DistanceTo(_worldState.Character.Position)).ToList();
            _lblRadarCount.Text = $"Nearby Entities: {entities.Count}";

            _dgvRadar.SuspendLayout();

            // Track current rows
            var existingRows = new Dictionary<string, DataGridViewRow>();
            foreach (DataGridViewRow row in _dgvRadar.Rows)
            {
                string uidStr = row.Cells["ID"].Value?.ToString();
                if (!string.IsNullOrEmpty(uidStr)) existingRows[uidStr] = row;
            }

            int rowIndex = 0;
            foreach (var e in entities)
            {
                string dist = e.Position.DistanceTo(_worldState.Character.Position).ToString("F1");
                string hp = e.HPMax > 0 ? $"{(e.HP * 100.0 / e.HPMax):F0}%" : "---";
                string type = e.EntityType.ToString();
                
                if (e is SRMob mob) type = mob.Rarity == MobRarity.Unique ? "UNIQUE" : "MOB";
                else if (e is SRPlayer) type = "PLAYER";
                else if (e is SRNpc) type = "NPC";
                else if (e is SRGroundItem) type = "DROP";

                if (!_loggedRadarEntities.Contains(e.UniqueID))
                {
                    BotLogger.Debug("Radar", $"[Entity] Discovered: Type={type}, Name='{e.Name}', Dist={dist}, HP={hp}, UID=0x{e.UniqueID:X}, ModelID={e.ModelID}");
                    _loggedRadarEntities.Add(e.UniqueID);
                }

                string uidKey = $"0x{e.UniqueID:X}";
                if (existingRows.TryGetValue(uidKey, out DataGridViewRow existingRow))
                {
                    // Update existing
                    existingRow.Cells["Type"].Value = type;
                    existingRow.Cells["Name"].Value = e.Name;
                    existingRow.Cells["Dist"].Value = dist;
                    existingRow.Cells["HP"].Value = hp;
                    // Move row to correct sorted position if needed
                    if (existingRow.Index != rowIndex)
                    {
                        _dgvRadar.Rows.Remove(existingRow);
                        _dgvRadar.Rows.Insert(rowIndex, existingRow);
                    }
                    existingRows.Remove(uidKey); // Mark as kept
                }
                else
                {
                    // Add new
                    _dgvRadar.Rows.Insert(rowIndex, type, e.Name, dist, hp, uidKey);
                }
                rowIndex++;
            }

            // Remove rows that no longer exist
            foreach (var rowToRemove in existingRows.Values)
            {
                _dgvRadar.Rows.Remove(rowToRemove);
            }

            _dgvRadar.ResumeLayout();
        }

        private void SetupMap(Panel page)
        {
            _map = new MapControl(_worldState, _entityRepo, _dataManager) { Dock = DockStyle.Fill };
            page.Controls.Add(_map);
        }

        private void SetupSniffer(Panel page)
        {
            _sniffer = new PacketSnifferControl { Dock = DockStyle.Fill };
            page.Controls.Add(_sniffer);
        }

        // ==========================================
        // MODULE: STATISTICS (The phBot Landing Page)
        // ==========================================
        private Panel SetupStatisticsView()
        {
            var page = CreateViewPanel("Statistics");
            var tc = new TabControl { Dock = DockStyle.Fill };
            page.Controls.Add(tc);

            var tpGeneral = new TabPage("General") { BackColor = ThemeColors.Background };
            tc.TabPages.Add(tpGeneral);

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
            tpGeneral.Controls.Add(flow);

            // Group: Character Info
            var gbChar = CreateGroupBox("Character Status", new Size(400, 180));
            _lblDashLv = CreateStatLabel("Level: ---", new Point(15, 30));
            _lblDashName = CreateStatLabel("Name: ---", new Point(15, 55));
            _lblDashExp = CreateStatLabel("EXP: ---", new Point(15, 80));
            _lblDashGold = CreateStatLabel("Gold: ---", new Point(15, 105));
            _lblDashPos = CreateStatLabel("Position: ---", new Point(15, 130));
            
            gbChar.Controls.AddRange(new Control[] { _lblDashLv, _lblDashName, _lblDashExp, _lblDashGold, _lblDashPos });
            flow.Controls.Add(gbChar);

            // Group: Session Info
            var gbSession = CreateGroupBox("Session", new Size(400, 200));
            _lblSessionTime = CreateStatLabel("Runtime: 00:00:00", new Point(15, 25));
            _lblSessionXp = CreateStatLabel("XP/h: 0", new Point(15, 50));
            _lblSessionSp = CreateStatLabel("SP/h: 0", new Point(15, 75));
            _lblSessionKills = CreateStatLabel("Kills: 0", new Point(15, 100));
            
            gbSession.Controls.AddRange(new Control[] { _lblSessionTime, _lblSessionXp, _lblSessionSp, _lblSessionKills });
            flow.Controls.Add(gbSession);

            return page;
        }

        private Label CreateStatLabel(string text, Point pos) 
            => new Label { Text = text, Location = pos, AutoSize = true, ForeColor = ThemeColors.TextPrimary, Font = new Font("Consolas", 9F) };

        private GroupBox CreateGroupBox(string text, Size size)
        {
            var gb = new GroupBox { Text = text.ToUpper(), Size = size, ForeColor = ThemeColors.GroupTitleText, Margin = new Padding(0, 0, 10, 10) };
            gb.Paint += GroupBox_Paint;
            return gb;
        }

        // ==========================================
        // MODULE: LOGIN (phBot Style)
        // ==========================================
        
        private Panel SetupMapView() 
        { 
            var page = CreateViewPanel("Map");
            _map = new MapControl(_worldState, _entityRepo, _dataManager) { Dock = DockStyle.Fill };
            page.Controls.Add(_map);
            return page;
        }

        private Panel SetupPacketView()
        {
            var page = CreateViewPanel("Packets");
            _sniffer = new PacketSnifferControl { Dock = DockStyle.Fill };
            page.Controls.Add(_sniffer);
            return page;
        }

        private void StartBot()
        {
            try
            {
                if (_proxy?.GetActiveServerConnection() == null)
                {
                    MessageBox.Show("No active server connection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_botController != null && !_botController.IsRunning)
                {
                    // Clear existing bundles to avoid duplicates on restart
                    // If BotController doesn't have ClearBundles, we should just ensure we don't add them twice
                    if (_botController.Bundles.Count == 0)
                    {
                        _botController.Register(new RecoveryBundle(_worldState, _botController));
                        _botController.Register(new AttackBundle(_worldState, _db, _botController));
                    }
                    
                    _botController.Start();
                }
                BotLogger.Info("MainForm", "Bot Started.");
            }
            catch (Exception ex) { BotLogger.Error("MainForm", $"Start Error: {ex.Message}"); }
        }

        private void StopBot() { _botController?.Stop(); BotLogger.Info("MainForm", "Bot Stopped."); }

        // ==========================================
        // 15-TAB MODULE SYSTEM
        // ==========================================
        private void SetupView_ModulesConfig(Panel page)
        {
            _tcModules = new TabControl { Dock = DockStyle.Fill, ItemSize = new Size(80, 28), Multiline = true };
            page.Controls.Add(_tcModules);

            string[] tabNames = { "Skills & Combat", "Safety", "Movement", "Loot", "Supply", "Quest", "Job System", "Social & Chat", "Party", "Guild", "COS (Pets)", "Alchemy", "Network/Stall", "Events", "Advanced", "Manual Actions", "Logs" };
            foreach(var t in tabNames) { _tcModules.TabPages.Add(new TabPage(t) { BackColor = ThemeColors.Background, AutoScroll = true }); }

            BuildTabCombat(_tcModules.TabPages[0]);
            BuildTabSafety(_tcModules.TabPages[1]);
            BuildTabMovement(_tcModules.TabPages[2]);
            BuildTabLoot(_tcModules.TabPages[3]);
            BuildTabSupply(_tcModules.TabPages[4]);
            BuildTabQuest(_tcModules.TabPages[5]);
            BuildTabJob(_tcModules.TabPages[6]);
            BuildTabSocial(_tcModules.TabPages[7]);
            BuildTabParty(_tcModules.TabPages[8]);
            BuildTabGuild(_tcModules.TabPages[9]);
            BuildTabCOS(_tcModules.TabPages[10]);
            BuildTabAlchemy(_tcModules.TabPages[11]);
            BuildTabNetwork(_tcModules.TabPages[12]);
            BuildTabEvents(_tcModules.TabPages[13]);
            BuildTabAdvanced(_tcModules.TabPages[14]);
            BuildTabManual(_tcModules.TabPages[15]);
            BuildTabLog(_tcModules.TabPages[16]);
        }

        private void BuildTabLog(TabPage page)
        {
            _rtbLogs.Dock = DockStyle.Fill;
            page.Controls.Add(_rtbLogs);
        }
        // T1: Combat -> Redesigned Skill Manager v1.3.0 (Game-Like)
        private TabControl _tcSkillTypes = null!;
        private string _activeSkillTab = "Active";

        private void BuildTabCombat(TabPage page)
        {
            var pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            page.Controls.Add(pnlMain);

            // Top: Hunting Area (Consolidated from Movement tab)
            var gbHunt = new GroupBox { Text = "HUNTING AREA SETTINGS", Dock = DockStyle.Top, Height = 100, ForeColor = ThemeColors.GroupTitleText, Margin = new Padding(0, 0, 0, 10) };
            gbHunt.Paint += GroupBox_Paint;
            
            _lblHuntCenter = new Label { Text = "Center: Not Set", Location = new Point(15, 22), AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var btnSet = CreateFlatButton("📍 Set Center (Current Pos)", new Point(15, 45), new Size(200, 30), ThemeColors.PrimaryAccent);
            btnSet.Click += (s, e) => SetTrainingCenter();
            
            var lblRad = new Label { Text = "Radius:", Location = new Point(230, 50), AutoSize = true, ForeColor = ThemeColors.TextPrimary };
            _nudHuntRadius = new NumericUpDown { Location = new Point(285, 48), Width = 70, Minimum = 10, Maximum = 5000, Value = _settings.HuntRadiusDistance, BackColor = ThemeColors.Background, ForeColor = ThemeColors.TextPrimary };
            _nudHuntRadius.ValueChanged += (s, e) => { 
                _settings.HuntRadiusDistance = (int)_nudHuntRadius.Value; 
                _entityRepo.SetTrainingArea(_worldState.GetTrainingArea() with { Radius = _settings.HuntRadiusDistance });
                _settings.EnableHuntRadius = true; // Auto-enable if setting radius
            };
            
            var chkRadius = CreateCheckBox("Enable Radius", _settings.EnableHuntRadius, val => { 
                _settings.EnableHuntRadius = val; 
                _entityRepo.SetTrainingArea(_worldState.GetTrainingArea() with { IsEnabled = val }); 
            });
            chkRadius.Location = new Point(370, 46);
            
            gbHunt.Controls.AddRange(new Control[] { _lblHuntCenter, btnSet, lblRad, _nudHuntRadius, chkRadius });
            pnlMain.Controls.Add(gbHunt);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 220, BackColor = ThemeColors.Background };
            pnlMain.Controls.Add(split);
            pnlMain.Controls.SetChildIndex(split, 0); // Put split below hunt

            // Left Side: Mastery Trees
            var pnlLeft = split.Panel1;
            pnlLeft.Padding = new Padding(5);

            var lblT = new Label { Text = "MASTERY TREES", Dock = DockStyle.Top, Height = 30, ForeColor = ThemeColors.GroupTitleText, Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            pnlLeft.Controls.Add(lblT);

            _lvMasteries = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false,
                HeaderStyle = ColumnHeaderStyle.None, BackColor = ThemeColors.PanelSidebarBackground, ForeColor = ThemeColors.TextPrimary,
                Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.None
            };
            _lvMasteries.Columns.Add("Tree", 140);
            _lvMasteries.Columns.Add("Lv", 40);
            _lvMasteries.SelectedIndexChanged += (s, e) => { if (_lvMasteries.SelectedItems.Count > 0) LoadSkillsForTree(_lvMasteries.SelectedItems[0].Text); };
            pnlLeft.Controls.Add(_lvMasteries);

            // Right Side: Grid & Details
            var pnlRight = split.Panel2;
            pnlRight.Padding = new Padding(10);

            // Mastery Progress / SP
            var pnlMasteryInfo = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(25, 25, 30), Padding = new Padding(10, 5, 10, 5) };
            _pbMasteryExp = new ProgressBar { Location = new Point(10, 20), Size = new Size(250, 12), Style = ProgressBarStyle.Continuous };
            _lblSP = new Label { Text = "SP: 0", Location = new Point(280, 18), AutoSize = true, ForeColor = ThemeColors.PrimaryAccent, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            pnlMasteryInfo.Controls.AddRange(new Control[] { new Label { Text = "Mastery Progress", Location = new Point(10, 2), AutoSize = true, Font = new Font("Segoe UI", 7), ForeColor = ThemeColors.TextMuted }, _pbMasteryExp, _lblSP });
            pnlRight.Controls.Add(pnlMasteryInfo);

            // v1.3.0: Skill Type Tabs
            _tcSkillTypes = new TabControl { Dock = DockStyle.Top, Height = 30 };
            _tcSkillTypes.TabPages.Add("Active");
            _tcSkillTypes.TabPages.Add("Passive");
            _tcSkillTypes.TabPages.Add("Others");
            _tcSkillTypes.SelectedIndexChanged += (s, e) => { 
                if (_tcSkillTypes.SelectedTab != null)
                    _activeSkillTab = _tcSkillTypes.SelectedTab.Text;
                if (_lvMasteries.SelectedItems.Count > 0) LoadSkillsForTree(_lvMasteries.SelectedItems[0].Text); 
            };
            pnlRight.Controls.Add(_tcSkillTypes);

            // Skill Grid (Center)
            _flpSkillGrid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ThemeColors.Background, Padding = new Padding(10) };
            pnlRight.Controls.Add(_flpSkillGrid);

            // Detail Card (Bottom Right)
            _pnlSkillDetail = new Panel { Dock = DockStyle.Bottom, Height = 130, BackColor = Color.FromArgb(30, 30, 35), Padding = new Padding(10), Visible = false };
            _lblSkillName = new Label { Text = "Selected Skill", Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.White };
            _lblSkillStats = new Label { Text = "Stats...", Dock = DockStyle.Top, Height = 40, ForeColor = ThemeColors.TextMuted, Font = new Font("Segoe UI", 8) };

            var flpToggles = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            flpToggles.Controls.Add(CreateCheckBox("Use in sequence", false, val => { if (_selectedSkill != null) UpdateSkillSetting(_selectedSkill.ID, "sequence", val); }));
            flpToggles.Controls.Add(CreateCheckBox("Use as opener", false, val => { if (_selectedSkill != null) UpdateSkillSetting(_selectedSkill.ID, "opener", val); }));
            flpToggles.Controls.Add(CreateCheckBox("AI auto-manage", false, val => { if (_selectedSkill != null) UpdateSkillSetting(_selectedSkill.ID, "ai", val); }));

            _pnlSkillDetail.Controls.AddRange(new Control[] { flpToggles, _lblSkillStats, _lblSkillName });
            pnlRight.Controls.Add(_pnlSkillDetail);

            // Attack Sequence (Very Bottom)
            var pnlSeq = new Panel { Dock = DockStyle.Bottom, Height = 100, BackColor = Color.FromArgb(20, 20, 25), Padding = new Padding(10) };
            var lblSeq = new Label { Text = "ATTACK SEQUENCE", Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = ThemeColors.GroupTitleText };
            _flpAttackSequence = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoScroll = true };
            pnlSeq.Controls.Add(_flpAttackSequence);
            pnlSeq.Controls.Add(lblSeq);
            pnlRight.Controls.Add(pnlSeq);
        }

        private SRSkill? _selectedSkill = null;

        private void LoadSkillsForTree(string treeName)
        {
            _flpSkillGrid.Controls.Clear();
            var learned = _worldState.GetCharacter().LearnedSkills;
            foreach (var ls in learned)
            {
                var def = _dataManager.GetSkill(ls.SkillID);
                if (def == null) continue;

                bool matchTree = def.MasteryTree.Contains(treeName, StringComparison.OrdinalIgnoreCase);
                bool matchType = _activeSkillTab switch {
                    "Active" => def.SkillType == "Attack" || def.SkillType == "Support",
                    "Passive" => def.SkillType == "Passive",
                    "Others" => def.SkillType == "Buff" || def.SkillType == "Other",
                    _ => true
                };

                if (matchTree && matchType)
                {
                    var slot = new SkillSlotControl();
                    slot.SetSkill(def);
                    slot.Click += (s, e) => ShowSkillDetail(def);
                    _flpSkillGrid.Controls.Add(slot);
                }
            }
        }

        private void ShowSkillDetail(SRSkill skill)
        {
            _selectedSkill = skill;
            _pnlSkillDetail.Visible = true;
            _lblSkillName.Text = skill.Name;
            _lblSkillStats.Text = $"ID: 0x{skill.ID:X8} | Type: {skill.SkillType} | Damage: {skill.DamageRange}\nCD: {skill.Cooldown}ms | MP: {skill.MPUsage} | Range: {skill.Range}";
            
            // Highlight selected slot in grid
            foreach (SkillSlotControl ctrl in _flpSkillGrid.Controls)
            {
                ctrl.IsSelected = (ctrl.Skill?.ID == skill.ID);
                ctrl.Invalidate();
            }
        }

        private void UpdateSkillSetting(uint skillID, string type, bool val)
        {
            var character = _worldState.GetCharacter();
            var ls = character.LearnedSkills.FirstOrDefault(x => x.SkillID == skillID);
            if (ls == null) return;

            if (type == "sequence") {
                _entityRepo.Update<SRCharacter>(character.UniqueID, c => c with { 
                    Skills = c.Skills.Select(s => s.SkillID == skillID ? s with { UseInSequence = val } : s).ToImmutableList() 
                });
                if (val && !_settings.AttackSkillSequence.Contains(skillID)) _settings.AttackSkillSequence.Add(skillID);
                else if (!val) _settings.AttackSkillSequence.Remove(skillID);
                RefreshAttackSequenceUI();
            }
            else if (type == "ai") {
                _entityRepo.Update<SRCharacter>(character.UniqueID, c => c with { 
                    Skills = c.Skills.Select(s => s.SkillID == skillID ? s with { AIAutoManage = val } : s).ToImmutableList() 
                });
            }
            
            BotLogger.Info("SkillMgr", $"Updated skill {skillID} {type} -> {val}");
        }

        private void RefreshAttackSequenceUI()
        {
            _flpAttackSequence.Controls.Clear();
            int i = 1;
            foreach (var id in _settings.AttackSkillSequence)
            {
                var def = _dataManager.GetSkill(id);
                if (def == null) continue;

                var slot = new SkillSlotControl { IsInSequence = true };
                slot.SetSkill(def);
                _flpAttackSequence.Controls.Add(slot);

                if (i < _settings.AttackSkillSequence.Count)
                {
                    _flpAttackSequence.Controls.Add(new Label { Text = "→", Margin = new Padding(0, 30, 0, 0), ForeColor = Color.Gray, AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) });
                }
                i++;
            }
        }

        private void RefreshMasteryList()
        {
            if (_lvMasteries.InvokeRequired) { _lvMasteries.Invoke(new Action(RefreshMasteryList)); return; }

            _lvMasteries.Items.Clear();
            
            // Define trees based on race
            string[] trees = _worldState.CharacterRace == (int)CharacterRace.Chinese 
                ? new[] { "SWORD", "SPEAR", "BOW", "ICE", "LIGHTNING", "FIRE", "FORCE" }
                : new[] { "WARRIOR", "ROGUE", "WIZARD", "WARLOCK", "BARD", "CLERIC" };

            foreach (var t in trees)
            {
                var m = _worldState.Character.Masteries.FirstOrDefault(x => _dataManager.GetCodeName(x.ID)?.ToUpper().Contains(t) == true);
                var level = m?.Level ?? 0;
                var item = new ListViewItem(new[] { t, level.ToString() });
                _lvMasteries.Items.Add(item);
            }

            _lblSP.Text = $"SP: {_worldState.Character.SkillPoints:N0}";
        }

        // T2: Safety
        private void BuildTabSafety(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            page.Controls.Add(flow);
            
            flow.Controls.Add(CreateCheckBox("Auto HP Potion", _settings.AutoHPPotion, val => _settings.AutoHPPotion = val));
            flow.Controls.Add(CreateCheckBox("Auto MP Potion", _settings.AutoMPPotion, val => _settings.AutoMPPotion = val));
            flow.Controls.Add(CreateCheckBox("Auto Pill (Cure Status)", _settings.AutoPill, val => _settings.AutoPill = val));
            flow.Controls.Add(CreateCheckBox("Return to Town on Death", _settings.ReturnToTownOnDeath, val => _settings.ReturnToTownOnDeath = val));
        }

        // T3: Movement / Hunting Area
        private void BuildTabMovement(TabPage page)
        {
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };
            page.Controls.Add(flow);

            // ----- Hunting Area Group -----
            var gbHunt = new GroupBox { Text = "Hunting Area", Width = 550, Height = 160, ForeColor = ThemeColors.GroupTitleText, Margin = new Padding(0, 0, 0, 10) };
            gbHunt.Paint += GroupBox_Paint;

            _lblHuntCenter = new Label { Text = "Center: Not Set", Location = new Point(15, 25), AutoSize = true, ForeColor = ThemeColors.TextPrimary, Font = new Font("Segoe UI", 9F) };

            _btnSetHuntCenter = CreateFlatButton("📍 Set Center (Use Current Position)", new Point(15, 50), new Size(280, 30), ThemeColors.PrimaryAccent);
            _btnSetHuntCenter.Click += (s, e) =>
            {
                var ch = _worldState?.Character;
                if (ch == null) { MessageBox.Show("Not in game yet.", "Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                var pos = ch.Position;
                if (pos.Region == 0)
                {
                    MessageBox.Show("Character position not yet known. Enter the game first.", "Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _worldState.TrainingArea = _worldState.TrainingArea with { 
                    Center = _worldState.Character.Position,
                    IsEnabled = true 
                };
                _lblHuntCenter.Text = $"Center: Region={pos.Region} X={pos.X:F1} Z={pos.Z:F1}";
                BotLogger.Info("MainForm", $"[HuntArea] Center set to {pos}");
            };

            var lblRadius = new Label { Text = "Radius (game units):", Location = new Point(15, 90), AutoSize = true, ForeColor = ThemeColors.TextPrimary };
            _nudHuntRadius = new NumericUpDown
            {
                Location = new Point(155, 88), Width = 100, Minimum = 10, Maximum = 5000,
                Value = _settings.HuntRadiusDistance, BackColor = ThemeColors.Background, ForeColor = ThemeColors.TextPrimary
            };
            _nudHuntRadius.ValueChanged += (s, e) =>
            {
                int radius = (int)_nudHuntRadius.Value;
                _settings.HuntRadiusDistance = radius;
                _worldState.TrainingArea = _worldState.TrainingArea with { Radius = radius };
            };

            var btnClearHunt = CreateFlatButton("✕ Clear", new Point(310, 88), new Size(80, 26), ThemeColors.Error);
            btnClearHunt.Click += (s, e) =>
            {
                _worldState.TrainingArea = _worldState.TrainingArea with { IsEnabled = false };
                _lblHuntCenter.Text = "Center: Not Set";
                BotLogger.Info("MainForm", "[HuntArea] Cleared.");
            };

            gbHunt.Controls.AddRange(new Control[] { _lblHuntCenter, _btnSetHuntCenter, lblRadius, _nudHuntRadius, btnClearHunt });
            flow.Controls.Add(gbHunt);

            // ----- Movement Options -----
            var gbMove = new GroupBox { Text = "Movement Options", Width = 550, Height = 150, ForeColor = ThemeColors.GroupTitleText };
            gbMove.Paint += GroupBox_Paint;
            var innerFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(5) };
            innerFlow.Controls.Add(CreateCheckBox("Enable Hunting Area Radius", _settings.EnableHuntRadius, val => { 
                _settings.EnableHuntRadius = val; 
                _worldState.TrainingArea = _worldState.TrainingArea with { IsEnabled = val }; 
            }));
            innerFlow.Controls.Add(CreateCheckBox("A* NavMesh Pathfinding", _settings.UseNavMeshPathfinding, val => _settings.UseNavMeshPathfinding = val));
            innerFlow.Controls.Add(CreateCheckBox("Avoid Obstacles / Auto-Unstuck", _settings.AvoidObstaclesAutoUnstuck, val => _settings.AvoidObstaclesAutoUnstuck = val));
            innerFlow.Controls.Add(CreateCheckBox("Use Return/Town Scroll", _settings.UseReturnScroll, val => _settings.UseReturnScroll = val));
            gbMove.Controls.Add(innerFlow);
            flow.Controls.Add(gbMove);
        }

        // T4: Loot
        private void BuildTabLoot(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            page.Controls.Add(flow);

            flow.Controls.Add(CreateCheckBox("Auto-Pickup Items", _settings.AutoPickupItems, val => _settings.AutoPickupItems = val));
            flow.Controls.Add(CreateCheckBox("Pick Gold", _settings.AutoPickupGold, val => _settings.AutoPickupGold = val));
            flow.Controls.Add(CreateCheckBox("Auto-Sort Inventory", _settings.AutoSortInventory, val => _settings.AutoSortInventory = val));
            flow.Controls.Add(CreateCheckBox("Enable Pet Pickup (COS)", _settings.EnablePetPickup, val => _settings.EnablePetPickup = val));
        }

        // T5: Supply
        private void BuildTabSupply(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            page.Controls.Add(flow);

            flow.Controls.Add(CreateCheckBox("Auto-Repair Items", _settings.AutoRepairItems, val => _settings.AutoRepairItems = val));
            flow.Controls.Add(CreateCheckBox("Buy HP/MP/Pills", _settings.RestockPotions, val => _settings.RestockPotions = val));
            flow.Controls.Add(CreateCheckBox("Buy Arrows/Bolts", _settings.RestockAmmo, val => _settings.RestockAmmo = val));
            flow.Controls.Add(CreateCheckBox("NPC Storage Auto-Deposit", _settings.NPCStorageAutoDeposit, val => _settings.NPCStorageAutoDeposit = val));
            flow.Controls.Add(CreateCheckBox("NPC Shop Auto Buy/Sell", _settings.NPCShopAutoBuySell, val => _settings.NPCShopAutoBuySell = val));
        }

        // T6: Quest
        private void BuildTabQuest(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            page.Controls.Add(flow);

            flow.Controls.Add(CreateCheckBox("Auto-Accept Quests from NPCs", _settings.AutoAcceptQuests, val => _settings.AutoAcceptQuests = val));
            flow.Controls.Add(CreateCheckBox("Auto-Complete Objectives", _settings.AutoCompleteObjectives, val => _settings.AutoCompleteObjectives = val));
            flow.Controls.Add(CreateCheckBox("Auto-Return Quest to NPC", _settings.AutoReturnQuests, val => _settings.AutoReturnQuests = val));
        }

        // T7: Job System
        private void BuildTabJob(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            page.Controls.Add(flow);

            flow.Controls.Add(CreateCheckBox("Auto-Equip Job Outfit", _settings.AutoEquipJobOutfit, val => _settings.AutoEquipJobOutfit = val));
            flow.Controls.Add(CreateCheckBox("Trader: Auto-Start Trade Run", _settings.JobAutoStartTradeRun, val => _settings.JobAutoStartTradeRun = val));
            flow.Controls.Add(CreateCheckBox("Hunter: Auto-Protect Trader", _settings.HunterAutoProtectTrader, val => _settings.HunterAutoProtectTrader = val));
            flow.Controls.Add(CreateCheckBox("Thief: Auto-Attack Traders/Hunters", _settings.ThiefAutoAttack, val => _settings.ThiefAutoAttack = val));
            flow.Controls.Add(CreateCheckBox("Thief: Auto-Collect & Deliver Goods", _settings.ThiefAutoCollectGoods, val => _settings.ThiefAutoCollectGoods = val));
        }

        // T8: Social
        private void BuildTabSocial(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };
            page.Controls.Add(flow);

            var gbSettings = new GroupBox { Text = "Social Settings", Width = 550, Height = 120, ForeColor = ThemeColors.GroupTitleText };
            gbSettings.Paint += GroupBox_Paint;
            var innerFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            innerFlow.Controls.Add(CreateCheckBox("Send Chat Messages via Channels", _settings.SendChatMessages, val => _settings.SendChatMessages = val));
            innerFlow.Controls.Add(CreateCheckBox("Auto-Respond to Whispers", _settings.AutoRespondWhispers, val => _settings.AutoRespondWhispers = val));
            innerFlow.Controls.Add(CreateCheckBox("Trigger Emotes/Gestures", _settings.TriggerEmotes, val => _settings.TriggerEmotes = val));
            gbSettings.Controls.Add(innerFlow);
            flow.Controls.Add(gbSettings);

            var gbManual = new GroupBox { Text = "Manual Chat", Width = 550, Height = 180, ForeColor = ThemeColors.GroupTitleText, Margin = new Padding(0, 10, 0, 0) };
            gbManual.Paint += GroupBox_Paint;
            
            var lblType = new Label { Text = "Type:", Location = new Point(15, 25), AutoSize = true };
            var cbType = new ComboBox { Location = new Point(60, 22), Width = 100, BackColor = ThemeColors.Background, ForeColor = Color.White };
            cbType.Items.AddRange(new object[] { "General", "Private", "Party", "Guild", "Global" });
            cbType.SelectedIndex = 0;

            var lblTarget = new Label { Text = "To (PM):", Location = new Point(175, 25), AutoSize = true };
            var txtTarget = new TextBox { Location = new Point(235, 22), Width = 150, BackColor = ThemeColors.Background, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            var lblMsg = new Label { Text = "Message:", Location = new Point(15, 55), AutoSize = true };
            var txtMsg = new TextBox { Location = new Point(15, 75), Width = 520, Height = 50, Multiline = true, BackColor = ThemeColors.Background, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            var btnSend = CreateFlatButton("✉ SEND MESSAGE", new Point(15, 135), new Size(520, 30), ThemeColors.PrimaryAccent);
            btnSend.Click += (s, e) => {
                if (_packetSender == null) return;
                byte type = (byte)(cbType.SelectedIndex + 1);
                if (cbType.Text == "Global") type = 6;
                _packetSender.SendChat(type, txtMsg.Text, txtTarget.Text);
                txtMsg.Clear();
            };

            gbManual.Controls.AddRange(new Control[] { lblType, cbType, lblTarget, txtTarget, lblMsg, txtMsg, btnSend });
            gbManual.Controls.Add(btnSend);
            flow.Controls.Add(gbManual);
        }

        // T9: Party
        private void BuildTabParty(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };
            page.Controls.Add(flow);

            var gbSettings = new GroupBox { Text = "Party AI Settings", Width = 550, Height = 140, ForeColor = ThemeColors.GroupTitleText };
            gbSettings.Paint += GroupBox_Paint;
            var innerFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            innerFlow.Controls.Add(CreateCheckBox("Auto-Accept Party Invites", _settings.AutoAcceptPartyInvites, val => _settings.AutoAcceptPartyInvites = val));
            innerFlow.Controls.Add(CreateCheckBox("Auto-Form Party Match", _settings.AutoFormPartyMatch, val => _settings.AutoFormPartyMatch = val));
            innerFlow.Controls.Add(CreateCheckBox("Auto-Resurrect Party Member", _settings.AutoResurrectPartyMember, val => _settings.AutoResurrectPartyMember = val));
            gbSettings.Controls.Add(innerFlow);
            flow.Controls.Add(gbSettings);

            var gbManual = new GroupBox { Text = "Manual Party Actions", Width = 550, Height = 100, ForeColor = ThemeColors.GroupTitleText, Margin = new Padding(0, 10, 0, 0) };
            gbManual.Paint += GroupBox_Paint;

            var lblTid = new Label { Text = "Target GID:", Location = new Point(15, 25), AutoSize = true };
            var txtTid = new TextBox { Location = new Point(90, 22), Width = 120, BackColor = ThemeColors.Background, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            var btnCreate = CreateFlatButton("👥 CREATE PARTY", new Point(220, 20), new Size(150, 30), ThemeColors.ConnectedStatus);
            btnCreate.Click += (s, e) => {
                if (_packetSender != null && uint.TryParse(txtTid.Text, out uint gid))
                    _packetSender.SendPartyCreate(gid, 0x00); // Free-for-all
            };

            var btnInvite = CreateFlatButton("📩 INVITE TO PARTY", new Point(380, 20), new Size(150, 30), ThemeColors.PrimaryAccent);
            btnInvite.Click += (s, e) => {
                if (_packetSender != null && uint.TryParse(txtTid.Text, out uint gid))
                    _packetSender.SendPartyInvite(gid);
            };

            gbManual.Controls.AddRange(new Control[] { lblTid, txtTid, btnCreate, btnInvite });
            flow.Controls.Add(gbManual);
        }

        // T10: Guild
        private void BuildTabGuild(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            page.Controls.Add(flow);

            flow.Controls.Add(CreateCheckBox("Auto Create/Join Guild", _settings.AutoManageGuild, val => _settings.AutoManageGuild = val));
            flow.Controls.Add(CreateCheckBox("Access Guild Storage", _settings.AccessGuildStorage, val => _settings.AccessGuildStorage = val));
            flow.Controls.Add(CreateCheckBox("Register for Fortress War", _settings.RegisterFortressWar, val => _settings.RegisterFortressWar = val));
            flow.Controls.Add(CreateCheckBox("Place Command Post", _settings.PlaceCommandPost, val => _settings.PlaceCommandPost = val));
            flow.Controls.Add(CreateCheckBox("Use Combat Flags", _settings.UseCombatFlags, val => _settings.UseCombatFlags = val));
        }

        // T11: COS
        private void BuildTabCOS(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            page.Controls.Add(flow);

            flow.Controls.Add(CreateCheckBox("Enable Pet Pickup", _settings.EnablePetPickup, val => _settings.EnablePetPickup = val));
            flow.Controls.Add(CreateCheckBox("Auto-Feed Pet", _settings.AutoFeedPet, val => _settings.AutoFeedPet = val));
            flow.Controls.Add(CreateCheckBox("Auto-Summon Pet", _settings.AutoSummonPet, val => _settings.AutoSummonPet = val));
        }

        // T12: Alchemy
        private void BuildTabAlchemy(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            page.Controls.Add(flow);

            flow.Controls.Add(CreateCheckBox("Auto-Reinforce with Elixirs (+Level)", _settings.AutoReinforce, val => _settings.AutoReinforce = val));
            flow.Controls.Add(CreateCheckBox("Auto-Enchant with Stones", _settings.AutoEnchant, val => _settings.AutoEnchant = val));
        }

        // T13: Network
        private void BuildTabNetwork(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };
            page.Controls.Add(flow);

            var gbStall = new GroupBox { Text = "Personal Stall", Width = 550, Height = 100, ForeColor = ThemeColors.GroupTitleText };
            gbStall.Paint += GroupBox_Paint;

            var lblName = new Label { Text = "Stall Name:", Location = new Point(15, 25), AutoSize = true };
            var txtName = new TextBox { Location = new Point(90, 22), Width = 300, BackColor = ThemeColors.Background, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            
            var btnStall = CreateFlatButton("🏪 OPEN STALL", new Point(400, 20), new Size(130, 30), ThemeColors.Warning);
            btnStall.Click += (s, e) => {
                if (_packetSender != null) _packetSender.SendStallCreate(txtName.Text);
            };

            gbStall.Controls.AddRange(new Control[] { lblName, txtName, btnStall });
            flow.Controls.Add(gbStall);

            flow.Controls.Add(CreateCheckBox("Auto-Stall Creation", _settings.AutoStallCreation, val => _settings.AutoStallCreation = val));
            flow.Controls.Add(CreateCheckBox("Consignment Check", _settings.ConsignmentCheck, val => _settings.ConsignmentCheck = val));
        }

        // T14: Events
        private void BuildTabEvents(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
            page.Controls.Add(flow);

            flow.Controls.Add(CreateCheckBox("Detect Active Server Events", _settings.DetectActiveEvents, val => _settings.DetectActiveEvents = val));
            flow.Controls.Add(CreateCheckBox("Auto-Participate in Objectives", _settings.AutoParticipateEvents, val => _settings.AutoParticipateEvents = val));
            flow.Controls.Add(CreateCheckBox("Auto-Exchange Event Items", _settings.AutoExchangeEventItems, val => _settings.AutoExchangeEventItems = val));
        }

        // T15: Advanced
        private void BuildTabAdvanced(TabPage page) {
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };
            page.Controls.Add(flow);

            var gbEx = new GroupBox { Text = "Quick Exchange", Width = 550, Height = 80, ForeColor = ThemeColors.GroupTitleText };
            gbEx.Paint += GroupBox_Paint;
            var lblUid = new Label { Text = "Target UID:", Location = new Point(15, 25), AutoSize = true };
            var txtUid = new TextBox { Location = new Point(90, 22), Width = 150, BackColor = ThemeColors.Background, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            var btnEx = CreateFlatButton("🤝 REQ EXCHANGE", new Point(260, 20), new Size(270, 30), ThemeColors.PrimaryAccent);
            btnEx.Click += (s, e) => {
                if (_packetSender != null && uint.TryParse(txtUid.Text, out uint uid))
                    _packetSender.SendExchangeStart(uid);
            };
            gbEx.Controls.AddRange(new Control[] { lblUid, txtUid, btnEx });
            flow.Controls.Add(gbEx);

            flow.Controls.Add(CreateCheckBox("Forgotten World (FGW) Auto-Farm Pillars", _settings.FGWAutoFarmPillars, val => _settings.FGWAutoFarmPillars = val));
            flow.Controls.Add(CreateCheckBox("Job SafeTrade Automation", _settings.JobSafeTradeAutomation, val => _settings.JobSafeTradeAutomation = val));
            flow.Controls.Add(CreateCheckBox("Auto-Academy Creation & Graduation", _settings.AutoAcademyCreation, val => _settings.AutoAcademyCreation = val));
        }

        private void SetTrainingCenter()
        {
            var ch = _worldState?.Character;
            if (ch == null || ch.Position.Region == 0) { MessageBox.Show("Position unknown. Enter game world first."); return; }
            _worldState.TrainingArea = _worldState.TrainingArea with { Center = ch.Position };
            _lblHuntCenter.Text = $"Center: Region={ch.Position.Region} X={ch.Position.X:F1} Y={ch.Position.Y:F1}";
            _settings.EnableHuntRadius = true;
            BotLogger.Info("MainForm", $"[Hunt] Center set to {ch.Position}");
        }

        private CheckBox CreateCheckBox(string text, bool defaultVal, Action<bool> onChange)
        {
            var chk = new CheckBox 
            { 
                Text = text, Checked = defaultVal, AutoSize = true, 
                Margin = new Padding(10, 10, 10, 0), Font = new Font("Segoe UI", 10F),
                ForeColor = ThemeColors.TextPrimary
            };
            chk.CheckedChanged += (s, e) => onChange(chk.Checked);
            return chk;
        }

        // ==========================================
        // OTHER VIEWS
        // ==========================================
        private void SetupView1_SetupAndLogin(Panel page)
        {
            Label lblTitle = new Label { Text = "GAME SETUP", Font = new Font("Segoe UI", 16, FontStyle.Bold), Location = new Point(20, 20), AutoSize = true, ForeColor = ThemeColors.PrimaryAccent };
            page.Controls.Add(lblTitle);

            GroupBox gbPath = new GroupBox { Text = "1. Game Directory", Location = new Point(20, 70), Size = new Size(600, 130), ForeColor = ThemeColors.GroupTitleText };
            page.Controls.Add(gbPath);

            _txtSroPath = new TextBox { Location = new Point(20, 40), Width = 430, ReadOnly = true, BackColor = ThemeColors.Background, ForeColor = ThemeColors.TextPrimary, BorderStyle = BorderStyle.FixedSingle };
            _btnSelectPath = CreateFlatButton("Select Folder", new Point(460, 38), new Size(120, 30), ThemeColors.PrimaryAccent);
            _btnSelectPath.Click += (s, e) => { 
                using (var fbd = new FolderBrowserDialog { Description = "Select Silkroad Game Folder" }) 
                {
                    if (fbd.ShowDialog() == DialogResult.OK) 
                    {
                        _txtSroPath.Text = fbd.SelectedPath;
                        ConfigManager.Config.SroPath = fbd.SelectedPath;
                        try 
                        {
                            string gameName = new DirectoryInfo(fbd.SelectedPath).Name;
                            DatabaseManager.Instance.ChangeDatabase(gameName);
                        }
                        catch { }
                        ConfigManager.Save();
                        _ = LoadGameData();
                    }
                }
            };
            
            _btnLoadData = CreateFlatButton("LOAD DATA", new Point(460, 75), new Size(120, 30), ThemeColors.SecondaryAccent);
            _btnLoadData.Click += async (s, e) => await LoadGameData();
            
            _lblLoadProgress = new Label { 
                Text = "Data Not Loaded", 
                Location = new Point(20, 75), 
                Size = new Size(430, 30), 
                ForeColor = ThemeColors.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };

            gbPath.Controls.AddRange(new Control[] { _txtSroPath, _btnSelectPath, _btnLoadData, _lblLoadProgress });

            GroupBox gbLaunch = new GroupBox { Text = "2. Launch Settings", Location = new Point(20, 220), Size = new Size(600, 220), ForeColor = ThemeColors.GroupTitleText };
            page.Controls.Add(gbLaunch);

            Label lblIp = new Label { Text = "Server Address (IP:Port):", Location = new Point(20, 30), AutoSize = true, ForeColor = ThemeColors.TextPrimary };
            _txtManualIp = new TextBox { Location = new Point(20, 50), Width = 300, BackColor = ThemeColors.Background, ForeColor = ThemeColors.TextPrimary, BorderStyle = BorderStyle.FixedSingle };
            
            _chkProxyMode = new CheckBox { Text = "Proxy Mode (SRO Client)", Location = new Point(20, 80), Checked = true, AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ThemeColors.PrimaryAccent };
            _chkNoDc = new CheckBox { Text = "Enable No-DC / Multi-Client Patch", Location = new Point(250, 80), Checked = true, AutoSize = true, Font = new Font("Segoe UI", 10), ForeColor = ThemeColors.Warning };
            
            Label lblWarning = new Label { Text = "Clientless requires Username/Password:", Location = new Point(20, 110), AutoSize = true, ForeColor = ThemeColors.TextMuted };
            _txtUsername = new TextBox { Location = new Point(20, 130), Width = 200, BackColor = ThemeColors.Background, ForeColor = ThemeColors.TextPrimary, BorderStyle = BorderStyle.FixedSingle };
            _txtPassword = new TextBox { Location = new Point(230, 130), Width = 200, BackColor = ThemeColors.Background, ForeColor = ThemeColors.TextPrimary, BorderStyle = BorderStyle.FixedSingle, PasswordChar = '*' };

            _btnStartGame = CreateFlatButton("START GAME", new Point(20, 170), new Size(270, 40), ThemeColors.ConnectedStatus);
            _btnStartGame.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            _btnStartGame.Click += async (s, e) => await StartGameSequence(true);

            _btnClientless = CreateFlatButton("START CLIENTLESS", new Point(310, 170), new Size(270, 40), ThemeColors.SecondaryAccent);
            _btnClientless.Click += async (s, e) => await StartGameSequence(false);
            
            gbLaunch.Controls.AddRange(new Control[] { lblIp, _txtManualIp, _chkProxyMode, _chkNoDc, lblWarning, _txtUsername, _txtPassword, _btnStartGame, _btnClientless });
        }

        private void InitializeBotComponents()
        {
            try 
            {
                BotLogger.Info("Startup", "Initializing IPC Manager...");
                _ipc = new IPCManager();
                _ipc.Start();

                BotLogger.Info("Startup", "Initializing AI Control Server (MCP)...");
                _mcp.Start(5999);

                BotLogger.Info("Startup", "Initializing Packet Systems...");
                PacketLogger.SetEnabled(true);

                string logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);
                DebugTrace.Enable(logsDir);

                // Injected: _packetParser, _packetSender, _worldStateAnalyzer, _skillController, _botController

                _dataManager.OnProgress += (percent, msg) => SafeInvoke(() => {
                    _lblLoadProgress.Text = $"{percent}% - {msg}";
                    _lblLoadProgress.ForeColor = percent == 100 ? ThemeColors.ConnectedStatus : ThemeColors.PrimaryAccent;
                    if (percent == 100) BotLogger.Info("System", $"Data Loading Complete: {msg}");
                });

                BotLogger.Info("System", $"=== SILKROAD AI BOT {_botVersion} READY ===");

                PacketDispatcher.OnGlobalPacketReceived += (pkt, isSent) =>
                {
                    if (_sniffer != null) SafeInvoke(() => _sniffer.AddPacket(pkt, isSent ? "[C>S]" : "[S>C]"));
                };

                _worldState.OnCharacterUpdated += () => SafeInvoke(() => { UpdateStatusBars(); });
                _worldState.OnSkillsUpdated    += () => SafeInvoke(() => { if (_lvMasteries != null && _lvMasteries.SelectedItems.Count > 0) LoadSkillsForTree(_lvMasteries.SelectedItems[0].Text); });

                // Initialize Session Timer
                var statsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                statsTimer.Tick += (s, e) => SafeInvoke(UpdateSessionStats);
                statsTimer.Start();

                ConfigManager.Load();
                _txtSroPath.Text = ConfigManager.Config.SroPath;
                _txtUsername.Text = ConfigManager.Config.Username;
                _txtPassword.Text = ConfigManager.Config.Password;

                if (!string.IsNullOrEmpty(ConfigManager.Config.SroPath))
                {
                    try 
                    {
                        string gameName = new DirectoryInfo(ConfigManager.Config.SroPath).Name;
                        DatabaseManager.Instance.ChangeDatabase(gameName);
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(_txtSroPath.Text) && ValidateSroFolder(_txtSroPath.Text)) _ = LoadGameData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CRITICAL STARTUP ERROR: {ex.Message}\n\n{ex.StackTrace}", "Startup Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                BotLogger.Error($"Startup Failed: {ex.Message}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            BotLogger.Info("MainForm", "Application shutting down...");
            
            // Revert DNS Redirect if we spoofed it
            string input = _txtManualIp.Text;
            if (!string.IsNullOrEmpty(input))
            {
                string targetIp = input.Contains(':') ? input.Split(':')[0] : input;
                Core.Memory.SroLoader.RevertDnsRedirect(targetIp);
            }
            
            _botController?.Stop();
            BotLogger.Shutdown();
            base.OnFormClosing(e);
        }

        private void BrowseForSroPath()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    if (ValidateSroFolder(fbd.SelectedPath, false))
                    {
                        _txtSroPath.Text = fbd.SelectedPath;
                        ConfigManager.Config.SroPath = fbd.SelectedPath;
                        try 
                        {
                            string gameName = new DirectoryInfo(fbd.SelectedPath).Name;
                            DatabaseManager.Instance.ChangeDatabase(gameName);
                        }
                        catch { }
                        ConfigManager.Save();
                    }
                }
            }
        }

        private bool ValidateSroFolder(string path, bool requireClient = true)
        {
            bool hasClient = File.Exists(Path.Combine(path, "sro_client.exe"));
            bool hasMedia = File.Exists(Path.Combine(path, "Media.pk2"));
            
            if (!hasMedia)
            {
                MessageBox.Show("Invalid Folder! Missing Media.pk2", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (requireClient && !hasClient)
            {
                MessageBox.Show("Invalid Folder! Missing sro_client.exe (Required to start game)", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private async Task LoadGameData()
        {
            string sroPath = _txtSroPath.Text;
            if (string.IsNullOrEmpty(sroPath)) return;

            BotLogger.Info("MainForm", $"Initialization started for: {sroPath}");
            _btnLoadData.Enabled = false;

            _dataManager.SetDatabase(_db);

            // Use the provided extracted path as a secondary source
            string extractedPath = @"d:\Anti\game files\SRODataExtractor\bin\Release\net10.0-windows\ExtractedData";
            
            bool success = await Task.Run(() => _dataManager.Initialize(sroPath, extractedPath));
            if (success)
            {
                BotLogger.Info("MainForm", "Game files verified. Beginning data synchronization...");
                await Task.Run(() => _dataManager.ExtractAndSaveToDatabase());
                
                if (_dataManager.AutoDiscoverServerConfig(out string ip, out int port))
                {
                    SafeInvoke(() => _txtManualIp.Text = $"{ip}:{port}");
                    BotLogger.Info("MainForm", $"Server discovered successfully: {ip}:{port}");
                }
                else
                {
                    BotLogger.Warn("MainForm", "Automatic server discovery failed. Please enter IP manually.");
                }

                BotLogger.Info("MainForm", "Database synchronization complete. Ready to connect.");
            }
            else 
            {
                BotLogger.Error("MainForm", "Failed to initialize game data. Please verify your Silkroad folder contains Media.pk2 or extracted Media files.");
            }

            _btnLoadData.Enabled = true;
        }

        private async Task StartGameSequence(bool useProxy)
        {
            if (string.IsNullOrEmpty(_txtSroPath.Text) || !ValidateSroFolder(_txtSroPath.Text)) return;

            ConfigManager.Config.Username = _txtUsername.Text;
            ConfigManager.Config.Password = _txtPassword.Text;
            ConfigManager.Save();

            _btnStartGame.Enabled = false;
            _btnClientless.Enabled = false;

            if (useProxy)
            {
                string input = _txtManualIp.Text;
                string targetIp = input.Contains(':') ? input.Split(':')[0] : input;
                int targetPort = input.Contains(':') ? int.Parse(input.Split(':')[1]) : 15779;

                if (string.IsNullOrEmpty(targetIp)) { _btnStartGame.Enabled = true; _btnClientless.Enabled = true; return; }

                string realServerIp = targetIp.Trim();
                // The realServerIp and targetPort are now already resolved by DataManager
                BotLogger.Info($"[General] Connecting to {realServerIp}:{targetPort}");

                try
                {
                    _proxy.Stop();
                    await Task.Delay(500);
                    
                    _proxy.ResetGateway(realServerIp, targetPort);
                    _proxy.SetDataManager(_dataManager);
                    _proxy.Start();

                    // SBot-style Memory Patching instead of Hosts file
                    string clientPath = Path.Combine(_txtSroPath.Text, "sro_client.exe");
                    int proxyPort = ConfigManager.Config.ProxyPort;
                    _sroProcess = SroLoader.LaunchAndPatch(clientPath, targetIp, targetPort, proxyPort, _chkNoDc.Checked);
                    
                    if (_sroProcess != null)
                    {
                        BotLogger.Info("MainForm", "SRO Client launched with memory patches active.");
                    }
                    else
                    {
                        BotLogger.Error("MainForm", "Failed to launch client via Memory Loader.");
                    }
                }
                catch (Exception ex)
                {
                    BotLogger.Error("MainForm", $"Failed to start proxy or loader: {ex.Message}");
                    _btnStartGame.Enabled = true; _btnClientless.Enabled = true;
                }
            }
            else
            {
                var loginManager = new LoginManager(_worldState, _entityRepo, _dataManager);
                string targetIp = ConfigManager.Config.OriginalServerIp;
                int targetPort = ConfigManager.Config.LastServerPort;
                bool success = await loginManager.LoginAsync(targetIp, targetPort, _txtUsername.Text, _txtPassword.Text);
                
                if (success)
                {
                    _packetSender = new PacketSender(_worldState, () => loginManager.Connection); // Uses internal connection logic

                    if (_botController != null)
                    {
                        _botController.Start();
                    }
                    
                    BotLogger.Info("MainForm", "Bot initialized and started.");
                }
                else
                {
                    _btnStartGame.Enabled = true; _btnClientless.Enabled = true;
                }
            }
        }

        private void RedirectConsole()
        {
            var writer = new ControlWriter(_rtbLogs);
            Console.SetOut(writer);
        }

        private void AppendRadarLog(string text)
        {
            if (_rtbRadarLog == null || _rtbRadarLog.IsDisposed) return;
            
            _rtbRadarLog.AppendText(text);
            if (_rtbRadarLog.Lines.Length > 500)
            {
                _rtbRadarLog.Select(0, _rtbRadarLog.GetFirstCharIndexFromLine(100));
                _rtbRadarLog.SelectedText = "";
            }
            _rtbRadarLog.SelectionStart = _rtbRadarLog.Text.Length;
            _rtbRadarLog.ScrollToCaret();
        }

        private void AppendColoredLog(string text, BotLogger.LogLevel level)
        {
            if (_rtbLogs == null || _rtbLogs.IsDisposed) return;
            
            _rtbLogs.SelectionStart = _rtbLogs.TextLength;
            _rtbLogs.SelectionLength = 0;
            
            switch (level)
            {
                case BotLogger.LogLevel.INFO: _rtbLogs.SelectionColor = ThemeColors.PrimaryAccent; break;
                case BotLogger.LogLevel.DEBUG: _rtbLogs.SelectionColor = ThemeColors.TextMuted; break;
                case BotLogger.LogLevel.WARN: _rtbLogs.SelectionColor = ThemeColors.Warning; break;
                case BotLogger.LogLevel.ERROR: _rtbLogs.SelectionColor = ThemeColors.Error; break;
                default: _rtbLogs.SelectionColor = ThemeColors.TextPrimary; break;
            }
            
            _rtbLogs.AppendText(text);
            _rtbLogs.ScrollToCaret();
        }

        // CreateFlatButton, CreateBotButton, GroupBox_Paint → MainForm_Layout.cs

        private void UpdateStatusBars()
        {
            var ch = _worldState.GetCharacter();
            if (ch.UniqueID == 0) return;

            string name = string.IsNullOrEmpty(ch.Name) ? "---" : ch.Name;
            
            // Update Top Bar
            _lblTopLv.Text = $"Lv: {ch.Level}";
            _lblTopName.Text = $"Name: {name}";
            _lblTopExp.Text = $"EXP: {ch.ExpPercent:F2}%";
            _lblTopGold.Text = $"Gold: {ch.Gold:N0}";
            _lblTopPos.Text = $"Pos: {ch.Position.Region}, {ch.Position.X:F0}, {ch.Position.Y:F0}";

            // Update Dashboard
            _lblDashLv.Text = $"Level: {ch.Level}";
            _lblDashName.Text = $"Name: {name}";
            _lblDashExp.Text = $"Experience: {ch.Experience:N0} ({ch.ExpPercent:F2}%)";
            _lblDashGold.Text = $"Gold: {ch.Gold:N0}";
            _lblDashPos.Text = $"Position: {ch.Position.Region}, {ch.Position.X:F1}, {ch.Position.Y:F1}";
        }

        private void UpdateSessionStats()
        {
            if (_botController == null) return;

            _lblSessionTime.Text = $"Runtime: {_botController.Runtime.ToString(@"hh\:mm\:ss")}";
            _lblSessionXp.Text = $"XP/h: {_botController.XpPerHour:N0}";
            // SP/h and Kills can be expanded if BotController is updated, for now we use KillsCount
            _lblSessionKills.Text = $"Kills: {_botController.KillsCount:N0}";
        }

        private void SafeInvoke(Action action)
        {
            try { if (!this.IsDisposed && this.IsHandleCreated) { if (this.InvokeRequired) this.BeginInvoke(action); else action(); } }
            catch { }
        }

        // ==========================================
        // MANUAL ACTIONS TAB & DEBUGGER
        // ==========================================
        private RichTextBox _rtbManualLogs = null!;

        private void BuildTabManual(TabPage page)
        {
            // Main container to hold everything
            var pnlMain = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.Background };
            page.Controls.Add(pnlMain);

            // 1. BOTTOM LOG PANEL (Fixed height)
            var pnlLogContainer = new Panel { Dock = DockStyle.Bottom, Height = 250, BackColor = Color.Black };
            pnlMain.Controls.Add(pnlLogContainer);

            var pnlLogHeader = new Panel { Dock = DockStyle.Top, Height = 25, BackColor = Color.FromArgb(30, 30, 40) };
            var lblLogTitle = new Label { Text = "TEST CONSOLE (ACTION DEBUGGER)", Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
            pnlLogHeader.Controls.Add(lblLogTitle);
            pnlLogContainer.Controls.Add(pnlLogHeader);

            _rtbManualLogs = new RichTextBox { 
                Dock = DockStyle.Fill, 
                BackColor = Color.Black, 
                ForeColor = Color.LimeGreen, 
                BorderStyle = BorderStyle.None, 
                ReadOnly = true, 
                Font = new Font("Consolas", 9F) 
            };
            pnlLogContainer.Controls.Add(_rtbManualLogs);
            _rtbManualLogs.BringToFront();

            // 2. TOP BUTTONS PANEL (Fill remaining space)
            var pnlButtons = new FlowLayoutPanel { 
                Dock = DockStyle.Fill, 
                Padding = new Padding(15), 
                AutoScroll = true, 
                BackColor = ThemeColors.Background 
            };
            pnlMain.Controls.Add(pnlButtons);
            pnlButtons.BringToFront();

            // SIZES: Compact layout
            Size gbSize = new Size(250, 110);

            // 1. RECOVERY
            var gbPotions = CreateManualGroupBox("RECOVERY DEBUG", gbSize);
            gbPotions.Controls.Add(CreateManualButton("Use HP Pot", Color.FromArgb(160, 30, 30), (s,e) => UsePotion("HP")));
            gbPotions.Controls.Add(CreateManualButton("Use MP Pot", Color.FromArgb(30, 70, 160), (s,e) => UsePotion("MP")));
            gbPotions.Controls.Add(CreateManualButton("Use Vigor", Color.FromArgb(100, 30, 130), (s,e) => UsePotion("Vigor")));
            pnlButtons.Controls.Add(gbPotions);

            // 2. MOVEMENT
            var gbMove = CreateManualGroupBox("MOVEMENT DEBUG", gbSize);
            gbMove.Controls.Add(CreateManualButton("Random Move", ThemeColors.PrimaryAccent, (s,e) => PerformRandomMove()));
            gbMove.Controls.Add(CreateManualButton("Trace Selection", Color.DarkCyan, (s,e) => TraceTarget()));
            gbMove.Controls.Add(CreateManualButton("Return Scroll", Color.FromArgb(60, 100, 60), (s,e) => UsePotion("Return")));
            pnlButtons.Controls.Add(gbMove);

            // 3. COMBAT
            var gbCombat = CreateManualGroupBox("COMBAT DEBUG", gbSize);
            gbCombat.Controls.Add(CreateManualButton("Select Mob", Color.DarkGoldenrod, (s,e) => SelectNearbyMob()));
            gbCombat.Controls.Add(CreateManualButton("Basic Attack", Color.IndianRed, (s,e) => {
                if (_worldState.CurrentTargetID == 0) { ManualLog("ERROR: No target selected.", ThemeColors.Error); return; }
                ManualLog($"ATTACK: Basic Attack (0x7074) -> {_worldState.CurrentTargetID:X}", ThemeColors.Warning);
                _packetSender?.SendBasicAttack(_worldState.CurrentTargetID);
            }));
            gbCombat.Controls.Add(CreateManualButton("Select NPC", Color.DarkSlateGray, (s,e) => {
                ManualLog("TEST: Scanning for nearest NPC...", Color.White);
                var npc = _worldState.NearbyEntities.OfType<SRNpc>().OrderBy(n => n.Position.DistanceTo(_worldState.Character.Position)).FirstOrDefault();
                if (npc != null) {
                    _worldState.CurrentTargetID = npc.UniqueID;
                    _packetSender?.SendSelectTarget(npc.UniqueID);
                    ManualLog($"ACTION: Selected NPC '{npc.Name}' ({npc.UniqueID:X})", Color.Cyan);
                } else ManualLog("FAIL: No NPCs found.", ThemeColors.Error);
            }));
            pnlButtons.Controls.Add(gbCombat);

            // 4. CHARACTER ACTIONS (NEW)
            var gbActions = CreateManualGroupBox("CHARACTER ACTIONS", gbSize);
            gbActions.Controls.Add(CreateManualButton("Sit / Stand", Color.DarkSlateBlue, (s,e) => {
                ManualLog("ACTION: Toggling Sit/Stand state (0x704F type 4)", Color.White);
                _packetSender?.SendAction(4);
            }));
            gbActions.Controls.Add(CreateManualButton("Berserk (Hwan)", Color.DarkRed, (s,e) => {
                ManualLog("ACTION: Sending Berserk Request (0x70A7 type 1)", Color.White);
                using var writer = new SilkroadAIBot.Domain.Network.SRPacketWriter(0x70A7);
                writer.WriteByte(1);
                _packetSender?.SendPacket(writer.Build());
            }));
            gbActions.Controls.Add(CreateManualButton("Walk / Run", Color.FromArgb(100, 100, 40), (s,e) => {
                ManualLog("ACTION: Toggling Walk/Run state (0x704F type 2)", Color.White);
                _packetSender?.SendAction(2);
            }));
            pnlButtons.Controls.Add(gbActions);

            // 5. DATA DEBUGGER (NEW)
            var gbData = CreateManualGroupBox("DATA DEBUGGER", gbSize);
            gbData.Controls.Add(CreateManualButton("Inventory Dump", Color.FromArgb(50, 50, 50), (s,e) => {
                ManualLog("DEBUG: Full Inventory Data Dump:", Color.Yellow);
                foreach(var i in _worldState.Character.Inventory) ManualLog($" Slot {i.Slot}: {i.Name} (ID: {i.ItemID}) x{i.Count}", Color.Gray);
            }));
            gbData.Controls.Add(CreateManualButton("Skill List Dump", Color.FromArgb(50, 50, 50), (s,e) => {
                ManualLog("DEBUG: Known Skills Dump:", Color.Yellow);
                foreach(var sk in _worldState.Character.LearnedSkills) ManualLog($" ID: {sk.SkillID} (Active: {sk.IsEnabled})", Color.Gray);
            }));
            gbData.Controls.Add(CreateManualButton("Entities Radar", Color.FromArgb(50, 50, 50), (s,e) => {
                ManualLog("DEBUG: Radar Entities Scan:", Color.Yellow);
                foreach(var ent in _worldState.NearbyEntities.Take(10)) ManualLog($" [{ent.GetType().Name}] {ent.Name} (UID: {ent.UniqueID:X})", Color.Gray);
            }));
            pnlButtons.Controls.Add(gbData);

            ManualLog("Ready. Press any action button to begin diagnostic testing.", Color.Gray);
        }

        private void ManualLog(string message, Color color)
        {
            SafeInvoke(() => {
                _rtbManualLogs.SelectionStart = _rtbManualLogs.TextLength;
                _rtbManualLogs.SelectionLength = 0;
                _rtbManualLogs.SelectionColor = color;
                _rtbManualLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                _rtbManualLogs.ScrollToCaret();
            });
        }

        private GroupBox CreateManualGroupBox(string text, Size size)
        {
            var gb = new GroupBox { Text = text, Size = size, ForeColor = ThemeColors.GroupTitleText, Margin = new Padding(0, 0, 10, 15) };
            gb.Paint += GroupBox_Paint;
            return gb;
        }

        private Button CreateManualButton(string text, Color color, EventHandler onClick)
        {
            var btn = CreateBotButton(text, color);
            btn.Size = new Size(110, 32);
            btn.Margin = new Padding(8, 25, 4, 4);
            btn.Font = new Font("Segoe UI Semibold", 8F);
            btn.Click += onClick;
            return btn;
        }

        private void UsePotion(string type)
        {
            ManualLog($"TEST: Requesting use of {type} item...", Color.White);
            if (_packetSender == null) { ManualLog("FAIL: PacketSender is not initialized.", ThemeColors.Error); return; }
            
            var item = _worldState.Character.Inventory
                .FirstOrDefault(i => i.Name.Contains(type, StringComparison.OrdinalIgnoreCase) || 
                                     i.ItemID.ToString().Contains(type));
            
            if (item != null)
            {
                ManualLog($"DATA: Item found: {item.Name} at Slot {item.Slot} (Count: {item.Count})", Color.Cyan);
                ManualLog($"NET: Sending 0x704C to Agent Server...", Color.Yellow);
                _packetSender.SendUseItem(item.Slot);
                ManualLog("SUCCESS: Item use packet dispatched.", Color.Lime);
            }
            else
            {
                ManualLog($"FAIL: No item matching '{type}' found in Inventory.", ThemeColors.Error);
                ManualLog("DEBUG: Dumping inventory names for search:", Color.Gray);
                foreach(var i in _worldState.Character.Inventory.Take(5)) ManualLog($" - {i.Name}", Color.Gray);
            }
        }

        private void PerformRandomMove()
        {
            var cur = _worldState.Character.Position;
            ManualLog($"TEST: Random movement from current: {cur.Region} ({cur.X:F1}, {cur.Y:F1})", Color.White);
            
            var rand = new Random();
            float dx = rand.Next(-40, 40);
            float dy = rand.Next(-40, 40);
            var dest = new SRCoord(cur.Region, cur.X + dx, cur.Y + dy, cur.Z);
            
            ManualLog($"DATA: Target destination: {dest.Region} ({dest.X:F1}, {dest.Y:F1})", Color.Cyan);
            ManualLog($"NET: Sending 0x7021 (Walk) to Server...", Color.Yellow);
            _packetSender?.SendMovement(dest);
            ManualLog("SUCCESS: Move packet dispatched.", Color.Lime);
        }

        private void SelectNearbyMob()
        {
            ManualLog("TEST: Scanning for nearest mob...", Color.White);
            var mobs = _worldState.NearbyEntities.OfType<SRMob>().Where(m => m.LifeState == LifeState.Alive).ToList();
            ManualLog($"DATA: Found {mobs.Count} mobs in radar range.", Color.Cyan);

            var mob = mobs.OrderBy(m => m.Position.DistanceTo(_worldState.Character.Position)).FirstOrDefault();

            if (mob != null)
            {
                ManualLog($"ACTION: Selecting '{mob.Name}' (UID: {mob.UniqueID:X})", Color.Cyan);
                ManualLog($"NET: Sending 0x7045 (Select) to Server...", Color.Yellow);
                _worldState.CurrentTargetID = mob.UniqueID;
                _packetSender?.SendSelectTarget(mob.UniqueID);
                ManualLog("SUCCESS: Selection packet dispatched.", Color.Lime);
            }
            else
            {
                ManualLog("FAIL: No alive mobs found in WorldState.", ThemeColors.Error);
            }
        }

        private void TraceTarget()
        {
             var target = _worldState.GetEntity(_worldState.CurrentTargetID);
             if (target != null)
             {
                 ManualLog($"TEST: Tracing target '{target.Name}' at {target.Position.X:F1}, {target.Position.Y:F1}", Color.White);
                 ManualLog($"NET: Sending 0x7021 (Move) to target coordinates...", Color.Yellow);
                 _packetSender?.SendMovement(target.Position);
                 ManualLog("SUCCESS: Trace dispatched.", Color.Lime);
             }
             else
             {
                 ManualLog("FAIL: No target currently selected to trace.", ThemeColors.Error);
             }
        }

    }

    public class ControlWriter : TextWriter
    {
        private RichTextBox _log;
        public ControlWriter(RichTextBox log) { _log = log; }
        public override void Write(string? value)
        {
            if (value == null) return;
            if (_log.InvokeRequired) _log.BeginInvoke(new Action<string?>(Write), value);
            else 
            {
                _log.SelectionStart = _log.TextLength;
                _log.SelectionLength = 0;
                _log.SelectionColor = ThemeColors.TextPrimary;
                _log.AppendText(value);
                _log.ScrollToCaret();
            }
        }
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}

