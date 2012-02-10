using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SimPlaza.UDProxy.SOCKS5;

namespace SimPlaza.UDProxy.UDP
{
    class UDPConnection
    {
        public static Thread MyThread;
        public static UdpClient MyUDP;
        public static IPEndPoint MyClient;

        private static IPEndPoint sender;
        private static byte[] data;

        public static void TryBegin(ref PacketReplyRequest reply, IPEndPoint client)
        {
            // Housekeeping
            if (MyThread != null)
                MyThread.Abort();

            if (MyUDP != null)
                MyUDP.Close();

            try
            {
                // Set the client's UDP destination port to ours
                reply.PORT = UDProxy.gConfiguration.ListenPort;

                // Set our client's expected IP and Port from SOCKS5
                MyClient = client;

                MyUDP = new UdpClient( UDProxy.gConfiguration.ListenPort );

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
            if (ip.Equals(UDProxy.gConfiguration.MyExternalIP))
                ip = UDProxy.gConfiguration.TargetIP;

            IPEndPoint destination = new IPEndPoint(ip, port);
            MyUDP.Send(packet.DATA, packet.DATA.Length, destination);
            //DebugInfo(String.Format("Transmitting packet: {0} bytes, raw data {1} bytes", data.Length, packet.DATA.Length));
        }

        public static void HandleIncoming(byte[] data, IPEndPoint sender)
        {
            // THIS IS WHERE ALL THE MAGIC UNHAPPENS, BABY
            // Loopback detection: If we're receiving from a server we're routing to,
            // make it look like it's coming from our external IP.
            if (sender.Address.Equals(UDProxy.gConfiguration.TargetIP) && sender.Port == UDProxy.gConfiguration.TargetPort)
                sender.Address = UDProxy.gConfiguration.MyExternalIP;

            var packet = new PacketUDP(
                SOCKS5Protocol.ATYP.IPV4,
                sender.Address,
                (ushort)sender.Port,
                data
            );

            var packetBytes = packet.GetBytes();
            MyUDP.Send(packetBytes, packetBytes.Length, MyClient);
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
