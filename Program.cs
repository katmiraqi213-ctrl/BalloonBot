using System;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

namespace BalloonBot
{
    public class Program
    {
        private static IWolfClient? _client;

        public static async Task Main(string[] args)
        {
            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(
                    "❌ WOLF_EMAIL أو WOLF_PASSWORD غير موجود."
                );
                return;
            }

            Console.WriteLine("🎈 تشغيل BalloonBot...");

            _client = new WolfClient();

            Console.WriteLine("🔐 جاري تسجيل الدخول إلى Wolf...");

            bool loginResult = await _client.Login(email, password);

            if (!loginResult)
            {
                Console.WriteLine(
                    "❌ فشل تسجيل الدخول إلى Wolf."
                );
                return;
            }

            Console.WriteLine(
                "✅ تم تسجيل الدخول بنجاح."
            );

            // نفس ترتيب MazajBot بالضبط:
            // Login → OnMessage → Connect

            _client.Messaging.OnMessage += async (client, message) =>
            {
                try
                {
                    Console.WriteLine("");
                    Console.WriteLine("🔥🔥🔥 MESSAGE RECEIVED 🔥🔥🔥");

                    string text =
                        message.Content?.Trim() ?? "";

                    Console.WriteLine(
                        "📩 Content: " + text
                    );

                    Console.WriteLine(
                        "👤 UserId: " + message.UserId
                    );

                    Console.WriteLine(
                        "👥 GroupId: " + message.GroupId
                    );

                    Console.WriteLine(
                        "🆔 MessageId: " + message.MessageId
                    );

                    Console.WriteLine(
                        "================================="
                    );

                    // اختبار فقط:
                    // إذا وصلت الرسالة للبوت، يرد عليها.
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        await client.Messaging.SendMessage(
                            message.GroupId,
                            "🎈 وصلتني رسالتك: " + text
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "❌ MESSAGE ERROR:"
                    );

                    Console.WriteLine(
                        ex.ToString()
                    );
                }
            };

            Console.WriteLine(
                "✅ تم تسجيل OnMessage."
            );

            await _client.Connect();

            Console.WriteLine(
                "🟢 BalloonBot يعمل الآن."
            );

            Console.WriteLine(
                "📡 البوت ينتظر رسائل WOLF..."
            );

            await Task.Delay(
                Timeout.Infinite
            );
        }
    }
}
