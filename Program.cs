using System;
using System; // لاستدعاء مكتبة قراءة متغيرات البيئة
using WolfLive.Api;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. استدعاء البيانات بأمان من الـ Secrets المخزنة في النظام
        string email = Environment.GetEnvironmentVariable("WOLF_EMAIL");
        string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD");

        // تحقق سريع للتأكد من أن السيرفر استطاع قراءة الأسرار
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("خطأ: لم يتم العثور على الأسرار (Secrets) في متغيرات البيئة!");
            return;
        }

        // 2. إنشاء عميل الاتصال بـ WOLF
        var client = new WolfClient();

        Console.WriteLine("جاري محاولة الاتصال بسيرفر ولف وتوليد الـ API...");

        // 3. تسجيل الدخول التلقائي باستخدام الأسرار المستدعاة
        var loginResult = await client.LoginAsync(email, password);

        if (loginResult.IsSuccess)
        {
            Console.WriteLine("🎉 تم تسجيل الدخول بنجاح عبر الأسرار الآمنة! البوت الآن أونلاين.");
            
            // يحافظ على عمل البوت ولا يدع البرنامج يغلق
            await Task.Delay(-1); 
        }
        else
        {
            Console.WriteLine($"❌ فشل الاتصال بالـ API: {loginResult.ErrorMessage}");
        }
    }
}
