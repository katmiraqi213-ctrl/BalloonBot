using System;
using System.Threading.Tasks;
using WolfLive.Api;

namespace BalloonBot
{
    public class Program
    {
        private static IWolfClient? _client;

        public static async Task Main(string[] args)
        {
            // 1. جلب البيانات من الـ Secrets بأمان
            string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
            string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("خطأ: لم يتم العثور على الأسرار في متغيرات البيئة!");
                return;
            }

            // 2. إنشاء عميل الاتصال القياسي للبوت
            _client = new WolfClient();

            // 3. الاستماع إلى أحداث الرسائل النصية القادمة
            _client.OnTextMessage += OnTextMessageReceived;

            Console.WriteLine("جاري محاولة الاتصال بسيرفر ولف وتوليد الـ API...");

            // 4. تسجيل الدخول باستخدام البيانات
            var loginResult = await _client.Login(email, password);

            if (loginResult)
            {
                Console.WriteLine("🎉 تم تسجيل الدخول بنجاح! البوت الآن يستمع للرسائل في الغرف.");
                
                // للحفاظ على اتصال السيرفر مفتوحاً ومستمراً
                await Task.Delay(-1); 
            }
            else
            {
                Console.WriteLine("❌ فشل الاتصال بالـ API: يرجى التحقق من صحة البيانات في الـ Secrets");
            }
        }

        // 5. استقبال ومعالجة الرسائل والرد التلقائي
        private static async Task OnTextMessageReceived(IWolfClient client, ChatMessage message)
        {
            try
            {
                // التأكد من أن الرسالة تحتوي على نص
                string body = message.Body ?? "";

                // التحقق من الكلمة المفتاحية للرد (سواء كتب المستخدم: البالون أو بوت)
                if (body.Contains("البالون") || body.Contains("بوت"))
                {
                    // الرد التلقائي على الرسالة المستلمة مباشرة
                    await client.Reply(message, "أهلاً بك! أنا بوت البالون المطور، كيف يمكنني مساعدتك اليوم؟ 🎈");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطأ أثناء معالجة الرسالة: {ex.Message}");
            }
        }
    }
}
