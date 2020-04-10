using System;
using DistanceVectorRoutingSimMasterNode.Master;

namespace DistanceVectorRoutingSimMasterNode
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 1 && Int32.TryParse(args[0], out int port))
            {
                // Run master node
                MasterNode.RunMaster(port);
            }
            else
            {
                Console.WriteLine("Usage: dotnet run <port>");
            }

            Console.WriteLine("\nPress enter to exit...");
            Console.ReadLine();
        }
    }
}
