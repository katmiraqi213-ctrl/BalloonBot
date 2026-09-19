using System;
using System.Threading.Tasks;
using WolfLive.Api;

namespace BalloonBot
{
    public class Program
    {
        private static WolfClient? _client;

        public static async Task Main(string[] args)
        {
            // 1. جلب البيانات القياسية الآمنة من الـ Secrets المخزنة في جيت هاب
            string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
            string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("❌ خطأ: لم يتم العثور على الأسرار (Secrets) في متغيرات البيئة!");
                return;
            }

            // 2. إنشاء عميل الاتصال القياسي المتوافق 100% مع بنيّة مكتبتك الحالية
            _client = new WolfClient();

            Console.WriteLine("🔄 جاري محاولة الاتصال بالسيرفر وتوليد الـ API المباشر...");

            try
            {
                // 3. استدعاء الدالة القياسية الوحيدة المدعومة بالمكتبة لتسجيل الدخول الصريح
                var loginResult = await _client.Login(email, password);

                if (loginResult)
                {
                    Console.WriteLine("🎉 إنجاز عظيم! تم توليد الـ API والبوت متصل الآن بالكامل وأونلاين داخل WOLF!");
                    
                    // الحفاظ على تشغيل السيرفر مستمراً في الخلفية بدون إغلاق البرنامج
                    await Task.Delay(-1);
                }
                else
                {
                    Console.WriteLine("❌ فشل الاتصال: يرجى التحقق من صحة الإيميل أو الباسورد في الـ Secrets.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ حدث خطأ غير متوقع أثناء الاتصال: {ex.Message}");
            }
        }
    }
}
