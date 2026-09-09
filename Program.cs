using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api; // مكتبة ولف الرسمية

namespace BalloonBot
{
    class Program
    {
        // استخدام الواجهة المباشرة IWolfClient لتمكين دوال تسجيل الدخول والاتصال
        private static IWolfClient? _client;
        private static AppCheckService? _appCheckService;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== جاري تشغيل بوت BalloonBot والاتصال الفعلي بـ WOLF ===");

            // قراءة البيانات بأمان من Secrets جيت هاب
            string botEmail = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? string.Empty;
            string botPassword = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? string.Empty;

            if (string.IsNullOrEmpty(botEmail) || string.IsNullOrEmpty(botPassword))
            {
                Console.WriteLine("خطأ حرج: لم يتم العثور على بيانات الحساب WOLF_EMAIL أو WOLF_PASSWORD في الـ Secrets!");
                return;
            }

            // 1. تشغيل خدمة جلب وتحديث التوكن التلقائي في الخلفية
            _appCheckService = new AppCheckService();
            await _appCheckService.StartAsync();

            if (!_appCheckService.IsInitialized)
            {
                Console.WriteLine("خطأ حرج: فشل البوت في توليد توكن Firebase AppCheck.");
                return;
            }

            // 2. إنشاء كائن البوت مع حقن الهيدرز الأساسية لتخطي حماية ولف عبر الـ Builder
            _client = new WolfClient(options =>
            {
                options.ExtraHeaders = new Dictionary<string, string>
                {
                    { "X-Firebase-API-Key", "AIzaSyAs8_UvS_W4Xl6fM7_XpQwYRtUv1nAmZbc" },
                    { "X-Firebase-AppCheck", _appCheckService.CurrentToken }
                };
            });

            // 3. تأمين اتصال البوت عند حدوث أي انقطاع مفاجئ بالشبكة ليقوم بتجديد التوكن
            _client.OnDisconnected += (s, e) =>
            {
                Console.WriteLine("انقطع اتصال البوت! جاري تحديث هيدرز حماية AppCheck...");
                if (_client.Options.ExtraHeaders != null)
                {
                    _client.Options.ExtraHeaders["X-Firebase-AppCheck"] = _appCheckService.CurrentToken;
                }
            };

            // 4. الدخول الفعلي والاتصال ليدخل الحساب أونلاين
            try
            {
                Console.WriteLine("جاري إرسال طلب تسجيل الدخول الفعلي للسيرفرات...");
                
                // استدعاء الدالة القياسية المعتمدة للربط والاتصال بالـ Websocket
                await _client.LoginAsync(botEmail, botPassword);
                
                Console.WriteLine("✅ تم دخول BalloonBot أونلاين بنجاح وهو الآن متصل ويتفاعل!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ فشل الاتصال والربط: {ex.Message}");
            }

            // إبقاء كونسول جيت هاب يعمل لمراقبة العمليات
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
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler);
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
