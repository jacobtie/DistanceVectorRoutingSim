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
        private int _iterations;

        public static void RunNode(string name, int nodePort, IPEndPoint masterEndpoint)
        {
            Logger.WriteLine($"Starting node {name}. Enter q to stop node.");
            var ipAddresses = Dns.GetHostAddresses("localhost");
            var ipAddress = ipAddresses.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            Logger.WriteLine($"Found IP Address: {ipAddress.ToString()}\n");

            var node = new NetworkNode(name, ipAddress, nodePort, masterEndpoint);

            var listeningTask = Task.Run(() => node.Listen());
            var registerTask = Task.Run(() => node.RegisterToMaster());
            var sendTask = Task.Run(() => node.SendUpdates());

            var tasks = new Task[] { listeningTask, registerTask, sendTask };

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

            Logger.Output(node._name);
        }

        public NetworkNode(string name, IPAddress ipAddress, int nodePort, IPEndPoint masterEndpoint)
        {
            _name = name;
            _ipAddress = ipAddress;
            _port = nodePort;
            _masterEndpoint = masterEndpoint;
            var inputFile = Path.Combine("input-files", $"{name}.dat");
            _state = new NetworkNodeState(_name, inputFile);
            _cancellationTokenSource = new CancellationTokenSource();
            _iterations = 0;
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

        public async Task Listen()
        {
            Logger.WriteLine("Listening...");
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
                            Logger.WriteLine("Stopping listening");
                            return;
                        }

                        var message = Encoding.UTF8.GetString(buffer.Take(result.ReceivedBytes).ToArray());

                        var messageTypeAndBody = message.Split("!-----!");
                        var (messageType, messageBody) = (messageTypeAndBody[0], messageTypeAndBody[1]);

                        if (messageType.Equals("M"))
                        {
                            _ = Task.Run(() => MasterDispatch(messageBody));
                        }
                        else if (messageType.Equals("N"))
                        {
                            _ = Task.Run(() => NeighborDispatch(messageBody));
                        }
                    }

                }
            }
        }

        public void MasterDispatch(string message)
        {
            Logger.WriteLine("Received master registration update broadcast. Parsing...");
            var neighborUpdate = new Dictionary<string, IPEndPoint>();
            foreach (var line in message.Split("\r\n")[..^1])
            {
                var splitLine = line.Split(" ");
                var neighborName = splitLine[0];
                var neighborAddress = IPAddress.Parse(splitLine[1]);
                var port = Int32.Parse(splitLine[2]);
                neighborUpdate.Add(neighborName, new IPEndPoint(neighborAddress, port));
            }

            _state.UpdateEndpoints(neighborUpdate);
        }

        public async Task NeighborDispatch(string message)
        {
            var messageSplit = message.Split("***");
            var (sourceNode, stringifiedForwardingTable) = (messageSplit[0], messageSplit[1]);
            Logger.WriteLine("Received neighbor update. Parsing...");
            var otherForwardingTable = ForwardingTable.Decode(stringifiedForwardingTable);

            if (otherForwardingTable is null)
            {
                return;
            }

            Logger.WriteLine($"Received update from neighbor {sourceNode}\n");

            await _state.UpdateForwardingTable(sourceNode, otherForwardingTable);
        }

        public async Task SendUpdates()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(15000, _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Logger.WriteLine("Stopping sending updates to neighbors");
                    return;
                }

                Logger.WriteLine("Reinitializing from input file");
                await _state.ReInitialize();

                Logger.WriteLine("Sending updates to neighbors\n");
                Logger.WriteLine($"output number {++_iterations}");
                Logger.WriteLine(_state.forwardingTable.ToString(_name));

                foreach (var (neighbor, endpoint) in _state.Neighbors)
                {
                    if (endpoint is null)
                    {
                        continue;
                    }

                    var forwardingTable = _state.forwardingTable;
                    var stringifiedForwardingTable = ForwardingTable.Encode(forwardingTable);
                    var encodedMessage = Encoding.UTF8.GetBytes("N!-----!" + _name + "***" + stringifiedForwardingTable);

                    using (var socket = _initSocket())
                    {
                        await socket.SendToAsync(encodedMessage, SocketFlags.None, endpoint);
                    }
                }
            }
        }

        private Socket _initSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }
    }
}
