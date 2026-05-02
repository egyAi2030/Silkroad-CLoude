using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.UI
{
    public partial class MainForm : Form
    {
        // ── LAYOUT ─────────────────────────────────────────────────────────
        private void InitializeLayout()
        {
            this.Size = new Size(1280, 800);
            this.MinimumSize = new Size(1100, 650);
            this.Text = $"Silkroad AI Bot — Antigravity {_botVersion}";
            this.BackColor = ThemeColors.Background;
            this.Font = new Font("Segoe UI", 9F);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;

            // SIDEBAR
            _sidebar = new Panel { Dock = DockStyle.Left, Width = 195, BackColor = ThemeColors.SidebarBg };
            _sidebar.Paint += (s, e) => {
                using var p = new Pen(Color.FromArgb(55, ThemeColors.PrimaryAccent), 1);
                e.Graphics.DrawLine(p, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
            };
            this.Controls.Add(_sidebar);

            // MAIN CONTAINER
            var main = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.Background };
            this.Controls.Add(main);
            main.BringToFront();

            // LOGO
            var pnlLogo = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = ThemeColors.SidebarBg };
            pnlLogo.Paint += PaintSidebarLogo;
            _sidebar.Controls.Add(pnlLogo);

            BuildPersistentStatusPanel(main);

            _rtbLogs = new RichTextBox {
                BackColor = ThemeColors.Background, ForeColor = ThemeColors.TextPrimary,
                BorderStyle = BorderStyle.None, ReadOnly = true, Font = new Font("Consolas", 9F)
            };

            _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = ThemeColors.PanelContentBackground, Padding = new Padding(8) };
            main.Controls.Add(_contentPanel);

            _views.Clear(); _navButtons.Clear();

            // Primary views
            _viewDashboard = SetupStatisticsView();
            AddSidebarNav("Status", _viewDashboard, "◈");

            var pnlLogin = CreateViewPanel("Login");
            SetupView1_SetupAndLogin(pnlLogin);
            AddSidebarNav("Login", pnlLogin, "⬡");

            _viewModules = CreateViewPanel("Configuration");
            SetupView_ModulesConfig(_viewModules);
            AddSidebarNav("Config", _viewModules, "⚙");

            AddNavSeparator("MODULES");

            AddSidebarNav("Skills",    _viewModules, "⚡",  0);
            AddSidebarNav("Party",     _viewModules, "⚔",  8);
            AddSidebarNav("Inventory", _viewModules, "⬡",  4);
            AddSidebarNav("Alchemy",   _viewModules, "◇", 11);
            AddSidebarNav("Quest",     _viewModules, "◈",  5);
            AddSidebarNav("Market",    _viewModules, "⬢", 12);
            AddSidebarNav("Manual",    _viewModules, "⬣", 15);
            AddSidebarNav("Logs",      _viewModules, "≡",  16);

            AddNavSeparator("TOOLS");

            var pnlSniffer = CreateViewPanel("Sniffer");
            SetupSniffer(pnlSniffer);
            _viewSniffer = pnlSniffer;
            AddSidebarNav("Sniffer", _viewSniffer, "◎");

            _viewRadar = CreateViewPanel("Entity Radar");
            SetupRadarView(_viewRadar);
            AddSidebarNav("Radar", _viewRadar, "◉");

            var pnlMap = CreateViewPanel("World Map");
            SetupMap(pnlMap);
            _viewMap = pnlMap;
            AddSidebarNav("Map", _viewMap, "⬟");

            _viewSettings = CreateViewPanel("Settings");
            AddSidebarNav("Global", _viewSettings, "◫");

            ShowView(_viewDashboard);
        }

        private void AddNavSeparator(string label)
        {
            var lbl = new Label {
                Text = " " + label, Dock = DockStyle.Top, Height = 24,
                ForeColor = ThemeColors.TextMuted, Font = new Font("Segoe UI", 6.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0),
                BackColor = Color.Transparent
            };
            var line = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(28, ThemeColors.PrimaryAccent) };
            _sidebar.Controls.Add(lbl); lbl.SendToBack();
            _sidebar.Controls.Add(line); line.SendToBack();
        }

        // ── SIDEBAR NAV ────────────────────────────────────────────────────
        private void AddSidebarNav(string title, Panel view, string icon = "•", int tabIndex = -1)
        {
            var btn = CreateSidebarButton($"  {icon}  {title.ToUpper()}");
            btn.Tag = new NavigationTarget { View = view, TabIndex = tabIndex };
            btn.Click += (s, e) => { var t = (NavigationTarget)btn.Tag; ShowView(t.View, t.TabIndex); };
            _sidebar.Controls.Add(btn);
            btn.SendToBack();
            _navButtons[title] = btn;
        }

        private Button CreateSidebarButton(string text)
        {
            var btn = new Button {
                Text = text, Dock = DockStyle.Top, Height = 40,
                FlatStyle = FlatStyle.Flat,
                ForeColor = ThemeColors.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(18, ThemeColors.PrimaryAccent);
            // Gold left accent bar when active
            btn.Paint += (s, e) => {
                if (btn.BackColor == ThemeColors.ActiveNavBg)
                {
                    using var br = new SolidBrush(ThemeColors.PrimaryAccent);
                    e.Graphics.FillRectangle(br, 0, 8, 3, btn.Height - 16);
                }
            };
            return btn;
        }

        private void ShowView(Panel view, int tabIndex = -1)
        {
            foreach (var v in _views.Values) v.Visible = false;
            view.Visible = true;
            view.BringToFront();

            if (tabIndex >= 0 && view == _viewModules && _tcModules != null)
                _tcModules.SelectedIndex = tabIndex;

            foreach (var btn in _navButtons.Values)
            {
                if (btn.Tag is NavigationTarget target &&
                    target.View == view &&
                    (tabIndex == -1 || target.TabIndex == tabIndex))
                {
                    btn.BackColor = ThemeColors.ActiveNavBg;
                    btn.ForeColor = ThemeColors.PrimaryAccent;
                }
                else
                {
                    btn.BackColor = Color.Transparent;
                    btn.ForeColor = ThemeColors.TextMuted;
                }
                btn.Invalidate();
            }
        }

        // ── STATUS BAR ─────────────────────────────────────────────────────
        private void BuildPersistentStatusPanel(Panel parent)
        {
            _pnlTopStatus = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = ThemeColors.HeaderBg };
            _pnlTopStatus.Paint += (s, e) => {
                var c = (Control)s!;
                // Gold bottom border
                using var pen = new Pen(Color.FromArgb(70, ThemeColors.PrimaryAccent), 1);
                e.Graphics.DrawLine(pen, 0, c.Height - 1, c.Width, c.Height - 1);
                // Subtle red glow on far left
                using var glow = new LinearGradientBrush(
                    new Rectangle(0, 0, 80, c.Height),
                    Color.FromArgb(18, ThemeColors.SecondaryAccent), Color.Transparent, 0f);
                e.Graphics.FillRectangle(glow, 0, 0, 80, c.Height);
            };
            parent.Controls.Add(_pnlTopStatus);

            var tbl = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1,
                BackColor = Color.Transparent
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 225));
            _pnlTopStatus.Controls.Add(tbl);

            // Col 0: Name + Level
            var c0 = MakeHeaderCell("CHARACTER");
            _lblTopName = AddHeaderValue(c0, "---", ThemeColors.GoldLight, 11F, FontStyle.Bold);
            _lblTopLv   = AddHeaderValue(c0, "Level 0", ThemeColors.TextMuted, 7.5F, FontStyle.Regular);
            tbl.Controls.Add(c0, 0, 0);

            // Col 1: EXP
            var c1 = MakeHeaderCell("EXPERIENCE");
            _lblTopExp = AddHeaderValue(c1, "0.00%", ThemeColors.ConnectedStatus, 11F, FontStyle.Bold);
            tbl.Controls.Add(c1, 1, 0);

            // Col 2: Gold
            var c2 = MakeHeaderCell("GOLD");
            _lblTopGold = AddHeaderValue(c2, "0", ThemeColors.PrimaryAccent, 11F, FontStyle.Bold);
            tbl.Controls.Add(c2, 2, 0);

            // Col 3: Position
            var c3 = MakeHeaderCell("POSITION");
            _lblTopPos = AddHeaderValue(c3, "—", ThemeColors.TextPrimary, 8.5F, FontStyle.Regular);
            tbl.Controls.Add(c3, 3, 0);

            // Col 4: Sync
            var c4 = MakeHeaderCell("SYNC STATUS");
            _lblLoadProgress = AddHeaderValue(c4, "Idle", ThemeColors.TextMuted, 8.5F, FontStyle.Italic);
            tbl.Controls.Add(c4, 4, 0);

            // Col 5: Buttons
            var pnlBtns = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 14, 10, 14), BackColor = Color.Transparent };
            _btnStartBot = CreateRpgButton("▶  START", ThemeColors.ConnectedStatus);
            _btnStopBot  = CreateRpgButton("■  STOP",  ThemeColors.SecondaryAccent);
            _btnStartBot.Dock = DockStyle.Left;  _btnStartBot.Width = 100;
            _btnStopBot.Dock  = DockStyle.Right; _btnStopBot.Width  = 88;
            _btnStartBot.Click += (s, e) => StartBot();
            _btnStopBot.Click  += (s, e) => StopBot();
            pnlBtns.Controls.Add(_btnStopBot);
            pnlBtns.Controls.Add(_btnStartBot);
            tbl.Controls.Add(pnlBtns, 5, 0);
        }

        private Panel MakeHeaderCell(string caption)
        {
            var cell = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(12, 10, 6, 6) };
            cell.Paint += (s, e) => {
                var c = (Control)s!;
                using var p = new Pen(Color.FromArgb(22, ThemeColors.PrimaryAccent), 1);
                e.Graphics.DrawLine(p, c.Width - 1, 8, c.Width - 1, c.Height - 8);
            };
            var cap = new Label {
                Text = caption, Dock = DockStyle.Top, Height = 13,
                ForeColor = ThemeColors.TextMuted, Font = new Font("Segoe UI", 6.5F, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            cell.Controls.Add(cap);
            return cell;
        }

        private Label AddHeaderValue(Panel cell, string text, Color color, float size, FontStyle style)
        {
            var lbl = new Label {
                Text = text, Dock = DockStyle.Top, Height = 22,
                ForeColor = color, Font = new Font("Segoe UI Semibold", size, style),
                BackColor = Color.Transparent
            };
            cell.Controls.Add(lbl);
            return lbl;
        }

        private Button CreateRpgButton(string text, Color accent)
        {
            var btn = new Button {
                Text = text, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, accent),
                BackColor = Color.FromArgb(35, accent),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(110, accent);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, accent);
            return btn;
        }

        // ── PAINT HELPERS ──────────────────────────────────────────────────
        private void PaintSidebarLogo(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rc = ((Control)sender!).ClientRectangle;

            // Subtle red glow behind
            using var glow = new LinearGradientBrush(rc,
                Color.FromArgb(25, ThemeColors.SecondaryAccent), Color.Transparent, 90f);
            g.FillRectangle(glow, rc);

            // Gold bottom line
            using var sep = new Pen(Color.FromArgb(65, ThemeColors.PrimaryAccent), 1);
            g.DrawLine(sep, 12, rc.Bottom - 1, rc.Width - 12, rc.Bottom - 1);

            using var fTitle = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold);
            using var fSub   = new Font("Segoe UI", 7F, FontStyle.Bold);
            using var brGold = new SolidBrush(ThemeColors.PrimaryAccent);
            using var brDim  = new SolidBrush(ThemeColors.TextMuted);

            const string line1 = "⚔  SILKROAD  AI";
            const string line2 = "ANTIGRAVITY ENGINE";
            var sz1 = g.MeasureString(line1, fTitle);
            var sz2 = g.MeasureString(line2, fSub);
            g.DrawString(line1, fTitle, brGold, (rc.Width - sz1.Width) / 2f, 16);
            g.DrawString(line2, fSub,   brDim,  (rc.Width - sz2.Width) / 2f, 44);

            // Small ornament line
            using var orn = new Pen(Color.FromArgb(40, ThemeColors.PrimaryAccent), 1);
            g.DrawLine(orn, 30, 63, rc.Width - 30, 63);
        }

        // ── BUTTON & GROUPBOX FACTORIES ────────────────────────────────────
        private Button CreateFlatButton(string text, Point pos, Size size, Color accent)
        {
            var btn = new Button {
                Text = text, Location = pos, Size = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, accent),
                ForeColor = Color.FromArgb(215, accent),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(90, accent);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, accent);
            return btn;
        }

        private Button CreateBotButton(string text, Color backColor)
        {
            var btn = new Button {
                Text = text, Size = new Size(110, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, backColor),
                ForeColor = Color.FromArgb(210, backColor),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(4, 0, 4, 0)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(95, backColor);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, backColor);
            return btn;
        }

        private void GroupBox_Paint(object? sender, PaintEventArgs e)
        {
            var box = (GroupBox)sender!;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(ThemeColors.PanelContentBackground);

            using var pen = new Pen(Color.FromArgb(55, ThemeColors.PrimaryAccent), 1);
            g.DrawRectangle(pen, new Rectangle(0, 10, box.Width - 1, box.Height - 11));

            // Corner ornaments
            using var orn = new Pen(ThemeColors.GoldDark, 1);
            const int cs = 5;
            g.DrawLine(orn, 0, 10, cs, 10);
            g.DrawLine(orn, box.Width - cs - 1, 10, box.Width - 1, 10);
            g.DrawLine(orn, 0, box.Height - 1, cs, box.Height - 1);
            g.DrawLine(orn, box.Width - cs - 1, box.Height - 1, box.Width - 1, box.Height - 1);

            // Title
            var tsz = g.MeasureString(box.Text, box.Font);
            using var tbg = new SolidBrush(ThemeColors.PanelContentBackground);
            g.FillRectangle(tbg, 8, 2, tsz.Width + 8, 16);
            using var tfg = new SolidBrush(ThemeColors.PrimaryAccent);
            g.DrawString(box.Text, box.Font, tfg, 10, 2);
        }

        private Image CreateSidebarIcon(string symbol, int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (var gr = Graphics.FromImage(bmp))
            {
                gr.Clear(Color.Transparent);
                gr.DrawString(symbol, new Font("Segoe UI Emoji", size - 8), Brushes.White, 0, 0);
            }
            return bmp;
        }
    }
}
