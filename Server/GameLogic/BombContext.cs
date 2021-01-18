using Bomberman.Client.Graphics;
using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Server.GameLogic
{
    internal class BombContext
    {
        public readonly int Id;
        private readonly GridContext _grid;
        private readonly Timer _bombTimer;
        private readonly PlayerContext _placedBy;

        private float _bombTime;
        private readonly float _strength;
        private bool _done, _detonated;

        private readonly Game _game;

        public Point Position { get; set; }

        public BombContext(Game game, GridContext grid, PlayerContext player, Point position, float time, int strength, int id) 
        {
            _game = game;
            Id = id;
            _placedBy = player;
            _grid = grid;
            _bombTime = time * 1000f;
            _strength = strength;
            Position = position;
            grid.GetValue(position.X, position.Y).HasBomb = true;
            _bombTimer = new Timer(_bombTime);
            _bombTimer.Elapsed += BombTimer_Elapsed;
            _bombTimer.Start();
        }

        private void BombTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!Network.Instance.GameOngoing)
                _done = true;
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

        public List<Point> CellPositions;
        public List<Point> GetCellPositions()
        {
            if (CellPositions != null) return CellPositions;
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

            return CellPositions = cells;
        }

        protected void CleanupFireAfter()
        {
            _done = true;
            _bombTimer.Stop();

            var cellPositions = GetCellPositions();

            var powerupSpawns = new List<Point>();

            // Remove fire cells
            var exceptPositions = new List<Point>();
            foreach (var pos in cellPositions)
            {
                var cell = _grid.GetValue(pos.X, pos.Y);
                _grid.Explore(cell.Position.X, cell.Position.Y);

                foreach (var id in _bombIds)
                {
                    cell.ContainsFireFrom.Remove(id);
                }

                if (cell.ContainsFireFrom.Count > 0)
                {
                    exceptPositions.Add(cell.Position);
                    continue; // Let other bomb handle this one
                }

                // Spawn powerup
                if (cell.PowerUp != PowerUp.None)
                {
                    powerupSpawns.Add(pos);
                }
            }

            // Send detonation packet to the client
            string formattedCellPositions = string.Join(":", cellPositions.Except(exceptPositions).Select(a => a.X + "," + a.Y));
            Network.Instance.SendPacket(_placedBy.Client, new Packet("detonatePhase2", Id.ToString() + ":" + formattedCellPositions));

            // Let all other clients know this bomb detonated
            foreach (var client in _game.Players)
            {
                if (client.Key != _placedBy.Client)
                {
                    Network.Instance.SendPacket(client.Key, new Packet("detonatePhase2", Id.ToString() + ":" + formattedCellPositions));
                }
            }

            // Tell client to spawn powerups
            // TODO: Check if detonation doesn't mess this up because of packet order
            foreach (var pos in powerupSpawns)
            {
                var cell = _grid.GetValue(pos.X, pos.Y);

                // Tell client to spawn a powerup on this tile
                Network.Instance.SendPacket(_placedBy.Client, new Packet("spawnpowerup", $"{pos.X}:{pos.Y}:{(int)cell.PowerUp}"));

                // Let all other clients know to spawn a powerup
                foreach (var client in _game.Players)
                {
                    if (client.Key != _placedBy.Client)
                    {
                        Network.Instance.SendPacket(client.Key, new Packet("spawnpowerup", $"{pos.X}:{pos.Y}:{(int)cell.PowerUp}"));
                    }
                }
            }
        }

        public class DetonationData
        {
            public List<int> BombIds = new List<int>();
            public List<Point> CellPositions = new List<Point>();

            public void Add(DetonationData data)
            {
                if (data == null)
                {
                    BombIds = null;
                    CellPositions = null;
                    return;
                }
                BombIds.AddRange(data.BombIds.Where(a => !BombIds.Contains(a)));
                CellPositions.AddRange(data.CellPositions.Where(a => !CellPositions.Contains(a)));
            }
        }

        private List<int> _bombIds;
        public DetonationData Detonate(bool sendPackets = true)
        {
            // Time for cleanup
            _detonated = true;
            _bombTime = 1250;
            _bombTimer.Interval = _bombTime;
            _bombTimer.Stop();

            // Remove from bombs collection
            _grid.GetValue(Position.X, Position.Y).HasBomb = false;
            _grid.Bombs.Remove(Position);

            _placedBy.BombsPlaced -= 1;

            var cellPositions = GetCellPositions();
            var data = new DetonationData();
            data.BombIds.Add(Id);
            data.CellPositions.AddRange(cellPositions);
            foreach (var pos in cellPositions)
            {
                // Game over, kill player
                var deadPlayers = _game.Players
                    .Where(a => a.Value.Position == pos && a.Value.Alive && a.Value.SecondsInvincible == 0)
                    .Select(a => a.Value)
                    .ToList();
                if (!_game.GameOver && deadPlayers.Any())
                {
                    foreach (var deadPlayer in deadPlayers)
                    {
                        deadPlayer.Alive = false;

                        // Let players know this player died
                        foreach (var player in _game.Players)
                            Network.Instance.SendPacket(player.Key, new Packet("playerdied", deadPlayer.Name));
                    }

                    // Check if there is 1 or no players left alive, then reset the game
                    if (_game.Players.Count(a => a.Value.Alive) <= 1 && !_game.GameOver)
                    {
                        _game.GameOver = true;
                        Task.Run(async () =>
                        {
                            await Task.Delay(3000);
                            Network.Instance.ResetGame();
                        });
                    }
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
                    var bomb = _grid.Bombs[cell.Position];
                    data.Add(bomb.Detonate(false));
                }
            }

            if (data.CellPositions == null || data.BombIds == null)
                return null;

            if (sendPackets)
            {
                CellPositions = data.CellPositions;
                _bombIds = data.BombIds;

                // Send detonation packet to the client
                string formattedCellPositions = string.Join(":", data.CellPositions.Select(a => a.X + "," + a.Y));
                Network.Instance.SendPacket(_placedBy.Client, new Packet("detonatePhase1", string.Join(",", data.BombIds) + ":" + formattedCellPositions));

                // Let all other clients know this bomb detonated
                foreach (var client in _game.Players)
                {
                    if (client.Key != _placedBy.Client)
                    {
                        Network.Instance.SendPacket(client.Key, new Packet("detonatePhase1", string.Join(",", data.BombIds) + ":" + formattedCellPositions));
                    }
                }
                _bombTimer.Start(); // Start again for cleanup
            }

            return data;
        }
    }
}
