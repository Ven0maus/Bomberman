using Bomberman.Client.Graphics;
using Microsoft.Xna.Framework;
using SadConsole.Entities;
using System;
using System.Collections.Generic;

namespace Bomberman.Client.GameObjects
{
    public class Bomb : Entity
    {
        private readonly int _strength;
        private readonly Grid _grid;

        private float _currentTime = 0;
        private float _bombTime;

        private bool _detonated = false;
        private bool _done = false;

        private List<Point> _cellPositions;

        public Bomb(Point position, float bombTime, int strength) : base(Color.White, Color.Transparent, 3)
        {
            Position = position;
            Font = Game.Font;
            _strength = strength;
            _grid = Game.GridScreen.Grid;
            _grid.GetValue(position.X, position.Y).HasBomb = true;
            _bombTime = bombTime * 1000;
        }

        public Bomb(int x, int y, float bombTime, int strength) : this(new Point(x, y), bombTime, strength)
        { }

        public override void Update(TimeSpan timeElapsed)
        {
            if (_done) return;
            _currentTime += timeElapsed.Milliseconds;
            if (_currentTime >= _bombTime)
            {
                if (!_detonated)
                    Detonate();
                else
                    CleanupFireAfter();
            }
        }

        private void CleanupFireAfter()
        {
            _done = true;

            var cellPositions = GetCellPositions();

            // Remove fire cells
            foreach (var pos in cellPositions)
            {
                var cell = _grid.GetValue(pos.X, pos.Y);
                _grid.Explore(cell.Position.X, cell.Position.Y);

                if (!cell.ContainsFireFrom.Contains(this) || cell.ContainsFireFrom.Count > 1)
                {
                    cell.ContainsFireFrom.Remove(this);
                    continue; // Let other bomb handle this one
                }

                // Spawn powerup
                if (cell.PowerUp != PowerUp.None)
                {
                    cell.Glyph = cell.PowerUp == PowerUp.ExtraBomb ? 5 : 6;
                    cell.Foreground = Color.White;
                }
            }

            Game.GridScreen.IsDirty = true;
            Parent = null;
        }

        public List<Point> GetCellPositions()
        {
            if (_cellPositions != null) return _cellPositions;
            var cells = new List<Point>
            {
                Position
            };

            // Check each direction and expand 1 cell for each strength level
            bool checkRight = true;
            bool checkLeft = true;
            bool checkUp = true;
            bool checkDown = true;
            for (int i = 1; i <= _strength; i++)
            {
                if (checkRight)
                {
                    var right = _grid.GetValue(Position.X + i, Position.Y);
                    checkRight = right != null && right.Explored && right.Destroyable;
                    if (right != null && right.Destroyable)
                        cells.Add(right.Position);
                }
                if (checkLeft)
                {
                    var left = _grid.GetValue(Position.X - i, Position.Y);
                    checkLeft = left != null && left.Explored && left.Destroyable;
                    if (left != null && left.Destroyable)
                        cells.Add(left.Position);
                }
                if (checkUp)
                {
                    var up = _grid.GetValue(Position.X, Position.Y - i);
                    checkUp = up != null && up.Explored && up.Destroyable;
                    if (up != null && up.Destroyable)
                        cells.Add(up.Position);
                }
                if (checkDown)
                {
                    var down = _grid.GetValue(Position.X, Position.Y + i);
                    checkDown = down != null && down.Explored && down.Destroyable;
                    if (down != null && down.Destroyable)
                        cells.Add(down.Position);
                }
            }

            return _cellPositions = cells;
        }

        public void Detonate()
        {
            _detonated = true;
            _currentTime = 0;
            _bombTime = 1250; // Time for cleanup

            Animation[0].Foreground = Color.Transparent;
            Animation.IsDirty = true;

            // Remove from bombs collection
            _grid.GetValue(Position.X, Position.Y).HasBomb = false;
            _grid.Bombs.Remove(Position);

            Player.Instance.BombsPlaced -= 1;

            var cellPositions = GetCellPositions();

            foreach (var pos in cellPositions)
            {
                // Game over, kill player
                if (Player.Instance.Position == pos)
                {
                    Game.GameOver();
                }

                var cell = _grid.GetValue(pos.X, pos.Y);

                // Destroy powerup in blast
                if (!cell.HasFire && cell.Explored && cell.PowerUp != PowerUp.None)
                    cell.PowerUp = PowerUp.None;

                // Set cell on fire
                cell.Glyph = 4;
                cell.Foreground = Color.White;
                cell.ContainsFireFrom.Add(this);

                if (cell.HasBomb)
                {
                    // Instantly detonate the bomb
                    _grid.Bombs[cell.Position].Detonate();
                }
            }

            Game.GridScreen.IsDirty = true;
        }
    }
}
