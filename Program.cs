using System;
using System.Net.Http;
using System.Threading.Tasks;
using WolfLive.Api; // مكتبة ولف الرسمية

namespace BalloonBot
{
    class Program
    {
        private static IWolfClient? _client;

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

            // إعداد معالج اتصال متوافق وسليم في دوت نت 8 لتفادي خطأ القراءة فقط
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            // 1. إنشاء كائن اتصال ولف القياسي
            _client = new WolfClient();

            // 2. محاولة تسجيل الدخول والاتصال الفعلي لرفع الحساب أونلاين
            try
            {
                Console.WriteLine("📡 جاري إرسال طلب تسجيل الدخول الفعلي إلى ولف...");
                
                // استخدام دوال تسجيل الدخول الأصلية المعتمدة في سورس البوت
                bool loginResult = await _client.Login(botEmail, botPassword);

                if (!loginResult)
                {
                    Console.WriteLine("❌ فشل تسجيل الدخول إلى ولف. تأكد من صحة بيانات الحساب.");
                    return;
                }

                Console.WriteLine("✅ تم تسجيل الدخول بنجاح! جاري فتح قنوات الـ Websocket...");
                
                // الدالة المسؤولة عن رفع الحساب أونلاين داخل الغرف والتطبيق
                await _client.Connect();
                
                Console.WriteLine("🎉 إنجاز رائع! البوت متصل الآن بنجاح وأونلاين 100% داخل WOLF!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ فشل الاتصال والربط مع سيرفرات ولف: {ex.Message}");
            }

            // إبقاء الكونسول نشطاً في سيرفر جيت هاب لمنع إغلاق البوت تلقائياً
            await Task.Delay(Timeout.Infinite);
        }
    }
}
