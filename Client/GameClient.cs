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
        public readonly List<Player> OtherPlayers;

        public bool Connected { get { return Client.Connected; } }
        public bool Running { get; private set; }

        private bool _clientRequestedDisconnect = false;

        private readonly Dictionary<string, Func<string, Task>> _commandHandlers;

        private double _timeSinceLastHeartbeat = 0f;

        public GameClient(string serverIp, int serverPort, string playerName)
        {
            Client = new TcpClient
            {
                NoDelay = true,
            };
            Client.Client.NoDelay = true;
            Client.ReceiveBufferSize = 250;
            PlayerName = playerName;
            _serverIp = serverIp;
            _serverPort = serverPort;
            OtherPlayers = new List<Player>();
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

                Game.MainMenuScreen.ServerConnectionScreen.IsFocused = false;
                Game.MainMenuScreen.ServerConnectionScreen.IsVisible = false;

                // Hook up some packet command handlers
                _commandHandlers["bye"] = HandleBye;
                _commandHandlers["joinwaitinglobby"] = HandleJoinWaitingLobby;
                _commandHandlers["removefromwaitinglobby"] = HandleRemoveFromWaitingLobby;
                _commandHandlers["message"] = HandleMessage;
                _commandHandlers["heartbeat"] = HandleHeartbeat;
                _commandHandlers["moveleft"] = HandleMovementLeft;
                _commandHandlers["moveright"] = HandleMovementRight;
                _commandHandlers["moveup"] = HandleMovementUp;
                _commandHandlers["movedown"] = HandleMovementDown;
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
                _commandHandlers["playerdied"] = HandlePlayerDied;
                _commandHandlers["gameover"] = HandleGameOver;
                _commandHandlers["invincibility"] = HandleInvincibilityPowerup;
                _commandHandlers["showplayers"] = HandleShowPlayers;

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

        private Task HandleShowPlayers(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                var players = new List<Player>
                {
                    _player
                };
                players.AddRange(OtherPlayers);

                // Show all players with default value for stats
                foreach (var player in players)
                {
                    Game.GridScreen.ShowStats(player.Id);
                }
            }
            else
            {
                var data = message.Split(':');
                var playerId = int.Parse(data[0]);
                var kills = int.Parse(data[1]);
                int? bombs = null, strength = null;
                if (data.Length > 2)
                    bombs = int.Parse(data[2]);
                if (data.Length > 3)
                    strength = int.Parse(data[3]);
                Game.GridScreen.ShowStats(playerId, kills, bombs, strength);
            }
            return Task.CompletedTask;
        }

        private Task HandleMovementDown(string message)
        {
            if (message == "bad entry")
            {
                _player.RequestedMovement = false;
                return Task.CompletedTask;
            }

            Player player;
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (!int.TryParse(message, out int id))
                {
                    Console.WriteLine("Could not parse: " + message);
                    return Task.CompletedTask;
                }

                player = GetPlayerById(id);
                if (player == null) return Task.CompletedTask;
            }
            else
            {
                player = _player;
            }

            player.RequestedMovement = false;
            player.Position += new Point(0, 1);
            return Task.CompletedTask;
        }

        private Task HandleMovementUp(string message)
        {
            if (message == "bad entry")
            {
                _player.RequestedMovement = false;
                return Task.CompletedTask;
            }

            Player player;
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (!int.TryParse(message, out int id))
                {
                    Console.WriteLine("Could not parse: " + message);
                    return Task.CompletedTask;
                }

                player = GetPlayerById(id);
                if (player == null) return Task.CompletedTask;
            }
            else
            {
                player = _player;
            }

            player.RequestedMovement = false;
            player.Position += new Point(0, -1);
            return Task.CompletedTask;
        }

        private Task HandleMovementRight(string message)
        {
            if (message == "bad entry")
            {
                _player.RequestedMovement = false;
                return Task.CompletedTask;
            }

            Player player;
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (!int.TryParse(message, out int id))
                {
                    Console.WriteLine("Could not parse: " + message);
                    return Task.CompletedTask;
                }

                player = GetPlayerById(id);
                if (player == null) return Task.CompletedTask;
            }
            else
            {
                player = _player;
            }

            player.RequestedMovement = false;
            player.Position += new Point(1, 0);
            return Task.CompletedTask;
        }

        private Task HandleMovementLeft(string message)
        {
            if (message == "bad entry")
            {
                _player.RequestedMovement = false;
                return Task.CompletedTask;
            }

            Player player;
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (!int.TryParse(message, out int id))
                {
                    Console.WriteLine("Could not parse: " + message);
                    return Task.CompletedTask;
                }

                player = GetPlayerById(id);
                if (player == null) return Task.CompletedTask;
            }
            else
            {
                player = _player;
            }

            player.RequestedMovement = false;
            player.Position += new Point(-1, 0);
            return Task.CompletedTask;
        }

        public Player GetPlayerById(int id)
        {
            var player = OtherPlayers.FirstOrDefault(a => a.Id == id);
            if (player == null)
            {
                if (_player != null && _player.Id == id)
                    player = _player;

                if (player == null)
                {
                    Console.WriteLine("Invalid player id supplied (" + id + "), or the game ended.");
                }
            }
            return player;
        }

        private Task HandleInvincibilityPowerup(string message)
        {
            var data = message.Split(':');
            var player = GetPlayerById(int.Parse(data[1]));
            if (player == null) return Task.CompletedTask;
            if (data[0] == "start")
            {
                player.StartBlinkingAnimation();
            }
            else
            {
                player.StopBlinkingAnimation();
            }
            return Task.CompletedTask;
        }

        private Task HandleGameOver(string arg)
        {
            foreach (var p in OtherPlayers)
            {
                p.IsVisible = false;
                p.Parent = null;
            }
            OtherPlayers.Clear();
            _player.IsVisible = false;
            _player.IsFocused = false;
            _player.Parent = null;
            _player = null;
            foreach (var bomb in _bombsPlaced)
            {
                bomb.IsVisible = false;
                bomb.Parent = null;
            }
            _bombsPlaced.Clear();
            Game.ClientWaitingLobby.ClearError();
            Game.GridScreen = null;
            return Task.CompletedTask;
        }

        private Task HandlePlayerDied(string message)
        {
            var player = GetPlayerById(int.Parse(message));
            if (player == null) return Task.CompletedTask;
            player.StartDeadAnimation();
            return Task.CompletedTask;
        }

        private Task HandleGameCountdown(string message)
        {
            var time = int.Parse(message);
            if (time > 0)
                Game.ClientWaitingLobby.StartCountdown(time);
            else
                Game.ClientWaitingLobby.StopCountdown();
            return Task.CompletedTask;
        }

        private Task HandleUnReady(string message)
        {
            if (Game.GridScreen == null)
                Game.ClientWaitingLobby.SetReady(message, false);
            return Task.CompletedTask;
        }

        private Task HandleReady(string message)
        {
            if (Game.GridScreen == null)
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
            Game.ClientWaitingLobby.StopCountdown();
            Game.InitializeGameScreen(true);
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
            var data = message.Split(',');

            foreach (var entry in data)
            {
                int bombId = int.Parse(entry);
                var bomb = _bombsPlaced.FirstOrDefault(a => a.Id == bombId);

                if (bomb != null)
                {
                    bomb.CleanupFireAfter();
                    _bombsPlaced.Remove(bomb);
                }
            }

            return Task.CompletedTask;
        }

        private Task HandleDetonationPhase1(string message)
        {
            var bombIds = message.Split(',');
            var ids = bombIds.Select(int.Parse);
            
            foreach (var bombId in ids)
            {
                var bomb = _bombsPlaced.FirstOrDefault(a => a.Id == bombId);
                if (bomb != null)
                {
                    bomb.Detonate();
                }
            }

            return Task.CompletedTask;
        }

        private Task HandleBombPlacementOther(string message)
        {
            var coords = message.Split(':');
            var position = new Point(int.Parse(coords[0]), int.Parse(coords[1]));
            var player = OtherPlayers.FirstOrDefault(a => a.Id == int.Parse(coords[4]));
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

        private Task HandleOtherInstantiation(string message)
        {
            var coords = message.Split(':');
            var position = new Point(int.Parse(coords[1]), int.Parse(coords[2]));
            var playerName = coords[3];
            var color = new Color(byte.Parse(coords[4]), byte.Parse(coords[5]), byte.Parse(coords[6]));
            var player = new Player(position, int.Parse(coords[0]), color, false)
            {
                Parent = Game.GridScreen,
                Name = playerName
            };
            // Make darker shade in the beginning
            player.Animation[0].Foreground = Color.Lerp(player.Color, Color.Black, 0.85f);
            OtherPlayers.Add(player);
            return Task.CompletedTask;
        }

        private Task HandlePlayerInstantiation(string message)
        {
            var coords = message.Split(':');
            var position = new Point(int.Parse(coords[1]), int.Parse(coords[2]));
            var color = new Color(byte.Parse(coords[3]), byte.Parse(coords[4]), byte.Parse(coords[5]));
            _player = new Player(position, int.Parse(coords[0]), color, true)
            {
                Parent = Game.GridScreen,
                Name = PlayerName
            };
            Game.GridScreen.Grid.UncoverTilesFromDarkness(_player.Position);
            return Task.CompletedTask;
        }

        private Task HandleBye(string message)
        {
            // Print the message
            Console.WriteLine("The server is disconnecting us with this message:");
            Console.WriteLine(message);
            if (SadConsole.Global.CurrentScreen is IErrorLogger logger)
                logger.ShowError(message);

            // Will start the disconnection process in Run()
            Running = false;
            // Go back to main menu
            Game.MainMenuScreen.ServerConnectionScreen.IsVisible = true;
            Game.MainMenuScreen.ServerConnectionScreen.IsFocused = true;
            SadConsole.Global.CurrentScreen = Game.MainMenuScreen.ServerConnectionScreen;
            return Task.CompletedTask; 
        }

        // Just prints out a message sent from the server
        private Task HandleMessage(string message)
        {
            Console.WriteLine(message);
            if (SadConsole.Global.CurrentScreen is IErrorLogger logger)
                logger.ShowError(message);
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
                if (!Packet.ReadableOpCodes.TryGetValue(packet.OpCode, out string readableOpCode))
                    return;
                if (_commandHandlers.ContainsKey(readableOpCode))
                    _commandHandlers[readableOpCode](packet.Arguments).GetAwaiter().GetResult();
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

        private readonly List<Task> _tasks = new List<Task>();
        // Main loop for the Games Client
        public void Update(GameTime gameTime)
        {
            bool wasRunning = Running;

            // Listen for messages
            if (Running)
            {
                // Check for new packets
                _tasks.RemoveAll(a => a.IsCompleted);
                _tasks.Add(PacketHandler.ReceivePackets(Client, Dispatcher, false));

                _timeSinceLastHeartbeat += gameTime.ElapsedGameTime.Milliseconds;
                if (_timeSinceLastHeartbeat > 5000)
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
            Game.Client = null;
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
