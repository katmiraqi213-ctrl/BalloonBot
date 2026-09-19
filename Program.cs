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
            // 1. استدعاء التوكنات الآمنة من الـ Secrets لمنع الحظر
            string token = Environment.GetEnvironmentVariable("WOLF_TOKEN") ?? "";
            string appCheck = Environment.GetEnvironmentVariable("APP_CHECK_TOKEN") ?? "";

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(appCheck))
            {
                Console.WriteLine("❌ خطأ: لم يتم العثور على التوكنات (Secrets) في متغيرات البيئة!");
                return;
            }

            // 2. إعداد خيارات الاتصال وتمرير ترويسة الحماية لتخطي جدار ولف
            var options = new WolfClientOptions { Device = DeviceType.Android };
            _client = new WolfClient(options);
            _client.AddHeader("X-AppCheck-Token", appCheck);

            // 3. ربط حدث استقبال الرسائل للرد التلقائي داخل الغرف والخاص
            _client.OnTextMessage += OnTextMessageReceived;

            Console.WriteLine("🔄 جاري محاولة تخطي جدار الحماية والاتصال المباشر بالسيرفر...");

            try
            {
                // 4. تسجيل الدخول الفوري والمباشر عبر التوكن المستخرج
                var loginResult = await _client.Login(token);

                if (loginResult)
                {
                    Console.WriteLine("🎉 البوت متصل الآن بالكامل وأونلاين داخل تطبيق WOLF ومستعد لاستقبال الأوامر!");
                    
                    // الحفاظ على تشغيل السيرفر مستمراً في الخلفية بدون إغلاق
                    await Task.Delay(-1); 
                }
                else
                {
                    Console.WriteLine("❌ فشل الاتصال: يرجى التحقق من صحة التوكنات المرفوعة.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ حدث خطأ غير متوقع أثناء الاتصال: {ex.Message}");
            }
        }

        // 5. دالة الاستماع والقراءة والرد التلقائي في الغرف
        private static async Task OnTextMessageReceived(WolfClient client, Message message)
        {
            try
            {
                string body = message.Body ?? "";

                // إذا كتب أي شخص في الغرفة كلمة "البالون" أو "بوت"
                if (body.Contains("البالون") || body.Contains("بوت"))
                {
                    // الرد التلقائي المباشر في الغرفة
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
