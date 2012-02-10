using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using SimPlaza.UDProxy.UDP;

namespace SimPlaza.UDProxy.SOCKS5
{
    public enum SOCKS5States
    {
        Disconnected,
        Connected
    }

    class SOCKS5Server
    {
        public SOCKS5States State = SOCKS5States.Disconnected;
        public IPEndPoint MyClient;

        public SOCKS5Server(IPEndPoint client)
        {
            MyClient = client;
        }

        /// <summary>
        /// Processes bytes received from client
        /// </summary>
        /// <param name="bytes">Bytes to process and react to.</param>
        /// <returns>Returns true on success, false on failure (with error in Error property)</returns>
        public bool Process(MemoryStream stream, NetworkStream connection) {
            if (State == SOCKS5States.Disconnected)
            {
                // SOCKS5 HANDSHAKE
                var packet = new SOCKS5.PacketHandshake(stream);

                if (packet.METHODS != 0x0)
                    throw new UDProxyException("Authentication is not yet supported!", new NotImplementedException());
                    
                UDProxy.DebugInfo("SOCKS5", "Responding to handshake request");
                if ( Respond(new PacketReplyHandshake(), connection) )
                {
                    State = SOCKS5States.Connected;
                    return true;
                }
            }
            else if (State == SOCKS5States.Connected)
            {
                // SOCKS5 Connection Requests
                // We're connected, so we're dealing with requests
                var packet = new SOCKS5.PacketRequest(stream);
                PacketReplyRequest reply = new PacketReplyRequest(packet);

                // Set our client's remote endpoint expected port to the one it's given us
                // Weird type casting is nessecary for safe translation
                MyClient.Port = (int)BitConverter.ToUInt16(packet.PORT, 0);

                // It's not ok :(
                if (reply.REP != SOCKS5Protocol.REP.OK)
                {
                    Respond(reply, connection);
                    return false;
                }

                switch (packet.CMD)
                {
                    case SOCKS5Protocol.CMD.BIND:
                        throw new UDProxyException("CMD BIND not yet supported!");
                    case SOCKS5Protocol.CMD.CONNECT:
                        throw new UDProxyException("CMD CONNECT not yet supported!");
                    case SOCKS5Protocol.CMD.UDP_ASSOC:
                        UDPConnection.TryBegin(ref reply, MyClient);
                        Respond(reply, connection);
                        break;
                }

            } 
            else
            {
                throw new UDProxyException("Unknown error (unimplemented function?)");
            }

            return false;
        }

        /// <summary>
        /// Writes a given packet to the client
        /// </summary>
        /// <param name="packet">IPacket packet</param>
        /// <param name="connection"></param>
        /// <returns>True on success, false on failure</returns>
        public bool Respond(IPacket packet, NetworkStream connection)
        {
            try
            {
                var bytes = packet.GetBytes();
                connection.Write(bytes, 0, bytes.Length);
            }
            catch (Exception e)
            {
                UDProxy.DebugException(e);
                return false;
            }

            return true;
        }

        #region Debugs

        public static void DebugInfo(string msg)
        {
            UDProxy.Debug("SOCKS5", msg, UDProxy.DebugLevels.Info);
        }

        public static void DebugWarn(string msg)
        {
            UDProxy.Debug("SOCKS5", msg, UDProxy.DebugLevels.Warn);
        }

        public static void DebugError(string msg)
        {
            UDProxy.Debug("SOCKS5", msg, UDProxy.DebugLevels.Error);
        }

        #endregion
    }

    
}
