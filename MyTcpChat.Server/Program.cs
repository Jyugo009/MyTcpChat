using System.Net.Sockets;
using System.Net;
using System.Text;

namespace MyTcpChat.Server
{
    public class Program
    {
        private static readonly List<ClientInfo> clients = new List<ClientInfo>();
        private static readonly AuthenticationService authService = new AuthenticationService();
        public static async Task Main(string[] args)
        {
            var task = Task.Run(async () =>
            {
                UdpClient udpClient = new UdpClient(7701, AddressFamily.InterNetwork);

                while (true)
                {
                    var result = await udpClient.ReceiveAsync();
                    var message = Encoding.UTF8.GetString(result.Buffer);
                    if (message == "ServerDiscoveryRequest")
                    {
                        message = $"YES PORT:{7700}";
                        var sendTask = udpClient.SendAsync(Encoding.UTF8.GetBytes(message), result.RemoteEndPoint);
                    }
                }
            });

            var server = new TcpListener(IPAddress.Any, 7700);
            server.Start();

            try
            {
                Console.WriteLine($"Server started on {server.LocalEndpoint}");

                while (true)
                {
                    Console.WriteLine("Waiting for a connection...");
                    TcpClient client = await server.AcceptTcpClientAsync();
                    Console.WriteLine("Connected!");

                    ClientInfo clientInfo = new ClientInfo(client);

                    _ = HandleConnectionAsync(clientInfo);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"SocketException: {e.Message}");
            }
            finally
            {
                server?.Stop();
            }
        }

        private static async Task HandleConnectionAsync(ClientInfo clientInfo)
        {
            try
            {
                await authService.AuthenticateClient(clientInfo);

                if (!clientInfo.IsAuthenticated) return;

                NetworkStream stream = clientInfo.TcpClient.GetStream();
                byte[] buffer = new byte[1024];

                lock (clients) clients.Add(clientInfo);

                BroadcastMessage($"{clientInfo.User.Username} has joined the chat.", clientInfo);

                while ((await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    string receivedMessage = Encoding.UTF8.GetString(buffer).Trim('\0', ' ');
                    Array.Clear(buffer);

                    if (receivedMessage.StartsWith("@"))
                    {
                        var parts = receivedMessage.Split(new[] { ' ' }, 2);
                        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                        {
                            string targetUsername = parts[0][1..];
                            SendPrivateMessage(targetUsername, parts[1], clientInfo);
                        }
                        else
                        {
                            SendMessage(clientInfo.TcpClient,
                                        "To send a private message use '@<username> <message>'.");
                        }
                    }
                    else
                    {
                        BroadcastMessage($"{clientInfo.User.Username}: {receivedMessage}", clientInfo);
                    }
                }
            }

            catch (Exception e)
            {
                Console.WriteLine($"An error occurred with the client {clientInfo.Id}: {e.Message}");
            }

            finally
            {
                lock (clients)
                {
                    clients.Remove(clientInfo);
                    clientInfo.TcpClient.Close();
                }

                BroadcastMessage($"{clientInfo.User.Username} has left the chat.", clientInfo);
            }
        }

        public static void BroadcastMessage(string message, ClientInfo sender = null)
        {
            SaveMessageToHistory(message);

            lock (clients)
            {
                foreach (var target in clients.ToList())
                {
                    if (target.IsAuthenticated && target != sender)
                    {
                        SendMessage(target.TcpClient, message);
                    }
                }
            }
        }
        private static void SendMessage(TcpClient tcpClient, string message)
        {
            if (tcpClient.Connected)
            {
                NetworkStream stream = tcpClient.GetStream();
                byte[] msgBuffer = Encoding.UTF8.GetBytes(message + Environment.NewLine);

                try
                {
                    stream.Write(msgBuffer, 0, msgBuffer.Length);
                    stream.Flush();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());

                    lock (clients)
                    {
                        var disconnectingClient = clients.FirstOrDefault(c => c.TcpClient == tcpClient);
                        if (disconnectingClient != null)
                        {
                            clients.Remove(disconnectingClient);
                        }
                        tcpClient.Close();
                    }

                    BroadcastMessage("A user has disconnected.");
                }
            }
            else
            {
                var disconnectedClient = clients.FirstOrDefault(c => c.TcpClient == tcpClient);
                if (disconnectedClient != null)
                {
                    clients.Remove(disconnectedClient);
                }

                tcpClient.Close();

                BroadcastMessage("A user has disconnected.");
            }

        }

        private static void SendPrivateMessage(string username, string message, ClientInfo sender)
        {
            lock (clients)
            {
                var targetClient = clients.FirstOrDefault(c => c.User.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
                if (targetClient != null && targetClient.IsAuthenticated)
                {
                    string formattedMsg = $"{sender.User.Username} whispers to {username}: {message}";
                    SendMessage(targetClient.TcpClient, formattedMsg);

                    SendMessage(sender.TcpClient,
                                $"You whisper to {username}: {message}");

                    SaveMessageToHistory(formattedMsg);

                }
                else
                {
                    SendMessage(sender.TcpClient,
                                $"User '{username}' not found or not available.");
                }
            }
        }

        private static void SaveMessageToHistory(string message)
        {
            try
            {
                var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDirectory);

                string logFile = Path.Combine(logDirectory, $"ChatHistory_{DateTime.UtcNow:yyyy-MM-dd}.txt");

                File.AppendAllText(logFile, $"{DateTime.UtcNow:HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save message to history: {ex.Message}");
            }
        }
    }
}
