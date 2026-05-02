using System;
using System.Drawing;
using System.Windows.Forms;
using SilkroadAIBot.Domain.Network;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SilkroadAIBot.UI.Controls
{
    public class PacketSnifferControl : UserControl
    {
        private ListView _packetList;
        private RichTextBox _hexView;
        private CheckBox _chkAutoScroll;
        private TextBox _txtFilter;
        private Button _btnClear;
        
        private List<SRPacket> _history = new List<SRPacket>();
        private object _lock = new object();

        public PacketSnifferControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            _packetList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                Dock = DockStyle.Top,
                Height = 300,
                Font = new Font("Consolas", 9),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.Gainsboro
            };

            _packetList.Columns.Add("Dir", 40);
            _packetList.Columns.Add("Opcode", 70);
            _packetList.Columns.Add("Name", 180);
            _packetList.Columns.Add("Len", 50);
            _packetList.Columns.Add("Time", 80);

            _hexView = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 10),
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.LimeGreen,
                BorderStyle = BorderStyle.None
            };

            Panel toolPanel = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5) };
            _chkAutoScroll = new CheckBox { Text = "Auto Scroll", Checked = true, AutoSize = true, Location = new Point(10, 10), ForeColor = Color.White };
            _btnClear = new Button { Text = "Clear", Location = new Point(120, 7), Width = 60, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White };
            _btnClear.Click += (s, e) => { _packetList.Items.Clear(); _history.Clear(); _hexView.Clear(); };

            _txtFilter = new TextBox { Location = new Point(200, 8), Width = 100, PlaceholderText = "Filter (0x...)" };

            toolPanel.Controls.Add(_chkAutoScroll);
            toolPanel.Controls.Add(_btnClear);
            toolPanel.Controls.Add(_txtFilter);

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 300
            };

            split.Panel1.Controls.Add(_packetList);
            split.Panel2.Controls.Add(_hexView);

            this.Controls.Add(split);
            this.Controls.Add(toolPanel);

            _packetList.SelectedIndexChanged += _packetList_SelectedIndexChanged;
        }

        private void _packetList_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_packetList.SelectedItems.Count == 0) return;
            var item = _packetList.SelectedItems[0];
            if (item.Tag is byte[] data)
            {
                _hexView.Text = FormatHex(data);
            }
        }

        public void AddPacket(SRPacket packet, string direction)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AddPacket(packet, direction)));
                return;
            }

            string opcodeHex = $"0x{packet.Opcode:X4}";
            
            // Basic Filter Check
            if (!string.IsNullOrEmpty(_txtFilter.Text) && !opcodeHex.Contains(_txtFilter.Text, StringComparison.OrdinalIgnoreCase))
                return;

            var lvi = new ListViewItem(direction);
            lvi.SubItems.Add(opcodeHex);
            lvi.SubItems.Add(GetOpcodeName(packet.Opcode));
            lvi.SubItems.Add(packet.Data.Length.ToString());
            lvi.SubItems.Add(DateTime.Now.ToString("HH:mm:ss.fff"));
            
            lvi.Tag = packet.Data;
            
            if (direction == "[C>S]") lvi.ForeColor = Color.LightSkyBlue;
            else lvi.ForeColor = Color.LightGreen;

            _packetList.Items.Add(lvi);

            if (_chkAutoScroll.Checked)
            {
                lvi.EnsureVisible();
            }

            // Cap items
            if (_packetList.Items.Count > 500) _packetList.Items.RemoveAt(0);
        }

        private string GetOpcodeName(ushort opcode)
        {
            // Simple mapping for now, can be expanded via Opcodes.cs reflection
            foreach (var field in typeof(SilkroadAIBot.Networking.Opcode).GetFields())
            {
                if (field.IsLiteral && (ushort)field.GetValue(null)! == opcode)
                    return field.Name;
            }
            return "UNKNOWN";
        }

        private string FormatHex(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            
            StringBuilder sb = new StringBuilder();
            StringBuilder ascii = new StringBuilder();

            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0 && i % 16 == 0)
                {
                    sb.Append("  " + ascii.ToString() + Environment.NewLine);
                    ascii.Clear();
                }
                
                sb.Append(data[i].ToString("X2") + " ");
                
                char ch = (char)data[i];
                if (char.IsControl(ch)) ascii.Append(".");
                else ascii.Append(ch);
            }

            // Pad last line
            int remaining = 16 - (data.Length % 16);
            if (remaining < 16)
            {
                for (int i = 0; i < remaining; i++) sb.Append("   ");
                sb.Append("  " + ascii.ToString());
            }
            else if (data.Length > 0 && data.Length % 16 == 0)
            {
                // Last full line was already appended but wait... 
                // Ah, the loop finished. 
            }

            return sb.ToString();
        }
    }
}
