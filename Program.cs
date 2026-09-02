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
            Console.WriteLine("=================================");
            Console.WriteLine("🎈 BalloonBot DIAGNOSTIC");
            Console.WriteLine("=================================");

            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrWhiteSpace(email))
            {
                Console.WriteLine("❌ WOLF_EMAIL غير موجود.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("❌ WOLF_PASSWORD غير موجود.");
                return;
            }

            Console.WriteLine("✅ WOLF_EMAIL موجود.");
            Console.WriteLine("✅ WOLF_PASSWORD موجود.");

            try
            {
                Console.WriteLine("🔧 إنشاء WolfClient...");

                _client = new WolfClient();

                Console.WriteLine("✅ تم إنشاء WolfClient.");

                // استقبال الرسائل
                _client.Messaging.OnMessage += async (client, message) =>
                {
                    try
                    {
                        Console.WriteLine("");
                        Console.WriteLine("=================================");
                        Console.WriteLine("🔥🔥🔥 MESSAGE RECEIVED 🔥🔥🔥");
                        Console.WriteLine("=================================");

                        Console.WriteLine(
                            "📩 Content: " +
                            (message.Content ?? "(فارغ)")
                        );

                        Console.WriteLine(
                            "👤 UserId: " +
                            message.UserId
                        );

                        Console.WriteLine(
                            "👥 GroupId: " +
                            message.GroupId
                        );

                        Console.WriteLine(
                            "🆔 MessageId: " +
                            message.MessageId
                        );

                        Console.WriteLine("=================================");

                        await Task.CompletedTask;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            "❌ خطأ داخل OnMessage: " +
                            ex
                        );
                    }
                };

                Console.WriteLine("✅ تم تسجيل OnMessage.");
                Console.WriteLine("🔐 جاري تسجيل الدخول...");

                bool loginResult =
                    await _client.Login(email, password);

                Console.WriteLine(
                    "📌 نتيجة Login = " +
                    loginResult
                );

                if (!loginResult)
                {
                    Console.WriteLine(
                        "❌ فشل تسجيل الدخول."
                    );

                    return;
                }

                Console.WriteLine(
                    "✅ تم تسجيل الدخول بنجاح."
                );

                Console.WriteLine(
                    "🔌 جاري تنفيذ Connect..."
                );

                await _client.Connect();

                Console.WriteLine(
                    "✅ Connect انتهى بدون Exception."
                );

                Console.WriteLine("");
                Console.WriteLine(
                    "🟢 BalloonBot يعمل."
                );

                Console.WriteLine(
                    "📡 الآن انتظر رسالة من الروم..."
                );

                Console.WriteLine(
                    "🧪 أرسل: اختبار 123"
                );

                Console.WriteLine("");

                await Task.Delay(
                    Timeout.Infinite
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("");
                Console.WriteLine("=================================");
                Console.WriteLine("❌ EXCEPTION");
                Console.WriteLine("=================================");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("=================================");
            }
        }
    }
}
