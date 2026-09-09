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
        private static WolfClient? _client;
        private static AppCheckService? _appCheckService;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== جاري تشغيل بوت BalloonBot مع حماية AppCheck التلقائية ===");

            // قراءة الإيميل والباسورد من متغيرات البيئة الأمنيّة في جيت هاب
            string botEmail = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? string.Empty;
            string botPassword = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? string.Empty;

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

            // 2. إنشاء كائن اتصال ولف القياسي
            _client = new WolfClient();

            // 3. محاولة تشغيل البوت والاتصال الفعلي بالسيرفرات
            try
            {
                // دالة الاتصال الرسمية لبدء تشغيل العميل والاستماع في إصدار المكتبة 1.2.3
                await _client.StartAsync();
                Console.WriteLine("تم اتصال BalloonBot بنجاح وهو الآن يعمل في الخلفية!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"فشل الاتصال بالسيرفر: {ex.Message}");
            }

            // إبقاء الكونسول مفتوحاً في سيرفر جيت هاب لمنع إغلاق البوت
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
            // إعداد HttpClient مع تخطي التحقق من الشهادات بشكل صحيح ومتوافق مع دوت نت 8
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _httpClient = new HttpClient(handler);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            CurrentToken = await FetchAppCheckTokenAsync();

            // مؤقت لتجديد التوكن تلقائياً كل 55 دقيقة لضمان استمرار الاتصال
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
                            Console.WriteLine($"[{DateTime.Now}] تم تجديد توكن AppCheck تلقائياً بنجاح.");
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
                
                // طلب فارغ لمحاكاة تطبيق ولف الرسمي
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
