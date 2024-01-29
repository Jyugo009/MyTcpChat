using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MyTcpChat.Server
{
    public class ClientInfo
    {
        public TcpClient TcpClient { get; set; }
        public string Id { get; private set; }
        public User User { get; private set; }
        public bool IsAuthenticated => User != null;

        public ClientInfo(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
            Id = tcpClient.Client.RemoteEndPoint.ToString();
            User = null;
        }

        public void Authenticate(User user)
        {
            User = user;
        }
    }
}
