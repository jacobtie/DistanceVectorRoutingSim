using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DistanceVectorRoutingSimNode.Logging;

namespace DistanceVectorRoutingSimNode.DistanceVectorRouting
{
    public class NetworkNodeState
    {
        private readonly SemaphoreSlim _forwardingMutex;
        private readonly ForwardingTable _forwardingTable;
        private readonly Dictionary<string, IPEndPoint?> _neighbors;

        public NetworkNodeState(string inputFile)
        {
            _forwardingMutex = new SemaphoreSlim(1, 1);
            _forwardingTable = new ForwardingTable();
            _neighbors = new Dictionary<string, IPEndPoint?>();

            try
            {
                var fileContents = File.ReadAllLines(inputFile);
                foreach (var line in fileContents[1..])
                {
                    var splitLine = line.Split(" ");
                    var neighbor = splitLine[0];
                    double cost = Double.Parse(splitLine[1]);

                    _forwardingTable.UpsertPath(neighbor, cost, neighbor);
                    _neighbors.Add(neighbor, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw ex;
            }
        }

        public void UpdateEndpoints(Dictionary<string, IPEndPoint> incomingEndpoints)
        {
            foreach (var (neighbor, endpoint) in incomingEndpoints)
            {
                if (_neighbors.ContainsKey(neighbor))
                {
                    _neighbors[neighbor] = endpoint;
                }
            }

            Logger.WriteLine("Updated neighbor endpoints:");
            foreach (var (neighbor, endpoint) in _neighbors)
            {
                Logger.WriteLine($"{neighbor} - {endpoint?.ToString() ?? "Not Found"}");
            }
            Logger.WriteLine("");
        }

        public async Task UpdateForwardingTable(string sourceNode, ForwardingTable otherTable)
        {
            await _forwardingMutex.WaitAsync();
            var pathToSource = _forwardingTable.GetCost(sourceNode);

            foreach (var (destination, record) in otherTable.GetRecords())
            {
                var cost = record.PathCost + pathToSource;

                if (!_forwardingTable.Exists(destination) || cost < _forwardingTable.GetCost(destination))
                {
                    _forwardingTable.UpsertPath(destination, cost, sourceNode);
                }
            }
            _forwardingMutex.Release();
        }

        public async Task ReInitialize(string inputFile)
        {
            try
            {
                var fileContents = File.ReadAllLines(inputFile);
                await _forwardingMutex.WaitAsync();
                foreach (var line in fileContents[1..])
                {
                    var splitLine = line.Split(" ");
                    var neighbor = splitLine[0];
                    double cost = Double.Parse(splitLine[1]);

                    _forwardingTable.UpsertPath(neighbor, cost, neighbor);
                }
                _forwardingMutex.Release();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw ex;
            }
        }
    }
}
