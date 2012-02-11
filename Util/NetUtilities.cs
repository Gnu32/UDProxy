using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Net.NetworkInformation;

namespace SimPlaza.UDProxy
{
    class NetUtilities
    {
        public static IPAddress GetExternalIP()
        {
            WebRequest request;
            WebResponse response;
            string ip;
            IPAddress finalIP;

            try
            {
                request = HttpWebRequest.Create("http://whatismyip.org/");
                request.Method = "GET";
                response = request.GetResponse();

                ip = new StreamReader(response.GetResponseStream(), System.Text.Encoding.UTF8).ReadToEnd();
                if (!IPAddress.TryParse(ip, out finalIP))
                    throw new UDProxyException("Invalid response from WhatIsMyIP: " + ip);
            }
            catch
            {
                UDProxy.DebugWarn("NetUtils", "Could not get external IP, reverting to configuration.");
                return IPAddress.Any;
            }

            UDProxy.DebugInfo("NetUtils", "Fetched external IP as " + ip);
            return finalIP;
        }
    }
}
