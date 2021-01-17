using Bomberman.Client.GameObjects;
using Bomberman.Client.Graphics;
using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Bomberman.Client
{
    public class GameClient
    {
        public readonly TcpClient Client;

        private readonly string _serverIp;
        private readonly int _serverPort;

        public readonly string PlayerName;
        private Player _player;
        private readonly List<Player> _otherPlayers;

        public bool Connected { get { return Client.Connected; } }
        public bool Running { get; private set; }

        private bool _clientRequestedDisconnect = false;

        private readonly Dictionary<string, Func<string, Task>> _commandHandlers;

        private double _timeSinceLastHeartbeat = 0f;

        public GameClient(string serverIp, int serverPort, string playerName)
        {
            Client = new TcpClient();
            PlayerName = playerName;
            _serverIp = serverIp;
            _serverPort = serverPort;
            _otherPlayers = new List<Player>();
            _commandHandlers = new Dictionary<string, Func<string, Task>>();
        }

        public async void SendPacket(TcpClient client, Packet packet)
        {
            try
            {
                await PacketHandler.SendPacket(client, packet, false);
            }
            catch (ProtocolViolationException)
            {
                // Bad packet?
            }
            catch (ObjectDisposedException)
            {
                Running = false;
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: " + e.ToString());
                Running = false;
            }
            catch (IOException e)
            {
                Console.WriteLine("IOException: " + e.ToString());
                Running = false;
            }
        }

        private void CleanupNetworkResources()
        {
            PacketHandler.RemoveClientPacketProtocol(Client);
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
                _commandHandlers["joinwaitinglobby"] = HandleJoinWaitingLobby;
                _commandHandlers["removefromwaitinglobby"] = HandleRemoveFromWaitingLobby;
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
                _commandHandlers["gamestart"] = HandleGameStart;
                _commandHandlers["ready"] = HandleReady;
                _commandHandlers["unready"] = HandleUnReady;
                _commandHandlers["gamecountdown"] = HandleGameCountdown;

                // Send our player name to the server
                SendPacket(Client, new Packet("playername", PlayerName));

                return true;
            }
            else
            {
                CleanupNetworkResources();
                Console.WriteLine($"Wasn't able to connect to the server at {_serverIp}:{_serverPort}.");
            }

            return false;
        }

        private Task HandleGameCountdown(string message)
        {
            Console.WriteLine("Game countdown has begon!");
            var time = int.Parse(message);
            if (time > 0)
                Game.ClientWaitingLobby.StartCountdown(time);
            else
                Game.ClientWaitingLobby.StopCountdown();
            return Task.CompletedTask;
        }

        private Task HandleUnReady(string message)
        {
            Game.ClientWaitingLobby.SetReady(message, false);
            return Task.CompletedTask;
        }

        private Task HandleReady(string message)
        {
            Game.ClientWaitingLobby.SetReady(message, true);
            return Task.CompletedTask;
        }

        private Task HandleRemoveFromWaitingLobby(string message)
        {
            Game.ClientWaitingLobby.RemovePlayer(message);
            return Task.CompletedTask;
        }

        private Task HandleGameStart(string message)
        {
            Game.InitializeGameScreen(true);
            Console.WriteLine("Game started!");
            return Task.CompletedTask;
        }

        private Task HandleJoinWaitingLobby(string message)
        {
            if (Game.ClientWaitingLobby == null)
                Game.ClientWaitingLobby = new ClientWaitingLobby(Game.GameWidth, Game.GameHeight);
            Game.ClientWaitingLobby.IsVisible = true;
            Game.ClientWaitingLobby.IsFocused = true;
            SadConsole.Global.CurrentScreen = Game.ClientWaitingLobby;
            Game.ClientWaitingLobby.AddPlayer(message);
            Game.MainMenuScreen.ServerConnectionScreen.Connecting = false;
            return Task.CompletedTask;
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
            var playerName = coords[3];
            _otherPlayers.Add(new Player(position, int.Parse(coords[0]), false)
            {
                Parent = Game.GridScreen,
                Name = playerName
            });
            Console.WriteLine("Spawned other: " + playerName);
            return Task.CompletedTask;
        }

        private Task HandlePlayerInstantiation(string message)
        {
            var coords = message.Split(':');
            var position = new Point(int.Parse(coords[1]), int.Parse(coords[2]));
            _player = new Player(position, int.Parse(coords[0]), true)
            {
                Parent = Game.GridScreen,
                Name = PlayerName
            };
            Console.WriteLine("Spawned ourself: " + PlayerName);
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
            // Go back to main menu
            Game.MainMenuScreen.IsVisible = true;
            Game.MainMenuScreen.IsFocused = true;
            SadConsole.Global.CurrentScreen = Game.MainMenuScreen;
            return Task.CompletedTask; 
        }

        // Just prints out a message sent from the server
        private Task HandleMessage(string message)
        {
            Console.WriteLine(message);
            return Task.CompletedTask;
        }

        // Just prints out a message sent from the server
        private Task HandleHeartbeat(string message)
        {
            _timeSinceLastHeartbeat = 0f;
            SendPacket(Client, new Packet("heartbeat"));
            return Task.CompletedTask;
        }

        private void Dispatcher(TcpClient client, Packet packet)
        {
            try
            {
                if (packet == null) return;
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

        private double _timeSinceLastReceive = 0;
        private readonly List<Task> _tasks = new List<Task>();
        // Main loop for the Games Client
        public void Update(GameTime gameTime)
        {
            _timeSinceLastReceive += gameTime.ElapsedGameTime.Milliseconds;
            bool wasRunning = Running;

            // Listen for messages
            if (Running)
            {
                // Check for new packets
                if (_timeSinceLastReceive > 20)
                {
                    _timeSinceLastReceive = 0;
                    _tasks.RemoveAll(a => a.IsCompleted);
                    _tasks.Add(PacketHandler.ReceivePackets(Client, Dispatcher, false));
                }

                _timeSinceLastHeartbeat += gameTime.ElapsedGameTime.Milliseconds;
                if (_timeSinceLastHeartbeat > 5 * 1000)
                {
                    _timeSinceLastHeartbeat = 0;
                    // Make sure that we didn't have a graceless disconnect
                    if (IsDisconnected(Client) && !_clientRequestedDisconnect)
                    {
                        Running = false;
                        Console.WriteLine("The server has disconnected from us ungracefully. :[");
                        // Go back to main menu
                        Game.MainMenuScreen.IsVisible = true;
                        Game.MainMenuScreen.IsFocused = true;
                        SadConsole.Global.CurrentScreen = Game.MainMenuScreen;
                    }
                }
            }

            if (Running) return;

            // Finish tasks if any where still added
            Task.WaitAll(_tasks.ToArray(), 1000);

            // Cleanup
            CleanupNetworkResources();
            if (wasRunning)
                Console.WriteLine("Disconnected from the server.");
        }

        private bool IsDisconnected(TcpClient client)
        {
            try
            {
                SendPacket(client, null);
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
            SendPacket(Client, new Packet("bye"));
        }
    }
}
