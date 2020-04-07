using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DistanceVectorRoutingSimMasterNode.Master
{
    public class MasterNode
    {
        private Dictionary<string, IPEndPoint> _nodeLocations;

        public static void RunMaster()
        {
            var masterNode = new MasterNode();

            var registrationTask = Task.Run(() => masterNode.ListenForRegistration());

            var tasks = new Task[] { registrationTask };
            Task.WaitAll(tasks);
        }

        private MasterNode()
        {
            _nodeLocations = new Dictionary<string, IPEndPoint>();
        }

        public async Task ListenForRegistration()
        {
            using (var socket = _initSocket())
            {
                var ipAddresses = await Dns.GetHostAddressesAsync("localhost");
                var ipAddress = ipAddresses.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                socket.Bind(new IPEndPoint(ipAddress, 42069));

                while (true)
                {
                    var buffer = new byte[2048];
                    EndPoint receivedEndpoint = new IPEndPoint(IPAddress.Any, 42069);

                    var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, receivedEndpoint);

                    var byteMessage = buffer.Take(result.ReceivedBytes).ToArray();

                    var decodedMessage = Encoding.UTF8.GetString(byteMessage);

                    var splitMessage = decodedMessage.Split(" ");

                    var registeringNode = splitMessage[0];
                    var portNum = Int32.Parse(splitMessage[1]);

                    var nodeEndpoint = new IPEndPoint((result.RemoteEndPoint as IPEndPoint).Address, portNum);

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

            var message = Encoding.UTF8.GetBytes(sb.ToString());
            foreach (var endpoint in endpoints)
            {
                _ = Task.Run(() => _broadcastEndpointsToNode(message, endpoint));
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
    }
}
