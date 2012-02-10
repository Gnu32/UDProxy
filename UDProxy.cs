using System;
using System.Net;
using System.Net.Sockets;

namespace SimPlaza.UDProxy
{
    class UDProxy
    {
        public static Connection gConnection;
        public struct gConfiguration {
            public static IPAddress ListenIP = IPAddress.Any;
            public static ushort ListenPort = 1080;

            public static IPAddress MyExternalIP = IPAddress.Parse("90.206.69.103");
            public static IPAddress TargetIP = IPAddress.Parse("192.168.0.32");
            public static int TargetPort = 9000;

        };

        public enum DebugLevels
        {
            Error = ConsoleColor.Red,
            Warn = ConsoleColor.Yellow,
            Info = ConsoleColor.Gray
        }

        private static TcpListener gTCP;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.Clear();
            Console.WriteLine("************************");
            Console.WriteLine("|   SPRD - UDP Proxy   |");
            Console.WriteLine("************************");

            // Begin listening for connection requests
            gTCP = new TcpListener(
                gConfiguration.ListenIP,
                gConfiguration.ListenPort
            );
            gTCP.Start();
            DebugInfo("UDProxy", "Waiting for SOCKS connections on " + gTCP.LocalEndpoint.ToString());

            while (true)
            {
                TcpClient newClient = gTCP.AcceptTcpClient();
                gConnection = new Connection(newClient);
            }
        }

        #region Debug

        public static void Debug(string cls, string msg, DebugLevels level)
        {
            Console.ForegroundColor = (ConsoleColor)level;
            Console.Write(cls);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" | " + msg);
        }

            public static void DebugInfo(string cls, string msg)
            {
                Debug(cls, msg, DebugLevels.Info);
            }

            public static void DebugWarn(string cls, string msg)
            {
                Debug(cls, msg, DebugLevels.Warn);
            }

            public static void DebugError(string cls, string msg)
            {
                Debug(cls, msg, DebugLevels.Error);
            }

            public static void DebugException(Exception e)
            {
                Debug(e.Source, e.Message, DebugLevels.Error);
            }

        #endregion
    }
}
