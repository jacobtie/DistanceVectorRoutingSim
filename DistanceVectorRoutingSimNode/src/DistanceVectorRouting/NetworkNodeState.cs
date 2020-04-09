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
        private readonly Dictionary<string, IPEndPoint?> _neighborsEndpoints;
        private readonly Dictionary<string, double> _neighborsCost;
        private readonly string _inputFile;

        public Dictionary<string, IPEndPoint?> Neighbors => _neighborsEndpoints;

        public ForwardingTable forwardingTable => _forwardingTable;

        public NetworkNodeState(string name, string inputFile)
        {
            _inputFile = inputFile;
            _forwardingMutex = new SemaphoreSlim(1, 1);
            _forwardingTable = new ForwardingTable();
            _neighborsEndpoints = new Dictionary<string, IPEndPoint?>();
            _neighborsCost = new Dictionary<string, double>();

            _forwardingTable.UpsertPath(name, 0, null);

            try
            {
                var fileContents = File.ReadAllLines(inputFile);
                foreach (var line in fileContents[1..])
                {
                    var splitLine = line.Split(" ");
                    var neighbor = splitLine[0];
                    double cost = Double.Parse(splitLine[1]);

                    _forwardingTable.UpsertPath(neighbor, cost, neighbor);
                    _neighborsEndpoints.Add(neighbor, null);
                    _neighborsCost.Add(neighbor, cost);
                }

                Logger.WriteLine("initial forwarding table");
                Logger.WriteLine(_forwardingTable.ToString(name));
            }
            catch (Exception ex)
            {
                Logger.WriteLine(ex.Message);
                throw ex;
            }
        }

        public void UpdateEndpoints(Dictionary<string, IPEndPoint> incomingEndpoints)
        {
            foreach (var (neighbor, endpoint) in incomingEndpoints)
            {
                if (Neighbors.ContainsKey(neighbor))
                {
                    Neighbors[neighbor] = endpoint;
                }
            }

            Logger.WriteLine("Updated neighbor endpoints:");
            foreach (var (neighbor, endpoint) in Neighbors)
            {
                Logger.WriteLine($"{neighbor} - {endpoint?.ToString() ?? "Not Found"}");
            }
            Logger.WriteLine("");
        }

        public async Task UpdateForwardingTable(string sourceNode, ForwardingTable otherTable)
        {
            await _forwardingMutex.WaitAsync();
            var pathToSource = _neighborsCost[sourceNode];

            foreach (var (destination, record) in otherTable.GetRecords())
            {
                var cost = record.PathCost + pathToSource;

                if (!forwardingTable.Exists(destination) ||
                    (forwardingTable.GetNextHop(destination)?.Equals(sourceNode) ?? false) ||
                    cost < forwardingTable.GetCost(destination))
                {
                    forwardingTable.UpsertPath(destination, cost, sourceNode);
                }
            }
            _forwardingMutex.Release();
        }

        public async Task ReInitialize()
        {
            try
            {
                var fileContents = await File.ReadAllLinesAsync(_inputFile);
                await _forwardingMutex.WaitAsync();
                foreach (var line in fileContents[1..])
                {
                    var splitLine = line.Split(" ");
                    var neighbor = splitLine[0];
                    double cost = Double.Parse(splitLine[1]);

                    if ((forwardingTable.GetNextHop(neighbor)?.Equals(neighbor) ?? true) ||
                    cost < forwardingTable.GetCost(neighbor))
                    {
                        forwardingTable.UpsertPath(neighbor, cost, neighbor);
                    }
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
