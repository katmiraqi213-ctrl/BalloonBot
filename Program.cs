using System;
using System.Threading.Tasks;
using WolfLive.Api;

class Program
{
    static async Task Main(string[] args)
    {
        string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
        string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("خطأ: لم يتم العثور على الأسرار في متغيرات البيئة!");
            return;
        }

        var client = new WolfClient();

        // 1. تفعيل ميزة الاستماع للرسائل القادمة من الغرف والخاص
        client.OnMessage += OnMessageReceived;

        Console.WriteLine("جاري محاولة الاتصال بسيرفر ولف وتوليد الـ API...");

        var loginResult = await client.Login(email, password);

        if (loginResult)
        {
            Console.WriteLine("🎉 تم تسجيل الدخول بنجاح! البوت الآن يستمع للرسائل في الغرف.");
            
            // يحافظ على عمل البوت مستمراً بدون توقف داخل الاستضافة
            await Task.Delay(-1); 
        }
        else
        {
            Console.WriteLine("❌ فشل الاتصال بالـ API: يرجى التحقق من الأسرار.");
        }
    }

    // 2. الدالة المسؤولة عن قراءة الرسائل والرد عليها تلقائياً
    private static async Task OnMessageReceived(WolfClient client, IMessage message)
    {
        // للتأكد من أن الرسالة نصية وليست صورة أو إيموجي متحرك
        if (message.IsText)
        {
            // إذا كتب أي شخص في الغرفة كلمة "البالون" أو "بوت"
            if (message.Body.Contains("البالون") || message.Body.Contains("بوت"))
            {
                // يقوم البوت بالرد التلقائي داخل نفس الغرفة أو الخاص
                await client.Reply(message, "أهلاً بك! أنا بوت البالون المطور، كيف يمكنني مساعدتك اليوم؟ 🎈");
            }
        }
    }
}
