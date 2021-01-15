using System;
using System.Configuration;

namespace Server
{
    internal class Program
    {
        public static Network Network;

        // For when the user Presses Ctrl-C, this will gracefully shutdown the server
        public static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            Network?.Shutdown();
        }

        private static void Main()
        {
            Console.WriteLine($"Loading server settings..");

            var serverIp = ConfigurationManager.AppSettings["server-ip"];
            string port = ConfigurationManager.AppSettings["server-port"];
            if (!int.TryParse(port, out int serverPort))
                throw new Exception($"Invalid port ({port}) specified in appSettings config file.");

            // Handler for Ctrl-C presses
            Console.CancelKeyPress += InterruptHandler;

            // Create and run the server
            Network = new Network(serverIp, serverPort, 8);
            Network.Run();

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey(true);
        }
    }
}
