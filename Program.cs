using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api; // مكتبة ولف المعتمدة للمشروع

namespace BalloonBot
{
    class Program
    {
        private static WolfClient? _client;
        private static AppCheckService? _appCheckService;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 🚀 تشغيل بوت BalloonBot والاتصال الفعلي بـ WOLF ===");

            // قراءة الإيميل والباسورد من متغيرات بيئة جيت هاب (Secrets) لضمان الأمان
            string botEmail = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? string.Empty;
            string botPassword = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? string.Empty;

            if (string.IsNullOrEmpty(botEmail) || string.IsNullOrEmpty(botPassword))
            {
                Console.WriteLine("❌ خطأ حرج: لم يتم العثور على بيانات الحساب WOLF_EMAIL أو WOLF_PASSWORD في الـ Secrets!");
                return;
            }

            // 1. تشغيل خدمة جلب وتحديث التوكن التلقائي في الخلفية لحماية AppCheck
            _appCheckService = new AppCheckService();
            await _appCheckService.StartAsync();

            if (!_appCheckService.IsInitialized)
            {
                Console.WriteLine("❌ خطأ حرج: فشل البوت في توليد توكن Firebase AppCheck. تحقق من الاتصال.");
                return;
            }

            // 2. إنشاء كائن اتصال ولف القياسي
            _client = new WolfClient();

            // 3. حقن الهيدرز والتوكن لتخطي الحماية أثناء مصافحة الـ Websocket
            if (_client.Connection?.Options != null)
            {
                _client.Connection.Options.ExtraHeaders = new Dictionary<string, string>
                {
                    { "X-Firebase-API-Key", "AIzaSyAs8_UvS_W4Xl6fM7_XpQwYRtUv1nAmZbc" },
                    { "X-Firebase-AppCheck", _appCheckService.CurrentToken }
                };

                // إعادة حقن التوكن تلقائياً عند حدوث ديسكونكت أو محاولة اتصال جديدة
                _client.OnDisconnected += (s, e) =>
                {
                    if (_client.Connection.Options.ExtraHeaders != null)
                    {
                        _client.Connection.Options.ExtraHeaders["X-Firebase-AppCheck"] = _appCheckService.CurrentToken;
                    }
                };
            }

            // 4. محاولة تسجيل الدخول والاتصال الفعلي ليدخل الحساب أونلاين
            try
            {
                Console.WriteLine("📡 جاري إرسال طلب تسجيل الدخول إلى سيرفرات ولف...");
                
                // استخدام الدوال الحقيقية والمطابقة للمكتبة لتشغيل الاتصال
                bool loginResult = await _client.Login(botEmail, botPassword);

                if (!loginResult)
                {
                    Console.WriteLine("❌ فشل تسجيل الدخول إلى ولف (تأكد من صحة الحساب).");
                    return;
                }

                Console.WriteLine("✅ تم تسجيل الدخول بنجاح. جاري فتح اتصال الـ Websocket...");
                
                // الدالة المسؤولة عن رفع الحساب أونلاين في الغرف
                await _client.Connect();
                
                Console.WriteLine("🎉 البوت الآن أونلاين بنجاح ومتصل بـ WOLF!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ فشل الاتصال: {ex.Message}");
            }

            // إبقاء الكونسول نشطاً في سيرفر جيت هاب لمنع إغلاق البوت تلقائياً
            await Task.Delay(Timeout.Infinite);
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
