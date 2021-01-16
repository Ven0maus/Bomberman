using Bomberman.Client.GameObjects;
using Bomberman.Client.Graphics;
using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bomberman.Client
{
    public class GameClient
    {
        public readonly TcpClient Client;

        private readonly string _serverIp;
        private readonly int _serverPort;

        private Player _player;
        private readonly List<Player> _otherPlayers;

        public bool Connected { get { return Client.Connected; } }
        public bool Running { get; private set; }

        private bool _clientRequestedDisconnect = false;

        private readonly Dictionary<string, Func<string, Task>> _commandHandlers;

        private double _timeSinceLastHeartbeat = 0f;

        public GameClient(string serverIp, int serverPort)
        {
            Client = new TcpClient();
            _serverIp = serverIp;
            _serverPort = serverPort;
            _otherPlayers = new List<Player>();
            _commandHandlers = new Dictionary<string, Func<string, Task>>();
        }

        private void CleanupNetworkResources()
        {
            if (Client.Connected)
                Client.GetStream().Close();
            Client.Close();
        }

        public bool Connect()
        {
            // Connect to the server
            try
            {
                Client.Connect(_serverIp, _serverPort);
            }
            catch (SocketException se)
            {
                Console.WriteLine("[ERROR] {0}", se.Message);
            }

            // check that we've connected
            if (Client.Connected)
            {
                // Connected!
                Console.WriteLine("Connected to the server at {0}.", Client.Client.RemoteEndPoint);
                Running = true;

                // Hook up some packet command handlers
                _commandHandlers["bye"] = HandleBye;
                _commandHandlers["message"] = HandleMessage;
                _commandHandlers["heartbeat"] = HandleHeartbeat;
                _commandHandlers["move"] = HandleMovement;
                _commandHandlers["moveother"] = HandleMovementOther;
                _commandHandlers["spawn"] = HandlePlayerInstantiation;
                _commandHandlers["spawnother"] = HandleOtherInstantiation;
                _commandHandlers["placebomb"] = HandleBombPlacement;
                _commandHandlers["placebombother"] = HandleBombPlacementOther;
                _commandHandlers["detonatePhase1"] = HandleDetonationPhase1;
                _commandHandlers["detonatePhase2"] = HandleDetonationPhase2;
                _commandHandlers["spawnpowerup"] = HandlePowerupSpawn;
                _commandHandlers["pickuppowerup"] = HandlePowerupPickup;

                return true;
            }
            else
            {
                CleanupNetworkResources();
                Console.WriteLine($"Wasn't able to connect to the server at {_serverIp}:{_serverPort}.");
            }

            return false;
        }

        private Task HandlePowerupPickup(string message)
        {
            var data = message.Split(':');
            var position = new Point(int.Parse(data[0]), int.Parse(data[1]));
            Game.GridScreen.Grid.DeletePowerUp(position);
            return Task.CompletedTask;
        }

        private Task HandlePowerupSpawn(string message)
        {
            var data = message.Split(':');
            var position = new Point(int.Parse(data[0]), int.Parse(data[1]));
            PowerUp powerUpType = (PowerUp)int.Parse(data[2]);
            Game.GridScreen.Grid.SpawnPowerUp(position, powerUpType);
            return Task.CompletedTask;
        }

        private Task HandleDetonationPhase2(string message)
        {
            var data = message.Split(':').ToList();
            int bombId = int.Parse(data[0]);
            var bomb = _bombsPlaced.FirstOrDefault(a => a.Id == bombId);
            data.RemoveAt(0);
            if (data.Count == 1 && string.IsNullOrEmpty(data[0])) return Task.CompletedTask;
            var positions = data.Select(a =>
            {
                var coords = a.Split(',');
                if (!int.TryParse(coords[0], out int x) || !int.TryParse(coords[1], out int y))
                    throw new Exception("Invalid coordinates phase2: [" + a + "] | Message: " + message);
                return new Point(x, y);
            }).ToList();
            if (bomb != null)
            {
                bomb.CleanupFireAfter(positions);
                _bombsPlaced.Remove(bomb);
            }
            return Task.CompletedTask;
        }

        private Task HandleDetonationPhase1(string message)
        {
            var data = message.Split(':').ToList();
            var bombIds = data[0].Split(',');
            var ids = bombIds.Select(int.Parse);
            
            foreach (var bombId in ids)
            {
                var bomb = _bombsPlaced.FirstOrDefault(a => a.Id == bombId);
                if (bomb != null)
                {
                    bomb.Detonate();
                }
            }

            data.RemoveAt(0);
            if (data.Count == 1 && string.IsNullOrEmpty(data[0])) return Task.CompletedTask;
            var positions = data.Select(a =>
            {
                var coords = a.Split(',');
                if (!int.TryParse(coords[0], out int x) || !int.TryParse(coords[1], out int y))
                    throw new Exception("Invalid coordinates phase1: [" + a + "] | Message: " + message);
                return new Point(x, y);
            }).ToList();

            Game.GridScreen.Grid.BombDetonationPhase1(positions);

            return Task.CompletedTask;
        }

        private Task HandleBombPlacementOther(string message)
        {
            var coords = message.Split(':');
            var position = new Point(int.Parse(coords[0]), int.Parse(coords[1]));
            var player = _otherPlayers.FirstOrDefault(a => a.Id == int.Parse(coords[4]));
            if (player != null)
            {
                var bomb = new Bomb(player, position, int.Parse(coords[2]), int.Parse(coords[3]))
                {
                    Parent = Game.GridScreen
                };
                _bombsPlaced.Add(bomb);
            }
            return Task.CompletedTask;
        }

        private readonly List<Bomb> _bombsPlaced = new List<Bomb>();
        private Task HandleBombPlacement(string message)
        {
            _player.RequestBombPlacement = false;
            if (message == "bad entry")
            {
                return Task.CompletedTask;
            }
            var coords = message.Split(':');
            var position = new Point(int.Parse(coords[0]), int.Parse(coords[1]));
            var bomb = new Bomb(_player, position, int.Parse(coords[2]), int.Parse(coords[3]))
            {
                Parent = Game.GridScreen
            };
            _bombsPlaced.Add(bomb);
            return Task.CompletedTask;
        }

        private Task HandleMovementOther(string message)
        {
            var coords = message.Split(':');
            var position = new Point(int.Parse(coords[1]), int.Parse(coords[2]));
            var id = int.Parse(coords[0]);
            var p = _otherPlayers.FirstOrDefault(a => a.Id == id);
            if (p != null)
                p.Position = position;
            return Task.CompletedTask;
        }

        private Task HandleOtherInstantiation(string message)
        {
            var coords = message.Split(':');
            var position = new Point(int.Parse(coords[1]), int.Parse(coords[2]));
            _otherPlayers.Add(new Player(position, int.Parse(coords[0]), false)
            {
                Parent = Game.GridScreen
            });
            return Task.CompletedTask;
        }

        private Task HandlePlayerInstantiation(string message)
        {
            var coords = message.Split(':');
            var position = new Point(int.Parse(coords[1]), int.Parse(coords[2]));
            _player = new Player(position, int.Parse(coords[0]), true)
            {
                Parent = Game.GridScreen
            };
            return Task.CompletedTask;
        }

        private Task HandleMovement(string message)
        {
            _player.RequestedMovement = false;
            if (message == "bad entry")
            {
                return Task.CompletedTask;
            }

            var coords = message.Split(':');
            var position = new Point(int.Parse(coords[0]), int.Parse(coords[1]));
            _player.Position = position;
            return Task.CompletedTask;
        }

        private Task HandleBye(string message)
        {
            // Print the message
            Console.WriteLine("The server is disconnecting us with this message:");
            Console.WriteLine(message);

            // Will start the disconnection process in Run()
            Running = false;
            return Task.CompletedTask; 
        }

        // Just prints out a message sent from the server
        private Task HandleMessage(string message)
        {
            Console.Write(message);
            return Task.CompletedTask;
        }

        // Just prints out a message sent from the server
        private async Task HandleHeartbeat(string message)
        {
            _timeSinceLastHeartbeat = 0f;
            Console.WriteLine("Received heartbeat request: " + message);
            await PacketHandler.SendPacket(Client, new Packet("heartbeat", "yes"));
            Console.WriteLine("Send heartbeat response: yes");
        }

        private void Dispatcher(TcpClient client, Packet packet)
        {
            try
            {
                if (_commandHandlers.ContainsKey(packet.Command))
                    _commandHandlers[packet.Command](packet.Message).GetAwaiter().GetResult();
            }
            catch(SocketException)
            {
                // Make sure that we didn't have a graceless disconnect
                if (IsDisconnected(Client) && !_clientRequestedDisconnect)
                {
                    Running = false;
                    Console.WriteLine("The server has disconnected from us ungracefully. :[");
                }
            }
            catch (IOException)
            {
                // Make sure that we didn't have a graceless disconnect
                if (IsDisconnected(Client) && !_clientRequestedDisconnect)
                {
                    Running = false;
                    Console.WriteLine("The server has disconnected from us ungracefully. :[");
                }
            }
        }

        // Main loop for the Games Client
        public void Update(GameTime gameTime)
        {
            bool wasRunning = Running;

            var tasks = new List<Task>();
            // Listen for messages
            if (Running)
            {
                // Check for new packets
                tasks.Add(PacketHandler.ReceivePackets(Client, Dispatcher));
                tasks.RemoveAll(a => a.IsCompleted);

                _timeSinceLastHeartbeat += gameTime.ElapsedGameTime.Milliseconds;
                if (_timeSinceLastHeartbeat > 5 * 1000)
                {
                    _timeSinceLastHeartbeat = 0;
                    // Make sure that we didn't have a graceless disconnect
                    if (IsDisconnected(Client) && !_clientRequestedDisconnect)
                    {
                        Running = false;
                        Console.WriteLine("The server has disconnected from us ungracefully. :[");
                    }
                }
            }

            // Finish tasks if any where still added
            Task.WaitAll(tasks.ToArray(), 1000);

            if (Running) return;

            // Cleanup
            CleanupNetworkResources();
            if (wasRunning)
                Console.WriteLine("Disconnected from the server.");
        }

        private static bool IsDisconnected(TcpClient client)
        {
            try
            {
                PacketHandler.SendPacket(client, null).GetAwaiter().GetResult();
            }
            catch (IOException)
            {
                return true;
            }
            catch (SocketException)
            {
                return true;
            }
            return false;
        }

        public void Disconnect()
        {
            Console.WriteLine("Disconnecting from the server...");
            Running = false;
            _clientRequestedDisconnect = true;
            PacketHandler.SendPacket(Client, new Packet("bye")).GetAwaiter().GetResult();
        }
    }
}
