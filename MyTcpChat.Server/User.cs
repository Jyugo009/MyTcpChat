using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace MyTcpChat.Server
{
    public class User
    {
        public string Username { get; }
        public string PasswordHash { get; }

        public User(string username, string password)
        {
            Username = username;
            PasswordHash = HashPassword(password);
        }

        private static string HashPassword(string password)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));

            StringBuilder builder = new StringBuilder();

            foreach (byte b in bytes)
                builder.Append(b.ToString("x2"));

            return builder.ToString();
        }

        public bool VerifyPassword(string passwordToVerify)
        {
            return PasswordHash == HashPassword(passwordToVerify);
        }
    }
}
