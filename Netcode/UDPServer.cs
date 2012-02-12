using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using SimPlaza.UDProxy.SOCKS5;

namespace SimPlaza.UDProxy.UDP
{
    class UDPConnection
    {
        public static Thread MyThread;
        public static UdpClient MyUDP;
        public static IPEndPoint MyClient;

        public static IPAddress ExternalIP;
        public static IPAddress TargetIP;
        public static List<ushort> TargetPorts;

        public static int TotalUp;
        public static int TotalDown;

        private static bool hasClientPort = true;
        private static IPEndPoint sender;
        private static byte[] data;

        public static void TryBegin(ref PacketReplyRequest reply, IPEndPoint client)
        {
            // Housekeeping
            if (MyThread != null)
                MyThread.Abort();

            if (MyUDP != null)
                MyUDP.Close();

            TotalUp = 0;
            TotalDown = 0;

            try
            {
                // Set our client's expected IP and Port from SOCKS5
                MyClient = client;

                if (MyClient.Port == 0)
                {
                    DebugWarn("Client's port is 0, going to have to guess source port (normal with V2/V3 viewers).");
                    hasClientPort = false;
                }

                // Get target's IP and ports from config (less lag)
                ExternalIP = (IPAddress)UDProxy.gConfiguration.Config["MyExternalIP"];
                TargetIP = (IPAddress)UDProxy.gConfiguration.Config["TargetIP"];
                TargetPorts = (List<ushort>)UDProxy.gConfiguration.Config["TargetPorts"];

                MyUDP = new UdpClient( (ushort)UDProxy.gConfiguration.Config["ListenPort"] );

                MyUDP.AllowNatTraversal(false);
                MyUDP.DontFragment = true;
                MyUDP.EnableBroadcast = true;
                
                MyThread = new Thread(new ThreadStart(StreamLoop));
            }
            catch (SocketException e)
            {
                reply.REP = SOCKS5Protocol.REP.GENERAL_FAILURE;
                throw new UDProxyException("Socket failure: " + e.Message);
            }

            DebugInfo("UDP Endpoint has been set up.");
            MyThread.Start();
        }

        public static void StreamLoop()
        {
            while (true)
            {
                try
                {
                    data = MyUDP.Receive(ref sender);
                }
                catch
                {
                    // Socket's closed, break out
                    break;
                }

                // V2 and V3 viewers don't give the SOCKS5 server their expected port
                // So we need to guess it from the first packet. :(
                // TODO: Make this safer by checking if packet is indeed a valid UDP assoc packet
                if (!hasClientPort)
                {
                    if (sender.Address.Equals(MyClient.Address))
                    {
                        hasClientPort = true;
                        MyClient.Port = sender.Port;
                        DebugWarn("Guessed client's unknown port as " + MyClient.Port);
                    }
                }

                // If the packet came from the client, it's a UDP assoc packet.
                if (sender.Equals(MyClient))
                    HandleOutgoing(data);
                else
                    HandleIncoming(data, sender);
            }
        }

        public static void HandleOutgoing(byte[] data)
        {
            var packet = new PacketUDP(new MemoryStream(data));
            
            var ip = new IPAddress(packet.ADDR);
            var port = BitConverter.ToUInt16(packet.PORT, 0);

            // THIS IS WHERE ALL THE MAGIC HAPPENS, BABY
            // Loopback detection: If we're making a request to our external IP, route it!
            // Note that we're rerouting regardless
            if (ip.Equals(ExternalIP) && TargetPorts.Contains(port))
                ip = (IPAddress)UDProxy.gConfiguration.Config["TargetIP"];

            IPEndPoint destination = new IPEndPoint(ip, port);
            TotalUp += MyUDP.Send(packet.DATA, packet.DATA.Length, destination);
        }

        public static void HandleIncoming(byte[] data, IPEndPoint sender)
        {
            // THIS IS WHERE ALL THE MAGIC UNHAPPENS, BABY
            // Loopback detection: If we're receiving from a server we're routing to,
            // make it look like it's coming from our external IP.
            if (sender.Address.Equals(TargetIP) && TargetPorts.Contains((ushort)sender.Port))
                sender.Address = ExternalIP;

            var packet = new PacketUDP(
                SOCKS5Protocol.ATYP.IPV4,
                sender.Address,
                (ushort)sender.Port,
                data
            );

            var packetBytes = packet.GetBytes();
            TotalDown += MyUDP.Send(packetBytes, packetBytes.Length, MyClient);
        }

        #region Debugs

        private static void DebugInfo(string msg)
        {
            UDProxy.Debug("UDPServer", msg, UDProxy.DebugLevels.Info);
        }

        private static void DebugWarn(string msg)
        {
            UDProxy.Debug("UDPServer", msg, UDProxy.DebugLevels.Warn);
        }

        private static void DebugError(string msg)
        {
            UDProxy.Debug("UDPServer", msg, UDProxy.DebugLevels.Error);
        }

        #endregion

    }
}
