using System;
using System.Threading.Tasks;
using WolfLive.Api;

class Program
{
    private static WolfClient? _client;

    static async Task Main(string[] args)
    {
        string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
        string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("خطأ: لم يتم العثور على الأسرار في متغيرات البيئة!");
            return;
        }

        // 1. تهيئة العميل
        _client = new WolfClient();

        // 2. ربط الـ Event الخاص باستقبال حزم البيانات (Packet) لتفادي أخطاء الأنواع غير المعروفة
        _client.OnPacket += OnPacketReceived;

        Console.WriteLine("جاري محاولة الاتصال بسيرفر ولف وتوليد الـ API...");

        var loginResult = await _client.Login(email, password);

        if (loginResult)
        {
            Console.WriteLine("🎉 تم تسجيل الدخول بنجاح! البوت الآن يستمع للرسائل والأوامر.");
            await Task.Delay(-1); 
        }
        else
        {
            Console.WriteLine("❌ فشل الاتصال بالـ API: يرجى التحقق من الأسرار.");
        }
    }

    // 3. معالجة الحزم القادمة للتعرف على الرسائل والرد عليها
    private static async Task OnPacketReceived(WolfClient client, IPacket packet)
    {
        // التحقق مما إذا كانت الحزمة القادمة عبارة عن رسالة دردشة نصية
        if (packet.Command == "message send" && packet.Payload != null)
        {
            try
            {
                // سحب نص الرسالة المكتوبة
                var body = packet.Payload["body"]?.ToString() ?? "";
                
                // إذا احتوى النص على الكلمات المفتاحية للبوت
                if (body.Contains("البالون") || body.Contains("بوت"))
                {
                    // تجهيز حزمة الرد لإرسالها لنفس الغرفة أو الخاص
                    var replyPacket = new Packet("message send");
                    replyPacket.Payload["recipient"] = packet.Payload["recipient"];
                    replyPacket.Payload["isGroup"] = packet.Payload["isGroup"];
                    replyPacket.Payload["mimeType"] = "text/plain";
                    replyPacket.Payload["body"] = "أهلاً بك! أنا بوت البالون المطور، كيف يمكنني مساعدتك اليوم؟ 🎈";

                    await client.Send(replyPacket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"خطأ أثناء معالجة الرسالة: {ex.Message}");
            }
        }
    }
}
