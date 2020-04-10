// This file contains the Master node
// The point of this node is to store information about
// each node, such as IP address and port number. This
// is useful so that each node will have the ability to
// send/receive information to/from each of its neighbors.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DistanceVectorRoutingSimNode.Logging;

namespace DistanceVectorRoutingSimMasterNode.Master
{
    public class MasterNode
    {
        // Port on which master listens
        private int _port;

        // Dictionary of IP addresses and ports for each node name
        private Dictionary<string, IPEndPoint> _nodeLocations;

        // Cancellation token for smooth shutdown of the program
        private CancellationTokenSource _cancellationTokenSource;

        // Method to begin running the master node
        public static void RunMaster(int port)
        {
            Logger.WriteLine("Starting master node");
            var masterNode = new MasterNode(port);

            // Create tasks to listen for the registration of the nodes
            var registrationTask = Task.Run(() => masterNode.ListenForRegistration());
            var tasks = new Task[] { registrationTask };

            // Code to continuously run until the user exits with "Q" in console
            string keyInfo;
            do
            {
                keyInfo = Console.ReadLine();
                if (keyInfo.ToUpper() == "Q")
                {
                    Console.WriteLine();
                    masterNode.StopNode();
                }
            }
            while (keyInfo.ToUpper() != "Q");

            // Make all tasks wait
            Task.WaitAll(tasks);

            Logger.Output();
        }

        // Constructor for the master node
        private MasterNode(int port)
        {
            // Initialize fields
            _port = port;
            _nodeLocations = new Dictionary<string, IPEndPoint>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        // Method to smoothly shutdown the program
        public void StopNode()
        {
            Logger.WriteLine("Shutting down master node...");
            _cancellationTokenSource.Cancel();
        }

        // Async method to listen for the registration of the nodes
        public async Task ListenForRegistration()
        {
            Logger.WriteLine("Listening for registering nodes...\n");
            using (var socket = _initSocket())
            {
                using (var cancellation = _cancellationTokenSource.Token.Register(() => socket.Dispose()))
                {
                    // Get the IPv4 Address and port number to bind with the socket
                    var ipAddresses = await Dns.GetHostAddressesAsync(Dns.GetHostName());
                    var ipAddress = ipAddresses.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                    var ipEndpoint = new IPEndPoint(ipAddress, _port);
                    socket.Bind(ipEndpoint);

                    Logger.WriteLine($"Running on {ipEndpoint.ToString()}");

                    try
                    {
                        while (true)
                        {
                            // Make buffer and endpoint to receive info from the nodes
                            var buffer = new byte[2048];
                            EndPoint receivedEndpoint = new IPEndPoint(IPAddress.Any, _port);

                            // Get the result from the socket
                            var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, receivedEndpoint);
                            Logger.WriteLine("Received node registration. Parsing...");

                            // Get the message in bytes, convert it to a string, and split it on spaces
                            var byteMessage = buffer.Take(result.ReceivedBytes).ToArray();
                            var decodedMessage = Encoding.UTF8.GetString(byteMessage);
                            var splitMessage = decodedMessage.Split(" ");

                            // Get all send information from the message about the node
                            var registeringNode = splitMessage[0];
                            var portNum = Int32.Parse(splitMessage[1]);
                            var nodeEndpoint = new IPEndPoint((result.RemoteEndPoint as IPEndPoint).Address, portNum);

                            Logger.WriteLine($"Received registration from {registeringNode} at {nodeEndpoint.ToString()}\n");

                            // If the location of the node is already stored
                            if (_nodeLocations.ContainsKey(registeringNode))
                            {
                                // Reassign the node location
                                _nodeLocations[registeringNode] = nodeEndpoint;
                            }
                            else
                            {
                                // Add the new node location
                                _nodeLocations.Add(registeringNode, nodeEndpoint);
                            }

                            // Broadcast the node locations to each node
                            _ = Task.Run(() => _broadcastEndpoints());
                        }
                    }
                    catch
                    {
                        Logger.WriteLine("Stopping listening for node registration");
                    }
                }
            }
        }

        // Method to send all node locations to every node
        private void _broadcastEndpoints()
        {
            // Create and format a Master message with the nodes' infor
            var sb = new StringBuilder();
            var endpoints = new List<IPEndPoint>();
            sb.Append("M!-----!");
            foreach (var (destination, endpoint) in _nodeLocations)
            {
                sb.Append($"{destination} {endpoint.Address.ToString()} {endpoint.Port}\r\n");
                endpoints.Add(endpoint);
            }

            var message = sb.ToString();

            Logger.WriteLine("Broadcasting node registration update:");
            Logger.WriteLine(message.Substring(8));

            // Encode and send the message to every node
            var encodedMessage = Encoding.UTF8.GetBytes(message);
            foreach (var endpoint in endpoints)
            {
                _ = Task.Run(() => _broadcastEndpointsToNode(encodedMessage, endpoint));
            }
        }

        // Method to broadcast the locations of all nodes to a node
        private async Task _broadcastEndpointsToNode(byte[] message, IPEndPoint endpoint)
        {
            using (var socket = _initSocket())
            {
                await socket.SendToAsync(message, SocketFlags.None, endpoint);
            }
        }

        // Method to initalize the socket
        private Socket _initSocket()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            return socket;
        }
    }
}
