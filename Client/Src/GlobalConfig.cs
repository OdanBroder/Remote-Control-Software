using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace Client.Src
{
    public static class GlobalConfig
    {
        public static string ip_server = "127.0.0.1";
        public static int port_server = 8080;

        // Define the type of connection
        public static int send_data_connection = 0x2000;
        public static int wait_connection = 0x2001;
        public static int try_connection = 0x2002;
        
    }
}

