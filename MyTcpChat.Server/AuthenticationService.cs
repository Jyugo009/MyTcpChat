using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MyTcpChat.Server
{
    public class AuthenticationService
    {
        private readonly ConcurrentDictionary<string, User> _users = new ConcurrentDictionary<string, User>();

        public bool Register(string username, string password)
        {
            if (_users.ContainsKey(username))
                return false;

            var user = new User(username, password);
            return _users.TryAdd(username, user);
        }

        public bool Login(string username, string password)
        {
            if (_users.TryGetValue(username, out var user))
                return user.VerifyPassword(password);

            return false;
        }

        public async Task AuthenticateClient(ClientInfo clientInfo)
        {
            NetworkStream stream = clientInfo.TcpClient.GetStream();
            byte[] buffer = new byte[1024];

            while (!clientInfo.IsAuthenticated && (await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                string command = Encoding.UTF8.GetString(buffer).Trim('\0', ' ');

                Array.Clear(buffer, 0, buffer.Length);

                if (command.StartsWith("/register "))
                {
                    string[] parts = command.Split(' ', 3);

                    if (parts.Length == 3 && Register(parts[1], parts[2]))
                    {
                        var newUser = new User(parts[1], parts[2]);
                        SendMessage(clientInfo.TcpClient, "Registration successful.");
                        clientInfo.Authenticate(newUser);

                    }
                    else
                    {

                        SendMessage(clientInfo.TcpClient, "Registration failed - username may already be taken or invalid.");
                    }
                }
                else if (command.StartsWith("/login "))
                {
                    string[] parts = command.Split(' ', 3);
                    if (parts.Length == 3 && Login(parts[1], parts[2]))
                    {

                        SendMessage(clientInfo.TcpClient, "Login successful.");
                        clientInfo.Authenticate(new User(parts[1], ""));

                    }
                    else
                    {

                        SendMessage(clientInfo.TcpClient, "Login failed. Incorrect credentials or user does not exist.");
                    }
                }
            }
        }

        private void SendMessage(TcpClient tcpclient, string message)
        {
            NetworkStream stream = tcpclient.GetStream();
            byte[] messageBuffer = Encoding.UTF8.GetBytes(message + "\n");
            stream.Write(messageBuffer, 0, messageBuffer.Length);
        }
    }
}
