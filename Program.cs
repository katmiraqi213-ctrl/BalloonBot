using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

namespace BalloonBot
{
    public class Program
    {
        private static IWolfClient? _client;
        private static BalloonGame? _game;

        private static readonly HashSet<string> _processedMessages = new();
        private static readonly object _messageLock = new();

        public static async Task Main()
        {
            Console.WriteLine("=================================");
            Console.WriteLine("🎈 Balloon Bot starting...");
            Console.WriteLine("=================================");

            string? email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL");

            string? password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD");

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("❌ لم يتم العثور على بيانات حساب WOLF.");
                Console.WriteLine("يجب توفير:");
                Console.WriteLine("WOLF_EMAIL");
                Console.WriteLine("WOLF_PASSWORD");
                return;
            }

            Console.WriteLine("✅ بيانات الحساب موجودة.");

            _client = new WolfClient();

            // استقبال الرسائل
            _client.Messaging.OnMessage += async (client, message) =>
            {
                try
                {
                    if (message == null)
                        return;

                    string text = message.Content?.Trim() ?? "";

                    Console.WriteLine(
                        $"📩 رسالة وصلت: [{message.GroupId}] {message.UserId}: {text}");

                    if (string.IsNullOrWhiteSpace(text))
                        return;

                    lock (_messageLock)
                    {
                        if (!string.IsNullOrWhiteSpace(message.MessageId))
                        {
                            if (_processedMessages.Contains(message.MessageId))
                            {
                                Console.WriteLine(
                                    "⚠️ الرسالة مكررة وتم تجاهلها.");
                                return;
                            }

                            _processedMessages.Add(message.MessageId);
                        }
                    }

                    string groupId = message.GroupId;

                    // أوامر البالونات
                    if (text.StartsWith(
                        "!بالونات",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(
                            $"🎈 أمر بالونات: {text}");

                        await HandleCommand(
                            client,
                            message,
                            text);

                        return;
                    }

                    // الأرقام أثناء اللعب
                    if (int.TryParse(text, out int number))
                    {
                        Console.WriteLine(
                            $"🔢 رقم مستلم: {number}");

                        if (_game == null)
                        {
                            Console.WriteLine(
                                "ℹ️ لا توجد لعبة حاليًا.");
                            return;
                        }

                        if (!_game.Started)
                        {
                            Console.WriteLine(
                                "ℹ️ اللعبة لم تبدأ بعد.");
                            return;
                        }

                        if (_game.GroupId != groupId)
                        {
                            Console.WriteLine(
                                "ℹ️ الرسالة من روم مختلف.");
                            return;
                        }

                        await HandleNumber(
                            client,
                            message,
                            number);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"❌ Message error: {ex}");
                }
            };

            try
            {
                Console.WriteLine("🔐 تسجيل الدخول...");

                await _client.Login(
                    email,
                    password);

                Console.WriteLine(
                    "✅ تم تسجيل الدخول بنجاح.");

                Console.WriteLine(
                    "🔌 الاتصال بـ WOLF...");

                await _client.Connect();

                Console.WriteLine(
                    "🟢 البوت يعمل الآن وينتظر الرسائل...");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "❌ فشل تشغيل البوت:");
                Console.WriteLine(ex);
            }

            await Task.Delay(Timeout.Infinite);
        }

        private static async Task HandleCommand(
            IWolfClient client,
            Message message,
            string
