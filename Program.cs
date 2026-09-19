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
            string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
            string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("خطأ: لم يتم العثور على الأسرار في متغيرات البيئة!");
                return;
            }

            // إنشاء الاتصال الأساسي بالسيرفر
            _client = new WolfClient();

            Console.WriteLine("جاري محاولة الاتصال بسيرفر ولف وتوليد الـ API...");

            // تسجيل الدخول المباشر الموثق في نواة المكتبة
            var loginResult = await _client.Login(email, password);

            if (loginResult)
            {
                Console.WriteLine("🎉 تم تسجيل الدخول بنجاح! البوت الآن أونلاين.");
                
                // للحفاظ على عمل البوت مفتوحاً داخل سرفر الاستضافة
                await Task.Delay(-1); 
            }
            else
            {
                Console.WriteLine("❌ فشل الاتصال بالـ API: يرجى التحقق من صحة البيانات في الـ Secrets");
            }
        }
    }
}
