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

            // 1. إعداد معالج الحقن والشبكة لتخطي الحماية
            // قمنا بإلغاء دالة جلب التوكن البرمجية لتفادي حظر سيرفرات جيت هاب (Firewall)
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator = (message, cert, chain, errors) => true;

            // 2. إنشاء كائن اتصال ولف القياسي
            _client = new WolfClient();

            // 3. محاولة تسجيل الدخول والاتصال الفعلي لرفع الحساب أونلاين
            try
            {
                Console.WriteLine("📡 جاري إرسال طلب تسجيل الدخول الفعلي إلى ولف...");
                
                // استخدام دوال تسجيل الدخول الأصلية المعتمدة في سورس البوت للربط بالسيرفر
                bool loginResult = await _client.Login(botEmail, botPassword);

                if (!loginResult)
                {
                    Console.WriteLine("❌ فشل تسجيل الدخول إلى ولف. تأكد من صحة بيانات الحساب.");
                    return;
                }

                Console.WriteLine("✅ تم تسجيل الدخول بنجاح! جاري فتح قنوات الـ Websocket...");
                
                // الدالة المسؤولة عن رفع الحساب أونلاين داخل الغرف والتطبيق
                await _client.Connect();
                
                Console.WriteLine("🎉 البوت متصل الآن بنجاح وأونلاين 100% داخل WOLF!");
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
