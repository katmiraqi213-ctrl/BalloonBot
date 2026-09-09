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

            // 2. ربط معالج الأحداث لطباعة نجاح الدخول الفعلي فور حدوثه
            _client.OnConnected += (sender, e) =>
            {
                Console.WriteLine("🎉 إنجاز رائع! البوت متصل الآن بنجاح وأونلاين 100% داخل WOLF!");
            };

            // 3. محاولة تسجيل الدخول والاتصال الفعلي لرفع الحساب أونلاين
            try
            {
                Console.WriteLine("📡 جاري إرسال طلب تسجيل الدخول الفعلي إلى ولف...");
                
                // استخدام دالة الدخول والربط الحقيقية المباشرة للمكتبة 1.2.3 
                await _client.LoginAsync(botEmail, botPassword);
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
