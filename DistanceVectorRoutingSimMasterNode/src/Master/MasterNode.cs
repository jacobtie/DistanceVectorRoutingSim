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
        private Dictionary<string, IPEndPoint> _nodeLocations;
        private CancellationTokenSource _cancellationTokenSource;

        public static void RunMaster()
        {
            Logger.WriteLine("Starting master node");
            var masterNode = new MasterNode();

            var registrationTask = Task.Run(() => masterNode.ListenForRegistration(), masterNode._getToken());

            var tasks = new Task[] { registrationTask };

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

            Task.WaitAll(tasks);

            Logger.Output();
        }

        private MasterNode()
        {
            _nodeLocations = new Dictionary<string, IPEndPoint>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void StopNode()
        {
            Logger.WriteLine("Shutting down master node...");
            _cancellationTokenSource.Cancel();
        }

        public async Task ListenForRegistration()
        {
            Logger.WriteLine("Listening for registering nodes...\n");
            using (var socket = _initSocket())
            {
                using (var cancellation = _cancellationTokenSource.Token.Register(() => socket.Dispose()))
                {
                    var ipAddresses = await Dns.GetHostAddressesAsync("localhost");
                    var ipAddress = ipAddresses.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                    socket.Bind(new IPEndPoint(ipAddress, 42069));

                    try
                    {
                        while (true)
                        {
                            var buffer = new byte[2048];
                            EndPoint receivedEndpoint = new IPEndPoint(IPAddress.Any, 42069);

                            var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, receivedEndpoint);

                            Logger.WriteLine("Received node registration. Parsing...");

                            var byteMessage = buffer.Take(result.ReceivedBytes).ToArray();

                            var decodedMessage = Encoding.UTF8.GetString(byteMessage);

                            var splitMessage = decodedMessage.Split(" ");

                            var registeringNode = splitMessage[0];
                            var portNum = Int32.Parse(splitMessage[1]);

                            var nodeEndpoint = new IPEndPoint((result.RemoteEndPoint as IPEndPoint).Address, portNum);

                            Logger.WriteLine($"Received registration from {registeringNode} at {nodeEndpoint.ToString()}\n");

                            if (_nodeLocations.ContainsKey(registeringNode))
                            {
                                _nodeLocations[registeringNode] = nodeEndpoint;
                            }
                            else
                            {
                                _nodeLocations.Add(registeringNode, nodeEndpoint);
                            }

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

        private void _broadcastEndpoints()
        {
            var sb = new StringBuilder();
            var endpoints = new List<IPEndPoint>();
            foreach (var (destination, endpoint) in _nodeLocations)
            {
                sb.Append($"{destination} {endpoint.Address.ToString()} {endpoint.Port}\r\n");
                endpoints.Add(endpoint);
            }

            var message = sb.ToString();

            Logger.WriteLine("Broadcasting node registration update:");
            Logger.WriteLine(message);

            var encodedMessage = Encoding.UTF8.GetBytes(sb.ToString());
            foreach (var endpoint in endpoints)
            {
                _ = Task.Run(() => _broadcastEndpointsToNode(encodedMessage, endpoint));
            }
        }

        private async Task _broadcastEndpointsToNode(byte[] message, IPEndPoint endpoint)
        {
            using (var socket = _initSocket())
            {
                await socket.SendToAsync(message, SocketFlags.None, endpoint);
            }
        }

        private Socket _initSocket()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            return socket;
        }

        private CancellationToken _getToken()
        {
            return _cancellationTokenSource.Token;
        }
    }
}
