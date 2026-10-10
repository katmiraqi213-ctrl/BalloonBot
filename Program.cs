using System;
using System.Threading.Tasks;
using WolfLive.Api;

namespace BalloonBot
{
    public class Program
    {
        private static WolfClient? _client;

        public static async Task Main(string[] args)
        {
            string token = Environment.GetEnvironmentVariable("WOLF_TOKEN") ?? "";
            string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
            string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            bool hasToken = !string.IsNullOrWhiteSpace(token);
            bool hasLogin = !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);

            if (!hasToken && !hasLogin)
            {
                Console.WriteLine("❌ لا يوجد WOLF_TOKEN ولا WOLF_EMAIL/WOLF_PASSWORD في متغيرات البيئة.");
                return;
            }

            // 1) المحاولة الأولى: الدخول بالتوكن
            if (hasToken)
            {
                try
                {
                    Console.WriteLine("🔄 محاولة الدخول بالتوكن...");
                    _client = new WolfClient(token: token);
