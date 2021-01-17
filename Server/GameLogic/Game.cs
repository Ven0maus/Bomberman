using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

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

        public Game(Dictionary<TcpClient, string> clients, out Dictionary<TcpClient, PlayerContext> players)
        {
            Console.WriteLine("Starting game.");

            // Setup players for clients
            Players = clients.ToDictionary(a => a.Key, a => new PlayerContext(a.Key, GetSpawnPosition(), ClientIdCounter++) { Name = a.Value });
            players = Players;

            // Remove from waiting lobby
            foreach (var player in Players)
            {
                Network.Instance.WaitingLobby.Remove(player.Key);
            }

            // Tell who is left to remove the players that went in game
            foreach (var player in Network.Instance.WaitingLobby)
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
                Network.Instance.SendPacket(player.Key, new Packet("spawn", player.Value.Id + ":" + player.Value.Position.X + ":" + player.Value.Position.Y));

                // Tell all others that we spawned
                foreach (var otherPlayer in Players)
                {
                    if (otherPlayer.Key != player.Key)
                    {
                        // Spawn all others for the client
                        Network.Instance.SendPacket(player.Key, new Packet("spawnother", otherPlayer.Value.Id + ":" + otherPlayer.Value.Position.X + ":" + otherPlayer.Value.Position.Y + ":" + otherPlayer.Value.Name));
                    }
                }
            }
        }

        public void AddPlayer(TcpClient client)
        {
            if (Players.Count < 8)
            {
                var player = new PlayerContext(client, GetSpawnPosition(), ClientIdCounter++);
                Players.Add(client, player);

                // Spawn ourself
                Network.Instance.SendPacket(client, new Packet("spawn", player.Id + ":" + player.Position.X + ":" + player.Position.Y));

                // Let all others know we spawned
                foreach (var otherPlayer in Players)
                {
                    if (otherPlayer.Key != client)
                    {
                        // Let the client spawn all others aswel
                        Network.Instance.SendPacket(client, new Packet("spawnother", otherPlayer.Value.Id + ":" + otherPlayer.Value.Position.X + ":" + otherPlayer.Value.Position.Y));
                        // Let all others spawn the client
                        Network.Instance.SendPacket(otherPlayer.Key, new Packet("spawnother", player.Id + ":" + player.Position.X + ":" + player.Position.Y));
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

            // Also check if we picked up a power up during player move
            Context.CheckPowerup(player, client);
        }

        public void PlaceBomb(TcpClient client)
        {
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
