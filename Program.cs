using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

namespace BalloonBot
{
    public class Program
    {
        private static IWolfClient? _client;
        private static BalloonGame? _game;

        private static readonly HashSet<string> ProcessedMessages = new();
        private static readonly object MessageLock = new();

        public static async Task Main(string[] args)
        {
            Console.WriteLine("=================================");
            Console.WriteLine("🎈 تشغيل BalloonBot");
            Console.WriteLine("=================================");

            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(
                    "❌ WOLF_EMAIL أو WOLF_PASSWORD غير موجود.");

                return;
            }

            Console.WriteLine("✅ بيانات الحساب موجودة.");

            _client = new WolfClient();

            // ==============================
            // الاتصال
            // ==============================

            _client.OnConnected += () =>
            {
                Console.WriteLine(
                    "🟢 تم الاتصال بـ WOLF بنجاح.");

                return Task.CompletedTask;
            };

            _client.OnDisconnected += (ex) =>
            {
                Console.WriteLine(
                    "🔴 انقطع الاتصال بـ WOLF.");

                if (ex != null)
                {
                    Console.WriteLine(
                        ex.ToString());
                }
            };

            _client.OnConnectionError += (ex) =>
            {
                Console.WriteLine(
                    "❌ Connection Error:");

                Console.WriteLine(
                    ex.ToString());
            };

            // ==============================
            // استقبال رسائل WOLF
            // ==============================

            _client.Messaging.OnMessage += async (client, message) =>
            {
                try
                {
                    if (message == null)
                        return;

                    string text =
                        message.Content?.Trim() ?? "";

                    Console.WriteLine(
                        "=================================");

                    Console.WriteLine(
                        $"📩 وصلت رسالة");

                    Console.WriteLine(
                        $"👤 User: {message.UserId}");

                    Console.WriteLine(
                        $"🏠 Room: {message.GroupId}");

                    Console.WriteLine(
                        $"💬 Text: {text}");

                    Console.WriteLine(
                        "=================================");

                    if (string.IsNullOrWhiteSpace(text))
                        return;

                    // منع التكرار
                    if (!string.IsNullOrWhiteSpace(
                        message.MessageId))
                    {
                        lock (MessageLock)
                        {
                            if (ProcessedMessages.Contains(
                                message.MessageId))
                            {
                                Console.WriteLine(
                                    "⚠️ رسالة مكررة.");

                                return;
                            }

                            ProcessedMessages.Add(
                                message.MessageId);

                            if (ProcessedMessages.Count > 5000)
                            {
                                ProcessedMessages.Clear();
                            }
                        }
                    }

                    // ==============================
                    // الأوامر
                    // ==============================

                    if (text.StartsWith(
                        "!بالونات",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(
                            $"🎈 تم اكتشاف أمر: {text}");

                        await HandleCommand(
                            client,
                            message,
                            text);

                        return;
                    }

                    // ==============================
                    // الأرقام أثناء اللعبة
                    // ==============================

                    if (int.TryParse(
                        text,
                        out int number))
                    {
                        Console.WriteLine(
                            $"🔢 تم استقبال الرقم: {number}");

                        if (_game == null)
                            return;

                        if (!_game.Started)
                            return;

                        if (_game.GroupId !=
                            message.GroupId)
                            return;

                        await HandleNumber(
                            client,
                            message,
                            number);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "❌ Message Error:");

                    Console.WriteLine(
                        ex.ToString());
                }
            };

            // ==============================
            // تسجيل الدخول
            // ==============================

            try
            {
                Console.WriteLine(
                    "🔐 تسجيل الدخول إلى WOLF...");

                bool loginResult =
                    await _client.Login(
                        email,
                        password);

                if (!loginResult)
                {
                    Console.WriteLine(
                        "❌ تسجيل الدخول فشل.");

                    Console.WriteLine(
                        "❌ تأكد من WOLF_EMAIL و WOLF_PASSWORD.");

                    return;
                }

                Console.WriteLine(
                    "✅ تسجيل الدخول نجح.");

                // ==============================
                // الاتصال
                // ==============================

                Console.WriteLine(
                    "🔌 جاري الاتصال بـ WOLF...");

                await _client.Connect();

                Console.WriteLine(
                    "🟢 BalloonBot متصل.");

                Console.WriteLine(
                    "🎈 البوت الآن ينتظر رسائل WOLF...");

                Console.WriteLine(
                    "📌 جرّب إرسال:");

                Console.WriteLine(
                    "!بالونات");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "❌ خطأ تشغيل البوت:");

                Console.WriteLine(
                    ex.ToString());

                return;
            }

            // إبقاء البوت يعمل
            await Task.Delay(
                Timeout.Infinite);
        }

        private static async Task HandleCommand(
            IWolfClient client,
            Message message,
            string text)
        {
            string[] parts =
                text.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

            string command =
                parts.Length > 1
                    ? parts[1]
                        .Trim()
                        .ToLowerInvariant()
                    : "مساعدة";

            Console.WriteLine(
                $"⚙️ Command = {command}");

            switch (command)
            {
                case "مساعدة":
                case "مساعده":

                    await client.Reply(
                        message,
                        "🎈 لعبة البالونات 🎈\n\n" +
                        "!بالونات جديد\n" +
                        "!بالونات انضم\n" +
                        "!بالونات لاعبين\n" +
                        "!بالونات بدء\n" +
                        "!بالونات انهاء\n\n" +
                        "🎯 أثناء اللعب:\n" +
                        "أرسل رقم الخصم ثم رقم البالونة.");

                    break;

                case "جديد":

                    await NewGame(
                        client,
                        message);

                    break;

                case "انضم":
                case "انضمام":

                    await JoinGame(
                        client,
                        message);

                    break;

                case "لاعبين":

                    await ShowPlayers(
                        client,
                        message);

                    break;

                case "بدء":

                    await StartGame(
                        client,
                        message);

                    break;

                case "انهاء":
                case "إنهاء":

                    await EndGame(
                        client,
                        message);

                    break;

                default:

                    await client.Reply(
                        message,
                        "❌ الأمر غير معروف.\n" +
                        "اكتب !بالونات مساعدة");

                    break;
            }
        }

        private static async Task NewGame(
            IWolfClient client,
            Message message)
        {
            if (_game != null)
            {
                await client.Reply(
                    message,
                    "⚠️ توجد لعبة بالونات حاليًا.");

                return;
            }

            _game =
                new BalloonGame(
                    message.GroupId);

            BalloonPlayer player =
                await CreatePlayer(
                    client,
                    message,
                    1);

            _game.Players.Add(
                player);

            await client.Reply(
                message,
                "🎈 تم إنشاء لعبة البالونات!\n\n" +
                $"1️⃣ {player.Nickname} — 7 🎈\n\n" +
                "للانضمام:\n" +
                "!بالونات انضم");
        }

        private static async Task JoinGame(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة.\n" +
                    "اكتب !بالونات جديد");

                return;
            }

            if (_game.Started)
            {
                await client.Reply(
                    message,
                    "❌ اللعبة بدأت بالفعل.");

                return;
            }

            if (_game.GroupId !=
                message.GroupId)
            {
                await client.Reply(
                    message,
                    "❌ اللعبة موجودة في روم آخر.");

                return;
            }

            if (_game.Players.Any(
                p => p.UserId ==
                     message.UserId))
            {
                await client.Reply(
                    message,
                    "⚠️ أنت مشترك بالفعل.");

                return;
            }

            int number =
                _game.Players.Count + 1;

            BalloonPlayer player =
                await CreatePlayer(
                    client,
                    message,
                    number);

            _game.Players.Add(
                player);

            await client.GroupMessage(
                message.GroupId,
                $"🎈 انضم لاعب!\n\n" +
                $"{number}️⃣ {player.Nickname} — 7 🎈");
        }

        private static async Task<BalloonPlayer> CreatePlayer(
            IWolfClient client,
            Message message,
            int number)
        {
            string nickname = "لاعب";

            try
            {
                var user =
                    await client.GetUser(
                        message.UserId);

                if (user != null &&
                    !string.IsNullOrWhiteSpace(
                        user.Nickname))
                {
                    nickname =
                        user.Nickname;
                }
            }
            catch
            {
                nickname = "لاعب";
            }

            return new BalloonPlayer
            {
                UserId =
                    message.UserId,

                Nickname =
                    nickname,

                PlayerNumber =
                    number,

                ActiveBalloons =
                    Enumerable.Range(
                        1,
                        7).ToList(),

                Eliminated =
                    false
            };
        }

        private static async Task ShowPlayers(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة.");

                return;
            }

            if (_game.GroupId !=
                message.GroupId)
            {
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة في هذا الروم.");

                return;
            }

            string result =
                "🎈 لاعبين البالونات 🎈\n\n";

            foreach (var player in _game.Players)
            {
                string status =
                    player.Eliminated
                        ? "❌ خرج"
                        : $"{player.ActiveBalloons.Count} 🎈";

                result +=
                    $"{player.PlayerNumber}️⃣ " +
                    $"{player.Nickname} — " +
                    $"{status}\n";
            }

            if (_game.Started &&
                _game.CurrentPlayer != null)
            {
                result +=
                    "\n🎯 الدور على: " +
                    _game.CurrentPlayer.Nickname;
            }

            await client.Reply(
                message,
                result);
        }

        private static async Task StartGame(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة.");

                return;
            }

            if (_game.GroupId !=
                message.GroupId)
            {
                await client.Reply(
                    message,
                    "❌ اللعبة في روم آخر.");

                return;
            }

            if (_game.Started)
            {
                await client.Reply(
                    message,
                    "⚠️ اللعبة بدأت بالفعل.");

                return;
            }

            if (_game.Players.Count < 2)
            {
                await client.Reply(
                    message,
                    "❌ تحتاج لاعبين على الأقل.");

                return;
            }

            _game.Started = true;
            _game.CurrentIndex = 0;
            _game.WaitingForOpponent = true;
            _game.WaitingForBalloon = false;

            await SendGameBoard(
                client,
                message.GroupId);

            await AskForOpponent(
                client);
        }

        private static async Task SendGameBoard(
            IWolfClient client,
            string groupId)
        {
            if (_game == null)
                return;

            string text =
                "🎈🔥 لعبة البالونات بدأت! 🔥🎈\n\n";

            foreach (var player in _game.Players)
            {
                if (player.Eliminated)
                {
                    text +=
                        $"{player.PlayerNumber}️⃣ " +
                        $"{player.Nickname} — ❌ خرج\n";
                }
                else
                {
                    text +=
                        $"{player.PlayerNumber}️⃣ " +
                        $"{player.Nickname} — " +
                        $"{player.ActiveBalloons.Count} 🎈\n";
                }
            }

            await client.GroupMessage(
                groupId,
                text);
        }

        private static async Task HandleNumber(
            IWolfClient client,
            Message message,
            int number)
        {
            if (_game == null ||
                !_game.Started ||
                _game.CurrentPlayer == null)
                return;

            if (_game.GroupId !=
                message.GroupId)
                return;

            if (_game.CurrentPlayer.UserId !=
                message.UserId)
            {
                await client.Reply(
                    message,
                    "⏳ مو دورك.");

                return;
            }

            if (_game.WaitingForOpponent)
            {
                await ChooseOpponent(
                    client,
                    message,
                    number);

                return;
            }

            if (_game.WaitingForBalloon)
            {
                await ChooseBalloon(
                    client,
                    message,
                    number);
            }
        }

        private static async Task ChooseOpponent(
            IWolfClient client,
            Message message,
            int number)
        {
            if (_game == null ||
                _game.CurrentPlayer == null)
                return;

            BalloonPlayer? opponent =
                _game.Players.FirstOrDefault(
                    p =>
                        p.PlayerNumber == number &&
                        !p.Eliminated);

            if (opponent == null)
            {
                await client.Reply(
                    message,
                    "❌ رقم اللاعب غير صحيح.");

                return;
            }

            if (opponent.UserId ==
                _game.CurrentPlayer.UserId)
            {
                await client.Reply(
                    message,
                    "❌ ما تگدر تختار نفسك 😄");

                return;
            }

            _game.SelectedOpponent =
                opponent;

            _game.WaitingForOpponent =
                false;

            _game.WaitingForBalloon =
                true;

            string balloons =
                string.Join(
                    " ",
                    opponent.ActiveBalloons.Select(
                        x => $"{x}🎈"));

            await client.Reply(
                message,
                $"🎯 الخصم: {opponent.Nickname}\n\n" +
                $"{balloons}\n\n" +
                "💥 أرسل رقم البالونة.");
        }

        private static async Task ChooseBalloon(
            IWolfClient client,
            Message message,
            int balloonNumber)
        {
            if (_game == null ||
                _game.CurrentPlayer == null ||
                _game.SelectedOpponent == null)
                return;

            BalloonPlayer opponent =
                _game.SelectedOpponent;

            if (!opponent.ActiveBalloons.Contains(
                balloonNumber))
            {
                await client.Reply(
                    message,
                    "❌ هذه البالونة غير موجودة.");

                return;
            }

            _game.WaitingForBalloon =
                false;

            int outcome =
                Random.Shared.Next(
                    1,
                    101);

            bool extraTurn = false;

            string result;

            if (outcome <= 55)
            {
                opponent.ActiveBalloons.Remove(
                    balloonNumber);

                result =
                    $"💥 طاخ! البالونة {balloonNumber} انفجرت!\n" +
                    $"🎈 بقي لـ {opponent.Nickname}: " +
                    $"{opponent.ActiveBalloons.Count}";
            }
            else if (outcome <= 70)
            {
                result =
                    $"🍀 حظ!\n" +
                    $"البالونة {balloonNumber} نجت.";
            }
            else if (outcome <= 85)
            {
                result =
                    $"🛡️ البالونة {balloonNumber} نجت.\n" +
                    "🎯 الدور ينتقل.";
            }
            else if (outcome <= 95)
            {
                opponent.ActiveBalloons.Remove(
                    balloonNumber);

                extraTurn = true;

                result =
                    $"💥 انفجرت البالونة!\n" +
                    $"🎈 بقي لـ {opponent.Nickname}: " +
                    $"{opponent.ActiveBalloons.Count}\n\n" +
                    "🔄 دور إضافي!";
            }
            else
            {
                if (Random.Shared.Next(
                    0,
                    2) == 1)
                {
                    opponent.ActiveBalloons.Remove(
                        balloonNumber);

                    result =
                        "🎲 الحظ قرر...\n" +
                        "💥 انفجرت البالونة!";
                }
                else
                {
                    result =
                        "🎲 الحظ قرر...\n" +
                        "🍀 البالونة نجت!";
                }
            }

            await client.GroupMessage(
                message.GroupId,
                result);

            if (opponent.ActiveBalloons.Count == 0)
            {
                opponent.Eliminated = true;

                await client.GroupMessage(
                    message.GroupId,
                    $"❌ {opponent.Nickname} خرج من اللعبة!");

                if (await CheckWinner(
                    client))
                    return;
            }

            _game.SelectedOpponent = null;

            if (extraTurn)
            {
                _game.WaitingForOpponent = true;
                _game.WaitingForBalloon = false;

                await AskForOpponent(
                    client);

                return;
            }

            MoveToNextPlayer();

            await SendGameBoard(
                client,
                message.GroupId);

            await AskForOpponent(
                client);
        }

        private static void MoveToNextPlayer()
        {
            if (_game == null)
                return;

            int count =
                _game.Players.Count;

            for (int i = 0; i < count; i++)
            {
                _game.CurrentIndex =
                    (_game.CurrentIndex + 1) %
                    count;

                if (!_game.Players[
                    _game.CurrentIndex].Eliminated)
                {
                    return;
                }
            }
        }

        private static async Task AskForOpponent(
            IWolfClient client)
        {
            if (_game == null ||
                _game.CurrentPlayer == null)
                return;

            _game.WaitingForOpponent = true;
            _game.WaitingForBalloon = false;

            string players =
                string.Join(
                    "\n",
                    _game.Players
                        .Where(
                            p =>
                                !p.Eliminated &&
                                p.UserId !=
                                _game.CurrentPlayer!.UserId)
                        .Select(
                            p =>
                                $"{p.PlayerNumber}️⃣ " +
                                $"{p.Nickname} — " +
                                $"{p.ActiveBalloons.Count} 🎈"));

            await client.GroupMessage(
                _game.GroupId,
                $"🎯 دور {_game.CurrentPlayer.Nickname}\n\n" +
                "أرسل رقم الخصم:\n\n" +
                players);
        }

        private static async Task<bool> CheckWinner(
            IWolfClient client)
        {
            if (_game == null)
                return true;

            var alive =
                _game.Players
                    .Where(p => !p.Eliminated)
                    .ToList();

            if (alive.Count > 1)
                return false;

            if (alive.Count == 1)
            {
                BalloonPlayer winner =
                    alive[0];

                await client.GroupMessage(
                    _game.GroupId,
                    $"🏆🎉 الفائز هو {winner.Nickname}! 🎉🏆\n" +
                    $"🎈 بقي لديه {winner.ActiveBalloons.Count} بالونات.");

                _game = null;

                return true;
            }

            _game = null;

            return true;
        }

        private static async Task EndGame(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة.");

                return;
            }

            if (_game.GroupId !=
                message.GroupId)
            {
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة بهذا الروم.");

                return;
            }

            await client.GroupMessage(
                message.GroupId,
                "🛑 تم إنهاء لعبة البالونات.");

            _game = null;
        }
    }

    public class BalloonGame
    {
        public string GroupId { get; }

        public bool Started { get; set; }

        public List<BalloonPlayer> Players { get; } = new();

        public int CurrentIndex { get; set; }

        public BalloonPlayer? SelectedOpponent { get; set; }

        public bool WaitingForOpponent { get; set; }

        public bool WaitingForBalloon { get; set; }

        public BalloonPlayer? CurrentPlayer
        {
            get
            {
                if (Players.Count == 0)
                    return null;

                if (CurrentIndex < 0 ||
                    CurrentIndex >= Players.Count)
                    return null;

                return Players[CurrentIndex];
            }
        }

        public BalloonGame(
            string groupId)
        {
            GroupId = groupId;
            Started = false;
            CurrentIndex = 0;
        }
    }

    public class BalloonPlayer
    {
        public string UserId { get; set; } = "";

        public string Nickname { get; set; } = "";

        public int PlayerNumber { get; set; }

        public List<int> ActiveBalloons { get; set; } = new();

        public bool Eliminated { get; set; }
    }
}
