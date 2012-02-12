using System;
using System.IO;
using System.Net;

namespace SimPlaza.UDProxy.SOCKS5
{
    public struct SOCKS5Protocol
    {
        public const byte VER = 0x5;
        public const byte RSV = 0x0;
        public static byte[] RSVx2 = new byte[] { 0x00, 0x00 };

        /// <summary>
        /// Commands for a SOCKS5 request
        /// </summary>
        public enum CMD : byte
        {
            INVALID = 0x00,
            CONNECT = 0x01,
            BIND = 0x02,
            UDP_ASSOC = 0x03
        }

        /// <summary>
        /// Type of destination for a SOCKS5 request
        /// </summary>
        public enum ATYP : byte
        {
            INVALID = 0x00,
            IPV4 = 0x01,
            DOMAIN = 0x03,
            IPV6 = 0x04
        }

        public enum REP : byte
        {
            OK,
            GENERAL_FAILURE,
            DISALLOWED,
            UNREACHABLE_NET,
            UNREACHABLE_HOST,
            REFUSED,
            TTL_EXPIRY,
            ILLEGAL_COMMAND,
            ILLEGAL_ADDRESS
        }
    }

    #region Packets

    public interface IPacket
    {
        byte[] GetBytes();
    }

    public class PacketHandshake
    {
        public byte VER
        {
            get { return SOCKS5Protocol.VER; }
            set { if (value != SOCKS5Protocol.VER) throw new UDProxyException("Incorrect Version: Only SOCKS version 5 is supported."); }
        }
        public byte NMETHODS;
        public byte METHODS; // Make this byte[] to eventually support multiple auths

        public PacketHandshake(MemoryStream stream)
        {
            // A handshake should always be 3 bytes. Reject otherwise
            if (stream.Length != 3)
                throw new UDProxyException("This isn't a valid handshake SOCKS5 handshake. Rejecting.");

            VER = (byte)stream.ReadByte();
            NMETHODS = (byte)stream.ReadByte();
            METHODS = (byte)stream.ReadByte();
            stream.Close();
        }
    }

    public class PacketReplyHandshake : IPacket 
    {
        public byte VER = SOCKS5Protocol.VER;
        public byte METHOD = 0x0;

        public byte[] GetBytes()
        {
            return new byte[] { VER, METHOD };
        }
    }

    public class PacketRequest
    {
        public byte VER
        {
            get { return SOCKS5Protocol.VER; }
            set { if (value != SOCKS5Protocol.VER) throw new UDProxyException("Incorrect Version: Only SOCKS version 5 is supported."); }
        }
        public SOCKS5Protocol.CMD CMD = 0x0;
        public byte RSV
        {
            get { return SOCKS5Protocol.RSV; }
            set { if (value != SOCKS5Protocol.RSV) throw new UDProxyException("Malformed SOCKS5 Packet: RSV should be 0"); }
        }
        public SOCKS5Protocol.ATYP ATYP = 0x0;
        public byte[] ADDR = new byte[] {0,0,0,0};
        public byte[] PORT = new byte[2];

        public PacketRequest(MemoryStream stream)
        {
            VER     = (byte) stream.ReadByte();
            CMD     = (SOCKS5Protocol.CMD) stream.ReadByte();
            RSV     = (byte) stream.ReadByte();
            ATYP    = (SOCKS5Protocol.ATYP) stream.ReadByte();

            // Get IP address
            switch (ATYP)
            {
                case SOCKS5Protocol.ATYP.IPV4:
                    ADDR = new byte[4];
                    break;
                case SOCKS5Protocol.ATYP.IPV6:
                    ADDR = new byte[16];
                    break;
                case SOCKS5Protocol.ATYP.DOMAIN:
                    int bytes = stream.ReadByte();
                    ADDR = new byte[bytes];
                    break;
                default:
                    ATYP = SOCKS5Protocol.ATYP.INVALID;
                    ADDR = new byte[4];
                    break;
            }
            stream.Read(ADDR, 0, ADDR.Length);

            // Endian check (fucking network byte order)
            // When held in a received packet, port byte order is little endian
            // When held in a reply/transmission packet, port byte order is big endian
            stream.Read(PORT, 0, 2);
            if (BitConverter.IsLittleEndian) Array.Reverse(PORT);

            stream.Close();
        }
    }

    public class PacketReplyRequest : IPacket
    {
        public byte VER = SOCKS5Protocol.VER;
        public byte RSV = SOCKS5Protocol.RSV;
        public SOCKS5Protocol.REP REP = 0x0;
        public SOCKS5Protocol.ATYP ATYP = 0x0;
        public byte[] ADDR = new byte[] {0,0,0,0};
        public ushort PORT = 0;

        public PacketReplyRequest(PacketRequest packet)
        {
            if (packet.ATYP == SOCKS5Protocol.ATYP.INVALID)
            {
                // We were given an invalid ATYP, so no more processing.
                REP = SOCKS5Protocol.REP.ILLEGAL_ADDRESS;
                ATYP = SOCKS5Protocol.ATYP.INVALID;
                throw new UDProxyException("Given an illegal address type.");
            }

            if (!Enum.IsDefined(typeof(SOCKS5Protocol.CMD), packet.CMD))
            {
                // We were given an invalid command, so no more processing
                REP = SOCKS5Protocol.REP.ILLEGAL_COMMAND;
                throw new UDProxyException("Given an illegal request command.");
            }

            ATYP = packet.ATYP;
            ADDR = packet.ADDR;

            // Our listening port for UDP, so the client knows where to transmit to
            PORT = (ushort)UDProxy.gConfiguration.Config["ListenPort"];
        }

        public byte[] GetBytes()
        {
            var stream = new MemoryStream();
            var header = new byte[] { VER, (byte)REP, RSV, (byte)ATYP };
            var port = BitConverter.GetBytes(PORT);

            // Fucking network byte order...
            if (BitConverter.IsLittleEndian) Array.Reverse(port);

            stream.Write(header, 0, 4);
            stream.Write(ADDR, 0, ADDR.Length);
            stream.Write(port, 0, port.Length);

            var final = stream.ToArray();
            stream.Close();
            return final;
        }
    }

    public class PacketUDP : IPacket
    {
        public byte[] RSVx2
        {
            get { return SOCKS5Protocol.RSVx2; }
            set { if (value != SOCKS5Protocol.RSVx2) throw new UDProxyException("Malformed UDP Data Packet: RSV should be 0x0000"); }
        }
        public byte FRAG = 0x00;
        public SOCKS5Protocol.ATYP ATYP;
        public byte[] ADDR;
        public byte[] PORT = new byte[2];
        public byte[] DATA;

        /// <summary>
        /// Creates a UDP association with native data types for transmission
        /// </summary>
        /// <param name="addressType"></param>
        /// <param name="port"></param>
        /// <param name="data"></param>
        public PacketUDP(SOCKS5Protocol.ATYP addressType, IPAddress address, ushort port, byte[] data)
        {
            RSVx2 = SOCKS5Protocol.RSVx2;
            ATYP = addressType;
            ADDR = address.GetAddressBytes();

            if (ATYP == SOCKS5Protocol.ATYP.IPV4 && ADDR.Length != 4)
                throw new UDProxyException("Invalid UDP packet parameter: Incorrect IPv4 address length");

            if (ATYP == SOCKS5Protocol.ATYP.IPV6 && ADDR.Length != 16)
                throw new UDProxyException("Invalid UDP packet parameter: Incorrect IPv6 address length");

            PORT = BitConverter.GetBytes(port);
            DATA = data;
        }

        public PacketUDP(SOCKS5Protocol.ATYP addressType, string domain, ushort port, byte[] data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a UDP association packet from a received one
        /// </summary>
        /// <param name="stream"></param>
        public PacketUDP(MemoryStream stream)
        {
            stream.Read(RSVx2, 0, 2);
            FRAG = (byte) stream.ReadByte();
            ATYP = (SOCKS5Protocol.ATYP) stream.ReadByte();

            // TODO: Turn this into external logic plz
            // Get IP address
            switch (ATYP)
            {
                case SOCKS5Protocol.ATYP.IPV4:
                    ADDR = new byte[4];
                    break;
                case SOCKS5Protocol.ATYP.IPV6:
                    ADDR = new byte[16];
                    break;
                case SOCKS5Protocol.ATYP.DOMAIN:
                    int bytes = stream.ReadByte();
                    ADDR = new byte[bytes];
                    break;
                default:
                    ATYP = SOCKS5Protocol.ATYP.INVALID;
                    ADDR = new byte[4];
                    break;
            }
            stream.Read(ADDR, 0, ADDR.Length);
            stream.Read(PORT, 0, 2);
            if (BitConverter.IsLittleEndian) Array.Reverse(PORT);

            // Trim the fat bitch!
            DATA = new byte[(stream.Length - stream.Position)];
            stream.Read(DATA, 0, DATA.Length);
            stream.Close();
        }

        public byte[] GetBytes()
        {
            var stream = new MemoryStream();
            var port = PORT;

            // Fucking network byte order...
            if (BitConverter.IsLittleEndian) Array.Reverse(port);

            stream.Write(RSVx2, 0, 2);
            stream.WriteByte(FRAG);
            stream.WriteByte((byte)ATYP);
            stream.Write(ADDR, 0, ADDR.Length);
            stream.Write(port, 0, 2);
            stream.Write(DATA, 0, DATA.Length);

            var final = stream.ToArray();
            stream.Close();
            return final;
        }
    }
    #endregion
}
