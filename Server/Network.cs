using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
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
        private const float HeartbeatInterval = 10f;

        private readonly string _serverIp;
        private readonly int _serverPort;
        private readonly TcpListener _listener;
        private readonly List<TcpClient> _clients;
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
                SendHeartbeat(_client).GetAwaiter().GetResult();
            }

            public void Reset()
            {
                HasBeenSend = false;
                Time = DateTime.Now;
            }

            private async Task SendHeartbeat(TcpClient client)
            {
                await PacketHandler.SendPacket(client, new Packet("heartbeat", "Are you alive?"));

                // Give client time to process
                Thread.Sleep(50);
            }
        }

        public Network(string serverIp, int serverPort, int maxPlayers)
        {
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

        private void HeartbeatTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!_running)
            {
                _heartbeatTimer.Stop();
                return;
            }

            foreach (var client in _timeSinceLastHeartbeat)
            {
                try
                {
                    if (!_clients.Contains(client.Key)) continue;
                    var heartbeatCheck = _timeSinceLastHeartbeat[client.Key];
                    if (heartbeatCheck.Time.AddMilliseconds(HeartbeatInterval * 1000) <= DateTime.Now)
                    {
                        if (!heartbeatCheck.HasBeenSend)
                            heartbeatCheck.Send();
                        else
                            HandleDisconnectedClient(client.Key);
                    }
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
                foreach (var client in _clients)
                {
                    try
                    {
                        tasks.Add(PacketHandler.ReceivePackets(client, HandlePacket));
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
                        HandleHeartbeatPacket(client, packet);
                        break;
                    case "move":
                        var coords = packet.Message.Split(':');
                        var position = new Point(int.Parse(coords[0]), int.Parse(coords[1]));
                        _game?.Move(client, position);
                        break;
                    default:
                        Console.WriteLine("Unhandled packet: " + packet.ToString());
                        break;
                }
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

        private void HandleHeartbeatPacket(TcpClient client, Packet packet)
        {
            Console.WriteLine("Received heartbeat response: " + packet.Message);
            _timeSinceLastHeartbeat[client].Reset();
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
                _game = new GameLogic.Game(_clients);
            else if (_game != null)
                _game.AddPlayer(newClient);

            // Send a welcome message
            string msg = "Welcome to the Bomberman Server.";

            try
            {
                await PacketHandler.SendPacket(newClient, new Packet("message", msg));
            }
            catch(SocketException)
            {
                HandleDisconnectedClient(newClient);
            }
            catch (IOException)
            {
                HandleDisconnectedClient(newClient);
            }
        }

        // Will attempt to gracefully disconnect a TcpClient
        // This should be use for clients that may be in a game
        public void DisconnectClient(TcpClient client, string message = "")
        {
            Console.WriteLine("Disconnecting the client from {0}.", client.Client.RemoteEndPoint);

            if (message == "")
                message = "Goodbye.";

            try
            {
                // Send the "bye," message
                var byePacket = PacketHandler.SendPacket(client, new Packet("bye", message));
                Thread.Sleep(100);
                byePacket.GetAwaiter().GetResult();
            }
            catch(IOException)
            {

            }
            catch (SocketException)
            {

            }

            // Cleanup resources on our end
            HandleDisconnectedClient(client);
        }

        // Cleans up the resources if a client has disconnected,
        // gracefully or not.  Will remove them from clint list and lobby
        public void HandleDisconnectedClient(TcpClient client)
        {
            Console.WriteLine("Client lost connection from {0}.", client.Client.RemoteEndPoint);

            // Remove from collections and free resources
            _timeSinceLastHeartbeat.Remove(client);
            _clients.Remove(client);
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
