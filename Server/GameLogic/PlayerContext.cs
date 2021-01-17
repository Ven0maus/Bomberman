using Microsoft.Xna.Framework;
using System.Net.Sockets;

namespace Server.GameLogic
{
    public class PlayerContext
    {
        public int Id;
        public int MaxBombs = 1;
        public int BombStrength = 1;
        public int BombsPlaced { get; set; }

        public Point Position { get; set; }

        public readonly TcpClient Client;
        public bool Alive;

        public string Name;

        public PlayerContext(TcpClient client, Point position, int id)
        {
            Alive = true;
            Client = client;
            Id = id;
            Position = position;
        }
    }
}
