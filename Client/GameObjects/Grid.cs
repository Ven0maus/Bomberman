using Bomberman.Client.Graphics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Bomberman.Client.GameObjects
{
    public class Grid
    {
        public readonly Tile[] Cells;
        protected readonly int _width, _height;

        public readonly Dictionary<Point, Bomb> Bombs = new Dictionary<Point, Bomb>();

        public Tile this[int x, int y]
        {
            get { return GetValue(x, y); }
            private set { Cells[y * _width + x] = value; }
        }

        public Grid(int width, int height)
        {
            _width = width;
            _height = height;

            // Initialize cells array
            Cells = new Tile[width * height];

            // Initialize default cells
            for (int x=0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    if (y > 0 && y < _height && y % 2 == 1)
                    {
                        if (x > 0 && x < _width && x % 2 == 1)
                        {
                            SetValue(x, y, new Tile(x, y, false));
                        }
                        else
                        {
                            SetValue(x, y, new Tile(x, y, true));
                        }
                    }
                    else
                    {
                        SetValue(x, y, new Tile(x, y, true));
                    }
                }
            }

            // Uncover default spawn location tiles
            UncoverSpawnLocations();
        }

        private readonly Dictionary<Point, PowerUpVisual> _powerups = new Dictionary<Point, PowerUpVisual>();
        public void SpawnPowerUp(Point position, PowerUp powerUp)
        {
            var cell = GetValue(position.X, position.Y);
            cell.Explored = true;
            Game.GridScreen.IsDirty = true;

            var visual = new PowerUpVisual(position, powerUp)
            {
                Parent = Game.GridScreen
            };
            _powerups.Add(position, visual);
        }

        public void DeletePowerUp(Point position)
        {
            if (_powerups.TryGetValue(position, out PowerUpVisual visual))
            {
                visual.Parent = null;
                _powerups.Remove(position);
            }
        }

        public bool IsWall(int x, int y)
        {
            return !Cells[y * _width + x].Explored;
        }

        public bool CanMove(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var cell = GetValue(x, y);
            return cell.Explored && cell.Destroyable && !cell.HasBomb;
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < _width && y < _height;
        }

        private void UncoverSpawnLocations()
        {
            // All corners
            var topLeft = new Point[] { new Point(0, 0), new Point(1, 0), new Point(0, 1) };
            var topRight = new Point[] { new Point(_width -1, 0), new Point(_width -2, 0), new Point(_width -1, 1) };
            var bottomLeft = new Point[] { new Point(0, _height -1), new Point(1, _height -1), new Point(0, _height -2) };
            var bottomRight = new Point[] { new Point(_width - 1, _height -1), new Point(_width - 2, _height-1), new Point(_width - 1, _height-2) };
            Explore(topLeft.Concat(topRight).Concat(bottomLeft).Concat(bottomRight));

            // Middle of all sides
            var top = new Point[] { new Point((_width /2)-1, 0), new Point(_width / 2, 0), new Point((_width / 2) +1, 0) };
            var bottom = new Point[] { new Point((_width / 2) - 1, _height-1), new Point(_width / 2, _height - 1), new Point((_width / 2) +1, _height - 1) };
            var left = new Point[] { new Point(0, (_height / 2) - 1), new Point(0, _height / 2), new Point(0, (_height / 2) + 1) };
            var right = new Point[] { new Point(_width -1, (_height / 2) - 1), new Point(_width - 1, _height / 2), new Point(_width - 1, (_height / 2) + 1) };
            Explore(top.Concat(bottom).Concat(left).Concat(right));
        }

        public Tile GetValue(int x, int y)
        {
            if (!InBounds(x, y)) return null;
            return Cells[y * _width + x];
        }

        private void SetValue(int x, int y, Tile tile)
        {
            Cells[y * _width + x] = tile;
        }

        public void Explore(int x, int y)
        {
            Cells[y * _width + x].Explored = true;
        }

        public void Explore(IEnumerable<Point> points)
        {
            foreach (var point in points)
                Explore(point.X, point.Y);
        }
    }
}
