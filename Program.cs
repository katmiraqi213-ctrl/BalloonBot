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

        public static async Task Main()
        {
            Console.WriteLine("=================================");
            Console.WriteLine("🎈 Balloon Bot starting...");
            Console.WriteLine("=================================");

            string? email = Environment.GetEnvironmentVariable("WOLF_EMAIL");
            string? password = Environment.GetEnvironmentVariable("WOLF_PASSWORD");

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("❌ بيانات WOLF غير موجودة.");
                Console.WriteLine("❌ تأكد من GitHub Secrets:");
                Console.WriteLine("WOLF_EMAIL");
                Console.WriteLine("WOLF_PASSWORD");
                return;
            }

            Console.WriteLine("✅ بيانات الحساب موجودة.");

            try
            {
                _client = new WolfClient();

                _client.Messaging.OnMessage += async (client, message) =>
                {
                    try
                    {
                        if (message == null)
                            return;

                        string text = message.Content?.Trim() ?? "";

                        Console.WriteLine(
                            $"📩 رسالة: [{message.GroupId}] {message.UserId}: {text}");

                        if (string.IsNullOrWhiteSpace(text))
                            return;

                        if (!string.IsNullOrWhiteSpace(message.MessageId))
                        {
                            lock (MessageLock)
                            {
                                if (ProcessedMessages.Contains(message.MessageId))
                                {
                                    Console.WriteLine("⚠️ رسالة مكررة.");
                                    return;
                                }

                                ProcessedMessages.Add(message.MessageId);

                                // حتى لا تكبر الذاكرة إلى ما لا نهاية
                                if (ProcessedMessages.Count > 5000)
                                {
                                    ProcessedMessages.Clear();
                                }
                            }
                        }

                        if (text.StartsWith(
                            "!بالونات",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"🎈 أمر: {text}");

                            await HandleCommand(
                                client,
                                message,
                                text);

                            return;
                        }

                        if (int.TryParse(text, out int number))
                        {
                            Console.WriteLine($"🔢 رقم: {number}");

                            if (_game == null)
                                return;

                            if (!_game.Started)
                                return;

                            if (_game.GroupId != message.GroupId)
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
                            $"❌ خطأ أثناء استقبال الرسالة: {ex}");
                    }
                };

                Console.WriteLine("🔐 تسجيل الدخول...");

                await _client.Login(
                    email,
                    password);

                Console.WriteLine("✅ تم تسجيل الدخول بنجاح.");

                Console.WriteLine("🔌 الاتصال بـ WOLF...");

                await _client.Connect();

                Console.WriteLine(
                    "🟢 BalloonBot يعمل الآن!");

                Console.WriteLine(
                    "🎈 بانتظار أوامر WOLF...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ فشل تشغيل البوت:");
                Console.WriteLine(ex);
            }

            await Task.Delay(Timeout.Infinite);
        }

        private static async Task HandleCommand(
            IWolfClient client,
            Message message,
            string text)
        {
            string[] parts = text.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            string command =
                parts.Length >= 2
                    ? parts[1].Trim().ToLowerInvariant()
                    : "مساعدة";

            Console.WriteLine(
                $"⚙️ تنفيذ الأمر: {command}");

            switch (command)
            {
                case "مساعدة":
                case "مساعده":

                    await client.Reply(
                        message,
                        "🎈 لعبة البالونات 🎈\n\n" +
                        "الأوامر:\n" +
                        "!بالونات جديد\n" +
                        "!بالونات انضم\n" +
                        "!بالونات لاعبين\n" +
                        "!بالونات بدء\n" +
                        "!بالونات انهاء\n\n" +
                        "🎯 طريقة اللعب:\n" +
                        "1️⃣ اللاعب يرسل رقم الخصم\n" +
                        "2️⃣ بعدها يرسل رقم البالونة\n\n" +
                        "🎈 كل لاعب يبدأ بـ 7 بالونات\n" +
                        "🏆 آخر لاعب يبقى هو الفائز.");

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
                        "❌ الأمر غير معروف.\n\n" +
                        "اكتب:\n" +
                        "!بالونات مساعدة");

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
                    "⚠️ توجد لعبة بالونات حاليًا.\n\n" +
                    "إذا تريد إنهاءها اكتب:\n" +
                    "!بالونات انهاء");

                return;
            }

            _game = new BalloonGame(
                message.GroupId);

            BalloonPlayer creator =
                await CreatePlayer(
                    client,
                    message,
                    1);

            _game.Players.Add(creator);

            await client.Reply(
                message,
                "🎈🎉 تم إنشاء لعبة البالونات! 🎉🎈\n\n" +
                $"1️⃣ {creator.Nickname} — 7 🎈\n\n" +
                "👥 حتى يدخل اللاعبون:\n" +
                "!بالونات انضم\n\n" +
                "📋 لعرض اللاعبين:\n" +
                "!بالونات لاعبين\n\n" +
                "🚀 لبدء اللعبة:\n" +
                "!بالونات بدء");
        }

        private static async Task JoinGame(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة حاليًا.\n\n" +
                    "اكتب:\n" +
                    "!بالونات جديد");

                return;
            }

            if (_game.Started)
            {
                await client.Reply(
                    message,
                    "❌ اللعبة بدأت بالفعل.\n" +
                    "لا يمكن الانضمام الآن.");

                return;
            }

            if (_game.GroupId != message.GroupId)
            {
                await client.Reply(
                    message,
                    "❌ اللعبة موجودة في روم آخر.");

                return;
            }

            if (_game.Players.Any(
                p => p.UserId == message.UserId))
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

            _game.Players.Add(player);

            await client.GroupMessage(
                message.GroupId,
                $"🎈 انضم لاعب جديد!\n\n" +
                $"{number}️⃣ {player.Nickname} — 7 🎈\n\n" +
                $"👥 عدد اللاعبين: {_game.Players.Count}\n\n" +
                "📋 اكتب !بالونات لاعبين لعرض القائمة.");
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
                    nickname = user.Nickname;
                }
            }
            catch
            {
                nickname = "لاعب";
            }

            return new BalloonPlayer
            {
                UserId = message.UserId,
                Nickname = nickname,
                PlayerNumber = number,
                ActiveBalloons =
                    Enumerable.Range(1, 7).ToList(),
                Eliminated = false
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

            if (_game.GroupId != message.GroupId)
            {
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة بالونات في هذا الروم.");

                return;
            }

            string result =
                "🎈🔥 لاعبين لعبة البالونات 🔥🎈\n\n";

            foreach (BalloonPlayer player in _game.Players)
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
                    "\n🎯 الدور الآن على: " +
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
                    "❌ لا توجد لعبة.\n\n" +
                    "اكتب:\n" +
                    "!بالونات جديد");

                return;
            }

            if (_game.GroupId != message.GroupId)
            {
                await client.Reply(
                    message,
                    "❌ اللعبة موجودة في روم آخر.");

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
                    "❌ لازم يكون هناك لاعبين على الأقل.");

                return;
            }

            _game.Started = true;
            _game.CurrentIndex = 0;
            _game.WaitingForOpponent = true;
            _game.WaitingForBalloon = false;

            await SendGameBoard(
                client,
                message.GroupId);

            await AskForOpponent(client);
        }

        private static async Task SendGameBoard(
            IWolfClient client,
            string groupId)
        {
            if (_game == null)
                return;

            string text =
                "🎈🔥 لعبة البالونات بدأت! 🔥🎈\n\n";

            foreach (BalloonPlayer player in _game.Players)
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

            text +=
                "\n🎯 كل لاعب يبدأ بـ 7 بالونات.";

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
            {
                return;
            }

            if (_game.GroupId != message.GroupId)
                return;

            if (_game.CurrentPlayer.UserId !=
                message.UserId)
            {
                await client.Reply(
                    message,
                    "⏳ مو دورك حاليًا.");

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
            {
                return;
            }

            BalloonPlayer? opponent =
                _game.Players.FirstOrDefault(
                    p =>
                        p.PlayerNumber == number &&
                        !p.Eliminated);

            if (opponent == null)
            {
                await client.Reply(
                    message,
                    "❌ رقم اللاعب غير صحيح.\n" +
                    "اختار رقم لاعب موجود.");

                return;
            }

            if (opponent.UserId ==
                _game.CurrentPlayer.UserId)
            {
                await client.Reply(
                    message,
                    "❌ ما تگدر تختار نفسك 😄\n" +
                    "اختار لاعب ثاني.");

                return;
            }

            _game.SelectedOpponent = opponent;
            _game.WaitingForOpponent = false;
            _game.WaitingForBalloon = true;

            string balloons =
                string.Join(
                    " ",
                    opponent.ActiveBalloons.Select(
                        x => $"{x}🎈"));

            await client.Reply(
                message,
                $"🎯 اخترت: {opponent.Nickname}\n\n" +
                $"🎈 بالونات {opponent.Nickname}:\n" +
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
            {
                return;
            }

            BalloonPlayer opponent =
                _game.SelectedOpponent;

            if (!opponent.ActiveBalloons.Contains(
                balloonNumber))
            {
                await client.Reply(
                    message,
                    "❌ رقم البالونة غير صحيح.\n" +
                    "اختار بالونة موجودة.");

                return;
            }

            _game.WaitingForBalloon = false;

            int outcome =
                Random.Shared.Next(1, 101);

            string result;
            bool extraTurn = false;

            // 55% انفجار
            if (outcome <= 55)
            {
                opponent.ActiveBalloons.Remove(
                    balloonNumber);

                result =
                    $"💥 طاخ! البالونة رقم {balloonNumber} انفجرت!\n" +
                    $"🎈 {opponent.Nickname} صار عنده " +
                    $"{opponent.ActiveBalloons.Count} بالونات.";
            }

            // 15% حظ
            else if (outcome <= 70)
            {
                result =
                    $"🍀 حظك قوي!\n" +
                    $"البالونة رقم {balloonNumber} نجت 😎\n" +
                    $"🎈 ما زالت عند {opponent.Nickname}.";
            }

            // 15% نجاة
            else if (outcome <= 85)
            {
                result =
                    $"🛡️ نجت البالونة!\n" +
                    $"البالونة رقم {balloonNumber} بقيت سالمة.\n" +
                    "🎯 الدور ينتقل للاعب التالي.";
            }

            // 10% انفجار + دور إضافي
            else if (outcome <= 95)
            {
                opponent.ActiveBalloons.Remove(
                    balloonNumber);

                extraTurn = true;

                result =
                    $"💥 انفجرت البالونة رقم {balloonNumber}!\n" +
                    $"🎈 {opponent.Nickname} صار عنده " +
                    $"{opponent.ActiveBalloons.Count} بالونات.\n\n" +
                    "🔄 مفاجأة! عندك دور إضافي!";
            }

            // 5% عشوائي
            else
            {
                bool pop =
                    Random.Shared.Next(0, 2) == 1;

                if (pop)
                {
                    opponent.ActiveBalloons.Remove(
                        balloonNumber);

                    result =
                        "🎲 الحظ العشوائي قرر...\n" +
                        "💥 انفجرت البالونة!\n" +
                        $"🎈 بقي لـ {opponent.Nickname}: " +
                        $"{opponent.ActiveBalloons.Count}";
                }
                else
                {
                    result =
                        "🎲 الحظ العشوائي قرر...\n" +
                        "🍀 البالونة نجت!\n" +
                        "🎈 ما انطكت.";
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
                    $"❌💥 {opponent.Nickname} خلصت بالوناته وطلع من اللعبة!");

                bool winnerFound =
                    await CheckWinner(client);

                if (winnerFound)
                    return;
            }

            _game.SelectedOpponent = null;

            if (extraTurn)
            {
                _game.WaitingForOpponent = true;
                _game.WaitingForBalloon = false;

                await AskForOpponent(client);

                return;
            }

            MoveToNextPlayer();

            await SendGameBoard(
                client,
                message.GroupId);

            await AskForOpponent(client);
        }

        private static void MoveToNextPlayer()
        {
            if (_game == null ||
                _game.Players.Count == 0)
            {
                return;
            }

            int count =
                _game.Players.Count;

            for (int i = 0; i < count; i++)
            {
                _game.CurrentIndex =
                    (_game.CurrentIndex + 1) % count;

                BalloonPlayer next =
                    _game.Players[
                        _game.CurrentIndex];

                if (!next.Eliminated)
                    return;
            }
        }

        private static async Task AskForOpponent(
            IWolfClient client)
        {
            if (_game == null ||
                _game.CurrentPlayer == null)
            {
                return;
            }

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
                "اختار الخصم بإرسال رقمه:\n\n" +
                players);
        }

        private static async Task<bool> CheckWinner(
            IWolfClient client)
        {
            if (_game == null)
                return true;

            List<BalloonPlayer> alive =
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
                    "🏆🎉 انتهت لعبة البالونات! 🎉🏆\n\n" +
                    $"👑 الفائز: {winner.Nickname}\n" +
                    $"🎈 بقي لديه: {winner.ActiveBalloons.Count} بالونات\n\n" +
                    "🔥 مبروك للفائز!");

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

            if (_game.GroupId != message.GroupId)
            {
                await client.Reply(
                    message,
                    "❌ ماكو لعبة بالونات بهذا الروم.");

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
                {
                    return null;
                }

                return Players[CurrentIndex];
            }
        }

        public BalloonGame(string groupId)
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
