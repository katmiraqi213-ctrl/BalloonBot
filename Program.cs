using System;
using System.Threading.Tasks;
using WolfLive.Api;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. استدعاء البيانات بأمان من الـ Secrets مع إضافة تفادي الـ Null بقيمة افتراضية فارغة
        string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
        string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

        // تحقق سريع للتأكد من أن السيرفر استطاع قراءة الأسرار من النظام
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("خطأ: لم يتم العثور على الأسرار (Secrets) في متغيرات البيئة!");
            return;
        }

        // 2. إنشاء عميل الاتصال بـ WOLF
        var client = new WolfClient();

        Console.WriteLine("جاري محاولة الاتصال بسيرفر ولف وتوليد الـ API...");

        // 3. تعديل اسم الدالة إلى الاسم الصحيح والموجود داخل مكتبة WolfLive.Api
        var loginResult = await client.Login(email, password);

        if (loginResult)
        {
            Console.WriteLine("🎉 تم تسجيل الدخول بنجاح عبر الأسرار الآمنة! البوت الآن أونلاين.");
            
            // يحافظ على عمل البوت مفتوحاً داخل سرفر الاستضافة
            await Task.Delay(-1); 
        }
        else
        {
            Console.WriteLine("❌ فشل الاتصال بالـ API: يرجى التحقق من صحة الإيميل أو الباسورد في الـ Secrets");
        }
    }
}
