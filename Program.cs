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
            Console.WriteLine("🎈 BalloonBot START");

            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("❌ WOLF_EMAIL أو WOLF_PASSWORD فارغ");
                return;
            }

            IWolfClient client = new WolfClient();

            client.Messaging.OnMessage += async (c, message) =>
            {
                try
                {
                    string text = message.Content?.Trim() ?? "";

                    Console.WriteLine("");
                    Console.WriteLine("════════════════════════════");
                    Console.WriteLine("🔥 MESSAGE FROM WOLF");
                    Console.WriteLine("📩 CONTENT = [" + text + "]");
                    Console.WriteLine("👤 USER = " + message.UserId);
                    Console.WriteLine("🏠 GROUP = " + message.GroupId);
                    Console.WriteLine("🆔 MESSAGE = " + message.MessageId);
                    Console.WriteLine("════════════════════════════");

                    // نقبل الأمر حتى لو كان بيه مسافات
                    if (text.StartsWith(
                        "!بالونات",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("🎈🎈🎈 BALLOON COMMAND DETECTED 🎈🎈🎈");

                        await c.Reply(
                            message,
                            "🎈 تم استلام أمر البالونات من هذا الروم!"
                        );

                        Console.WriteLine("✅ REPLY SENT");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ ERROR = " + ex);
                }
            };

            Console.WriteLine("✅ OnMessage registered");
            Console.WriteLine("🔐 Login...");

            bool login = await client.Login(email, password);

            Console.WriteLine("LOGIN RESULT = " + login);

            if (!login)
            {
                Console.WriteLine("❌ Login failed");
                return;
            }

            Console.WriteLine("✅ Login OK");
            Console.WriteLine("🟢 CONNECTED");
            Console.WriteLine("📡 WAITING FOR ROOM MESSAGES...");

            await Task.Delay(Timeout.Infinite);
        }
    }
}
