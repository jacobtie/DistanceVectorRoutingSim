using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace DistanceVectorRoutingSimNode.DistanceVectorRouting
{
    public class ForwardingTable
    {
        private readonly Dictionary<string, ForwardingTableRecord> _table;

        public static string Encode(ForwardingTable forwardingTable)
        {
            return forwardingTable.Encode();
        }

        public static ForwardingTable? Decode(string stringifiedForwardingTable)
        {
            var lines = stringifiedForwardingTable.Split("\r\n")[..^1];

            ForwardingTable table = new ForwardingTable();
            foreach (var line in lines)
            {
                var splitLine = line.Split(" ");
                var destination = splitLine[0];
                var pathCost = Double.Parse(splitLine[1]);
                var nextHop = splitLine[2];
                table.UpsertPath(destination, pathCost, nextHop);
            }
            return table;
        }

        public ForwardingTable()
        {
            _table = new Dictionary<string, ForwardingTableRecord>();
        }

        public (double pathCost, string? nextHop) GetForwarding(string destination)
        {
            var record = _table[destination];
            if (record is null)
            {
                throw new ArgumentException("Cannot see requested node");
            }

            return (record.PathCost, record.NextHop);
        }

        public double GetCost(string destination)
        {
            var record = _table[destination];
            if (record is null)
            {
                throw new ArgumentException("Cannot see requested node");
            }

            return record.PathCost;
        }

        public string? GetNextHop(string destination)
        {
            var record = _table[destination];
            if (record is null)
            {
                throw new ArgumentException("Cannot see requested node");
            }

            return record.NextHop;
        }

        public bool Exists(string destination)
        {
            return _table.ContainsKey(destination);
        }

        public bool UpsertPath(string destination, double cost, string? nextHop)
        {
            if (_table.ContainsKey(destination))
            {
                _table[destination].PathCost = cost;
                _table[destination].NextHop = nextHop;
                return false;
            }
            else
            {
                _table.Add(destination, new ForwardingTableRecord(cost, nextHop));
            }

            return true;
        }

        public Dictionary<string, ForwardingTableRecord> GetRecords()
        {
            return _table;
        }

        public string Encode()
        {
            var sb = new StringBuilder();

            foreach (var (destination, record) in _table)
            {
                sb.Append($"{destination} {record.PathCost} {record.NextHop}\r\n");
            }

            return sb.ToString();
        }

        public string ToString(string source)
        {
            var sb = new StringBuilder();

            foreach (var (destination, record) in _table)
            {
                sb.Append($"shortest path {source}-{destination}: the next hop is {record.NextHop ?? "nil"} and the cost is {record.PathCost}\n");
            }

            return sb.ToString();
        }
    }
}
