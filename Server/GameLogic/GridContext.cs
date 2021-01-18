using Bomberman.Client.GameObjects;
using Bomberman.Client.Graphics;
using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Server.GameLogic
{
    internal class GridContext : Grid
    {
        public int _bombCounter = 0;
        public new Dictionary<Point, BombContext> Bombs;
        private readonly Game _game;
        public GridContext(Game game, int width, int height) : base(width, height)
        {
            _game = game;
            Bombs = new Dictionary<Point, BombContext>();
            // Set power-ups server sided
            SetPowerUps();
        }

        public bool PlaceBomb(PlayerContext placedBy, Point position, int strength, out BombContext bomb)
        {
            bomb = null;
            if (Bombs.ContainsKey(position) || GetValue(position.X, position.Y).HasBomb) return false;
            if (placedBy.BombsPlaced >= placedBy.MaxBombs) return false;
            bomb = new BombContext(_game, this, placedBy, position, 3f, strength, _bombCounter++);
            placedBy.BombsPlaced += 1;
            Bombs.Add(position, bomb);
            return true;
        }

        public void CheckPowerup(PlayerContext player, TcpClient client)
        {
            var position = player.Position;
            var cell = GetValue(position.X, position.Y);

            if (cell.PowerUp == PowerUp.None) return;

            switch (cell.PowerUp)
            {
                case PowerUp.ExtraBomb:
                    player.MaxBombs++;
                    break;
                case PowerUp.BombStrength:
                    player.BombStrength++;
                    break;
                case PowerUp.Invicibility:
                    player.StartInvincibility();
                    break;
                default:
                    break;
            }

            cell.PowerUp = PowerUp.None;

            Network.Instance.SendPacket(client, new Packet("pickuppowerup", $"{position.X}:{position.Y}"));

            // Tell all other clients that the powerup was picked up!
            foreach (var p in _game.Players)
            {
                if (p.Key != client)
                {
                    Network.Instance.SendPacket(p.Key, new Packet("pickuppowerup", $"{position.X}:{position.Y}"));
                }
            }
        }

        private void SetPowerUps()
        {
            for (int x=0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    // Small chance to contain a random powerup
                    if (Game.Random.Next(0, 100) < 25)
                    {
                        var tile = GetValue(x, y);
                        if (!tile.Explored && tile.Destroyable)
                        {
                            tile.PowerUp = (PowerUp)Game.Random.Next(1, 4);
                        }
                    }
                }
            }
        }
    }
}
