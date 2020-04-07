using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            var ipAddresses = Dns.GetHostAddresses("localhost");
            var ipAddress = ipAddresses.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            var node = new NetworkNode(name, ipAddress, nodePort, masterEndpoint);

            Console.CancelKeyPress += (sender, args) =>
            {
                node.StopNode();
            };

            var masterTask = Task.Run(() => node.ListenForMaster());
            var registerTask = Task.Run(() => node.RegisterToMaster());
            var neighborTask = Task.Run(() => node.ListenForNeighbors());
            var sendTask = Task.Run(() => node.SendUpdates());

            var tasks = new Task[] { registerTask, masterTask, neighborTask, sendTask };
            Task.WaitAll(tasks);
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
            _cancellationTokenSource.Cancel();
        }

        public async Task RegisterToMaster()
        {
            var encodedName = Encoding.UTF8.GetBytes($"{_name} {_port}");

            using (var socket = _initSocket())
            {
                await socket.SendToAsync(encodedName, SocketFlags.None, _masterEndpoint);
            }
        }

        public async Task ListenForMaster()
        {
            using (var socket = _initSocket())
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
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.StackTrace);
                        throw ex;
                    }

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
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        public async Task ListenForNeighbors()
        {

        }

        public async Task SendUpdates()
        {

        }

        private Socket _initSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }
    }
}
