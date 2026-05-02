using System;
using System.Drawing;
using System.Windows.Forms;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Data;

namespace SilkroadAIBot.UI.Controls
{
    public class SkillSlotControl : UserControl
    {
        public SRSkill? Skill { get; private set; }
        public bool IsSelected { get; set; }
        public bool IsInSequence { get; set; }

        private static readonly Color ColorAttack = Color.FromArgb(40, 60, 100); // Blueish
        private static readonly Color ColorBuff = Color.FromArgb(100, 80, 40);   // Amber
        private static readonly Color ColorPassive = Color.FromArgb(40, 80, 40); // Greenish

        public SkillSlotControl()
        {
            this.Size = new Size(64, 80);
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Hand;
        }

        public void SetSkill(SRSkill? skill)
        {
            Skill = skill;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Background
            using (var brush = new SolidBrush(Color.FromArgb(30, 30, 35)))
            {
                g.FillRectangle(brush, new Rectangle(0, 0, Width, Height));
            }

            Rectangle iconRect = new Rectangle(8, 8, 48, 48);

            if (Skill == null)
            {
                // Dash placeholder
                using (var pen = new Pen(Color.FromArgb(60, 60, 70), 2))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawRectangle(pen, iconRect);
                }
                return;
            }

            // Draw Icon
            var icon = DataManager.Instance.GetSkillIcon(Skill.IconPath);
            if (icon != null)
            {
                g.DrawImage(icon, iconRect);
            }
            else
            {
                // Fallback colored square
                Color baseColor = Skill.SkillType switch
                {
                    "Buff" => Color.FromArgb(180, 140, 40),
                    "Passive" => Color.FromArgb(60, 140, 60),
                    _ => Color.FromArgb(40, 100, 180) // Attack
                };
                using (var brush = new SolidBrush(baseColor))
                {
                    g.FillRectangle(brush, iconRect);
                }
            }

            // Border
            Color borderColor = Skill.SkillType switch
            {
                "Buff" => Color.Orange,
                "Passive" => Color.LightGreen,
                _ => Color.SkyBlue
            };

            if (IsSelected) borderColor = Color.White;
            else if (IsInSequence) borderColor = Color.Cyan;

            using (var pen = new Pen(borderColor, IsSelected || IsInSequence ? 3 : 2))
            {
                g.DrawRectangle(pen, iconRect);
            }

            // Name
            string displayName = Skill.Name.Length > 8 ? Skill.Name.Substring(0, 7) + ".." : Skill.Name;
            TextRenderer.DrawText(g, displayName, new Font("Segoe UI", 7), new Point(0, 58), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            
            // Level
            TextRenderer.DrawText(g, $"Lv.{Skill.Level}", new Font("Segoe UI", 7, FontStyle.Bold), new Point(0, 70), Color.FromArgb(200, 200, 200), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

