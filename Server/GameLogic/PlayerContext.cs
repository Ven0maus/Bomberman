using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server.GameLogic
{
    public class PlayerContext
    {
        public readonly int Id;
        public readonly TcpClient Client;
        public readonly string Name;

        public int MaxBombs = 1;
        public int BombStrength = 1;
        public int Kills;

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
                Network.Instance.SendPacket(player.Key, new Packet("invincibility", "start:" + Id));
            }
        }

        private void InvincibilityTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            SecondsInvincible -= 1;
            if (SecondsInvincible <= 0)
            {
                _invincibilityTimer.Stop();

                // Let everyone know this client is no longer invincible
                foreach (var player in _game.Players)
                {
                    Network.Instance.SendPacket(player.Key, new Packet("invincibility", "stop:" + Id));
                }

                // Check if we're currently standing in fire
                if (_game.Context.IsOnFire(Position))
                {
                    // Die
                    Alive = false;

                    // Let players know this player died
                    foreach (var player in _game.Players)
                        Network.Instance.SendPacket(player.Key, new Packet("playerdied", Id.ToString()));

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
            }
        }
    }
}
