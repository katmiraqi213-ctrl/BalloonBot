using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;

namespace BalloonBot
{
    class Program
    {
        private static IWolfClient _client;
        private static AppCheckService _appCheckService;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== جاري تشغيل بوت BalloonBot مع حماية AppCheck ===");

            // قراءة الإيميل والباسورد من متغيرات البيئة (GitHub Secrets) لضمان الأمان
            string botEmail = Environment.GetEnvironmentVariable("WOLF_EMAIL");
            string botPassword = Environment.GetEnvironmentVariable("WOLF_PASSWORD");

            if (string.IsNullOrEmpty(botEmail) || string.IsNullOrEmpty(botPassword))
            {
                Console.WriteLine("خطأ حرج: لم يتم العثور على بيانات الحساب WOLF_EMAIL أو WOLF_PASSWORD في متغيرات البيئة!");
                return;
            }

            // 1. تشغيل خدمة جلب وتحديث التوكن التلقائي
            _appCheckService = new AppCheckService();
            await _appCheckService.StartAsync();

            if (!_appCheckService.IsInitialized)
            {
                Console.WriteLine("خطأ حرج: فشل البوت في توليد توكن Firebase AppCheck.");
                return;
            }

            // 2. إنشاء كائن الاتصال بالمكتبة
            _client = new WolfClient();

            // 3. حقن الهيدرز الأساسية لتخطي جدار الحماية
            _client.Headers["X-Firebase-API-Key"] = "AIzaSyAs8_UvS_W4Xl6fM7_XpQwYRtUv1nAmZbc";
            _client.Headers["X-Firebase-AppCheck"] = _appCheckService.CurrentToken;

            // 4. تحديث التوكن في الهيدرز عند إعادة الاتصال التلقائي
            _client.OnDisconnected += async (s, e) => 
            {
                Console.WriteLine("انقطع اتصال البوت! جاري تحديث التوكن وتجهيز الهيدرز...");
                _client.Headers["X-Firebase-AppCheck"] = _appCheckService.CurrentToken;
            };

            // 5. محاولة تسجيل الدخول
            try
            {
                await _client.ConnectAsync(botEmail, botPassword); 
                Console.WriteLine("تم اتصال BalloonBot بنجاح وهو الآن يتخطى الحماية تلقائياً!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"فشل الاتصال بالسيرفر: {ex.Message}");
            }

            await Task.Delay(-1);
        }
    }

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
