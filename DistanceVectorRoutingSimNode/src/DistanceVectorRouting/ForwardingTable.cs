// This file contains the ForwardingTable class.
// The purpose of this file is the storage of the
// path information to each other node in the 
// network, as well as encoding/decoding this
// information when it is sent/received.

using System;
using System.Collections.Generic;
using System.Text;

namespace DistanceVectorRoutingSimNode.DistanceVectorRouting
{
    internal class ForwardingTable
    {
        // Dictionary to store info about the path to each node
        private readonly Dictionary<string, ForwardingTableRecord> _table;

        // Static method to encode a forwarding table to be sent
        internal static string Encode(ForwardingTable forwardingTable)
        {
            return forwardingTable.Encode();
        }

        // Static method to decode a forwarding table once it is received
        internal static ForwardingTable Decode(string stringifiedForwardingTable)
        {
            // Get each line of the message
            var lines = stringifiedForwardingTable.Split("\r\n")[..^1];

            // Split each line of the message and extract the forwarding table records
            ForwardingTable table = new ForwardingTable();
            foreach (var line in lines)
            {
                var splitLine = line.Split(" ");
                var destination = splitLine[0];
                var pathCost = Double.Parse(splitLine[1]);
                var nextHop = splitLine[2];

                // Upsert the information to the forwarding table
                table.UpsertPath(destination, pathCost, nextHop);
            }

            // Return the new forwarding table
            return table;
        }

        // Constructor for the forwarding table
        internal ForwardingTable()
        {
            // Initialize the forwarding table's dictionary
            _table = new Dictionary<string, ForwardingTableRecord>();
        }

        // Method to get forwarding info pertaining to a particular destination
        internal (double pathCost, string? nextHop) GetForwarding(string destination)
        {
            var record = _table[destination];
            if (record is null)
            {
                throw new ArgumentException("Cannot see requested node");
            }

            return (record.PathCost, record.NextHop);
        }

        // Method to get the path cost pertaining to a particular destination
        internal double GetCost(string destination)
        {
            var record = _table[destination];
            if (record is null)
            {
                throw new ArgumentException("Cannot see requested node");
            }

            return record.PathCost;
        }

        // Method to get the next hop pertaining to a particular destination
        internal string? GetNextHop(string destination)
        {
            var record = _table[destination];
            if (record is null)
            {
                throw new ArgumentException("Cannot see requested node");
            }

            return record.NextHop;
        }

        // Method to check if a particular destination is in the forwarding table
        internal bool Exists(string destination)
        {
            return _table.ContainsKey(destination);
        }

        // Method to update/insert a new entry in the forwarding table
        internal bool UpsertPath(string destination, double cost, string? nextHop)
        {
            if (_table.ContainsKey(destination))
            {
                _table[destination].PathCost = cost;
                _table[destination].NextHop = nextHop;
                return false;
            }

            _table.Add(destination, new ForwardingTableRecord(cost, nextHop));
            return true;
        }

        // Method to get the forwarding table's dictionary
        internal Dictionary<string, ForwardingTableRecord> GetRecords()
        {
            return _table;
        }

        // Method to encode the forwarding table to be sent
        internal string Encode()
        {
            var sb = new StringBuilder();

            foreach (var (destination, record) in _table)
            {
                sb.Append($"{destination} {record.PathCost} {record.NextHop}\r\n");
            }

            return sb.ToString();
        }

        // Method to convert the forwarding table to a string format
        internal string ToString(string source)
        {
            var sb = new StringBuilder();

            foreach (var (destination, record) in _table)
            {
                sb.Append($"shortest path {source}-{destination}: the next hop is {record.NextHop ?? "nil"} and the cost is {record.PathCost}\n");
            }

            return sb.ToString();
        }

        // Method to clone the forwarding table 
        internal ForwardingTable Clone()
        {
            var ft = new ForwardingTable();

            foreach (var (destination, record) in _table)
            {
                ft.UpsertPath(destination, record.PathCost, record.NextHop);
            }

            return ft;
        }
    }
}
