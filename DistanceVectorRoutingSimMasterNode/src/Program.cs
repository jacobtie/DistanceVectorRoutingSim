using System;
using DistanceVectorRoutingSimMasterNode.Master;

namespace DistanceVectorRoutingSimMasterNode
{
    class Program
    {
        static void Main(string[] args)
        {
            MasterNode.RunMaster();

            Console.WriteLine("\nPress enter to exit...");
            Console.ReadLine();
        }
    }
}
