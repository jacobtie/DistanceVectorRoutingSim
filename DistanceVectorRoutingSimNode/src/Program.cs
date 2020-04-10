using System;
using System.Net;
using DistanceVectorRoutingSimNode.DistanceVectorRouting;

namespace DistanceVectorRoutingSimNode
{
    class Program
    {

        // Main method to parse the arguments and run the node
        static void Main(string[] args)
        {
            // Create variables for the arguments
            string name = null!;
            int nodePort = 0;
            IPAddress ip = null!;
            int masterPort = 0;
            bool valid = false;

            // Try to parse the arguments
            try
            {
                // If there are four arguments
                if (args.Length == 4)
                {
                    // Parse the arguments
                    name = args[0];
                    nodePort = Int32.Parse(args[1]);
                    ip = IPAddress.Parse(args[2]);
                    masterPort = Int32.Parse(args[3]);

                    // Signal that the arguments are valid
                    valid = true;
                }
                else
                {
                    Console.WriteLine("Usage: dotnet run <node_name> <node_port> <master_ip> <master_port>");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Usage: dotnet run <node_name> <node_port> <master_ip> <master_port>");
            }

            // If the arguments were valid
            if (valid)
            {
                // Run the node with the given arguments
                NetworkNode.RunNode(name, nodePort, new IPEndPoint(ip, masterPort));
            }

            Console.WriteLine("\nPress enter to exit...");
            Console.ReadLine();
        }
    }
}
