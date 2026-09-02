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

        private static readonly Random Random = new Random();

        public static async Task Main(string[] args)
        {
            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(
                    "❌ WOLF_EMAIL أو WOLF_PASSWORD غير موجود."
                );

                return;
            }

            Console.WriteLine("🎈 تشغيل BalloonBot...");

            _client = new WolfClient();

            _client.OnConnected += () =>
            {
                Console.WriteLine("✅ تم الاتصال بـ WOLF.");
            };

            _client.OnDisconnected += (ex) =>
            {
                Console.WriteLine("⚠️ انقطع الاتصال بـ WOLF.");
            };

            _client.OnConnectionError += (ex) =>
            {
                Console.WriteLine(
                    "❌ Connection Error: " + ex.Message
                );
            };

            _client.Messaging.OnMessage += async (client, message) =>
            {
                try
                {
                    string text =
                        message.Content?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(text))
                        return;

                    Console.WriteLine(
                        $"📩 Message: {text} | User: {message.UserId}"
                    );

                    // ==========================================
                    // الأرقام أثناء اللعبة
                    // ==========================================

                    if (TryParseNumber(text, out int number))
                    {
                        if (_game != null &&
                            _game.Started &&
                            (message.GroupId ?? "") == _game.GroupId)
                        {
                            await HandleNumber(
                                client,
                                message,
                                number
                            );
                        }

                        return;
                    }

                    // ==========================================
                    // أوامر البالونات
                    // ==========================================

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
                    Console.WriteLine(
                        "❌ COMMAND ERROR:"
                    );

                    Console.WriteLine(ex);
                }
            };

            bool loginResult =
                await _client.Login(
                    email,
                    password
                );

            if (!loginResult)
            {
                Console.WriteLine(
                    "❌ فشل تسجيل الدخول. تأكد من بيانات الحساب الثاني."
                );

                return;
            }

            Console.WriteLine(
                "✅ تم تسجيل الدخول بنجاح."
            );

            await _client.Connect();

            Console.WriteLine(
                "🎈 BalloonBot يعمل الآن..."
            );

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

            if (parts.Length == 0)
            {
                await SendHelp(client, message);
                return;
            }

            string action =
                parts[0].ToLowerInvariant();

            switch (action)
            {
                case "مساعدة":
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
                    await SendHelp(client, message);
                    break;
            }
        }

        // =========================================================
        // المساعدة
        // =========================================================

        private static async Task SendHelp(
            IWolfClient client,
            Message message)
        {
            string help =
                "🎈 لعبة البالونات 🎈\n\n" +
                "📌 الأوامر:\n\n" +
                "🎮 !بالونات جديد\n" +
                "إنشاء لعبة جديدة\n\n" +

                "👤 !بالونات انضم\n" +
                "الانضمام إلى اللعبة\n\n" +

                "👥 !بالونات لاعبين\n" +
                "عرض اللاعبين وعدد البالونات\n\n" +

                "🚀 !بالونات بدء\n" +
                "بدء اللعبة\n\n" +

                "🛑 !بالونات انهاء\n" +
                "إنهاء اللعبة\n\n" +

                "ℹ️ !بالونات مساعدة\n" +
                "عرض التعليمات\n\n" +

                "🎈 كل لاعب يبدأ بـ 7 بالونات.\n" +
                "🎯 اللاعب الحالي يختار رقم اللاعب المنافس.\n" +
                "🎈 بعدها يختار رقم البالون من 1 إلى 7.\n" +
                "💥 بعض البالونات تنفجر وبعضها لها تأثيرات عشوائية.\n" +
                "👑 آخر لاعب يبقى هو الفائز.";

            await Reply(
                client,
                message,
                help
            );
        }

        // =========================================================
        // لعبة جديدة
        // =========================================================

        private static async Task NewGame(
            IWolfClient client,
            Message message)
        {
            string groupId =
                message.GroupId ?? "";

            if (string.IsNullOrWhiteSpace(groupId))
            {
                await Reply(
                    client,
                    message,
                    "❌ لم أستطع تحديد الروم."
                );

                return;
            }

            if (_game != null)
            {
                await Reply(
                    client,
                    message,
                    "⚠️ توجد لعبة بالونات حالياً.\n" +
                    "استخدم !بالونات انهاء أولاً."
                );

                return;
            }

            _game =
                new BalloonGame(groupId);

            await Reply(
                client,
                message,
                "🎈🔥 تم إنشاء لعبة البالونات!\n\n" +
                "👥 اللعبة فردية، كل لاعب يلعب لنفسه.\n" +
                "🎈 كل لاعب يبدأ بـ 7 بالونات.\n\n" +
                "للانضمام اكتب:\n" +
                "!بالونات انضم"
            );
        }

        // =========================================================
        // انضمام
        // =========================================================

        private static async Task JoinGame(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                await Reply(
                    client,
                    message,
                    "❌ لا توجد لعبة حالياً.\n" +
                    "اكتب !بالونات جديد"
                );

                return;
            }

            if (_game.Started)
            {
                await Reply(
                    client,
                    message,
                    "❌ اللعبة بدأت بالفعل، لا يمكن الانضمام."
                );

                return;
            }

            string groupId =
                message.GroupId ?? "";

            if (groupId != _game.GroupId)
                return;

            string userId =
                message.UserId;

            if (string.IsNullOrWhiteSpace(userId))
                return;

            if (_game.GetPlayer(userId) != null)
            {
                await Reply(
                    client,
                    message,
                    "⚠️ أنت منضم بالفعل."
                );

                return;
            }

            string playerName =
                await GetNickname(
                    client,
                    userId
                );

            BalloonPlayer player =
                new BalloonPlayer(
                    userId,
                    playerName
                );

            _game.Players.Add(player);

            await Reply(
                client,
                message,
                "🎈✅ تم انضمامك إلى لعبة البالونات!\n\n" +
                $"👤 اللاعب: {player.Name}\n" +
                "🎈 بالوناتك: 7\n\n" +
                "استخدم !بالونات لاعبين لعرض القائمة."
            );
        }

        // =========================================================
        // عرض اللاعبين
        // =========================================================

        private static async Task ShowPlayers(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                await Reply(
                    client,
                    message,
                    "❌ لا توجد لعبة حالياً."
                );

                return;
            }

            if ((message.GroupId ?? "") != _game.GroupId)
                return;

            if (_game.Players.Count == 0)
            {
                await Reply(
                    client,
                    message,
                    "👥 لا يوجد لاعبون حتى الآن."
                );

                return;
            }

            string result =
                "🎈👥 لاعبو لعبة البالونات\n\n";

            for (int i = 0; i < _game.Players.Count; i++)
            {
                BalloonPlayer player =
                    _game.Players[i];

                string status =
                    player.Eliminated
                        ? " ❌ خرج"
                        : "";

                result +=
                    $"{i + 1}️⃣ {player.Name} — " +
                    $"{player.Balloons} 🎈{status}\n";
            }

            await Reply(
                client,
                message,
                result
            );
        }

        // =========================================================
        // بدء اللعبة
        // =========================================================

        private static async Task StartGame(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                await Reply(
                    client,
                    message,
                    "❌ لا توجد لعبة."
                );

                return;
            }

            if ((message.GroupId ?? "") != _game.GroupId)
                return;

            if (_game.Started)
            {
                await Reply(
                    client,
                    message,
                    "⚠️ اللعبة بدأت بالفعل."
                );

                return;
            }

            if (_game.Players.Count < 2)
            {
                await Reply(
                    client,
                    message,
                    "❌ يجب أن يكون هناك لاعبان على الأقل لبدء اللعبة."
                );

                return;
            }

            _game.Started = true;
            _game.CurrentPlayerIndex = 0;

            await Reply(
                client,
                message,
                "🎈🔥 بدأت لعبة البالونات!\n\n" +
                BuildPlayersBoard() +
                "\n\n" +
                $"🎯 الدور على: {_game.CurrentPlayer.Name}\n\n" +
                "اختر رقم اللاعب الذي تريد اللعب ضده."
            );
        }

        // =========================================================
        // التعامل مع الأرقام
        // =========================================================

        private static async Task HandleNumber(
            IWolfClient client,
            Message message,
            int number)
        {
            if (_game == null ||
                !_game.Started)
            {
                return;
            }

            if ((message.GroupId ?? "") != _game.GroupId)
                return;

            string userId =
                message.UserId;

            BalloonPlayer? current =
                _game.CurrentPlayer;

            if (current == null)
                return;

            if (current.UserId != userId)
            {
                await Reply(
                    client,
                    message,
                    $"⏳ مو دورك.\n" +
                    $"🎯 الدور حالياً على: {current.Name}"
                );

                return;
            }

            // =====================================================
            // اختيار المنافس
            // =====================================================

            if (_game.WaitingForOpponent)
            {
                await ChooseOpponent(
                    client,
                    message,
                    number
                );

                return;
            }

            // =====================================================
            // اختيار البالون
            // =====================================================

            if (_game.WaitingForBalloon)
            {
                await ChooseBalloon(
                    client,
                    message,
                    number
                );

                return;
            }
        }

        // =========================================================
        // اختيار المنافس
        // =========================================================

        private static async Task ChooseOpponent(
            IWolfClient client,
            Message message,
            int number)
        {
            if (_game == null)
                return;

            BalloonPlayer? current =
                _game.CurrentPlayer;

            if (current == null)
                return;

            if (number < 1 ||
                number > _game.Players.Count)
            {
                await Reply(
                    client,
                    message,
                    $"❌ رقم اللاعب يجب أن يكون من 1 إلى {_game.Players.Count}."
                );

                return;
            }

            BalloonPlayer opponent =
                _game.Players[number - 1];

            if (opponent.UserId == current.UserId)
            {
                await Reply(
                    client,
                    message,
                    "❌ لا يمكنك اختيار نفسك."
                );

                return;
            }

            if (opponent.Eliminated)
            {
                await Reply(
                    client,
                    message,
                    "❌ هذا اللاعب خرج من اللعبة."
                );

                return;
            }

            if (opponent.Balloons <= 0)
            {
                await Reply(
                    client,
                    message,
                    "❌ هذا اللاعب لا يملك بالونات."
                );

                return;
            }

            _game.SelectedOpponentId =
                opponent.UserId;

            _game.WaitingForOpponent = false;
            _game.WaitingForBalloon = true;

            string balloons =
                string.Join(
                    " ",
                    opponent.ActiveBalloons
                        .OrderBy(x => x)
                        .Select(x => x.ToString())
                );

            await Reply(
                client,
                message,
                $"🎯 اخترت: {opponent.Name}\n\n" +
                $"🎈 بالونات {opponent.Name}:\n" +
                $"{balloons}\n\n" +
                "💥 اختر رقم البالون:"
            );
        }

        // =========================================================
        // اختيار البالون
        // =========================================================

        private static async Task ChooseBalloon(
            IWolfClient client,
            Message message,
            int number)
        {
            if (_game == null)
                return;

            BalloonPlayer? current =
                _game.CurrentPlayer;

            if (current == null)
                return;

            BalloonPlayer? opponent =
                _game.GetPlayer(
                    _game.SelectedOpponentId
                );

            if (opponent == null)
            {
                ResetTurnState();

                await Reply(
                    client,
                    message,
                    "❌ حدث خطأ في اختيار اللاعب."
                );

                return;
            }

            if (!opponent.ActiveBalloons.Contains(number))
            {
                string available =
                    string.Join(
                        " ",
                        opponent.ActiveBalloons
                            .OrderBy(x => x)
                    );

                await Reply(
                    client,
                    message,
                    $"❌ هذا الرقم غير موجود.\n\n" +
                    $"🎈 البالونات المتاحة:\n{available}"
                );

                return;
            }

            // إزالة البالون مؤقتاً
            opponent.ActiveBalloons.Remove(number);

            int effect =
                Random.Next(1, 101);

            string result;

            bool extraTurn = false;

            // =====================================================
            // 🍀 حظ
            // =====================================================

            if (effect <= 15)
            {
                opponent.ActiveBalloons.Add(number);

                result =
                    "🍀✨ حظك اليوم!\n\n" +
                    $"🎈 البالون رقم {number} ما انفجر!\n" +
                    $"😎 {opponent.Name} نجا من الخطر!";
            }

            // =====================================================
            // 🛡️ نجاة
            // =====================================================

            else if (effect <= 30)
            {
                opponent.ActiveBalloons.Add(number);

                result =
                    "🛡️🎈 نجاة!\n\n" +
                    $"البالون رقم {number} بقي موجوداً.\n" +
                    $"لكن الدور ينتقل للاعب التالي.";
            }

            // =====================================================
            // 🔄 دور إضافي
            // =====================================================

            else if (effect <= 40)
            {
                result =
                    "💥🎈 بوم!\n\n" +
                    $"انفجر البالون رقم {number} من {opponent.Name}.\n" +
                    $"🔄 حصلت على دور إضافي!";

                extraTurn = true;
            }

            // =====================================================
            // 💥 انفجار طبيعي
            // =====================================================

            else
            {
                result =
                    "💥🎈 بــــوم!\n\n" +
                    $"انفجر البالون رقم {number}!\n" +
                    $"😈 {opponent.Name} خسر بالوناً.";
            }

            opponent.Balloons =
                opponent.ActiveBalloons.Count;

            // =====================================================
            // خروج اللاعب
            // =====================================================

            if (opponent.Balloons <= 0)
            {
                opponent.Eliminated = true;

                result +=
                    $"\n\n💀 {opponent.Name} فقد جميع بالوناته!\n" +
                    "❌ خرج من اللعبة.";
            }

            // =====================================================
            // فوز
            // =====================================================

            int aliveCount =
                _game.Players.Count(
                    x => !x.Eliminated
                );

            if (aliveCount <= 1)
            {
                BalloonPlayer? winner =
                    _game.Players.FirstOrDefault(
                        x => !x.Eliminated
                    );

                _game.Started = false;

                result +=
                    "\n\n🏆🎉 انتهت اللعبة!\n\n" +
                    $"👑 الفائز: {winner?.Name ?? "غير معروف"}\n" +
                    "🎈🎈🎈 مبروك!";

                ResetTurnState();

                await Reply(
                    client,
                    message,
                    result
                );

                _game = null;

                return;
            }

            // =====================================================
            // الدور الإضافي
            // =====================================================

            if (extraTurn)
            {
                ResetTurnState();

                await Reply(
                    client,
                    message,
                    result +
                    "\n\n" +
                    BuildPlayersBoard() +
                    "\n\n" +
                    $"🔄 دور إضافي لـ {current.Name}\n" +
                    "🎯 اختر لاعباً."
                );

                return;
            }

            // =====================================================
            // الدور التالي
            // =====================================================

            _game.MoveToNextPlayer();

            ResetTurnState();

            BalloonPlayer? next =
                _game.CurrentPlayer;

            await Reply(
                client,
                message,
                result +
                "\n\n" +
                BuildPlayersBoard() +
                "\n\n" +
                $"🎯 الدور الآن على: {next?.Name}\n" +
                "اختر رقم اللاعب."
            );
        }

        // =========================================================
        // لوحة اللاعبين
        // =========================================================

        private static string BuildPlayersBoard()
        {
            if (_game == null)
                return "";

            string result =
                "🎈👥 اللاعبين:\n\n";

            for (int i = 0; i < _game.Players.Count; i++)
            {
                BalloonPlayer player =
                    _game.Players[i];

                string status =
                    player.Eliminated
                        ? " ❌"
                        : "";

                result +=
                    $"{i + 1}️⃣ {player.Name} — " +
                    $"{player.Balloons} 🎈{status}\n";
            }

            return result.TrimEnd();
        }

        // =========================================================
        // إنهاء اللعبة
        // =========================================================

        private static async Task EndGame(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                await Reply(
                    client,
                    message,
                    "❌ لا توجد لعبة حالياً."
                );

                return;
            }

            if ((message.GroupId ?? "") != _game.GroupId)
                return;

            string result =
                "🛑 تم إنهاء لعبة البالونات.\n\n" +
                BuildPlayersBoard();

            _game = null;

            await Reply(
                client,
                message,
                result
            );
        }

        // =========================================================
        // الحصول على اسم اللاعب
        // =========================================================

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
            catch
            {
                // إذا تعذر جلب الاسم نستخدم ID
            }

            return userId;
        }

        // =========================================================
        // إرسال الرد
        // =========================================================

        private static async Task Reply(
            IWolfClient client,
            Message message,
            string text)
        {
            try
            {
                await client.Reply(
                    message,
                    text
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "❌ Reply Error:"
                );

                Console.WriteLine(ex);
            }
        }

        // =========================================================
        // قراءة الرقم
        // =========================================================

        private static bool TryParseNumber(
            string text,
            out int number)
        {
            number = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return int.TryParse(
                text.Trim(),
                out number
            );
        }

        // =========================================================
        // تصفير حالة الاختيار
        // =========================================================

        private static void ResetTurnState()
        {
            if (_game == null)
                return;

            _game.WaitingForOpponent = true;
            _game.WaitingForBalloon = false;
            _game.SelectedOpponentId = "";
        }
    }

    // =================================================================
    // BalloonGame
    // =================================================================

    public class BalloonGame
    {
        public string GroupId { get; }

        public List<BalloonPlayer> Players { get; }

        public bool Started { get; set; }

        public int CurrentPlayerIndex { get; set; }

        public bool WaitingForOpponent { get; set; }

        public bool WaitingForBalloon { get; set; }

        public string SelectedOpponentId { get; set; }

        public BalloonGame(string groupId)
        {
            GroupId = groupId;

            Players =
                new List<BalloonPlayer>();

            Started = false;

            CurrentPlayerIndex = 0;

            WaitingForOpponent = false;

            WaitingForBalloon = false;

            SelectedOpponentId = "";
        }

        public BalloonPlayer? CurrentPlayer
        {
            get
            {
                if (Players.Count == 0)
                    return null;

                if (CurrentPlayerIndex < 0)
                    CurrentPlayerIndex = 0;

                if (CurrentPlayerIndex >= Players.Count)
                    CurrentPlayerIndex = 0;

                // البحث عن اللاعب التالي غير الخارج
                for (int i = 0; i < Players.Count; i++)
                {
                    int index =
                        (CurrentPlayerIndex + i)
                        % Players.Count;

                    BalloonPlayer player =
                        Players[index];

                    if (!player.Eliminated)
                    {
                        CurrentPlayerIndex =
                            index;

                        return player;
                    }
                }

                return null;
            }
        }

        public BalloonPlayer? GetPlayer(
            string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            return Players.FirstOrDefault(
                x => x.UserId == userId
            );
        }

        public void MoveToNextPlayer()
        {
            if (Players.Count == 0)
                return;

            int start =
                CurrentPlayerIndex;

            for (int i = 1; i <= Players.Count; i++)
            {
                int index =
                    (start + i)
                    % Players.Count;

                BalloonPlayer player =
                    Players[index];

                if (!player.Eliminated)
                {
                    CurrentPlayerIndex =
                        index;

                    return;
                }
            }
        }
    }

    // =================================================================
    // BalloonPlayer
    // =================================================================

    public class BalloonPlayer
    {
        public string UserId { get; }

        public string Name { get; set; }

        public List<int> ActiveBalloons { get; }

        public bool Eliminated { get; set; }

        public int Balloons
        {
            get
            {
                return ActiveBalloons.Count;
            }

            set
            {
                // القيمة يتم احتسابها من القائمة
            }
        }

        public BalloonPlayer(
            string userId,
            string name)
        {
            UserId = userId;

            Name =
                string.IsNullOrWhiteSpace(name)
                    ? userId
                    : name;

            ActiveBalloons =
                Enumerable
                    .Range(1, 7)
                    .ToList();

            Eliminated = false;
        }
    }
}
