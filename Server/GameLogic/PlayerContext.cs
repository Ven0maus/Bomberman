using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using System.Net.Sockets;

namespace Server.GameLogic
{
    public class PlayerContext
    {
        public readonly int Id;
        public readonly TcpClient Client;
        public readonly string Name;

        public int MaxBombs = 1;
        public int BombStrength = 1;
        public int SecondsInvincible { get; private set; }
        public int BombsPlaced = 0;
        public Point Position;
        public bool Alive = true;

        public Color Color { get; private set; }
        private readonly System.Timers.Timer _invincibilityTimer;
        private readonly Game _game;

        public PlayerContext(Game game, TcpClient client, Point position, int id, string name, Color color)
        {
            Color = color;
            _game = game;
            Client = client;
            Id = id;
            Position = position;
            Name = name;

            _invincibilityTimer = new System.Timers.Timer(1000);
            _invincibilityTimer.Elapsed += InvincibilityTimer_Elapsed;
        }

        public void StartInvincibility()
        {
            SecondsInvincible = 10;
            _invincibilityTimer.Stop();
            _invincibilityTimer.Start();

            // Let everyone know this client is invincible
            foreach (var player in _game.Players)
            {
                Network.Instance.SendPacket(player.Key, new Packet("invincibility", "start:" + Name));
            }
        }

        private void InvincibilityTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            SecondsInvincible -= 1;
            if (SecondsInvincible <= 0)
            {
                // Let everyone know this client is no longer invincible
                foreach (var player in _game.Players)
                {
                    Network.Instance.SendPacket(player.Key, new Packet("invincibility", "stop:" + Name));
                }

                _invincibilityTimer.Stop();
            }
        }
    }
}
