using System;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

namespace BalloonBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            Console.WriteLine("🎈 BalloonBot START");

            if (string.IsNullOrWhiteSpace(email))
            {
                Console.WriteLine("❌ WOLF_EMAIL فارغ");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("❌ WOLF_PASSWORD فارغ");
                return;
            }

            IWolfClient client = new WolfClient();

            Console.WriteLine("🔐 Login...");

            bool result = await client.Login(email, password);

            Console.WriteLine("LOGIN RESULT = " + result);

            if (!result)
            {
                Console.WriteLine("❌ Login failed");
                return;
            }

            Console.WriteLine("✅ Login OK");

            client.Messaging.OnMessage += async (c, message) =>
            {
                Console.WriteLine("");
                Console.WriteLine("🔥🔥🔥 RECEIVED 🔥🔥🔥");
                Console.WriteLine("Content = " + message.Content);
                Console.WriteLine("UserId = " + message.UserId);
                Console.WriteLine("GroupId = " + message.GroupId);
                Console.WriteLine("MessageId = " + message.MessageId);
                Console.WriteLine("");

                await Task.CompletedTask;
            };

            Console.WriteLine("✅ OnMessage registered");

            await client.Connect();

            Console.WriteLine("🟢 CONNECTED");
            Console.WriteLine("📡 WAITING FOR MESSAGES...");

            await Task.Delay(Timeout.Infinite);
        }
    }
}
