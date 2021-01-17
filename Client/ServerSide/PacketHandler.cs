using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bomberman.Client.ServerSide
{
    public static class PacketHandler
    {
        private const int MaxPacketSize = 1024;

        public static async Task SendPacket(TcpClient client, Packet packet, bool server)
        {
            try
            {
                // convert JSON to buffer and its length to a 16 bit unsigned integer buffer
                byte[] jsonBuffer = packet != null ? PacketProtocol.WrapMessage(Encoding.UTF8.GetBytes(packet.ToJson())) : PacketProtocol.WrapKeepaliveMessage();
                Console.WriteLine("Packet size: " + jsonBuffer.Length);
                // Send the packet
                await client.GetStream().WriteAsync(jsonBuffer, 0, jsonBuffer.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine($"There was an issue sending a packet to the {(server ? "Server" : "Client")}.");
                Console.WriteLine("Reason: {0}", e.Message);
                throw;
            }
        }

        private static readonly Dictionary<TcpClient, PacketProtocol> _packetProtocols = new Dictionary<TcpClient, PacketProtocol>();
        public static async Task ReceivePackets(TcpClient client, Action<TcpClient, Packet> action, bool server)
        {
            try
            {
                // First check if there is data available
                if (client.Available == 0)
                    return;

                if (!_packetProtocols.TryGetValue(client, out PacketProtocol packetProtocol))
                {
                    packetProtocol = new PacketProtocol(MaxPacketSize);
                    packetProtocol.MessageArrived += (data) =>
                    {
                        if (data.Length == 0)
                        {
                            action(client, null);
                            return;
                        }

                        // Convert data into a packet datatype
                        string jsonString = Encoding.UTF8.GetString(data);
                        var packet = Packet.FromJson(jsonString);
                        action(client, packet);
                    };
                    Console.WriteLine($"Created packet protocol: [{(server ? "Server" : "Client")}]");
                    _packetProtocols.Add(client, packetProtocol);
                }

                // Read data through protocol
                var stream = client.GetStream();
                while (stream.DataAvailable && stream.CanRead)
                {
                    var readBuffer = new byte[MaxPacketSize];
                    int totalRead = await stream.ReadAsync(readBuffer);
                    packetProtocol.DataReceived(readBuffer, totalRead);
                }

                if (packetProtocol.ContainsReadedData)
                    Console.WriteLine($"Warning. No data left in stream but packet contains readed data [{(server ? "Server" : "Client")}].");
            }
            catch (Exception e)
            {
                // There was an issue in receiving
                Console.WriteLine("There was an issue receiving a packet from the {0} [{1}].", server ? "Server" : "Client", client.Client.RemoteEndPoint);
                Console.WriteLine("Reason: {0}", e.Message);
                throw;
            }
        }

        public static async void RemoveClientPacketProtocol(TcpClient client)
        {
            await Task.Run(async () =>
            {
                if (_packetProtocols.TryGetValue(client, out PacketProtocol packetProtocol))
                {
                    // Allow one second to process packet data
                    if (packetProtocol.ContainsReadedData)
                        await Task.Delay(1000);
                }
                _packetProtocols.Remove(client);
            });
        }
    }
}
