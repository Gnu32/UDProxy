using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Drawing;

namespace SimPlaza.UDProxy
{
    class UDProxy
    {
        public static Connection gConnection;
        public static Configuration gConfiguration;
        //public static NotifyIcon gTrayIcon;
        public static bool lol;
        private static TcpListener gTCP;
        private static Array fCols;
        private static Random fRand;

        public enum DebugLevels
        {
            Error = ConsoleColor.Red,
            Warn = ConsoleColor.Yellow,
            Info = ConsoleColor.Gray
        }

        static void Main(string[] args)
        {
            // Set up console
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.Clear();
            Console.WriteLine(@"
`..     `..`.....    `.......                                     
`..     `..`..   `.. `..    `..                                   
`..     `..`..    `..`..    `..`. `...   `..    `..   `..`..   `..
`..     `..`..    `..`.......   `..    `..  `..   `. `..  `.. `.. 
`..     `..`..    `..`..        `..   `..    `..   `.       `...  
`..     `..`..   `.. `..        `..    `..  `..  `.  `..     `..  
  `.....   `.....    `..       `...      `..    `..   `..   `..   
                             SIMPL -  Major Rasputin 2012  `..    
__________________________________________________________________");

            // Load configuration
            gConfiguration = new Configuration(args);

            // Begin listening for connection requests
            gTCP = new TcpListener(
                (IPAddress)gConfiguration.Config["ListenIP"],
                (ushort)gConfiguration.Config["ListenPort"]
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
            if ( UDProxy.lol )
            {
                if (fRand == null) fRand = new Random(69);
                if (fCols == null) fCols = Enum.GetValues(typeof(ConsoleColor));
                ConsoleColor color;

                do
                {
                    color = (ConsoleColor)fCols.GetValue(fRand.Next(fCols.Length));
                } while (color == ConsoleColor.DarkRed);

                Console.ForegroundColor = color;
                Console.WriteLine(cls + " | " + msg);
            }
            else
            {
                Console.ForegroundColor = (ConsoleColor)level;
                Console.Write(cls);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(" | " + msg);
            }
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
