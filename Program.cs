using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api; 
using WolfLive.Api.Commands; // تأكد من وجود مكتبة الأوامر إذا كنت تستخدمها

namespace BalloonBot
{
    class Program
    {
        private static IWolfClient? _client;
        private static AppCheckService? _appCheckService;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== جاري تشغيل بوت BalloonBot والاتصال الفعلي بـ WOLF ===");

            // قراءة الإيميل والباسورد من متغيرات بيئة جيت هاب
            string botEmail = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? string.Empty;
            string botPassword = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? string.Empty;

            if (string.IsNullOrEmpty(botEmail) || string.IsNullOrEmpty(botPassword))
            {
                Console.WriteLine("خطأ حرج: لم يتم العثور على بيانات الحساب WOLF_EMAIL أو WOLF_PASSWORD في الـ Secrets!");
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

            // 2. إعداد الحماية وتخطي شهادات الحقل للشبكة
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator = (message, cert, chain, errors) => true;

            // 3. بناء اتصال العميل مع حقن الـ ExtraHeaders المناسبة لحماية ولف
            // قمنا بالاعتماد على الـ Builder القياسي للمكتبة 1.2.3 لتفعيل التشغيل الحقيقي
            var clientConfiguration = new WolfClient()
                .WithSetOptions(options =>
                {
                    options.ExtraHeaders = new Dictionary<string, string>
                    {
                        { "X-Firebase-API-Key", "AIzaSyAs8_UvS_W4Xl6fM7_XpQwYRtUv1nAmZbc" },
                        { "X-Firebase-AppCheck", _appCheckService.CurrentToken }
                    };
                });

            _client = clientConfiguration;

            // 4. محاولة تسجيل الدخول وربط الحساب ليدخل أونلاين
            try
            {
                Console.WriteLine("جاري إرسال طلب تسجيل الدخول إلى سيرفرات ولف...");
                
                // الدالة الرسمية للمكتبة لتسجيل دخول الحساب الفعلي بالتوكن والهيدرز
                await _client.LoginAsync(botEmail, botPassword);
                
                Console.WriteLine("✅ تم دخول BalloonBot أونلاين بنجاح وهو متصل الآن بولف!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ فشل تسجيل الدخول إلى ولف: {ex.Message}");
            }

            // إبقاء الكونسول نشطاً في جيت هاب لمنع الإغلاق المفاجئ
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
