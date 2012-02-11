using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SimPlaza.UDProxy.SOCKS5;
using SimPlaza.UDProxy.UDP;

namespace SimPlaza.UDProxy
{
    class Connection
    {
        // Thanks http://www.yoda.arachsys.com/csharp/readbinary.html for optimization tips

        public Thread MyThread;
        public TcpClient MyConnection;
        public NetworkStream MyStream;
        public SOCKS5Server MySocks;

        private int finalBytes;
        private byte[] finalBuffer;
        private MemoryStream packetReadStream;

        public Connection(TcpClient client)
        {
            // We've recieved a connection request, create thread for it!
            MyConnection = client;
            MyThread = new Thread(new ThreadStart(StreamLoop));
            MyStream = MyConnection.GetStream();
            MySocks = new SOCKS5Server(
                (IPEndPoint) MyConnection.Client.RemoteEndPoint
            );

            Console.WriteLine("\n___ CONNECTION BEGIN ________________________________");
            DebugInfo("Accepted connection with " + MyConnection.Client.RemoteEndPoint.ToString());

            MyThread.Start();
        }

        private void StreamLoop()
        {
            while (true)
            {
                try
                {
                    GetNextTransmission();
                }
                catch
                {
                    // Error, break out!
                    break;
                }

                // No bytes == failure
                if (finalBytes == 0) break;

                packetReadStream = new MemoryStream(finalBuffer);

                try
                {
                    MySocks.Process(packetReadStream, MyStream);
                }
                catch (Exception e)
                {
                    packetReadStream.Close();
                    UDProxy.DebugException(e);
                    break;
                }
            }

            DebugInfo("No more data left, closing stream and ending thread.");
            Console.WriteLine("___ CONNECTION END __________________________________");
            Console.WriteLine(
                String.Format(" {0} bytes sent, {1} bytes received", UDPConnection.TotalUp, UDPConnection.TotalDown)
            );

            if (UDPConnection.MyThread != null)
                UDPConnection.MyThread.Abort();

            if (UDPConnection.MyUDP != null)
                UDPConnection.MyUDP.Close();

            MyStream.Close();
            MyConnection.Close();
        }


        /// <summary>
        /// Waits for and recieves a FULL stream of data into the final buffer for processing.
        /// </summary>
        private void GetNextTransmission()
        {
            int totalBytes = 0;
            int recvBytes = 0;
            byte[] totalBuffer = new byte[MyConnection.ReceiveBufferSize];

            do
            {
                // Read the bytes in the stream
                try
                {
                    // TODO: Check for off-by-one errors
                    recvBytes = MyStream.Read(totalBuffer, recvBytes, totalBuffer.Length);
                }
                catch (Exception e)
                {
                    finalBuffer = new byte[0];
                    finalBytes = 0;
                    throw new UDProxyException(e.Message);
                }

                // Buffer was too small, more data is avaliable!
                if (recvBytes == totalBuffer.Length && MyStream.DataAvailable)
                {
                    DebugWarn("Buffer was too small, compensating...");

                    byte[] newBuffer = new byte[totalBuffer.Length + 8126];
                    Array.Copy(totalBuffer, newBuffer, totalBuffer.Length);
                    totalBuffer = newBuffer;
                }

                totalBytes += recvBytes;

            } while (MyStream.DataAvailable);

            //DebugInfo(String.Format("Received full stream of {0} bytes.", totalBytes));

            // Trim the buffer and return
            finalBuffer = new byte[totalBytes];
            finalBytes = totalBytes;
            Array.Copy(totalBuffer, finalBuffer, totalBytes);
        }

        #region Debugs

        private void DebugInfo(string msg)
        {
            UDProxy.Debug(this.GetType().Name, MyConnection.Client.RemoteEndPoint.ToString() + " | " + msg, UDProxy.DebugLevels.Info);
        }

        private void DebugWarn(string msg)
        {
            UDProxy.Debug(this.GetType().Name, MyConnection.Client.RemoteEndPoint.ToString() + " | " + msg, UDProxy.DebugLevels.Warn);
        }

        private void DebugError(string msg)
        {
            UDProxy.Debug(this.GetType().Name, MyConnection.Client.RemoteEndPoint.ToString() + " | " + msg, UDProxy.DebugLevels.Error);
        }

        #endregion

        
    }

    
}
