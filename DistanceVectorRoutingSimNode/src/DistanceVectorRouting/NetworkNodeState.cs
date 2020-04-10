// This file contains the NetworkNodeState class.
// The purpose of this class is the storage of 
// information pertaining to the communication 
// between each node in the network.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DistanceVectorRoutingSimNode.Logging;

namespace DistanceVectorRoutingSimNode.DistanceVectorRouting
{
    internal class NetworkNodeState
    {
        // Semaphore to control access to data
        private readonly SemaphoreSlim _forwardingMutex;

        // Fields to store data about node and it's neighbors
        private readonly ForwardingTable _forwardingTable;
        private readonly Dictionary<string, IPEndPoint?> _neighborsEndpoints;
        private readonly Dictionary<string, double> _neighborsCost;
        private readonly string _inputFile;

        // Method to access the neighbors of the node
        internal Dictionary<string, IPEndPoint?> Neighbors => _neighborsEndpoints;

        // Method to access the forwarding table of the node
        internal ForwardingTable forwardingTable => _forwardingTable;

        // Constructor for the Network Node State
        internal NetworkNodeState(string name, string inputFile)
        {
            // Initialize the fields
            _inputFile = inputFile;
            _forwardingMutex = new SemaphoreSlim(1, 1);
            _forwardingTable = new ForwardingTable();
            _neighborsEndpoints = new Dictionary<string, IPEndPoint?>();
            _neighborsCost = new Dictionary<string, double>();

            // Add the node itself to the forwarding table
            _forwardingTable.UpsertPath(name, 0, null);

            // Try to read and parse the contents of the file
            try
            {
                // Get all the lines of the file
                var fileContents = File.ReadAllLines(inputFile);

                // For each line the the file but the first
                foreach (var line in fileContents[1..])
                {
                    // Parse the neighbor and cost of the link
                    var splitLine = line.Split(" ");
                    var neighbor = splitLine[0];
                    double cost = Double.Parse(splitLine[1]);

                    // Update/insert the neighbor to the forwarding table
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

        // Method to update the endpoints for the neighbors
        internal void UpdateEndpoints(Dictionary<string, IPEndPoint> incomingEndpoints)
        {
            // For each existing neighbor, update the endpoint
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

        // Async method to update the forwarding table of the node
        internal async Task UpdateForwardingTable(string sourceNode, ForwardingTable otherTable)
        {
            // Wait for the mutex to update
            await _forwardingMutex.WaitAsync();

            // Get the cost from this node to the source
            var pathToSource = _neighborsCost[sourceNode];

            // For each record in the sent forwarding table
            foreach (var (destination, record) in otherTable.GetRecords())
            {
                // Get the cost to reach that node based on the new forwarding table
                var cost = record.PathCost + pathToSource;

                // If the destination does not exist or the next hop is the same as the source
                // node or the cost is less than the current stored cost
                if (!forwardingTable.Exists(destination) ||
                    (forwardingTable.GetNextHop(destination)?.Equals(sourceNode) ?? false) ||
                    cost < forwardingTable.GetCost(destination))
                {
                    // Update/insert the the new forwarding table record
                    forwardingTable.UpsertPath(destination, cost, sourceNode);
                }
            }

            // Release the mutex
            _forwardingMutex.Release();
        }

        // Async method to reread the file associated with this node
        internal async Task ReInitialize()
        {
            try
            {
                // Read all the lines from the file
                var fileContents = await File.ReadAllLinesAsync(_inputFile);

                // Wait for the mutex to update
                await _forwardingMutex.WaitAsync();

                // For each line in the file but the first
                foreach (var line in fileContents[1..])
                {
                    // Parse the line for the neighbor's info
                    var splitLine = line.Split(" ");
                    var neighbor = splitLine[0];
                    double cost = Double.Parse(splitLine[1]);

                    // If the next hop of the neighbor is the neighbor and the cost
                    // is less than the current cost
                    if ((forwardingTable.GetNextHop(neighbor)?.Equals(neighbor) ?? true) ||
                    cost < forwardingTable.GetCost(neighbor))
                    {
                        // Update/insert the new forwarding table record
                        forwardingTable.UpsertPath(neighbor, cost, neighbor);
                    }

                    // If the new cost is not equal to the old cost
                    if (cost != _neighborsCost[neighbor])
                    {
                        // Update the neighbor cost
                        Logger.WriteLine($"Link to {neighbor} changed from {_neighborsCost[neighbor]} to {cost}!");
                        _neighborsCost[neighbor] = cost;
                    }
                }

                // Release the mutex
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
