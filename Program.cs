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
            // 1. جلب بيانات الحساب بأمان من الـ Secrets لمنع قراءتها على جيت هاب
            string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
            string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                Console.WriteLine("❌ خطأ: لم يتم العثور على الأسرار (Secrets) في متغيرات البيئة!");
                return;
            }

            // 2. إنشاء عميل الاتصال الفعلي بـ WOLF
            _client = new WolfClient();

            // 3. ربط حدث استقبال الرسائل النصية للرد التلقائي
            _client.OnTextMessage += OnTextMessageReceived;

            Console.WriteLine("🔄 جاري محاولة الاتصال بسيرفر ولف وتوليد الـ API...");

            try
            {
                // 4. تسجيل الدخول والتحقق من الحساب
                var loginResult = await _client.Login(email, password);

                if (loginResult)
                {
                    Console.WriteLine("🔑 تم التحقق من الحساب بنجاح! جاري فتح الجلسة الحية (Socket)...");

                    // 5. [التعديل الأهم] إجبار البوت على الاتصال بالبوابة والبقاء أونلاين في الغرف
                    await _client.ConnectAsync(); 

                    Console.WriteLine("🎉 البوت متصل الآن بالكامل وأونلاين داخل تطبيق WOLF!");
                    
                    // الحفاظ على تشغيل السيرفر مفتوحاً ومستمراً بدون إغلاق
                    await Task.Delay(-1); 
                }
                else
                {
                    Console.WriteLine("❌ فشل الاتصال بالـ API: يرجى التحقق من صحة الإيميل أو الباسورد في الـ Secrets.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ حدث خطأ غير متوقع أثناء الاتصال: {ex.Message}");
            }
        }

        // 6. دالة معالجة الرسائل والرد التلقائي في الغرف والخاص
        private static async Task OnTextMessageReceived(WolfClient client, Message message)
        {
            try
            {
                string body = message.Body ?? "";

                // إذا كتب أي مستخدم في الغرفة الكلمات المفتاحية
                if (body.Contains("البالون") || body.Contains("بوت"))
                {
                    // الرد التلقائي السريع
                    await client.Reply(message, "أهلاً بك! أنا بوت البالون المطور، كيف يمكنني مساعدتك اليوم؟ 🎈");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ خطأ أثناء معالجة الرسالة: {ex.Message}");
            }
        }
    }
}
