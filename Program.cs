using System;
using System.Threading.Tasks;
using WolfLive.Api; // مكتبة ولف الرسمية

namespace BalloonBot
{
    class Program
    {
        private static WolfClient? _client;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 🚀 بدء تشغيل اتصال بوت ولف الصافي والآمن ===");

            // قراءة الإيميل والباسورد بأمان من متغيرات بيئة جيت هاب (Secrets)
            string botEmail = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? string.Empty;
            string botPassword = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? string.Empty;

            if (string.IsNullOrEmpty(botEmail) || string.IsNullOrEmpty(botPassword))
            {
                Console.WriteLine("❌ خطأ حرج: لم يتم العثور على WOLF_EMAIL أو WOLF_PASSWORD في الـ Secrets!");
                return;
            }

            // 1. إنشاء كائن اتصال ولف القياسي جداً المتوافق مع إصدار المكتبة 1.2.3
            _client = new WolfClient();

            // 2. ربط معالج الأحداث الصحيح (يأخذ مَعاملاً واحداً فقط لتفادي خطأ CS1593)
            _client.OnConnected += (client) =>
            {
                Console.WriteLine("🎉 إنجاز رائع! البوت متصل الآن بنجاح وأونلاين 100% داخل WOLF!");
            };

            // 3. محاولة تسجيل الدخول والاتصال الفعلي لرفع الحساب أونلاين
            try
            {
                Console.WriteLine("📡 جاري إرسال طلب تسجيل الدخول الفعلي إلى ولف...");
                
                // استخدام دالة الدخول القياسية والاتصال المتوافقة مع كود لعبة مزاج الأصلي
                bool loginResult = await _client.Login(botEmail, botPassword);

                if (!loginResult)
                {
                    Console.WriteLine("❌ فشل تسجيل الدخول إلى ولف (تأكد من صحة الحساب).");
                    return;
                }

                Console.WriteLine("✅ تم تسجيل الدخول بنجاح! جاري فتح اتصال الـ Websocket لرفع الحساب...");
                await _client.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ فشل الاتصال والربط مع سيرفرات ولف: {ex.Message}");
            }

            // إبقاء الكونسول نشطاً في سيرفر جيت هاب لمنع إغلاق البوت تلقائياً
            await Task.Delay(-1);
        }
    }
}
