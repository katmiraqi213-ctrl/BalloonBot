using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api; // مكتبة ولف الرسمية للبوت

namespace BalloonBot
{
    class Program
    {
        private static IWolfClient _client;
        private static AppCheckService _appCheckService;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== جاري تشغيل بوت BalloonBot مع حماية AppCheck ===");

            // 1. تشغيل خدمة جلب وتحديث التوكن التلقائي في الخلفية
            _appCheckService = new AppCheckService();
            await _appCheckService.StartAsync();

            if (!_appCheckService.IsInitialized)
            {
                Console.WriteLine("خطأ حرج: فشل البوت في توليد توكن Firebase AppCheck. تأكد من الإنترنت.");
                return;
            }

            // 2. إنشاء كائن الاتصال بالمكتبة
            _client = new WolfClient();

            // 3. حقن الهيدرز الأساسية لتخطي جدار حماية تطبيق ولف
            _client.Headers["X-Firebase-API-Key"] = "AIzaSyAs8_UvS_W4Xl6fM7_XpQwYRtUv1nAmZbc";
            _client.Headers["X-Firebase-AppCheck"] = _appCheckService.CurrentToken;

            // 4. تحديث التوكن في الهيدرز بشكل مستمر في حال حدوث ديسكونكت وإعادة اتصال تلقائي
            _client.OnDisconnected += async (s, e) => 
            {
                Console.WriteLine("انقطع اتصال البوت! جاري تحديث التوكن وتجهيز الهيدرز لإعادة الاتصال التلقائي...");
                _client.Headers["X-Firebase-AppCheck"] = _appCheckService.CurrentToken;
            };

            // 5. محاولة تسجيل الدخول والاتصال بسيرفرات ولف
            try
            {
                // ضع إيميل وباسورد حساب البوت هنا
                await _client.ConnectAsync("bot_email@example.com", "bot_password"); 
                Console.WriteLine("تم تشغيل BalloonBot بنجاح وهو الآن متصل ويتخطى الحماية تلقائياً!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"فشل الاتصال بالسيرفر: {ex.Message}");
            }

            // إبقاء الكونسول مفتوحاً لمنع إغلاق البوت
            await Task.Delay(-1);
        }
    }

    // === الخدمة المسؤولة عن توليد وتحديث التوكن تلقائياً دون انقطاع ===
    public class AppCheckService
    {
        private readonly HttpClient _httpClient;
        private const string ApiKey = "AIzaSyAs8_UvS_W4Xl6fM7_XpQwYRtUv1nAmZbc";
        private const string AppId = "1:1036495349544:android:051187428f52ce8a13a7c6"; 

        public string CurrentToken { get; private set; } = string.Empty;
        public bool IsInitialized => !string.IsNullOrEmpty(CurrentToken);

        public AppCheckService()
        {
            _httpClient = new HttpClient();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            CurrentToken = await FetchAppCheckTokenAsync();

            // مؤقت لتجديد التوكن كل 55 دقيقة تلقائياً لضمان عدم فصل البوت
            _ = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMinutes(55));
                try
                {
                    while (await timer.WaitForNextTickAsync(cancellationToken))
                    {
                        var newToken = await FetchAppCheckTokenAsync();
                        if (!string.IsNullOrEmpty(newToken))
                        {
                            CurrentToken = newToken;
                            Console.WriteLine($"[{DateTime.Now}] تم تجديد توكن AppCheck تلقائياً.");
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"خطأ أثناء التحديث التلقائي للتوكن: {ex.Message}");
                }
            }, cancellationToken);
        }

        private async Task<string> FetchAppCheckTokenAsync()
        {
            try
            {
                string url = $"https://googleapis.com{AppId}:exchangeCustomToken?key={ApiKey}";
                var response = await _httpClient.PostAsJsonAsync(url, new { });
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<FirebaseResponse>();
                    return result?.Token ?? string.Empty;
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private class FirebaseResponse
        {
            [JsonPropertyName("token")]
            public string Token { get; set; } = string.Empty;
        }
    }
}
