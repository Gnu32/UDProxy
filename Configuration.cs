using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SimPlaza.UDProxy
{
    class Configuration
    {
        public Dictionary<string, object> Config = new Dictionary<string, object>{
            {"ListenIP", null},
            {"ListenPort", null},
            {"MyExternalIP", null},
            {"TargetIP", null},
            {"TargetPorts", null},
            {"GUI", null}
        };

        private bool complete = false;

        public Configuration(string[] args)
        {
            if (Array.IndexOf(args, "--fab") > -1)
                UDProxy.lol = true;

            if (Config["MyExternalIP"] == null)
                Config["MyExternalIP"] = NetUtilities.GetExternalIP();

            Console.WriteLine("# I'm now going to ask you a few configuration questions.");
            //Console.WriteLine("# This will generate a batch file that executes UDProxy with these settings.");
            Console.WriteLine("# Each question has a default or example answer in [Brackets]. Simply press [ENTER] to use it.");

            if (Config["MyExternalIP"] == null)
            {
                while (true)
                {
                    Config["MyExternalIP"] = Ask<IPAddress>("What is your external IP? (Check http://whatismyip.org)", "0.0.0.0") as IPAddress;

                    if (Config["MyExternalIP"] != IPAddress.Any) break;
                    Console.WriteLine("# 0.0.0.0 is not a valid external IP!");
                }
            }

            if (Config["TargetIP"] == null)
                Config["TargetIP"] = Ask<IPAddress>("What is the local IP of your server?", "192.168.0.32") as IPAddress;

            if (Config["TargetPorts"] == null)
                Config["TargetPorts"] = Ask<List<ushort>>("What are the ports of the regions of your server? (Seperate by comma)", "9000") as List<ushort>;

            if (Config["ListenIP"] == null)
                Config["ListenIP"] = Ask<IPAddress>("What system IP do you want the SOCKS5 server to listen on?", "0.0.0.0") as IPAddress;

            if (Config["ListenPort"] == null)
                Config["ListenPort"] = Ask<ushort>("What system port do you want the SOCKS5 server to listen on?", "1080");

            complete = true;
            Console.WriteLine();
            UDProxy.DebugInfo("Configuration", "Configuration complete! I am...");
            UDProxy.DebugInfo("Configuration", "> Redirecting UDP packets intended for: "
                + Config["MyExternalIP"].ToString() + ":" + AnonymousList2String<ushort>(Config["TargetPorts"]));
            UDProxy.DebugInfo("Configuration", "> ...to and from local server: "
                + Config["TargetIP"].ToString() + ":" + AnonymousList2String<ushort>(Config["TargetPorts"]));


        }

        public object Ask<T>(string question, string def)
        {
            bool validResponse = false;
            string type = typeof(T).Name;

            if (type.StartsWith("List"))
                type = "List." + typeof(T).GetGenericArguments()[0].Name;

            do
            {
                Console.Write( String.Format("\n# {0} [{1}]:\n> ", question, def.ToString()) );
                var response = Console.ReadLine();
                if (String.IsNullOrWhiteSpace(response)) response = def;

                switch (type)
                {
                    case "IPAddress":
                        IPAddress IPOutput;
                        validResponse = IPAddress.TryParse(response, out IPOutput);
                        if (!validResponse) break;
                        return IPOutput;
                    case "Boolean":
                        if (Regex.IsMatch(response, "^(y(es)?|true)$", RegexOptions.IgnoreCase)) return true;
                        if (Regex.IsMatch(response, "^(no|false)$", RegexOptions.IgnoreCase)) return false;
                        break;
                    case "UInt16":
                        UInt16 shortOutput;
                        validResponse = UInt16.TryParse(response, out shortOutput);
                        if (!validResponse) break;
                        return shortOutput;
                    case "List.UInt16":
                        List<UInt16> portListOutput;
                        validResponse = TryParsePorts(response, out portListOutput);
                        if (!validResponse) break;
                        return portListOutput;
                }

                Console.WriteLine("# Sorry, '" + response + "' is not a valid answer.");
            } while (true);
        }

        public bool TryParsePorts(string ports, out List<ushort> listPorts)
        {
            listPorts = new List<ushort>();
            var list = ports.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string port in list)
            {
                ushort portnum;
                if (UInt16.TryParse(port, out portnum))
                    listPorts.Add(portnum);
            }

            return (listPorts.Count > 0); 
        }

        public string AnonymousList2String<T>(object list)
        {
            return String.Join(",", ( (List<T>)list ).ToArray());
        }
    }
}
