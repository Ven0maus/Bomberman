using Bomberman.Client.GameObjects;
using Bomberman.Client.Graphics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Timers;

namespace Server.GameLogic
{
    internal class BombContext
    {
        public int Id;

        private readonly GridContext _grid;
        private readonly Timer _bombTimer;
        private readonly PlayerContext _placedBy;

        private float _bombTime;
        private readonly float _strength;
        private bool _done, _detonated;

        public Point Position { get; set; }

        public BombContext(GridContext grid, PlayerContext player, Point position, float time, int strength) 
        {
            _placedBy = player;
            _grid = grid;
            _bombTime = time;
            _strength = strength;
            Position = position;
            _bombTimer = new Timer(_bombTime);
            _bombTimer.Elapsed += BombTimer_Elapsed;
        }

        private void BombTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_done)
            {
                _bombTimer.Stop();
                return;
            }

            if (!_detonated)
                Detonate();
            else
                CleanupFireAfter();
        }

        private List<Point> _cellPositions;
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

        protected void CleanupFireAfter()
        {
            _done = true;

            var cellPositions = GetCellPositions();

            // Remove fire cells
            foreach (var pos in cellPositions)
            {
                var cell = _grid.GetValue(pos.X, pos.Y);
                _grid.Explore(cell.Position.X, cell.Position.Y);

                if (!cell.ContainsFireFrom.Contains(Id) || cell.ContainsFireFrom.Count > 1)
                {
                    cell.ContainsFireFrom.Remove(Id);
                    continue; // Let other bomb handle this one
                }

                // Spawn powerup
                if (cell.PowerUp != PowerUp.None)
                {
                    // TODO: Tell client to spawn a powerup on this tile
                }
            }
        }

        public void Detonate()
        {
            // Time for cleanup
            _detonated = true;
            _bombTime = 1250;
            _bombTimer.Interval = _bombTime;

            // Remove from bombs collection
            _grid.GetValue(Position.X, Position.Y).HasBomb = false;
            _grid.Bombs.Remove(Position);

            _placedBy.BombsPlaced -= 1;

            var cellPositions = GetCellPositions();

            foreach (var pos in cellPositions)
            {
                // Game over, kill player
                if (_placedBy.Position == pos)
                {
                    // TODO: Disable player until game is over
                }

                var cell = _grid.GetValue(pos.X, pos.Y);

                // Destroy powerup in blast
                if (!cell.HasFire && cell.Explored && cell.PowerUp != PowerUp.None)
                    cell.PowerUp = PowerUp.None;

                // Set cell on fire
                cell.ContainsFireFrom.Add(Id);

                if (cell.HasBomb)
                {
                    // Instantly detonate the bomb
                    _grid.Bombs[cell.Position].Detonate();
                }
            }

            // TODO: Send detonation cells to client
        }
    }
}
