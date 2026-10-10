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
                Console.WriteLine("ERROR: no WOLF_TOKEN and no WOLF_EMAIL/WOLF_PASSWORD found.");
                return;
            }

            if (hasToken)
            {
                try
                {
                    Console.WriteLine("Trying token login...");
                    _client = new WolfClient(token: token);
                    Console.WriteLine("Client created with token.");
                    await Task.Delay(-1);
