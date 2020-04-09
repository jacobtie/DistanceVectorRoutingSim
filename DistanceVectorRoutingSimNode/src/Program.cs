using System;
using System.Net;
using DistanceVectorRoutingSimNode.DistanceVectorRouting;

namespace DistanceVectorRoutingSimNode
{
    class Program
    {
        static void Main(string[] args)
        {
            string name = null!;
            int nodePort = 0;
            IPAddress ip = null!;
            int masterPort = 0;

            bool valid = false;

            try
            {
                if (args.Length == 4)
                {
                    name = args[0];
                    nodePort = Int32.Parse(args[1]);
                    ip = IPAddress.Parse(args[2]);
                    masterPort = Int32.Parse(args[3]);

                    valid = true;
                }
                else
                {
                    Console.WriteLine("Usage: dotnet run <node_name> <master_ip> <master_port>");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Usage: dotnet run <node_name> <node_port> <master_ip> <master_port>");
            }

            if (valid)
            {
                NetworkNode.RunNode(name, nodePort, new IPEndPoint(ip, masterPort));
            }

            Console.WriteLine("\nPress enter to exit...");
            Console.ReadLine();
        }
    }
}
