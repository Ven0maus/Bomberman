using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using Server.GameLogic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public class Network
    {
        public static Network Instance { get; private set; }

        private const float HeartbeatInterval = 2f;
        private const float GameStartCountdown = 120f;

        private readonly string _serverIp;
        private readonly int _serverPort;
        private readonly TcpListener _listener;
        private readonly Dictionary<TcpClient, string> _clients;
        private Dictionary<TcpClient, PlayerContext> _players;
        public readonly Dictionary<TcpClient, bool> WaitingLobby;
        private readonly int _maxPlayers;

        private readonly System.Timers.Timer _heartbeatTimer;
        private bool _running;
        private GameLogic.Game _game;

        private readonly System.Timers.Timer _gameStartTimer;

        private readonly Dictionary<TcpClient, HeartbeatCheck> _timeSinceLastHeartbeat;

        public bool GameOngoing { get { return _game != null; } }

        private class HeartbeatCheck
        {
            public DateTime Time { get; private set; }
            public bool HasBeenSend { get; private set; }

            private readonly TcpClient _client;

            public HeartbeatCheck(TcpClient client)
            {
                _client = client;
                Reset();
            }

            public void Send()
            {
                HasBeenSend = true;
                Time = DateTime.Now;
                SendHeartbeat(_client);
            }

            public void Reset()
            {
                HasBeenSend = false;
                Time = DateTime.Now;
            }

            private void SendHeartbeat(TcpClient client)
            {
                Instance.SendPacket(client, new Packet("heartbeat", "Are you alive?"));
            }
        }

        public async void SendPacket(TcpClient client, Packet packet)
        {
            try
            {
                await PacketHandler.SendPacket(client, packet, true);
            }
            catch (ObjectDisposedException)
            {
                HandleDisconnectedClient(client);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: " + e.ToString());
                HandleDisconnectedClient(client);
            }
            catch (IOException e)
            {
                Console.WriteLine("IOException: " + e.ToString());
                HandleDisconnectedClient(client);
            }
        }

        public Network(string serverIp, int serverPort, int maxPlayers)
        {
            Instance = this;

            _serverIp = serverIp;
            _serverPort = serverPort;
            _maxPlayers = maxPlayers;
            _running = false;
            _clients = new Dictionary<TcpClient, string>();
            _timeSinceLastHeartbeat = new Dictionary<TcpClient, HeartbeatCheck>();
            _listener = new TcpListener(IPAddress.Parse(serverIp), serverPort);
            WaitingLobby = new Dictionary<TcpClient, bool>();

            // Countdown timer for game start once 2 or more players are ready
            _gameStartTimer = new System.Timers.Timer(GameStartCountdown * 1000);
            _gameStartTimer.Elapsed += GameStartTimer_Elapsed;

            _heartbeatTimer = new System.Timers.Timer(1000);
            _heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
        }

        private void GameStartTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            StartGame();
        }

        private void StartGame()
        {
            var readyClients = WaitingLobby
                .Where(a => a.Value)
                .Select(a => a.Key)
                .ToList();
            var clients = _clients
                .Where(a => readyClients.Contains(a.Key))
                .ToDictionary(a => a.Key, a => a.Value);
            _game = new GameLogic.Game(clients, out _players);
        }

        private readonly List<TcpClient> _clientsToRemoveFromHeartbeatMonitoring = new List<TcpClient>();
        private void HeartbeatTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!_running)
            {
                _heartbeatTimer.Stop();
                return;
            }

            foreach (var client in _timeSinceLastHeartbeat)
            {
                if (!_clients.ContainsKey(client.Key)) continue;
                var heartbeatCheck = _timeSinceLastHeartbeat[client.Key];
                if (heartbeatCheck.Time.AddMilliseconds(HeartbeatInterval * 1000) <= DateTime.Now)
                {
                    if (!heartbeatCheck.HasBeenSend)
                        heartbeatCheck.Send();
                    else
                    {
                        Console.WriteLine("Client [" + client.Key.Client.RemoteEndPoint + "] did not respond to heartbeat check in time, closing down client resources.");
                        _clientsToRemoveFromHeartbeatMonitoring.Add(client.Key);
                    }
                }
            }

            foreach (var client in _clientsToRemoveFromHeartbeatMonitoring)
            {
                HandleDisconnectedClient(client);
            }
            _clientsToRemoveFromHeartbeatMonitoring.Clear();
        }

        public void Shutdown()
        {
            if (_running)
            {
                _running = false;
                Console.WriteLine("Shutting down server..");
            }
        }

        public void Run()
        {
            Console.WriteLine($"Starting server on {_serverIp}:{_serverPort}.");
            Console.WriteLine("Press Ctrl-C to shutdown the server at any time.");

            _listener.Start();
            _running = true;

            Console.WriteLine($"Server is online and waiting for incoming connections.");

            // Start heartbeat timer
            _heartbeatTimer.Start();

            var tasks = new List<Task>();
            while (_running)
            {
                tasks.RemoveAll(a => a.IsCompleted);

                if (_listener.Pending())
                {
                    tasks.Add(HandleNewConnection());
                }

                // Check game logic steps
                foreach (var client in _clients.ToList())
                {
                    try
                    {
                        tasks.Add(PacketHandler.ReceivePackets(client.Key, HandlePacket, true));
                    }
                    catch (SocketException)
                    {
                        HandleDisconnectedClient(client.Key);
                    }
                    catch (IOException)
                    {
                        HandleDisconnectedClient(client.Key);
                    }
                }

                // Take a small nap
                Thread.Sleep(10);
            }

            // Allow tasks to finish
            Task.WaitAll(tasks.ToArray(), 1000);

            // Disconnect any clients still here
            Parallel.ForEach(_clients, (client) =>
            {
                DisconnectClient(client.Key, "The server is being shutdown.");
            });

            // Cleanup our resources
            _listener.Stop();

            // Info
            Console.WriteLine("The server has been shut down.");
        }

        private void HandlePacket(TcpClient client, Packet packet)
        {
            if (packet == null) return;

            try
            {
                switch (packet.Command)
                {
                    case "heartbeat":
                        Console.WriteLine("Received heartbeat response: " + packet.Message);
                        break;
                    case "move":
                        var coords = packet.Message.Split(':');
                        var position = new Point(int.Parse(coords[0]), int.Parse(coords[1]));
                        _game?.Move(client, position);
                        break;
                    case "placebomb":
                        _game?.PlaceBomb(client);
                        Console.WriteLine("Client attempted to place a bomb.");
                        break;
                    case "bye":
                        HandleDisconnectedClient(client);
                        break;
                    case "playername":
                        string playerName = packet.Message;

                        // Sanity check if name already exists
                        if (_clients.Any(a => a.Value != null && a.Value.Equals(playerName, StringComparison.OrdinalIgnoreCase)))
                        {
                            DisconnectClient(client, $"Name: [{playerName}] is already taken.");
                            return;
                        }

                        if (_clients.ContainsKey(client))
                            _clients[client] = playerName;

                        // Send a welcome message
                        string msg = $"Welcome to the Bomberman Server {playerName}.";
                        SendPacket(client, new Packet("message", msg));

                        WaitingLobby.Add(client, false);

                        // Add all clients in waiting lobby to the client's lobby
                        foreach (var c in _clients.Where(a => a.Value != null))
                            SendPacket(client, new Packet("joinwaitinglobby", c.Value));

                        // Tell other clients that we joined the waiting lobby
                        foreach (var c in _clients.Where(a => a.Value != null && a.Key != client))
                        {
                            SendPacket(c.Key, new Packet("joinwaitinglobby", playerName));
                        }
                        break;
                    case "ready":
                        if (!WaitingLobby.ContainsKey(client))
                        {
                            Console.WriteLine("Received a ready response but client was not in waiting lobby.");
                            return;
                        }

                        var ready = packet.Message.Equals("1");
                        WaitingLobby[client] = ready;

                        // Let clients know the client has readied/unreadied
                        foreach (var c in WaitingLobby.Where(a => a.Key != client))
                        {
                            SendPacket(c.Key, new Packet(ready ? "ready" : "unready", _clients[client]));
                        }

                        // If a game is already ongoing, we don't need to overwrite the current with new players
                        if (GameOngoing) return;

                        // Check if more than one client is ready then start the countdown
                        // Else stop the countdown and reset it
                        // If all clients are ready, start game instantly.
                        if (WaitingLobby.Count(a => a.Value) == WaitingLobby.Count)
                        {
                            _gameStartTimer.Stop();
                            StartGame();
                        }
                        else if (WaitingLobby.Count(a => a.Value) >= 2)
                        {
                            _gameStartTimer.Start();
                            // TODO: Let clients know timer has started
                        }
                        else
                        {
                            _gameStartTimer.Stop();
                        }
                        break;
                    default:
                        Console.WriteLine("Unhandled packet: " + packet.ToString());
                        break;
                }

                // Reset heartbeat when we receive something
                _timeSinceLastHeartbeat[client].Reset();
            }
            catch(SocketException)
            {
                HandleDisconnectedClient(client);
            }
            catch(IOException)
            {
                HandleDisconnectedClient(client);
            }
        }

        // Awaits for a new connection and then adds them to the waiting lobby
        private async Task HandleNewConnection()
        {
            // Get the new client using a Future
            TcpClient newClient = await _listener.AcceptTcpClientAsync();
            Console.WriteLine("New connection from {0}.", newClient.Client.RemoteEndPoint);

            // Disconnect client because server is full.
            if (_clients.Count == _maxPlayers)
            {
                DisconnectClient(newClient, "Server is full.");
                return;
            }

            // Store them and put them in the game
            _clients.Add(newClient, null);

            // Add client
            _timeSinceLastHeartbeat.Add(newClient, new HeartbeatCheck(newClient));
        }

        // Will attempt to gracefully disconnect a TcpClient
        // This should be use for clients that may be in a game
        public void DisconnectClient(TcpClient client, string message = "")
        {
            Console.WriteLine("Disconnecting the client from {0}.", client.Client.RemoteEndPoint);

            if (message == "")
                message = "Goodbye.";

            SendPacket(client, new Packet("bye", message));

            // Let packed be processed by client
            Thread.Sleep(50);

            // Cleanup resources on our end
            HandleDisconnectedClient(client);
        }

        // Cleans up the resources if a client has disconnected,
        // gracefully or not.  Will remove them from clint list and lobby
        public void HandleDisconnectedClient(TcpClient client)
        {
            // We already handled this client, this call came from lingering packets
            if (!_clients.ContainsKey(client)) return;

            Console.WriteLine("Client disconnected.");

            // First notify other waiting lobby clients if this one is still in waiting lobby
            if (WaitingLobby.ContainsKey(client))
            {
                var newLobby = WaitingLobby.Where(a => a.Key != client).ToList();
                if (newLobby.Count(a => a.Value) == newLobby.Count)
                {
                    StartGame();
                }
                else if (WaitingLobby.Count(a => a.Value) < 2)
                {
                    _gameStartTimer.Stop();
                }

                foreach (var c in WaitingLobby)
                {
                    if (c.Key != client)
                        SendPacket(c.Key, new Packet("removefromwaitinglobby", _clients[client]));
                }
            }

            // Remove from collections and free resources
            _timeSinceLastHeartbeat.Remove(client);
            _clients.Remove(client);
            _players?.Remove(client);
            WaitingLobby.Remove(client);
            PacketHandler.RemoveClientPacketProtocol(client);
            CleanupClient(client);
        }

        // cleans up resources for a TcpClient and closes it
        private static void CleanupClient(TcpClient client)
        {
            if (client.Connected)
                client.GetStream().Close();     // Close network stream
            client.Close();                 // Close client
        }
    }
}
