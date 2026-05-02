using System;
using System.Drawing;
using System.Windows.Forms;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Data;
using SilkroadAIBot.Domain.Entities;
using SRCoord = SilkroadAIBot.Domain.Entities.SRCoord;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Domain.Events;

namespace SilkroadAIBot.UI.Controls
{
    public class MapControl : UserControl
    {
        private Application.Interfaces.IWorldStateRepository _world;
        private Application.Interfaces.IEntityRepository _entityRepo;
        private DataManager _data;
        private float _zoom = 1.0f;
        private PointF _centerOffset = new PointF(0, 0);
        private Point _lastMousePos;
        private bool _isDragging;

        public MapControl(Application.Interfaces.IWorldStateRepository world, Application.Interfaces.IEntityRepository entityRepo, DataManager data)
        {
            _world = world;
            _entityRepo = entityRepo;
            _data = data;
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(15, 15, 20);
            
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _isDragging = true; _lastMousePos = e.Location; } };
            this.MouseUp += (s, e) => { _isDragging = false; };
            this.MouseMove += (s, e) => {
                if (_isDragging) {
                    _centerOffset.X += (e.X - _lastMousePos.X);
                    _centerOffset.Y += (e.Y - _lastMousePos.Y);
                    _lastMousePos = e.Location;
                    this.Invalidate();
                }
            };
            this.MouseWheel += (s, e) => {
                float oldZoom = _zoom;
                if (e.Delta > 0) _zoom *= 1.2f;
                else _zoom /= 1.2f;
                _zoom = Math.Max(0.05f, Math.Min(5.0f, _zoom));
                this.Invalidate();
            };
            
            this.MouseDoubleClick += (s, e) => {
                if (_world == null || _world.Character == null) return;
                
                var worldPos = ScreenToWorld(e.Location);
                BotLogger.Info("MapControl", $"Navigating to {worldPos}");
                
                // Calculate Path
                var start = new System.Numerics.Vector3(_world.Character.Position.X, _world.Character.Position.Y, _world.Character.Position.Z);
                var goal = new System.Numerics.Vector3(worldPos.X, worldPos.Y, worldPos.Z);
                
                // Ensure terrain is loaded in Pathfinder
                var terrain = _data.Navmesh?.GetTerrain(worldPos.Region);
                if (terrain != null)
                {
                    _data.Pathfinder.AddTerrain(worldPos.Region, terrain);
                }

                var path = _data.Pathfinder.FindPath(start, goal, worldPos.Region);
                
                // Convert Vector3 path back to SRCoord list
                var srPath = path.Select(p => new SRCoord(worldPos.Region, p.X, p.Y, p.Z)).ToList();
                _entityRepo.SetManualPath(srPath);
            };

            // v4.1.2 — Force refresh on world updates via generic events
            _world.Subscribe<EntitySpawnedEvent>(ev => SafeRefresh());
            _world.Subscribe<EntityDespawnedEvent>(ev => SafeRefresh());
            _world.Subscribe<CharacterPositionChangedEvent>(ev => SafeRefresh());
            _world.Subscribe<CharacterHpChangedEvent>(ev => SafeRefresh());
        }

        private void SafeRefresh()
        {
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                try { this.Invoke(new Action(Invalidate)); } catch { }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            if (_world == null || _world.GetCharacter().UniqueID == 0) {
                g.DrawString("WAITING FOR WORLD DATA...", this.Font, Brushes.Gray, this.Width/2 - 80, this.Height/2);
                return;
            }

            // 1. Draw Grid Lines (Reference)
            DrawGrid(g);

            // 2. Draw Map Tiles
            DrawMinimap(g);

            // 2.5 Draw Navmesh (Walkable areas)
            DrawNavmesh(g);

            // 3. Draw Entities (Mobs/Items/Players)
            var entities = _world.NearbyEntities.ToList();
            foreach (var entity in entities)
            {
                var point = WorldToScreen(entity.Position);
                if (point.X < -50 || point.X > this.Width + 50 || point.Y < -50 || point.Y > this.Height + 50) continue;

                Color c = Color.White;
                int size = 6;
                if (entity is SRMob) { c = Color.FromArgb(255, 80, 80); size = 5; }
                else if (entity is SRPlayer) { c = Color.FromArgb(80, 180, 255); size = 7; }
                else if (entity is SRGroundItem) { c = Color.Gold; size = 4; }
                else if (entity is SRNpc) { c = Color.LightGreen; size = 8; }

                using (var brush = new SolidBrush(c))
                {
                    g.FillEllipse(brush, point.X - size/2, point.Y - size/2, size, size);
                }
            }

            // 4. Draw Player (Triangle pointing to Angle)
            var character = _world.GetCharacter();
            var playerPoint = WorldToScreen(character.Position);
            DrawPlayer(g, playerPoint, character.Angle);

            // 5. Overlay Info
            DrawOverlay(g);
        }

        private void DrawGrid(Graphics g)
        {
            using (var pen = new Pen(Color.FromArgb(40, 40, 50), 1))
            {
                float step = 192 * _zoom; // 1/10th of a region
                if (step < 10) return;

                for (float x = _centerOffset.X % step; x < this.Width; x += step)
                    g.DrawLine(pen, x, 0, x, this.Height);
                for (float y = _centerOffset.Y % step; y < this.Height; y += step)
                    g.DrawLine(pen, 0, y, this.Width, y);
            }
        }

        private void DrawPlayer(Graphics g, PointF p, float angle)
        {
            float size = 10;
            // Convert SRO angle to radians (0 is East/North depending on version, usually 0 is +X)
            double rad = (angle / 65535.0) * 2 * Math.PI;
            
            PointF[] pts = new PointF[] {
                new PointF(p.X + (float)Math.Cos(rad) * size, p.Y - (float)Math.Sin(rad) * size),
                new PointF(p.X + (float)Math.Cos(rad + 2.5) * size*0.7f, p.Y - (float)Math.Sin(rad + 2.5) * size*0.7f),
                new PointF(p.X + (float)Math.Cos(rad - 2.5) * size*0.7f, p.Y - (float)Math.Sin(rad - 2.5) * size*0.7f)
            };
            g.FillPolygon(Brushes.LimeGreen, pts);
            g.DrawPolygon(Pens.Black, pts);
        }

        private void DrawOverlay(Graphics g)
        {
            var charPos = _world.Character.Position;
            string text = $"POS: {charPos.X:F1}, {charPos.Y:F1} | REGION: {charPos.Region} | ZOOM: {_zoom:F2}";
            
            g.FillRectangle(new SolidBrush(Color.FromArgb(150, 0, 0, 0)), 5, this.Height - 30, 400, 25);
            g.DrawString(text, new Font("Consolas", 9, FontStyle.Bold), Brushes.White, 10, this.Height - 25);
        }

        private void DrawMinimap(Graphics g)
        {
            var character = _world.GetCharacter();
            if (character.UniqueID == 0) return;
            ushort region = character.Position.Region;
            
            // Try to load current region tile
            string iconPath = $"minimap\\{region}.ddj";
            var mapBmp = _data.GetSkillIcon(iconPath);

            if (mapBmp != null)
            {
                // In SRO, (0,0) of a region is the center of the 1920x1920 block
                // Screen mapping needs to account for this
                var screenPos = WorldToScreen(new SRCoord(region, 0, 0, 0));
                float size = 1920 * _zoom;
                g.DrawImage(mapBmp, screenPos.X - size/2, screenPos.Y - size/2, size, size);
            }
        }

        private void DrawNavmesh(Graphics g)
        {
            var character = _world.GetCharacter();
            if (character.UniqueID == 0 || _zoom < 0.5f) return;
            ushort region = character.Position.Region;
            
            var terrain = _data.Navmesh?.GetTerrain(region);
            if (terrain == null || terrain.Tiles == null) return;

            // Only draw within visible screen area to save performance
            using (var walkableBrush = new SolidBrush(Color.FromArgb(50, 0, 255, 0))) // Transparent Green
            {
                // Each tile is 1.92 x 1.92 units (approx)
                // 96x96 tiles = 192x192 units total in local space? No, 1920x1920 is a region.
                // Each region is 1920x1920 units.
                // 96x96 tiles means each tile is 20x20 units.
                float tileSize = 20.0f;

                for (int y = 0; y < 96; y++)
                {
                    for (int x = 0; x < 96; x++)
                    {
                        var tile = terrain.Tiles[y * 96 + x];
                        if (tile.Flag == 1) // Walkable
                        {
                            // Local coords (0 to 1920)
                            float localX = x * tileSize;
                            float localY = y * tileSize;

                            var screenPoint = WorldToScreen(new SRCoord(region, localX, localY, 0));
                            float drawSize = tileSize * 0.5f * _zoom;
                            
                            if (screenPoint.X > 0 && screenPoint.X < this.Width && screenPoint.Y > 0 && screenPoint.Y < this.Height)
                            {
                                g.FillRectangle(walkableBrush, screenPoint.X, screenPoint.Y, drawSize, drawSize);
                            }
                        }
                    }
                }
            }
        }

        private PointF WorldToScreen(SRCoord coord)
        {
            float worldX = coord.WorldX;
            float worldY = coord.WorldY;
            var character = _world.GetCharacter();
            float charWorldX = character.Position.WorldX;
            float charWorldY = character.Position.WorldY;

            float dx = (worldX - charWorldX) * 0.5f; 
            float dy = (charWorldY - worldY) * 0.5f; 

            float screenX = (dx * _zoom) + (this.Width / 2) + _centerOffset.X;
            float screenY = (dy * _zoom) + (this.Height / 2) + _centerOffset.Y;

            return new PointF(screenX, screenY);
        }

        private SRCoord ScreenToWorld(Point screenPoint)
        {
            float dx = (screenPoint.X - (this.Width / 2) - _centerOffset.X) / _zoom;
            float dy = (screenPoint.Y - (this.Height / 2) - _centerOffset.Y) / _zoom;

            float worldX = dx / 0.5f + _world.Character.Position.WorldX;
            float worldY = _world.Character.Position.WorldY - dy / 0.5f;

            // Convert back to Region + Local X/Y
            // Inverse of WorldX = ((Region & 0xFF) - 135) * 192 + X / 10;
            // 1. Find region (approximate)
            int rX = (int)Math.Floor(worldX / 192) + 135;
            int rY = (int)Math.Floor(worldY / 192) + 92;
            ushort region = (ushort)((rY << 8) | rX);

            float localX = (worldX - (rX - 135) * 192) * 10;
            float localY = (worldY - (rY - 92) * 192) * 10;

            var character = _world.GetCharacter();
            return new SRCoord(region, localX, localY, character.Position.Z);
        }
    }
}

