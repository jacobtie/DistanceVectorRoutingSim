using System;
using System.Net;
using DistanceVectorRoutingSimNode.DistanceVectorRouting;

namespace DistanceVectorRoutingSimNode
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 4)
                {
                    var name = args[0];
                    var nodePort = Int32.Parse(args[1]);
                    var ip = IPAddress.Parse(args[2]);
                    var masterPort = Int32.Parse(args[3]);

                    NetworkNode.RunNode(name, nodePort, new IPEndPoint(ip, masterPort));
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

            Console.WriteLine("\nPress enter to exit...");
            Console.ReadLine();
        }
    }
}
