using System;
using System.Net;

namespace DistanceVectorRoutingSimNode.DistanceVectorRouting
{
    public class ForwardingTableRecord
    {
        public double PathCost { get; set; }
        public string? NextHop { get; set; }

        public ForwardingTableRecord(double pathCost, string? nextHop)
        {
            PathCost = pathCost;
            NextHop = nextHop;
        }
    }
}
