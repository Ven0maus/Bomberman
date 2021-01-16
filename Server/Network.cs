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

        private const float HeartbeatInterval = 10f;

        private readonly string _serverIp;
        private readonly int _serverPort;
        private readonly TcpListener _listener;
        private readonly List<TcpClient> _clients;
        private Dictionary<TcpClient, PlayerContext> _players;
        private readonly int _maxPlayers;

        private readonly System.Timers.Timer _heartbeatTimer;
        private bool _running;
        private GameLogic.Game _game;

        private readonly Dictionary<TcpClient, HeartbeatCheck> _timeSinceLastHeartbeat;

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
            _clients = new List<TcpClient>();
            _timeSinceLastHeartbeat = new Dictionary<TcpClient, HeartbeatCheck>();
            _listener = new TcpListener(IPAddress.Parse(serverIp), serverPort);

            _heartbeatTimer = new System.Timers.Timer(1000);
            _heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
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
                if (!_clients.Contains(client.Key)) continue;
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
                if (_listener.Pending())
                {
                    tasks.Add(HandleNewConnection());
                }

                // Check game logic steps
                foreach (var client in _clients.ToList())
                {
                    try
                    {
                        tasks.Add(PacketHandler.ReceivePackets(client, HandlePacket, true));
                    }
                    catch (SocketException)
                    {
                        HandleDisconnectedClient(client);
                    }
                    catch (IOException)
                    {
                        HandleDisconnectedClient(client);
                    }
                }

                tasks.RemoveAll(a => a.IsCompleted);

                // Take a small nap
                Thread.Sleep(10);
            }

            // Allow tasks to finish
            Task.WaitAll(tasks.ToArray(), 1000);

            // Disconnect any clients still here
            Parallel.ForEach(_clients, (client) =>
            {
                DisconnectClient(client, "The server is being shutdown.");
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
            _clients.Add(newClient);

            // Add client
            _timeSinceLastHeartbeat.Add(newClient, new HeartbeatCheck(newClient));

            if (_clients.Any() && _game == null)
                _game = new GameLogic.Game(_clients, out _players);
            else if (_game != null)
                _game.AddPlayer(newClient);

            // Send a welcome message
            string msg = "Welcome to the Bomberman Server.";
            SendPacket(newClient, new Packet("message", msg));
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
            if (!_clients.Contains(client)) return;

            Console.WriteLine("Client lost connection.");

            // Remove from collections and free resources
            _timeSinceLastHeartbeat.Remove(client);
            _clients.Remove(client);
            _players?.Remove(client);
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
