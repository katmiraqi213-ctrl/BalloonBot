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
                Console.WriteLine("❌ WOLF_EMAIL أو WOLF_PASSWORD غير موجود.");
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

                    Console.WriteLine("=================================");
                    Console.WriteLine("📩 وصلت رسالة");
                    Console.WriteLine($"👤 User: {message.UserId}");
                    Console.WriteLine($"🏠 Room: {message.GroupId}");
                    Console.WriteLine($"💬 Text: {text}");
                    Console.WriteLine("=================================");

                    if (string.IsNullOrWhiteSpace(text))
                        return;

                    if (!string.IsNullOrWhiteSpace(message.MessageId))
                    {
                        lock (MessageLock)
                        {
                            if (ProcessedMessages.Contains(message.MessageId))
                                return;

                            ProcessedMessages.Add(message.MessageId);

                            if (ProcessedMessages.Count > 5000)
                                ProcessedMessages.Clear();
                        }
                    }

                    if (text.StartsWith(
                        "!بالونات",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleCommand(message, text);
                        return;
                    }

                    if (int.TryParse(text, out int number))
                    {
                        if (_game == null || !_game.Started)
                            return;

                        if (_game.GroupId != message.GroupId)
                            return;

                        await HandleNumber(message, number);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Message Error:");
                    Console.WriteLine(ex);
                }
            };

            try
            {
                Console.WriteLine("🔐 تسجيل الدخول إلى WOLF...");

                bool loginResult =
                    await _client.Login(email, password);

                if (!loginResult)
                {
                    Console.WriteLine("❌ تسجيل الدخول فشل.");
                    return;
                }

                Console.WriteLine("✅ تسجيل الدخول نجح.");
                Console.WriteLine("🔌 جاري الاتصال بـ WOLF...");

                await _client.Connect();

                Console.WriteLine("🟢 BalloonBot متصل.");
                Console.WriteLine("🎈 البوت الآن ينتظر رسائل WOLF...");
                Console.WriteLine("📌 جرّب إرسال: !بالونات");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ خطأ تشغيل البوت:");
                Console.WriteLine(ex);
                return;
            }

            await Task.Delay(Timeout.Infinite);
        }

        private static async Task HandleCommand(
            dynamic message,
            string text)
        {
            string command = text.Trim();

            if (command.Equals("!بالونات",
                    StringComparison.OrdinalIgnoreCase) ||
                command.Equals("!بالونات مساعدة",
                    StringComparison.OrdinalIgnoreCase))
            {
                await Send(
                    message.GroupId,
                    GetHelp());

                return;
            }

            if (command.Equals("!بالونات جديد",
                    StringComparison.OrdinalIgnoreCase))
            {
                await NewGame(message);
                return;
            }

            if (command.Equals("!بالونات انضم",
                    StringComparison.OrdinalIgnoreCase) ||
                command.Equals("!بالونات انضمام",
                    StringComparison.OrdinalIgnoreCase))
            {
                await JoinGame(message);
                return;
            }

            if (command.Equals("!بالونات لاعبين",
                    StringComparison.OrdinalIgnoreCase))
            {
                await ShowPlayers(message);
                return;
            }

            if (command.Equals("!بالونات بدء",
                    StringComparison.OrdinalIgnoreCase))
            {
                await StartGame(message);
                return;
            }

            if (command.Equals("!بالونات انهاء",
                    StringComparison.OrdinalIgnoreCase) ||
                command.Equals("!بالونات إنهاء",
                    StringComparison.OrdinalIgnoreCase))
            {
                await EndGame(message);
                return;
            }

            await Send(
                message.GroupId,
                "❌ الأمر غير معروف.\nاكتب !بالونات للمساعدة.");
        }

        private static string GetHelp()
        {
            return
                "🎈 لعبة البالونات 🎈\n\n" +
                "📌 الأوامر:\n\n" +
                "!بالونات جديد\n" +
                "إنشاء لعبة جديدة\n\n" +
                "!بالونات انضم\n" +
                "الانضمام إلى اللعبة\n\n" +
                "!بالونات لاعبين\n" +
                "عرض اللاعبين وعدد البالونات\n\n" +
                "!بالونات بدء\n" +
                "بدء اللعبة\n\n" +
                "!بالونات انهاء\n" +
                "إنهاء اللعبة\n\n" +
                "🎯 طريقة اللعب:\n" +
                "كل لاعب يبدأ بـ 7 🎈\n" +
                "اللعبة فردية بدون فرق.\n\n" +
                "بعد بدء اللعبة يظهر رقم كل لاعب.\n" +
                "مثال:\n" +
                "1️⃣ محمد — 7 🎈\n" +
                "2️⃣ علي — 7 🎈\n" +
                "3️⃣ حيدر — 7 🎈\n\n" +
                "اختار رقم الخصم فقط.\n" +
                "مثال: 3\n\n" +
                "بعدها اختار رقم البالون.\n" +
                "مثال: 5\n\n" +
                "💥 انفجار\n" +
                "🍀 حظ\n" +
                "🛡️ نجاة\n" +
                "🔄 دور إضافي\n\n" +
                "🏆 آخر لاعب يبقى هو الفائز.";
        }

        private static async Task NewGame(dynamic message)
        {
            if (_game != null)
            {
                await Send(
                    message.GroupId,
                    "⚠️ توجد لعبة بالونات موجودة حالياً.\n" +
                    "اكتب !بالونات انهاء أولاً.");

                return;
            }

            _game = new BalloonGame(message.GroupId);

            await Send(
                message.GroupId,
                "🎈 تم إنشاء لعبة البالونات!\n\n" +
                "كل لاعب لديه 7 🎈\n" +
                "اللعبة فردية بدون فرق.\n\n" +
                "للانضمام اكتب:\n" +
                "!بالونات انضم\n\n" +
                "بعد اكتمال اللاعبين اكتب:\n" +
                "!بالونات بدء");
        }

        private static async Task JoinGame(dynamic message)
        {
            if (_game == null)
            {
                await Send(
                    message.GroupId,
                    "❌ لا توجد لعبة.\nاكتب !بالونات جديد");

                return;
            }

            if (_game.Started)
            {
                await Send(
                    message.GroupId,
                    "❌ اللعبة بدأت بالفعل.");

                return;
            }

            if (_game.GroupId != message.GroupId)
                return;

            if (_game.Players.Any(
                p => p.UserId == message.UserId))
            {
                await Send(
                    message.GroupId,
                    "⚠️ أنت منضم للعبة بالفعل.");

                return;
            }

            string playerName =
                string.IsNullOrWhiteSpace(message.UserName)
                    ? $"لاعب {message.UserId}"
                    : message.UserName;

            BalloonPlayer player =
                new BalloonPlayer(
                    message.UserId,
                    playerName);

            _game.Players.Add(player);

            await Send(
                message.GroupId,
                $"🎈 انضم {playerName} إلى اللعبة!\n\n" +
                $"👥 عدد اللاعبين: {_game.Players.Count}\n\n" +
                "اكتب !بالونات لاعبين لرؤية القائمة.");
        }

        private static async Task ShowPlayers(dynamic message)
        {
            if (_game == null)
            {
                await Send(
                    message.GroupId,
                    "❌ لا توجد لعبة.");

                return;
            }

            if (_game.GroupId != message.GroupId)
                return;

            if (_game.Players.Count == 0)
            {
                await Send(
                    message.GroupId,
                    "👥 لا يوجد لاعبون حتى الآن.");

                return;
            }

            string result = "🎈 لاعبو اللعبة:\n\n";

            for (int i = 0; i < _game.Players.Count; i++)
            {
                BalloonPlayer p = _game.Players[i];

                string status =
                    p.Eliminated
                        ? "❌ خارج اللعبة"
                        : $"{p.Balloons} 🎈";

                result +=
                    $"{i + 1}️⃣ {p.Name} — {status}\n";
            }

            await Send(message.GroupId, result);
        }

        private static async Task StartGame(dynamic message)
        {
            if (_game == null)
            {
                await Send(
                    message.GroupId,
                    "❌ لا توجد لعبة.\nاكتب !بالونات جديد");

                return;
            }

            if (_game.GroupId != message.GroupId)
                return;

            if (_game.Started)
            {
                await Send(
                    message.GroupId,
                    "⚠️ اللعبة بدأت بالفعل.");

                return;
            }

            if (_game.Players.Count < 2)
            {
                await Send(
                    message.GroupId,
                    "❌ يجب أن يكون هناك لاعبان على الأقل.");

                return;
            }

            _game.Started = true;
            _game.CurrentPlayerIndex = 0;
            _game.WaitingForOpponent = true;

            await SendGameBoard(message.GroupId);
            await AskForOpponent(message.GroupId);
        }

        private static async Task SendGameBoard(string groupId)
        {
            if (_game == null)
                return;

            string result =
                "🎈🎈 لعبة البالونات 🎈🎈\n\n";

            for (int i = 0; i < _game.Players.Count; i++)
            {
                BalloonPlayer p = _game.Players[i];

                if (p.Eliminated)
                    continue;

                result +=
                    $"{i + 1}️⃣ {p.Name} — {p.Balloons} 🎈\n";
            }

            await Send(groupId, result);
        }

        private static async Task AskForOpponent(string groupId)
        {
            if (_game == null)
                return;

            BalloonPlayer? current =
                _game.GetCurrentPlayer();

            if (current == null)
                return;

            _game.WaitingForOpponent = true;
            _game.WaitingForBalloon = false;
            _game.SelectedOpponent = null;

            string result =
                $"🎯 الدور على: {current.Name}\n\n" +
                "اختر رقم الخصم:\n";

            for (int i = 0; i < _game.Players.Count; i++)
            {
                BalloonPlayer p = _game.Players[i];

                if (p.Eliminated)
                    continue;

                if (p.UserId == current.UserId)
                    continue;

                result +=
                    $"{i + 1}️⃣ {p.Name} — {p.Balloons} 🎈\n";
            }

            result += "\n📌 أرسل رقم الخصم فقط.";

            await Send(groupId, result);
        }

        private static async Task HandleNumber(
            dynamic message,
            int number)
        {
            if (_game == null || !_game.Started)
                return;

            BalloonPlayer? current =
                _game.GetCurrentPlayer();

            if (current == null)
                return;

            if (current.UserId != message.UserId)
                return;

            if (_game.WaitingForOpponent)
            {
                await ChooseOpponent(message, number);
                return;
            }

            if (_game.WaitingForBalloon)
            {
                await ChooseBalloon(message, number);
            }
        }

        private static async Task ChooseOpponent(
            dynamic message,
            int number)
        {
            if (_game == null)
                return;

            BalloonPlayer? current =
                _game.GetCurrentPlayer();

            if (current == null)
                return;

            int index = number - 1;

            if (index < 0 ||
                index >= _game.Players.Count)
            {
                await Send(
                    message.GroupId,
                    "❌ رقم اللاعب غير صحيح.");

                return;
            }

            BalloonPlayer opponent =
                _game.Players[index];

            if (opponent.Eliminated)
            {
                await Send(
                    message.GroupId,
                    "❌ هذا اللاعب خرج من اللعبة.");

                return;
            }

            if (opponent.UserId == current.UserId)
            {
                await Send(
                    message.GroupId,
                    "❌ لا يمكنك اختيار نفسك.");

                return;
            }

            _game.SelectedOpponent = opponent;
            _game.WaitingForOpponent = false;
            _game.WaitingForBalloon = true;

            string balloons =
                string.Join(
                    " ",
                    opponent.ActiveBalloons);

            await Send(
                message.GroupId,
                $"🎯 اخترت: {opponent.Name}\n\n" +
                $"🎈 بالونات {opponent.Name}:\n" +
                $"{balloons}\n\n" +
                "📌 أرسل رقم البالون فقط.");
        }

        private static async Task ChooseBalloon(
            dynamic message,
            int number)
        {
            if (_game == null)
                return;

            BalloonPlayer? current =
                _game.GetCurrentPlayer();

            BalloonPlayer? opponent =
                _game.SelectedOpponent;

            if (current == null ||
                opponent == null)
                return;

            if (!opponent.ActiveBalloons.Contains(number))
            {
                await Send(
                    message.GroupId,
                    "❌ رقم البالون غير صحيح.\n" +
                    "اختر من الأرقام الموجودة.");

                return;
            }

            opponent.ActiveBalloons.Remove(number);

            Random random = new Random();

            int effect = random.Next(1, 101);

            string result;

            if (effect <= 15)
            {
                opponent.ActiveBalloons.Add(number);

                result =
                    $"🍀 حظ!\n" +
                    $"{current.Name} اختار البالون رقم {number}.\n" +
                    "لكن البالون لم ينفجر!\n" +
                    "لا توجد خسارة 🎈";
            }
            else if (effect <= 30)
            {
                opponent.ActiveBalloons.Add(number);

                result =
                    $"🛡️ نجاة!\n" +
                    $"البالون رقم {number} بقي سليماً.\n" +
                    "لكن الدور ينتقل للاعب التالي.";
            }
            else if (effect <= 40)
            {
                result =
                    $"💥 انفجر البالون رقم {number}!\n" +
                    $"❌ {opponent.Name} خسر بالوناً.\n" +
                    "🔄 حصلت على دور إضافي!";
            }
            else
            {
                result =
                    $"💥 انفجر البالون رقم {number}!\n" +
                    $"❌ {opponent.Name} خسر بالوناً.";
            }

            opponent.Balloons =
                opponent.ActiveBalloons.Count;

            await Send(
                message.GroupId,
                result +
                $"\n🎈 المتبقي: {opponent.Balloons}");

            if (opponent.Balloons == 0)
            {
                opponent.Eliminated = true;

                await Send(
                    message.GroupId,
                    $"💀 {opponent.Name} خرج من اللعبة!");
            }

            BalloonPlayer? winner = CheckWinner();

            if (winner != null)
            {
                await Send(
                    message.GroupId,
                    "🏆🎉 انتهت اللعبة! 🎉🏆\n\n" +
                    $"👑 الفائز: {winner.Name}\n" +
                    $"🎈 لديه {winner.Balloons} بالونات!");

                _game = null;
                return;
            }

            bool extraTurn =
                effect > 30 &&
                effect <= 40 &&
                opponent.Balloons > 0;

            _game.WaitingForBalloon = false;
            _game.SelectedOpponent = null;

            if (!extraTurn)
                MoveToNextPlayer();

            await SendGameBoard(message.GroupId);
            await AskForOpponent(message.GroupId);
        }

        private static void MoveToNextPlayer()
        {
            if (_game == null)
                return;

            int total = _game.Players.Count;

            for (int i = 0; i < total; i++)
            {
                _game.CurrentPlayerIndex =
                    (_game.CurrentPlayerIndex + 1) % total;

                BalloonPlayer p =
                    _game.Players[
                        _game.CurrentPlayerIndex];

                if (!p.Eliminated &&
                    p.Balloons > 0)
                {
                    return;
                }
            }
        }

        private static BalloonPlayer? CheckWinner()
        {
            if (_game == null)
                return null;

            List<BalloonPlayer> alive =
                _game.Players
                    .Where(p =>
                        !p.Eliminated &&
                        p.Balloons > 0)
                    .ToList();

            return alive.Count == 1
                ? alive[0]
                : null;
        }

        private static async Task EndGame(dynamic message)
        {
            if (_game == null)
            {
                await Send(
                    message.GroupId,
                    "❌ لا توجد لعبة حالياً.");

                return;
            }

            if (_game.GroupId != message.GroupId)
                return;

            _game = null;

            await Send(
                message.GroupId,
                "🛑 تم إنهاء لعبة البالونات.");
        }

        private static async Task Send(
            string groupId,
            string text)
        {
            try
            {
                if (_client == null)
                    return;

                await _client.Messaging.SendMessage(
                    groupId,
                    text);
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Send Error:");
                Console.WriteLine(ex);
            }
        }
    }

    public class BalloonGame
    {
        public string GroupId { get; }

        public bool Started { get; set; }

        public bool WaitingForOpponent { get; set; }

        public bool WaitingForBalloon { get; set; }

        public int CurrentPlayerIndex { get; set; }

        public BalloonPlayer? SelectedOpponent { get; set; }

        public List<BalloonPlayer> Players { get; }

        public BalloonGame(string groupId)
        {
            GroupId = groupId;
            Started = false;
            WaitingForOpponent = false;
            WaitingForBalloon = false;
            CurrentPlayerIndex = 0;
            Players = new List<BalloonPlayer>();
        }

        public BalloonPlayer? GetCurrentPlayer()
        {
            if (Players.Count == 0)
                return null;

            if (CurrentPlayerIndex < 0 ||
                CurrentPlayerIndex >= Players.Count)
                return null;

            BalloonPlayer p =
                Players[CurrentPlayerIndex];

            if (p.Eliminated)
                return null;

            return p;
        }
    }

    public class BalloonPlayer
    {
        public string UserId { get; }

        public string Name { get; }

        public int Balloons { get; set; }

        public bool Eliminated { get; set; }

        public List<int> ActiveBalloons { get; }

        public BalloonPlayer(
            string userId,
            string name)
        {
            UserId = userId;
            Name = name;
            Balloons = 7;
            Eliminated = false;

            ActiveBalloons =
                Enumerable
                    .Range(1, 7)
                    .ToList();
        }
    }
}
