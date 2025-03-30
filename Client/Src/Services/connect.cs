using System;
using System.Text;
using Client.Src.Utils;

namespace Client.Src.Services
{
    public static class Connect
    {
        public static void WaitConnect(string ip, int port)
        {
            string id = GetID.GetUniqueID();
            byte[] dataBytes = Encoding.UTF8.GetBytes(id);
            Connect2Server.SendToServer(ip, port, dataBytes, GlobalConfig.send_data_connection);
        }

        public static void TryConnect(string ipServer, int portServer, string uniqueId)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(uniqueId);
            Connect2Server.SendToServer(ipServer, portServer, dataBytes, GlobalConfig.try_connection);
        }

        public static void SendData(string ipServer, int portServer, string data)
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            Connect2Server.SendToServer(ipServer, portServer, dataBytes, GlobalConfig.send_data_connection);
        }
    }
}
