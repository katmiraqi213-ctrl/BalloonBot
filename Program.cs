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
                    await _client.Connect();
                    Console.WriteLine("Connect finished. Check WOLF to see if the bot is online.");
                    await Task.Delay(-1);
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Token login failed: " + ex.Message);
                }
            }

            if (hasLogin)
            {
                try
                {
                    Console.WriteLine("Trying email/password login...");
                    _client = new WolfClient();
                    bool ok = await _client.Login(email, password);

                    if (ok)
                    {
                        Console.WriteLine("Logged in. Bot is online.");
                        await Task.Delay(-1);
                    }
                    else
                    {
                        Console.WriteLine("Login failed: check email and password.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unexpected error: " + ex.Message);
                }
            }
        }
    }
}
