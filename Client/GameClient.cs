using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bomberman.Client
{
    public class GameClient
    {
        private readonly TcpClient _client;
        private readonly string _serverIp;
        private readonly int _serverPort;

        public bool Connected { get { return _client.Connected; } }
        public bool Running { get; private set; }

        private bool _clientRequestedDisconnect = false;

        private readonly Dictionary<string, Func<string, Task>> _commandHandlers;

        private double _timeSinceLastHeartbeat = 0f;

        public GameClient(string serverIp, int serverPort)
        {
            _client = new TcpClient();
            _serverIp = serverIp;
            _serverPort = serverPort;
            _commandHandlers = new Dictionary<string, Func<string, Task>>();
        }

        private void CleanupNetworkResources()
        {
            if (_client.Connected)
                _client.GetStream().Close();
            _client.Close();
        }

        public bool Connect()
        {
            // Connect to the server
            try
            {
                _client.Connect(_serverIp, _serverPort);
            }
            catch (SocketException se)
            {
                Console.WriteLine("[ERROR] {0}", se.Message);
            }

            // check that we've connected
            if (_client.Connected)
            {
                // Connected!
                Console.WriteLine("Connected to the server at {0}.", _client.Client.RemoteEndPoint);
                Running = true;

                // Hook up some packet command handlers
                _commandHandlers["bye"] = HandleBye;
                _commandHandlers["message"] = HandleMessage;
                _commandHandlers["heartbeat"] = HandleHeartbeat;

                return true;
            }
            else
            {
                CleanupNetworkResources();
                Console.WriteLine($"Wasn't able to connect to the server at {_serverIp}:{_serverPort}.");
            }

            return false;
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
            await PacketHandler.SendPacket(_client, new Packet("heartbeat", "yes"));
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
                if (IsDisconnected(_client) && !_clientRequestedDisconnect)
                {
                    Running = false;
                    Console.WriteLine("The server has disconnected from us ungracefully. :[");
                }
            }
            catch (IOException)
            {
                // Make sure that we didn't have a graceless disconnect
                if (IsDisconnected(_client) && !_clientRequestedDisconnect)
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
                tasks.Add(PacketHandler.ReceivePackets(_client, Dispatcher));
                tasks.RemoveAll(a => a.IsCompleted);

                _timeSinceLastHeartbeat += gameTime.ElapsedGameTime.Milliseconds;
                if (_timeSinceLastHeartbeat > 5 * 1000)
                {
                    _timeSinceLastHeartbeat = 0;
                    // Make sure that we didn't have a graceless disconnect
                    if (IsDisconnected(_client) && !_clientRequestedDisconnect)
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
            PacketHandler.SendPacket(_client, new Packet("bye")).GetAwaiter().GetResult();
        }
    }
}
