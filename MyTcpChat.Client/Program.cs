using System.Net.Sockets;
using System.Net;
using System.Text;

namespace MyTcpChat.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);

            try
            {
                UdpClient scanClient = new UdpClient(AddressFamily.InterNetwork);
                scanClient.Client.ReceiveTimeout = 5000;

                UdpReceiveResult result = default;
                string message = string.Empty;

                try
                {
                    for (int i = 1; i <= 5; i++)
                    {
                        Console.WriteLine("Scan network for the chat server. Try " + i + ".");
                        await scanClient.SendAsync(Encoding.UTF8.GetBytes("ServerDiscoveryRequest"), new IPEndPoint(IPAddress.Broadcast, 7701));
                        try
                        {
                            IPEndPoint? remoteEndPoint = null;
                            var data = scanClient.Receive(ref remoteEndPoint);
                            result = new UdpReceiveResult(data, remoteEndPoint);
                            message = Encoding.UTF8.GetString(result.Buffer);

                            Console.WriteLine($"Scan receive message [{message}] from {remoteEndPoint}");

                            if (!message.StartsWith("YES"))
                            {
                                Console.WriteLine("Servers not found!");
                                return;
                            }
                            else
                            {
                                break;
                            }
                        }
                        catch
                        {
                            Console.WriteLine("Servers not found!");
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Servers not found!");
                    return;
                }

                var port = int.Parse(message.Split(':')[1]);
                var ip = result.RemoteEndPoint.Address;
                Console.WriteLine($"Server found at {ip}:{port}");

                await tcpClient.ConnectAsync(ip, port);
                Console.WriteLine("Connected to the chat server.");

                NetworkStream stream = tcpClient.GetStream();

                _ = Task.Run(() => ReceiveMessagesAsync(stream));

                bool isAuthenticated = false;

                while (!isAuthenticated)
                {
                    Console.WriteLine("Please register (/register <username> <password>) or login (/login <username> <password>):");
                    string authCommand = Console.ReadLine();

                    if (!string.IsNullOrEmpty(authCommand) && (authCommand.StartsWith("/register ") || authCommand.StartsWith("/login ")))
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes(authCommand);

                        await stream.WriteAsync(buffer, 0, buffer.Length);

                        await Task.Delay(500);

                        isAuthenticated = authCommand.StartsWith("/login ");
                    }

                    if (isAuthenticated)
                    {
                        Console.WriteLine("You are now authenticated!");
                        break;
                    }
                }

                while (true)
                {
                    string messageToSend = Console.ReadLine();

                    if (!string.IsNullOrEmpty(messageToSend))
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes(messageToSend);

                        await stream.WriteAsync(buffer, 0, buffer.Length);

                        if (messageToSend.ToUpper() == "QUIT")
                            break;
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
            }
            finally
            {
                tcpClient.Close();
                Console.WriteLine("Disconnected from chat server.");
            }
        }

        private static async Task ReceiveMessagesAsync(NetworkStream stream)
        {

            byte[] buffer = new byte[1024];

            try
            {

                int bytesRead;


                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {

                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine(receivedMessage);


                    Array.Clear(buffer, 0, buffer.Length);
                }
            }
            catch (Exception e)
            {

                Console.WriteLine($"An exception occurred: {e.Message}");

            }
        }

    }
}
