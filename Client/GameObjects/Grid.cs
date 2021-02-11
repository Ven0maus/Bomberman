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

        private readonly List<Point> _spawnPositions = new List<Point>
        {
            // Top Left, Top Middle, Top Right
            new Point(0,0),
            new Point((Game.GridWidth / 2)-1, 0),
            new Point(Game.GridWidth -1, 0),

            // Middle Left, Middle Right
            new Point(0, (Game.GridHeight / 2) - 1),
            new Point(Game.GridWidth -1, (Game.GridHeight / 2) - 1), 

            // Bottom Left, Bottom Middle,  Bottom right
            new Point(0, Game.GridHeight -1),
            new Point((Game.GridWidth / 2)-1, Game.GridHeight -1),
            new Point(Game.GridWidth -1, Game.GridHeight -1),
        };

        private readonly List<Color> _availableColors = new List<Color>
        {
            Color.Red,
            Color.Cyan,
            Color.Orange,
            Color.Yellow,
            Color.LightGreen,
            Color.LightCoral,
            Color.Magenta,
            Color.White
        };

        public Color GetAvailableColor()
        {
            var color = _availableColors[Game.Random.Next(0, _availableColors.Count)];
            _availableColors.Remove(color);
            return color;
        }

        public Point GetAvailableSpawnPosition()
        {
            var pos = _spawnPositions[Game.Random.Next(0, _spawnPositions.Count)];
            _spawnPositions.Remove(pos);
            return pos;
        }

        public Tile this[int x, int y]
        {
            get { return GetValue(x, y); }
            private set { Cells[y * _width + x] = value; }
        }

        public Grid(int width, int height, bool singleplayer)
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
            CoverInDarkness();

            if (singleplayer)
            {
                // Create power ups client side
                SetPowerUps();
            }
        }

        protected void SetPowerUps()
        {
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    // Small chance to contain a random powerup
                    if (Game.Random.Next(0, 100) < 25)
                    {
                        var tile = GetValue(x, y);
                        if (!tile.Explored && tile.Destroyable)
                        {
                            var randomValue = Game.Random.Next(1, 8);
                            if (randomValue <= 3)
                                tile.PowerUp = PowerUp.BombStrength;
                            else if (randomValue <= 6)
                                tile.PowerUp = PowerUp.ExtraBomb;
                            else
                                tile.PowerUp = PowerUp.Invincibility;
                        }
                    }
                }
            }
        }

        private List<Point> GetNeighbors(int x, int y)
        {
            var neighbors = new List<Point>();
            if (InBounds(x - 1, y))
                neighbors.Add(new Point(x - 1, y));
            if (InBounds(x + 1, y))
                neighbors.Add(new Point(x + 1, y));
            if (InBounds(x, y - 1))
                neighbors.Add(new Point(x, y - 1));
            if (InBounds(x, y + 1))
                neighbors.Add(new Point(x, y + 1));
            return neighbors;
        }

        public void UncoverTilesFromDarkness(Point point, List<Point> values = null)
        {
            var processed = values ?? new List<Point>();
            var neighbors = GetNeighbors(point.X, point.Y);
            neighbors.Add(point);
            foreach (var neighbor in neighbors)
            {
                if (processed.Contains(neighbor)) continue;
                var tile = GetValue(neighbor.X, neighbor.Y);
                if (tile.Destroyable && tile.Explored)
                {
                    if (!tile.Destroyable && tile.Explored)
                        tile.Foreground = Color.White;
                    else if (tile.Explored)
                        tile.Foreground = Color.DarkBlue;
                    else
                        tile.Foreground = Color.White;
                    processed.Add(tile.Position);
                    UncoverTilesFromDarkness(tile.Position, processed);
                }
                else
                {
                    // Also uncover but don't continue in search
                    if (!tile.Destroyable && tile.Explored)
                        tile.Foreground = Color.White;
                    else if (tile.Explored)
                        tile.Foreground = Color.DarkBlue;
                    else
                        tile.Foreground = Color.White;
                    processed.Add(tile.Position);
                }
            }
            Game.GridScreen.IsDirty = true;
        }

        public void CoverInDarkness()
        {
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    var tile = GetValue(x, y);
                    tile.Foreground = Color.Lerp(tile.Foreground, Color.Black, 0.85f);
                }
            }
        }

        public void UncoverFromDarkness(bool multiplayer)
        {
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    var tile = GetValue(x, y);
                    if (!tile.Destroyable && tile.Explored)
                        tile.Foreground = Color.White;
                    else if (tile.Explored)
                        tile.Foreground = Color.DarkBlue;
                    else
                        tile.Foreground = Color.White;
                }
            }

            if (multiplayer)
            {
                // Reset player colors
                foreach (var player in Game.Client.OtherPlayers)
                {
                    player.Animation[0].Foreground = player.Color;
                    player.Animation.IsDirty = true;
                }
            }
            else
            {
                // TODO
            }

            Game.GridScreen.IsDirty = true;
        }

        public void CheckPowerup(Point position)
        {
            var cell = GetValue(position.X, position.Y);

            if (cell.PowerUp == PowerUp.None) return;

            switch (cell.PowerUp)
            {
                case PowerUp.ExtraBomb:
                    Game.Player.MaxBombs++;
                    break;
                case PowerUp.BombStrength:
                    Game.Player.BombStrength++;
                    break;
                case PowerUp.Invincibility:
                    Game.Player.BecomeInvincible();
                    break;
                default:
                    break;
            }

            DeletePowerUp(position);
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
                GetValue(position.X, position.Y).PowerUp = PowerUp.None;
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
