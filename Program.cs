استبدل

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

        private static readonly HashSet<string> _processedMessages = new();
        private static readonly object _messageLock = new();

        public static async Task Main()
        {
            Console.WriteLine("=================================");
            Console.WriteLine("🎈 Balloon Bot starting...");
            Console.WriteLine("=================================");

            string? email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL");

            string? password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD");

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("❌ لم يتم العثور على بيانات حساب WOLF.");
                Console.WriteLine("يجب توفير:");
                Console.WriteLine("WOLF_EMAIL");
                Console.WriteLine("WOLF_PASSWORD");
                return;
            }

            Console.WriteLine("✅ بيانات الحساب موجودة.");

            _client = new WolfClient();

            _client.Messaging.OnMessage += async (client, message) =>
            {
                try
                {
                    if (message == null)
                        return;

                    string text = message.Content?.Trim() ?? "";

                    Console.WriteLine(
                        $"📩 رسالة وصلت: [{message.GroupId}] {message.UserId}: {text}");

                    if (string.IsNullOrWhiteSpace(text))
                        return;

                    lock (_messageLock)
                    {
                        if (!string.IsNullOrWhiteSpace(message.MessageId))
                        {
                            if (_processedMessages.Contains(message.MessageId))
                            {
                                Console.WriteLine(
                                    "⚠️ الرسالة مكررة وتم تجاهلها.");
                                return;
                            }

                            _processedMessages.Add(message.MessageId);
                        }
                    }

                    string groupId = message.GroupId;

                    if (text.StartsWith(
                        "!بالونات",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(
                            $"🎈 أمر بالونات: {text}");

                        await HandleCommand(
                            client,
                            message,
                            text);

                        return;
                    }

                    if (int.TryParse(text, out int number))
                    {
                        Console.WriteLine(
                            $"🔢 رقم مستلم: {number}");

                        if (_game == null)
                            return;

                        if (!_game.Started)
                            return;

                        if (_game.GroupId != groupId)
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
                        $"❌ Message error: {ex}");
                }
            };

            try
            {
                Console.WriteLine("🔐 تسجيل الدخول...");

                await _client.Login(
                    email,
                    password);

                Console.WriteLine(
                    "✅ تم تسجيل الدخول بنجاح.");

                Console.WriteLine(
                    "🔌 الاتصال بـ WOLF...");

                await _client.Connect();

                Console.WriteLine(
                    "🟢 البوت يعمل الآن وينتظر الرسائل...");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "❌ فشل تشغيل البوت:");
                Console.WriteLine(ex);
            }

            await Task.Delay(Timeout.Infinite);
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
                        "!بالونات جديد — إنشاء لعبة جديدة\n" +
                        "!بالونات انضم — الانضمام للعبة\n" +
                        "!بالونات لاعبين — عرض اللاعبين\n" +
                        "!بالونات بدء — بدء اللعبة\n" +
                        "!بالونات انهاء — إنهاء اللعبة\n\n" +
                        "🎯 أثناء دورك:\n" +
                        "1️⃣ أرسل رقم الخصم\n" +
                        "2️⃣ بعدها أرسل رقم البالونة\n\n" +
                        "كل لاعب يبدأ بـ 7 🎈\n" +
                        "آخر لاعب يبقى هو الفائز 🏆");

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
                    "⚠️ توجد لعبة بالونات حاليًا.\n" +
                    "إذا تريد إلغاءها استخدم:\n" +
                    "!بالونات انهاء");

                return;
            }

            _game =
                new BalloonGame(message.GroupId);

            BalloonPlayer creator =
                await CreatePlayer(
                    client,
                    message,
                    1);

            _game.Players.Add(creator);

            await client.Reply(
                message,
                "🎈 تم إنشاء لعبة البالونات! 🎈\n\n" +
                $"👤 اللاعب رقم 1: {creator.Nickname}\n" +
                "🎈 البالونات: 7\n\n" +
                "للانضمام أرسل:\n" +
                "!بالونات انضم\n\n" +
                "بعد اكتمال اللاعبين أرسل:\n" +
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
                    "❌ لا توجد لعبة حاليًا.\n" +
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

            if (_game.GroupId != message.GroupId)
            {
                await client.Reply(
                    message,
                    "❌ توجد اللعبة في روم آخر.");

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

            await client.Reply(
                message,
                $"🎈 انضم اللاعب رقم {number}\n" +
                $"👤 {player.Nickname}\n" +
                "🎈 البالونات: 7\n\n" +
                "اكتب !بالونات لاعبين لعرض القائمة.");
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
                    await client.GetUser(message.UserId);

                if (user != null &&
                    !string.IsNullOrWhiteSpace(user.Nickname))
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
                    Enumerable.Range(1, 7).ToList()
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
                "🎈 لاعبين لعبة البالونات 🎈\n\n";

            foreach (var player in _game.Players)
            {
                string status;

                if (player.Eliminated)
                {
                    status = "❌ خرج";
                }
                else
                {
                    status =
                        $"{player.ActiveBalloons.Count} 🎈";
                }

                result +=
                    $"{player.PlayerNumber}️⃣ " +
                    $"{player.Nickname} — {status}\n";
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
                    "❌ لا توجد لعبة.\n" +
                    "اكتب !بالونات جديد");

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
                    "❌ تحتاج اللعبة إلى لاعبين على الأقل.");

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
                return;

            if (_game.GroupId != message.GroupId)
                return;

            if (_game.CurrentPlayer.UserId != message.UserId)
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
                    "❌ رقم اللاعب غير صحيح أو اللاعب خرج.\n" +
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
                    opponent.ActiveBalloons
                        .Select(
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
                return;

            BalloonPlayer opponent =
                _game.SelectedOpponent;

            if (!opponent.ActiveBalloons.Contains(
                balloonNumber))
            {
                await client.Reply(
                    message,
                    "❌ هذه البالونة غير موجودة.\n" +
                    "اختار رقم بالونة موجودة.");

                return;
            }

            _game.WaitingForBalloon = false;

            int outcome =
                Random.Shared.Next(1, 101);

            string result;
            bool extraTurn = false;

            if (outcome <= 55)
            {
                opponent.ActiveBalloons.Remove(
                    balloonNumber);

                result =
                    $"💥 طاخ! البالونة رقم {balloonNumber} انفجرت!\n" +
                    $"🎈 {opponent.Nickname} صار عنده " +
                    $"{opponent.ActiveBalloons.Count} بالونات.";
            }
            else if (outcome <= 70)
            {
                result =
                    $"🍀 حظك قوي!\n" +
                    $"البالونة رقم {balloonNumber} ما انطكت 😎\n" +
                    $"🎈 ما زالت عند {opponent.Nickname}.";
            }
            else if (outcome <= 85)
            {
                result =
                    $"🛡️ نجت البالونة!\n" +
                    $"البالونة رقم {balloonNumber} بقيت سالمة.\n" +
                    "🎈 الدور راح ينتقل للاعب التالي.";
            }
            else if (outcome <= 95)
            {
                opponent.ActiveBalloons.Remove(
                    balloonNumber);

                extraTurn = true;

                result =
                    $"💥 انفجرت البالونة!\n" +
                    $"🎈 {opponent.Nickname} صار عنده " +
                    $"{opponent.ActiveBalloons.Count} بالونات.\n\n" +
                    "🔄 مفاجأة! حصلت على دور إضافي!";
            }
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
                        "ما انطكت.";
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

                if (await CheckWinner(client))
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
            if (_game == null)
                return;

            int count =
                _game.Players.Count;

            for (int i = 0; i < count; i++)
            {
                _game.CurrentIndex =
                    (_game.CurrentIndex + 1) % count;

                BalloonPlayer next =
                    _game.Players[_game.CurrentIndex];

                if (!next.Eliminated)
                    return;
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
                "اختار الخصم بإرسال رقمه:\n\n" +
                players);
        }

        private static async Task<bool> CheckWinner(
            IWolfClient client)
        {
            if (_game == null)
                return true;

            var alive =
                _game.Players
                    .Where(
                        p => !p.Eliminated)
                    .ToList();

            if (alive.Count > 1)
                return false;

            if (alive.Count == 1)
            {
                BalloonPlayer winner =
                    alive[0];

                await client.GroupMessage(
                    _game.GroupId,
                    $"🏆🎉 انتهت اللعبة! 🎉🏆\n\n" +
                    $"👑 الفائز هو: {winner.Nickname}\n" +
                    $"🎈 بقي لديه: {winner.ActiveBalloons.Count} بالونات\n\n" +
                    "🔥 مبروك للفائز!");

                _game = null;

                return true;
            }

            return false;
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
                    return null;

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
