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
        private string _name;
        private IPAddress _ipAddress;
        private int _port;
        private IPEndPoint _masterEndpoint;
        private NetworkNodeState _state;
        private CancellationTokenSource _cancellationTokenSource;

        public static void RunNode(string name, int nodePort, IPEndPoint masterEndpoint)
        {
            Logger.WriteLine($"Starting node {name}. Enter q to stop node.");
            var ipAddresses = Dns.GetHostAddresses("localhost");
            var ipAddress = ipAddresses.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            Logger.WriteLine($"Found IP Address: {ipAddress.ToString()}\n");

            var node = new NetworkNode(name, ipAddress, nodePort, masterEndpoint);

            var masterTask = Task.Run(() => node.ListenForMaster(), node._getToken());
            var registerTask = Task.Run(() => node.RegisterToMaster(), node._getToken());
            var neighborTask = Task.Run(() => node.ListenForNeighbors(), node._getToken());
            var sendTask = Task.Run(() => node.SendUpdates(), node._getToken());

            var tasks = new Task[] { registerTask, masterTask, neighborTask, sendTask };

            string keyInfo;
            do
            {
                keyInfo = Console.ReadLine();
                if (keyInfo.ToUpper() == "Q")
                {
                    Console.WriteLine();
                    node.StopNode();
                }
            }
            while (keyInfo.ToUpper() != "Q");

            Task.WaitAll(tasks);

            Logger.Output();
        }

        public NetworkNode(string name, IPAddress ipAddress, int nodePort, IPEndPoint masterEndpoint)
        {
            _name = name;
            _ipAddress = ipAddress;
            _port = nodePort;
            _masterEndpoint = masterEndpoint;
            var inputFile = Path.Combine("input-files", $"{name}.dat");
            _state = new NetworkNodeState(inputFile);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void StopNode()
        {
            Logger.WriteLine("Shutting down node...");
            _cancellationTokenSource.Cancel();
        }

        public async Task RegisterToMaster()
        {
            Logger.WriteLine("Registering to master\n");
            var encodedName = Encoding.UTF8.GetBytes($"{_name} {_port}");

            using (var socket = _initSocket())
            {
                await socket.SendToAsync(encodedName, SocketFlags.None, _masterEndpoint);
            }
        }

        public async Task ListenForMaster()
        {
            Logger.WriteLine("Listening for registration update broadcasts from master...\n");
            using (var socket = _initSocket())
            {
                using (var cancellation = _cancellationTokenSource.Token.Register(() => socket.Dispose()))
                {
                    socket.Bind(new IPEndPoint(_ipAddress, _port));
                    while (true)
                    {
                        byte[] buffer;
                        SocketReceiveFromResult result;
                        try
                        {
                            EndPoint receivedEndpoint = new IPEndPoint(IPAddress.Any, _port);
                            buffer = new byte[2048];

                            result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, receivedEndpoint);
                        }
                        catch
                        {
                            Logger.WriteLine("Stopping listening for registration broadcasts");
                            return;
                        }

                        Logger.WriteLine("Received master registration update broadcast. Parsing...");

                        try
                        {
                            var byteMessage = buffer.Take(result.ReceivedBytes).ToArray();
                            var decodedMessage = Encoding.UTF8.GetString(byteMessage);

                            var neighborUpdate = new Dictionary<string, IPEndPoint>();
                            foreach (var line in decodedMessage.Split("\r\n")[..^1])
                            {
                                var splitLine = line.Split(" ");
                                var neighborName = splitLine[0];
                                var neighborAddress = IPAddress.Parse(splitLine[1]);
                                var port = Int32.Parse(splitLine[2]);
                                neighborUpdate.Add(neighborName, new IPEndPoint(neighborAddress, port));
                            }

                            _state.UpdateEndpoints(neighborUpdate);
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine(ex.Message);
                        }
                    }
                }
            }
        }

        public async Task ListenForNeighbors()
        {
            // TODO: Write listener socket to listen for updates from neighbors
        }

        public async Task SendUpdates()
        {
            // TODO: Write sending socket to send updates to neighbors every 15 seconds
        }

        private Socket _initSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }

        private CancellationToken _getToken()
        {
            return _cancellationTokenSource.Token;
        }
    }
}
