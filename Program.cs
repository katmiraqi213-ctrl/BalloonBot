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
            // 1. جلب بيانات الحساب بأمان من الـ Secrets المخزنة في الـ Environment
            string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
            string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("❌ خطأ: لم يتم العثور على الأسرار (Secrets) في متغيرات البيئة!");
                return;
            }

            // 2. إنشاء عميل الاتصال الأساسي بالنواة (Core)
            _client = new WolfClient();

            Console.WriteLine("🔄 جاري محاولة الاتصال بسيرفر ولف وتوليد الـ API...");

            try
            {
                // 3. تسجيل الدخول والتحقق من صحة الحساب
                var loginResult = await _client.Login(email, password);

                if (loginResult)
                {
                    Console.WriteLine("🔑 تم التحقق من الحساب بنجاح! جاري فتح الجلسة الحية (Socket)...");

                    // 4. فتح قناة الاتصال المستمر للبقاء أونلاين بشكل دائم داخل التطبيق
                    await _client.ConnectAsync(); 

                    Console.WriteLine("🎉 البوت متصل الآن بالكامل وأونلاين داخل تطبيق WOLF!");
                    
                    // الحفاظ على عمل السيرفر مفتوحاً ومستمراً بدون إغلاق
                    await Task.Delay(-1); 
                }
                else
                {
                    Console.WriteLine("❌ فشل الاتصال بالـ API: يرجى التحقق من صحة البيانات في الـ Secrets.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ حدث خطأ غير متوقع أثناء الاتصال: {ex.Message}");
            }
        }
    }
}
