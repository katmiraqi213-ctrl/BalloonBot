using System;
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

            // استقبال الرسائل قبل تسجيل الدخول
            client.Messaging.OnMessage += async (c, message) =>
            {
                try
                {
                    string text = message.Content?.Trim() ?? "";

                    Console.WriteLine("");
                    Console.WriteLine("🔥🔥🔥 MESSAGE RECEIVED 🔥🔥🔥");
                    Console.WriteLine("📩 الرسالة: " + text);
                    Console.WriteLine("👤 UserId: " + message.UserId);
                    Console.WriteLine("🏠 GroupId: " + message.GroupId);
                    Console.WriteLine("🆔 MessageId: " + message.MessageId);

                    // اختبار الأمر
                    if (text == "!بالونات")
                    {
                        Console.WriteLine("🎈 تم اكتشاف أمر البالونات");

                        await c.Reply(
                            message,
                            "🎈 بوت البالونات شغال!\n" +
                            "اكتب !بالونات مساعدة"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "❌ MESSAGE ERROR: " + ex
                    );
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
            Console.WriteLine("🟢 الاتصال تم بواسطة Login");
            Console.WriteLine("📡 BalloonBot ينتظر رسائل الروم...");

            // مهم: لا نستدعي Connect() مرة ثانية
            await Task.Delay(Timeout.Infinite);
        }
    }
}
