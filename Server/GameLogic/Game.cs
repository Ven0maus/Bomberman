using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server.GameLogic
{
    public class Game
    {
        public static Random Random = new Random();
        internal readonly GridContext Context;

        public int ClientIdCounter = 0;
        public readonly Dictionary<TcpClient, PlayerContext> Players;
        private readonly List<Point> _spawnPositions = new List<Point>
        {
            // Top Left, Top Middle, Top Right
            new Point(0,0), 
            new Point((Bomberman.Client.Game.GridWidth / 2)-1, 0),
            new Point(Bomberman.Client.Game.GridWidth -1, 0),

            // Middle Left, Middle Right
            new Point(0, (Bomberman.Client.Game.GridHeight / 2) - 1), 
            new Point(Bomberman.Client.Game.GridWidth -1, (Bomberman.Client.Game.GridHeight / 2) - 1), 

            // Bottom Left, Bottom Middle,  Bottom right
            new Point(0, Bomberman.Client.Game.GridHeight -1),
            new Point((Bomberman.Client.Game.GridWidth / 2)-1, Bomberman.Client.Game.GridHeight -1),
            new Point(Bomberman.Client.Game.GridWidth -1, Bomberman.Client.Game.GridHeight -1),
        };

        private readonly List<Color> _availableColors = new List<Color>
        {
            Color.Red,
            Color.Cyan,
            Color.Orange,
            Color.Yellow,
            Color.Green,
            Color.Brown,
            Color.Magenta,
            Color.White
        };

        public bool GameOver = false;

        private Color GetRandomColor()
        {
            var color = _availableColors[Random.Next(0, _availableColors.Count)];
            _availableColors.Remove(color);
            return color;
        }

        public Game(Dictionary<TcpClient, string> clients, out Dictionary<TcpClient, PlayerContext> players)
        {
            Console.WriteLine("Starting game.");

            // Setup players for clients
            Players = clients.ToDictionary(a => a.Key, a => new PlayerContext(this, a.Key, GetSpawnPosition(), ClientIdCounter++, a.Value, GetRandomColor()));
            players = Players;

            // Remove from waiting lobby
            foreach (var player in Players)
            {
                Network.Instance.WaitingLobby.Remove(player.Key);
            }

            // Tell everyone to remove the players that went in game
            foreach (var player in Network.Instance.Clients)
            {
                foreach (var p in Players)
                {
                    Network.Instance.SendPacket(player.Key, new Packet("removefromwaitinglobby", Network.Instance.GetClientPlayerName(p.Key)));
                }
            }

            // Generate server-side grid
            Context = new GridContext(this, Bomberman.Client.Game.GridWidth, Bomberman.Client.Game.GridHeight);

            // Let players know where to spawn
            foreach (var player in Players)
            {
                // Tell client the game started
                Network.Instance.SendPacket(player.Key, new Packet("gamestart"));

                // Tell client to spawn itself
                Network.Instance.SendPacket(player.Key, new Packet("spawn", player.Value.Id + ":" + player.Value.Position.X + 
                    ":" + player.Value.Position.Y + ":" + player.Value.Color.R + ":" + player.Value.Color.G + ":" + player.Value.Color.B));

                // Tell all others that we spawned
                foreach (var otherPlayer in Players)
                {
                    if (otherPlayer.Key != player.Key)
                    {
                        // Spawn all others for the client
                        Network.Instance.SendPacket(player.Key, new Packet("spawnother", otherPlayer.Value.Id + 
                            ":" + otherPlayer.Value.Position.X + ":" + otherPlayer.Value.Position.Y + ":" + otherPlayer.Value.Name + 
                            ":" + otherPlayer.Value.Color.R + ":" + otherPlayer.Value.Color.G + ":" + otherPlayer.Value.Color.B));
                    }
                }
            }
        }

        private PlayerContext GetPlayer(TcpClient client)
        {
            if (Players.TryGetValue(client, out PlayerContext player))
                return player;
            Console.WriteLine("Player object not found for client: " + client.Client.RemoteEndPoint);
            return null;
        }

        public void Move(TcpClient client, Point position)
        {
            if (GameOver) return;

            var player = GetPlayer(client);
            if (player == null)
                throw new Exception("Player is null!");

            if (!player.Alive) return;

            var previous = player.Position;
            var diffX = previous.X - position.X;
            var diffY = previous.Y - position.Y;

            if (diffX > 1 || diffX < -1 || diffY > 1 || diffY < -1)
            {
                Console.WriteLine("Player attempted a wrong movement action, packet ignored.");
                Network.Instance.SendPacket(client, new Packet("move", $"bad entry"));
                return;
            }

            if (diffX == 0 && diffY == 0)
            {
                Console.WriteLine("Player attempted to move to the same position, packet ignored.");
                Network.Instance.SendPacket(client, new Packet("move", $"bad entry"));
                return;
            }

            if (!Context.CanMove(position.X, position.Y))
            {
                Network.Instance.SendPacket(client, new Packet("move", $"bad entry"));
                return;
            }

            // Update server-side player position
            player.Position = position;

            Network.Instance.SendPacket(client, new Packet("move", $"{player.Position.X}:{player.Position.Y}"));

            // Let all other clients know we moved
            foreach (var p in Players)
            {
                if (p.Key != client)
                {
                    Network.Instance.SendPacket(p.Key, new Packet("moveother", $"{player.Id}:{player.Position.X}:{player.Position.Y}"));
                }
            }

            // Check if we moved onto a tile that is on fire
            if (Context.IsOnFire(position) && Players[client].Alive && Players[client].SecondsInvincible <= 0)
            {
                Players[client].Alive = false;

                // Let players know this player died
                foreach (var p in Players)
                    Network.Instance.SendPacket(p.Key, new Packet("playerdied", player.Id.ToString()));

                // Check if there is 1 or no players left alive, then reset the game
                if (Players.Count(a => a.Value.Alive) <= 1 && !GameOver)
                {
                    GameOver = true;
                    Task.Run(async () =>
                    {
                        await Task.Delay(3000);
                        Network.Instance.ResetGame();
                    });
                }

                return;
            }

            // Also check if we picked up a power up during player move
            Context.CheckPowerup(player, client);
        }

        public void PlaceBomb(TcpClient client)
        {
            if (GameOver) return;

            var player = GetPlayer(client);
            if (player == null) return;

            if (Context.PlaceBomb(player, player.Position, player.BombStrength, out BombContext bomb))
            {
                Network.Instance.SendPacket(client, new Packet("placebomb", $"{player.Position.X}:{player.Position.Y}:{player.BombStrength}:{bomb.Id}"));

                // Let all other clients know we placed a bomb
                foreach (var p in Players)
                {
                    if (p.Key != client)
                    {
                        Network.Instance.SendPacket(p.Key, new Packet("placebombother", $"{player.Position.X}:{player.Position.Y}:{player.BombStrength}:{bomb.Id}:{player.Id}"));
                    }
                }
            }
            else
            {
                Network.Instance.SendPacket(client, new Packet("placebomb", "bad entry"));
            }
        }

        private Point GetSpawnPosition()
        {
            var position = _spawnPositions[Random.Next(0, _spawnPositions.Count)];
            _spawnPositions.Remove(position);
            return position;
        }
    }
}
