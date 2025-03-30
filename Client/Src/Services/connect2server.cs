using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using Client.Src;

namespace Client.Src.Services
{
    public static class Connect2Server
    {
        public static bool SendToServer(string ip, int port, byte[] data, int type = 0x2000)
        {
            try
            {
                MessageBox.Show("Connecting to server...");
                if (UDPConnect(ip, port, data, type))
                {
                    return true;
                }
                else if (TCPConnect(ip, port, data, type))
                {
                    return true;
                }
                else
                {
                    MessageBox.Show("Error: Connection failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
                return false;
            }
        }

        public static bool UDPConnect(string ip, int port, byte[] data, int type = 0x2000)
        {
            if (!IPAddress.TryParse(ip, out IPAddress ipAddress))
            {
                MessageBox.Show("Invalid IP address");
                return false;
            }

            try
            {
                using (UdpClient client = new UdpClient())
                {
                    client.Connect(ipAddress, port);
                    client.Send(data, data.Length);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
                return false;
            }
        }

        public static bool TCPConnect(string ip, int port, byte[] data, int type = 0x2000)
        {
            if (!IPAddress.TryParse(ip, out IPAddress ipAddress))
            {
                MessageBox.Show("Invalid IP address");
                return false;
            }

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(ipAddress, port);
                    using (NetworkStream stream = client.GetStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
                return false;
            }
        }
    }
}

