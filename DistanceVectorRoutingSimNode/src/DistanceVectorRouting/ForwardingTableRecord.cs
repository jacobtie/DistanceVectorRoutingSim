// This file contains the ForwardingTableRecord class.
// The purpose of this class is to contain information
// from the forwarding table about the path to a 
// particular node in a compact format.

using System;
using System.Net;

namespace DistanceVectorRoutingSimNode.DistanceVectorRouting
{
    public class ForwardingTableRecord
    {
        // Fields for the path cost and next hop
        public double PathCost { get; set; }
        public string? NextHop { get; set; }

        // Constructor for the forwarding table record
        public ForwardingTableRecord(double pathCost, string? nextHop)
        {
            PathCost = pathCost;
            NextHop = nextHop;
        }
    }
}
