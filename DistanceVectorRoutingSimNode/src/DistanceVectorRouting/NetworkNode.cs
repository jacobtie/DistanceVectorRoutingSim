// This file contains the NetworkNode class.
// This purpose of this class is direct communication
// between all nodes, as well as the Master node.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DistanceVectorRoutingSimNode.Logging;

namespace DistanceVectorRoutingSimNode.DistanceVectorRouting
{
    public class NetworkNode
    {
        // Fields used to identify the node
        private string _name;
        private IPAddress _ipAddress;
        private int _port;
        private NetworkNodeState _state;
        
        // Field used to communicate with the master
        private IPEndPoint _masterEndpoint;

        // Fields used to cancel program and track iterations
        private CancellationTokenSource _cancellationTokenSource;
        private int _iterations;

        // Static method to run the node with the given arguments
        public static void RunNode(string name, int nodePort, IPEndPoint masterEndpoint)
        {
            Logger.WriteLine($"Starting node {name}. Enter q to stop node.");

            // Get the IPv4 Address of the host
            var ipAddresses = Dns.GetHostAddresses(Dns.GetHostName());
            var ipAddress = ipAddresses.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            Logger.WriteLine($"Found IP Address: {ipAddress.ToString()}\n");

            // Create a new Network Node
            var node = new NetworkNode(name, ipAddress, nodePort, masterEndpoint);

            // Create three tasks to listen for info, register with the master node, 
            // and send info to the node's neighbors
            var listeningTask = Task.Run(() => node._listen());
            var registerTask = Task.Run(() => node._registerToMaster());
            var sendTask = Task.Run(() => node._sendUpdates());

            // Store the tasks 
            var tasks = new Task[] { listeningTask, registerTask, sendTask };

            // Continuously run the program until the user enters "Q" to console
            string keyInfo;
            do
            {
                keyInfo = Console.ReadLine();
                if (keyInfo.ToUpper() == "Q")
                {
                    Console.WriteLine();
                    node._stopNode();
                }
            }
            while (keyInfo.ToUpper() != "Q");

            // Wait all tasks
            Task.WaitAll(tasks);

            Logger.Output(node._name);
        }

        // Constructor for the Network Node
        private NetworkNode(string name, IPAddress ipAddress, int nodePort, IPEndPoint masterEndpoint)
        {
            // Initialize the fields
            _name = name;
            _ipAddress = ipAddress;
            _port = nodePort;
            _masterEndpoint = masterEndpoint;
            var inputFile = Path.Combine("input-files", $"{name}.dat");
            _state = new NetworkNodeState(_name, inputFile);
            _cancellationTokenSource = new CancellationTokenSource();
            _iterations = 0;
        }

        // Method to stop the node smoothly
        private void _stopNode()
        {
            Logger.WriteLine("Shutting down node...");
            _cancellationTokenSource.Cancel();
        }

        // Async method to send the node's info to Master for registration
        private async Task _registerToMaster()
        {
            Logger.WriteLine("Registering to master\n");
            var encodedName = Encoding.UTF8.GetBytes($"{_name} {_port}");

            using (var socket = _initSocket())
            {
                await socket.SendToAsync(encodedName, SocketFlags.None, _masterEndpoint);
            }
        }

        // Async method to listen for neighbor information
        private async Task _listen()
        {
            Logger.WriteLine("Listening...");
            using (var socket = _initSocket())
            {
                using (var cancellation = _cancellationTokenSource.Token.Register(() => socket.Dispose()))
                {
                    // Bind the IP address and port to the socket
                    socket.Bind(new IPEndPoint(_ipAddress, _port));

                    while (true)
                    {
                        // Create variables to store received messages
                        byte[] buffer;
                        SocketReceiveFromResult result;

                        // Try to receive and store the new message in result
                        try
                        {
                            EndPoint receivedEndpoint = new IPEndPoint(IPAddress.Any, _port);
                            buffer = new byte[2048];

                            result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, receivedEndpoint);
                        }
                        catch
                        {
                            Logger.WriteLine("Stopping listening");
                            return;
                        }

                        // Decode the message as a string and extract the message type and body
                        var message = Encoding.UTF8.GetString(buffer.Take(result.ReceivedBytes).ToArray());
                        var messageTypeAndBody = message.Split("!-----!");
                        var (messageType, messageBody) = (messageTypeAndBody[0], messageTypeAndBody[1]);

                        // If the message came from the Master Node
                        if (messageType.Equals("M"))
                        {
                            // Dispatch the message body as a Master message
                            _ = Task.Run(() => _masterDispatch(messageBody));
                        }
                        // Else if the message came from a neighboring node
                        else if (messageType.Equals("N"))
                        {
                            // Dispatch the message body as a neighbor node
                            _ = Task.Run(() => _neighborDispatch(messageBody));
                        }
                    }

                }
            }
        }

        // Method to parse Master messages
        private void _masterDispatch(string message)
        {
            // Create a new dictionary for the new forwarding locations
            Logger.WriteLine("Received master registration update broadcast. Parsing...");
            var neighborUpdate = new Dictionary<string, IPEndPoint>();

            // For each line in the message but the first
            foreach (var line in message.Split("\r\n")[..^1])
            {
                // Parse the information about the neighbor's locations
                var splitLine = line.Split(" ");
                var neighborName = splitLine[0];
                var neighborAddress = IPAddress.Parse(splitLine[1]);
                var port = Int32.Parse(splitLine[2]);

                // Add the neighbor's information to the dictionary
                neighborUpdate.Add(neighborName, new IPEndPoint(neighborAddress, port));
            }

            // Update the endpoints for each neighbor
            _state.UpdateEndpoints(neighborUpdate);
        }

        // Async method to parse neighbor messages
        private async Task _neighborDispatch(string message)
        {
            // Parse the message for the source node and forwarding table
            var messageSplit = message.Split("***");
            var (sourceNode, stringifiedForwardingTable) = (messageSplit[0], messageSplit[1]);
            Logger.WriteLine("Received neighbor update. Parsing...");

            // Decode the forwarding table from a string
            var otherForwardingTable = ForwardingTable.Decode(stringifiedForwardingTable);

            // If no valid forwarding table could be found
            if (otherForwardingTable is null)
            {
                return;
            }

            Logger.WriteLine($"Received update from neighbor {sourceNode}\n");

            // Update the forwarding table with the source node and new forwarding table
            await _state.UpdateForwardingTable(sourceNode, otherForwardingTable);
        }

        // Async method to send updates to the neighboring nodes
        private async Task _sendUpdates()
        {
            while (true)
            {
                // Wait 15 seconds before sending out each update
                try
                {
                    await Task.Delay(15000, _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Logger.WriteLine("Stopping sending updates to neighbors");
                    return;
                }

                // Reload the associated file to update any changes
                Logger.WriteLine("Reinitializing from input file");
                await _state.ReInitialize();

                Logger.WriteLine("Sending updates to neighbors\n");
                Logger.WriteLine($"output number {++_iterations}");
                Logger.WriteLine(_state.forwardingTable.ToString(_name));

                // For each neighbor of this node
                foreach (var (neighbor, endpoint) in _state.Neighbors)
                {
                    // If there is no endpoint
                    if (endpoint is null)
                    {
                        continue;
                    }

                    // Clone the forwarding table of the node
                    var forwardingTable = _state.forwardingTable.Clone();

                    // For each record in the forwarding table
                    foreach (var (destination, record) in forwardingTable.GetRecords())
                    {
                        // If the current next hop is equal to the current neighbor
                        if (record.NextHop?.Equals(neighbor) ?? false)
                        {
                            // Change the path cost to positive infinity
                            record.PathCost = Double.PositiveInfinity;
                        }
                    }

                    // Encode the forwarding table as a string then as bytes
                    var stringifiedForwardingTable = ForwardingTable.Encode(forwardingTable);
                    var encodedMessage = Encoding.UTF8.GetBytes("N!-----!" + _name + "***" + stringifiedForwardingTable);

                    // Send the encoded forwarding table to the current neighbor
                    using (var socket = _initSocket())
                    {
                        await socket.SendToAsync(encodedMessage, SocketFlags.None, endpoint);
                    }
                }
            }
        }

        // Method to initialize a socket
        private Socket _initSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }
    }
}
