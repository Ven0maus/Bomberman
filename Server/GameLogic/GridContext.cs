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
        public new Dictionary<Point, BombContext> Bombs;
        public GridContext(int width, int height) : base(width, height)
        {
            Bombs = new Dictionary<Point, BombContext>();
            // Set power-ups clientsided
            SetPowerUps();
        }

        public bool PlaceBomb(PlayerContext placedBy, Point position, int strength)
        {
            if (Bombs.ContainsKey(position) || GetValue(position.X, position.Y).HasBomb) return false;
            var bomb = new BombContext(this, placedBy, position, 3f, strength);
            Bombs.Add(position, bomb);
            return true;
        }

        public async void CheckPowerup(PlayerContext player, TcpClient client)
        {
            var position = player.Position;
            var cell = GetValue(position.X, position.Y);

            if (cell.PowerUp == PowerUp.None) return;

            string powerup;
            switch (cell.PowerUp)
            {
                case PowerUp.ExtraBomb:
                    player.MaxBombs++;
                    powerup = "extrabomb";
                    break;
                case PowerUp.BombStrength:
                    player.BombStrength++;
                    powerup = "bombstrength";
                    break;
                default:
                    powerup = "unhandled";
                    break;
            }

            await PacketHandler.SendPacket(client, new Packet("powerup", powerup));
            cell.PowerUp = PowerUp.None;
        }

        private void SetPowerUps()
        {
            for (int x=0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    // Small chance to contain a random powerup
                    if (Game.Random.Next(0, 101) <= 15)
                    {
                        var tile = GetValue(x, y);
                        if (!tile.Explored && tile.Destroyable)
                        {
                            tile.PowerUp = (PowerUp)Game.Random.Next(1, 3);
                        }
                    }
                }
            }
        }
    }
}
