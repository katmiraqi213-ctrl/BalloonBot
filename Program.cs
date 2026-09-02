using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

public class Program
{
    private static IWolfClient? _client;
    private static BalloonGame? _game;

    private static readonly HashSet<string> _processedMessages = new();
    private static readonly object _messageLock = new();

    public static async Task Main(string[] args)
    {
        string email = Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";
        string password = Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("❌ WOLF_EMAIL أو WOLF_PASSWORD غير موجود.");
            return;
        }

        Console.WriteLine("🎈 تشغيل BalloonBot...");
        Console.WriteLine("🔐 محاولة تسجيل الدخول...");

        _client = new WolfClient();

        // =====================================================
        // اختبار الاتصال
        // =====================================================

        _client.OnConnected += (_) =>
        {
            Console.WriteLine("🟢 CONNECTED - تم الاتصال بـ Wolf.");
            Console.WriteLine("📡 البوت ينتظر الرسائل...");
        };

        // =====================================================
        // استقبال رسائل WOLF
        // =====================================================

        _client.Messaging.OnMessage += async (client, message) =>
        {
            try
            {
                Console.WriteLine("🔥🔥🔥 MESSAGE RECEIVED 🔥🔥🔥");

                string text = message.Content?.Trim() ?? "";

                Console.WriteLine($"📩 Content: {text}");
                Console.WriteLine($"👤 UserId: {message.UserId}");
                Console.WriteLine($"👥 GroupId: {message.GroupId}");
                Console.WriteLine($"🆔 MessageId: {message.MessageId}");

                if (!string.IsNullOrWhiteSpace(message.MessageId))
                {
                    lock (_messageLock)
                    {
                        if (!_processedMessages.Add(message.MessageId))
                            return;

                        if (_processedMessages.Count > 5000)
                            _processedMessages.Clear();
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                    return;

                // =================================================
                // اختبار الأمر
                // =================================================

                if (text.Equals("!بالونات", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("!بالونات مساعدة", StringComparison.OrdinalIgnoreCase))
                {
                    await client.Reply(
                        message,
                        "🎈🔥 BalloonBot يعمل بنجاح! 🔥🎈\n\n" +
                        "تم استلام رسالتك بنجاح ✅\n\n" +
                        "اكتب:\n" +
                        "!بالونات جديد"
                    );

                    return;
                }

                // =================================================
                // الأرقام أثناء اللعبة
                // =================================================

                if (int.TryParse(text, out int number))
                {
                    if (_game != null && _game.Started)
                    {
                        await HandleNumber(client, message, number);
                    }

                    return;
                }

                // =================================================
                // أوامر البوت
                // =================================================

                if (!text.StartsWith(
                        "!بالونات",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string command =
                    text.Length > 8
                        ? text.Substring(8).Trim()
                        : "";

                await HandleCommand(
                    client,
                    message,
                    command
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ MESSAGE ERROR:");
                Console.WriteLine(ex.ToString());
            }
        };

        // =====================================================
        // تسجيل الدخول
        // =====================================================

        bool login = await _client.Login(
            email,
            password
        );

        Console.WriteLine(
            $"🔐 LOGIN RESULT: {login}"
        );

        if (!login)
        {
            Console.WriteLine("❌ فشل تسجيل الدخول إلى Wolf.");
            return;
        }

        Console.WriteLine("✅ تم تسجيل الدخول إلى Wolf.");

        // =====================================================
        // الاتصال
        // =====================================================

        await _client.Connect();

        Console.WriteLine(
            "📡 تم تشغيل الاتصال وانتظار أوامر WOLF..."
        );

        // =====================================================
        // إبقاء البوت شغال
        // =====================================================

        await Task.Delay(
            Timeout.Infinite
        );
    }

    // =========================================================
    // الأوامر
    // =========================================================

    private static async Task HandleCommand(
        IWolfClient client,
        Message message,
        string command)
    {
        string[] parts =
            command.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries
            );

        string action =
            parts.Length > 0
                ? parts[0].Trim().ToLowerInvariant()
                : "";

        switch (action)
        {
            case "":
            case "مساعدة":
            case "help":
                await SendHelp(client, message);
                break;

            case "جديد":
                await NewGame(client, message);
                break;

            case "انضم":
            case "انضمام":
                await JoinGame(client, message);
                break;

            case "لاعبين":
                await ShowPlayers(client, message);
                break;

            case "بدء":
                await StartGame(client, message);
                break;

            case "انهاء":
            case "إنهاء":
                await EndGame(client, message);
                break;

            default:
                await client.Reply(
                    message,
                    "❌ أمر غير معروف.\n\n" +
                    "اكتب:\n" +
                    "!بالونات مساعدة"
                );
                break;
        }
    }

    private static async Task SendHelp(
        IWolfClient client,
        Message message)
    {
        string help =
            "🎈🔥 لعبة البالونات 🔥🎈\n\n" +
            "🎮 الأوامر:\n\n" +
            "🎈 إنشاء لعبة:\n" +
            "!بالونات جديد\n\n" +
            "👤 الانضمام:\n" +
            "!بالونات انضم\n\n" +
            "👥 عرض اللاعبين:\n" +
            "!بالونات لاعبين\n\n" +
            "▶️ بدء اللعبة:\n" +
            "!بالونات بدء\n\n" +
            "🛑 إنهاء اللعبة:\n" +
            "!بالونات انهاء\n\n" +
            "🎯 طريقة اللعب:\n" +
            "كل لاعب يبدأ بـ 7 🎈 بالونات.\n\n" +
            "عندما يأتي دورك، تختار رقم اللاعب الذي تريد مهاجمته.\n\n" +
            "ثم تختار رقم البالون.\n\n" +
            "🏆 آخر لاعب يبقى في اللعبة هو الفائز.";

        await client.Reply(
            message,
            help
        );
    }

    private static async Task NewGame(
        IWolfClient client,
        Message message)
    {
        if (_game != null)
        {
            await client.Reply(
                message,
                "⚠️ توجد لعبة بالونات حالياً.\n\n" +
                "استخدم:\n" +
                "!بالونات انهاء"
            );

            return;
        }

        _game = new BalloonGame
        {
            GroupId = message.GroupId ?? ""
        };

        await client.Reply(
            message,
            "🎈🔥 تم إنشاء لعبة البالونات! 🔥🎈\n\n" +
            "كل لاعب يبدأ بـ 7 🎈 بالونات.\n\n" +
            "للانضمام اكتب:\n" +
            "!بالونات انضم\n\n" +
            "بعد اكتمال اللاعبين اكتب:\n" +
            "!بالونات بدء"
        );
    }

    private static async Task JoinGame(
        IWolfClient client,
        Message message)
    {
        if (_game == null)
        {
            await client.Reply(
                message,
                "❌ لا توجد لعبة حالياً.\n\n" +
                "اكتب:\n" +
                "!بالونات جديد"
            );

            return;
        }

        if (_game.Started)
        {
            await client.Reply(
                message,
                "❌ اللعبة بدأت بالفعل، لا يمكن الانضمام الآن."
            );

            return;
        }

        string userId = message.UserId;

        if (_game.Players.Any(
                x => x.UserId == userId))
        {
            await client.Reply(
                message,
                "⚠️ أنت منضم للعبة بالفعل."
            );

            return;
        }

        string nickname =
            await GetNickname(
                client,
                userId
            );

        BalloonPlayer player =
            new BalloonPlayer
            {
                UserId = userId,
                Name = nickname,
                Balloons = 7,
                Alive = true
            };

        _game.Players.Add(player);

        await client.Reply(
            message,
            $"🎈 تم انضمامك إلى اللعبة!\n\n" +
            $"👤 اللاعب: {nickname}\n" +
            $"🎈 البالونات: 7\n\n" +
            $"👥 عدد اللاعبين: {_game.Players.Count}\n\n" +
            $"!بالونات لاعبين"
        );
    }

    private static async Task ShowPlayers(
        IWolfClient client,
        Message message)
    {
        if (_game == null)
        {
            await client.Reply(
                message,
                "❌ لا توجد لعبة حالياً."
            );

            return;
        }

        string result =
            "🎈👥 لاعبو لعبة البالونات 👥🎈\n\n";

        int index = 1;

        foreach (BalloonPlayer player in _game.Players)
        {
            string status =
                player.Alive
                    ? $"{player.Balloons} 🎈"
                    : "💀 خرج من اللعبة";

            result +=
                $"{GetNumberEmoji(index)} " +
                $"{player.Name} — {status}\n";

            index++;
        }

        await client.Reply(
            message,
            result
        );
    }

    private static async Task StartGame(
        IWolfClient client,
        Message message)
    {
        if (_game == null)
        {
            await client.Reply(
                message,
                "❌ لا توجد لعبة.\n\n" +
                "!بالونات جديد"
            );

            return;
        }

        if (_game.Started)
        {
            await client.Reply(
                message,
                "⚠️ اللعبة بدأت بالفعل."
            );

            return;
        }

        if (_game.Players.Count < 2)
        {
            await client.Reply(
                message,
                "❌ يجب أن يكون هناك لاعبان على الأقل."
            );

            return;
        }

        foreach (BalloonPlayer player in _game.Players)
        {
            player.Alive = true;
            player.Balloons = 7;
        }

        _game.Started = true;

        _game.TurnOrder =
            _game.Players
                .OrderBy(
                    _ => Random.Shared.Next()
                )
                .Select(
                    x => x.UserId
                )
                .ToList();

        _game.CurrentTurnIndex = 0;
        _game.WaitingForOpponent = true;
        _game.SelectedOpponentId = null;

        BalloonPlayer? current =
            _game.CurrentPlayer;

        if (current == null)
            return;

        await client.Reply(
            message,
            "🎈🔥 بدأت لعبة البالونات! 🔥🎈\n\n" +
            BuildPlayersBoard() +
            "\n\n" +
            $"🎯 الدور الآن على: {current.Name}\n\n" +
            "👊 اختر رقم اللاعب الذي تريد مهاجمته."
        );
    }

    private static async Task HandleNumber(
        IWolfClient client,
        Message message,
        int number)
    {
        if (_game == null ||
            !_game.Started)
            return;

        BalloonPlayer? current =
            _game.CurrentPlayer;

        if (current == null)
            return;

        if (message.UserId != current.UserId)
            return;

        if (_game.WaitingForOpponent)
        {
            await ChooseOpponent(
                client,
                message,
                number
            );

            return;
        }

        if (_game.SelectedOpponentId != null)
        {
            await ChooseBalloon(
                client,
                message,
                number
            );
        }
    }

    private static async Task ChooseOpponent(
        IWolfClient client,
        Message message,
        int number)
    {
        if (_game == null)
            return;

        if (number < 1 ||
            number > _game.Players.Count)
        {
            await client.Reply(
                message,
                "❌ رقم اللاعب غير صحيح."
            );

            return;
        }

        BalloonPlayer selected =
            _game.Players[number - 1];

        BalloonPlayer current =
            _game.CurrentPlayer!;

        if (selected.UserId == current.UserId)
        {
            await client.Reply(
                message,
                "❌ لا يمكنك اختيار نفسك."
            );

            return;
        }

        if (!selected.Alive)
        {
            await client.Reply(
                message,
                "❌ هذا اللاعب خرج من اللعبة."
            );

            return;
        }

        _game.SelectedOpponentId =
            selected.UserId;

        _game.WaitingForOpponent = false;

        await client.Reply(
            message,
            $"🎯 اخترت اللاعب: {selected.Name}\n\n" +
            $"🎈 لديه {selected.Balloons} بالونات.\n\n" +
            "اختر رقم البالون:\n\n" +
            BuildBalloonNumbers(
                selected.Balloons
            )
        );
    }

    private static async Task ChooseBalloon(
        IWolfClient client,
        Message message,
        int number)
    {
        if (_game == null)
            return;

        BalloonPlayer current =
            _game.CurrentPlayer!;

        BalloonPlayer? opponent =
            _game.Players.FirstOrDefault(
                x => x.UserId ==
                     _game.SelectedOpponentId
            );

        if (opponent == null ||
            !opponent.Alive)
        {
            _game.WaitingForOpponent = true;
            _game.SelectedOpponentId = null;

            await client.Reply(
                message,
                "❌ اللاعب لم يعد متاحاً."
            );

            return;
        }

        if (number < 1 ||
            number > opponent.Balloons)
        {
            await client.Reply(
                message,
                $"❌ اختر رقماً من 1 إلى {opponent.Balloons}."
            );

            return;
        }

        int result =
            Random.Shared.Next(1, 101);

        if (result <= 15)
        {
            await client.Reply(
                message,
                "🍀✨ حظك اليوم!\n\n" +
                $"🎈 البالون رقم {number} لم ينفجر!\n" +
                $"👤 اللاعب: {opponent.Name}"
            );

            NextTurn();
            await SendNextTurn(client, message);
            return;
        }

        if (result <= 30)
        {
            await client.Reply(
                message,
                "🛡️💨 البالون نجا!\n\n" +
                $"🎈 البالون رقم {number} بقي مكانه."
            );

            NextTurn();
            await SendNextTurn(client, message);
            return;
        }

        if (result <= 40)
        {
            opponent.Balloons--;

            await client.Reply(
                message,
                "🔄💥 طاخ!!! انفجر البالون!\n\n" +
                $"👤 {opponent.Name}\n" +
                $"🎈 المتبقي: {opponent.Balloons}\n\n" +
                "🔥 حصلت على دور إضافي!"
            );

            if (opponent.Balloons <= 0)
            {
                opponent.Balloons = 0;
                opponent.Alive = false;

                if (GetAlivePlayers().Count <= 1)
                {
                    BalloonPlayer winner =
                        GetAlivePlayers().First();

                    await client.Reply(
                        message,
                        "🏆🎉 انتهت اللعبة! 🎉🏆\n\n" +
                        $"👑 الفائز: {winner.Name}"
                    );

                    _game = null;
                    return;
                }
            }

            _game.WaitingForOpponent = true;
            _game.SelectedOpponentId = null;

            await client.Reply(
                message,
                BuildPlayersBoard() +
                "\n\n" +
                $"🔥 دورك مستمر يا {current.Name}!\n\n" +
                "👊 اختر رقم اللاعب."
            );

            return;
        }

        opponent.Balloons--;

        string resultText =
            "💥🎈 طاخ!!! انفجر البالون!\n\n" +
            $"👤 اللاعب: {opponent.Name}\n" +
            $"🎈 البالون رقم: {number}\n" +
            $"🎈 المتبقي: {opponent.Balloons}";

        if (opponent.Balloons <= 0)
        {
            opponent.Balloons = 0;
            opponent.Alive = false;

            resultText +=
                "\n\n💀 خرج اللاعب من اللعبة!";

            if (GetAlivePlayers().Count <= 1)
            {
                BalloonPlayer winner =
                    GetAlivePlayers().First();

                resultText +=
                    "\n\n🏆🎉 الفائز: " +
                    winner.Name;

                await client.Reply(
                    message,
                    resultText
                );

                _game = null;
                return;
            }
        }

        await client.Reply(
            message,
            resultText
        );

        NextTurn();

        await SendNextTurn(
            client,
            message
        );
    }

    private static void NextTurn()
    {
        if (_game == null)
            return;

        List<BalloonPlayer> alive =
            GetAlivePlayers();

        if (alive.Count <= 1)
            return;

        int count =
            _game.TurnOrder.Count;

        for (int i = 0; i < count; i++)
        {
            _game.CurrentTurnIndex++;

            if (_game.CurrentTurnIndex >= count)
                _game.CurrentTurnIndex = 0;

            string userId =
                _game.TurnOrder[
                    _game.CurrentTurnIndex
                ];

            BalloonPlayer? player =
                _game.Players.FirstOrDefault(
                    x => x.UserId == userId
                );

            if (player != null &&
                player.Alive)
            {
                break;
            }
        }

        _game.WaitingForOpponent = true;
        _game.SelectedOpponentId = null;
    }

    private static async Task SendNextTurn(
        IWolfClient client,
        Message message)
    {
        if (_game == null ||
            !_game.Started)
            return;

        BalloonPlayer? current =
            _game.CurrentPlayer;

        if (current == null)
            return;

        await client.Reply(
            message,
            BuildPlayersBoard() +
            "\n\n" +
            $"🎯 الدور الآن على: {current.Name}\n\n" +
            "👊 اختر رقم اللاعب."
        );
    }

    private static async Task EndGame(
        IWolfClient client,
        Message message)
    {
        if (_game == null)
        {
            await client.Reply(
                message,
                "❌ لا توجد لعبة حالياً."
            );

            return;
        }

        string result =
            "🛑🎈 تم إنهاء لعبة البالونات.\n\n";

        foreach (BalloonPlayer player in _game.Players)
        {
            result +=
                $"{player.Name} — " +
                $"{player.Balloons} 🎈";

            if (!player.Alive)
                result += " 💀";

            result += "\n";
        }

        _game = null;

        await client.Reply(
            message,
            result
        );
    }

    private static string BuildPlayersBoard()
    {
        if (_game == null)
            return "";

        string result =
            "🎈👥 قائمة اللاعبين 👥🎈\n\n";

        int index = 1;

        foreach (BalloonPlayer player in _game.Players)
        {
            string status =
                player.Alive
                    ? $"{player.Balloons} 🎈"
                    : "💀 خارج اللعبة";

            result +=
                $"{GetNumberEmoji(index)} " +
                $"{player.Name} — {status}\n";

            index++;
        }

        return result.TrimEnd();
    }

    private static string BuildBalloonNumbers(
        int count)
    {
        List<string> numbers = new();

        for (int i = 1; i <= count; i++)
        {
            numbers.Add(
                $"{GetNumberEmoji(i)} {i}"
            );
        }

        return string.Join(
            "   ",
            numbers
        );
    }

    private static List<BalloonPlayer> GetAlivePlayers()
    {
        if (_game == null)
            return new List<BalloonPlayer>();

        return _game.Players
            .Where(x => x.Alive)
            .ToList();
    }

    private static async Task<string> GetNickname(
        IWolfClient client,
        string userId)
    {
        try
        {
            var user =
                await client.GetUser(userId);

            if (user != null &&
                !string.IsNullOrWhiteSpace(
                    user.Nickname))
            {
                return user.Nickname;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "⚠️ GetNickname Error: " +
                ex.Message
            );
        }

        return userId;
    }

    private static string GetNumberEmoji(
        int number)
    {
        return number switch
        {
            1 => "1️⃣",
            2 => "2️⃣",
            3 => "3️⃣",
            4 => "4️⃣",
            5 => "5️⃣",
            6 => "6️⃣",
            7 => "7️⃣",
            8 => "8️⃣",
            9 => "9️⃣",
            10 => "🔟",
            _ => $"{number}."
        };
    }
}

public class BalloonGame
{
    public string GroupId { get; set; } = "";

    public bool Started { get; set; }

    public List<BalloonPlayer> Players { get; set; } = new();

    public List<string> TurnOrder { get; set; } = new();

    public int CurrentTurnIndex { get; set; }

    public bool WaitingForOpponent { get; set; }

    public string? SelectedOpponentId { get; set; }

    public BalloonPlayer? CurrentPlayer
    {
        get
        {
            if (!Started ||
                TurnOrder.Count == 0)
                return null;

            if (CurrentTurnIndex < 0 ||
                CurrentTurnIndex >= TurnOrder.Count)
                return null;

            string userId =
                TurnOrder[
                    CurrentTurnIndex
                ];

            return Players.FirstOrDefault(
                x => x.UserId == userId &&
                     x.Alive
            );
        }
    }
}

public class BalloonPlayer
{
    public string UserId { get; set; } = "";

    public string Name { get; set; } = "";

    public int Balloons { get; set; } = 7;

    public bool Alive { get; set; } = true;
}
