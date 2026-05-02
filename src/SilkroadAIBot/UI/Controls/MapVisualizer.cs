using System;
using System.Drawing;
using System.Windows.Forms;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.UI.Controls
{
    public class MapVisualizer : UserControl
    {
        public SRCoord BotPos { get; set; }
        public SRCoord? TrainingCenter { get; set; }
        public int TrainingRadius { get; set; }

        public MapVisualizer()
        {
            this.DoubleBuffered = true;
            this.BackColor = ThemeColors.PanelSidebarBackground;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int centerX = Width / 2;
            int centerY = Height / 2;
            float scale = 2.0f; // Zoom scale

            // Draw Grid
            using (var pen = new Pen(Color.FromArgb(50, 50, 50)))
            {
                for (int x = 0; x < Width; x += 50) g.DrawLine(pen, x, 0, x, Height);
                for (int y = 0; y < Height; y += 50) g.DrawLine(pen, 0, y, Width, y);
            }

            // Draw Training Circle
            if (TrainingCenter != null)
            {
                float relX = (TrainingCenter.WorldX - BotPos.WorldX) * scale;
                float relY = (TrainingCenter.WorldY - BotPos.WorldY) * scale;
                
                float drawR = TrainingRadius * scale;
                using (var pen = new Pen(ThemeColors.PrimaryAccent, 2))
                {
                    g.DrawEllipse(pen, centerX + relX - drawR, centerY - relY - drawR, drawR * 2, drawR * 2);
                }
            }

            // Draw Bot (Center)
            using (var brush = new SolidBrush(ThemeColors.ConnectedStatus))
            {
                g.FillEllipse(brush, centerX - 5, centerY - 5, 10, 10);
            }
            
            g.DrawString("BOT", Font, Brushes.White, centerX + 8, centerY - 8);
        }
    }
}


