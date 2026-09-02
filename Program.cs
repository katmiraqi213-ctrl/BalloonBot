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
            Console.WriteLine("🔐 جاري تسجيل الدخول إلى Wolf...");

            _client = new WolfClient();

            // =====================================================
            // تسجيل الدخول - نفس طريقة MazajBot العامل
            // =====================================================

            bool loginResult =
                await _client.Login(email, password);

            if (!loginResult)
            {
                Console.WriteLine(
                    "❌ فشل تسجيل الدخول إلى Wolf. تأكد من WOLF_EMAIL و WOLF_PASSWORD."
                );

                return;
            }

            Console.WriteLine(
                "✅ تم تسجيل الدخول بنجاح."
            );

            // =====================================================
            // استقبال الرسائل
            // مهم: بعد Login وقبل Connect
            // =====================================================

            _client.Messaging.OnMessage += async (client, message) =>
            {
                try
                {
                    Console.WriteLine(
                        $"📩 MESSAGE: {message.Content}"
                    );

                    // منع تكرار نفس الرسالة
                    if (!string.IsNullOrWhiteSpace(message.MessageId))
                    {
                        lock (_messageLock)
                        {
                            if (!_processedMessages.Add(message.MessageId))
                            {
                                return;
                            }

                            if (_processedMessages.Count > 5000)
                            {
                                _processedMessages.Clear();
                            }
                        }
                    }

                    string text =
                        message.Content?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return;
                    }

                    // =================================================
                    // إذا كانت الرسالة رقم
                    // =================================================

                    if (int.TryParse(text, out int number))
                    {
                        if (_game != null &&
                            _game.Started &&
                            _game.GroupId == (message.GroupId ?? ""))
                        {
                            await HandleNumber(
                                client,
                                message,
                                number
                            );
                        }

                        return;
                    }

                    // =================================================
                    // أوامر بالونات
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
                    Console.WriteLine(
                        "❌ MESSAGE ERROR:"
                    );

                    Console.WriteLine(
                        ex.ToString()
                    );
                }
            };

            // =====================================================
            // الاتصال
            // =====================================================

            await _client.Connect();

            Console.WriteLine(
                "🟢 BalloonBot يعمل الآن."
            );

            Console.WriteLine(
                "📡 البوت ينتظر أوامر WOLF..."
            );

            // إبقاء البرنامج يعمل
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
                await SendHelp(
                    client,
                    message
                );

                return;
            }

            string action =
                parts[0].ToLowerInvariant();

            switch (action)
            {
                case "جديد":
                    await NewGame(
                        client,
                        message
                    );
                    break;

                case "انضم":
                case "انضمام":
                    await JoinGame(
                        client,
                        message
                    );
                    break;

                case "لاعبين":
                    await ShowPlayers(
                        client,
                        message
                    );
                    break;

                case "بدء":
                    await StartGame(
                        client,
                        message
                    );
                    break;

                case "انهاء":
                case "إنهاء":
                    await EndGame(
                        client,
                        message
                    );
                    break;

                case "مساعدة":
                case "help":
                    await SendHelp(
                        client,
                        message
                    );
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

        // =========================================================
        // المساعدة
        // =========================================================

        private static async Task SendHelp(
            IWolfClient client,
            Message message)
        {
            string help =
                "🎈🔥 أوامر لعبة البالونات 🔥🎈\n\n" +

                "🎮 !بالونات جديد\n" +
                "إنشاء لعبة جديدة\n\n" +

                "👥 !بالونات انضم\n" +
                "الانضمام إلى اللعبة\n\n" +

                "👥 !بالونات لاعبين\n" +
                "عرض اللاعبين وعدد بالوناتهم\n\n" +

                "▶️ !بالونات بدء\n" +
                "بدء اللعبة\n\n" +

                "🛑 !بالونات انهاء\n" +
                "إنهاء اللعبة\n\n" +

                "🎯 طريقة اللعب:\n" +
                "1️⃣ اللاعب يختار رقم الخصم\n" +
                "2️⃣ بعدها يختار رقم البالون\n" +
                "3️⃣ كل لاعب يبدأ بـ 7 🎈\n\n" +

                "🎲 النتائج:\n" +
                "💥 60% تنفجر البالونة\n" +
                "🍀 15% حظ - لا تنفجر\n" +
                "🛡️ 15% نجاة - لا تنفجر\n" +
                "🔄 10% دور إضافي\n\n" +

                "🏆 آخر لاعب تبقى عنده بالونات هو الفائز.";

            await client.Reply(
                message,
                help
            );
        }

        // =========================================================
        // إنشاء اللعبة
        // =========================================================

        private static async Task NewGame(
            IWolfClient client,
            Message message)
        {
            if (_game != null)
            {
                await client.Reply(
                    message,
                    "⚠️ توجد لعبة بالونات حالياً.\n" +
                    "استخدم !بالونات انهاء أولاً."
                );

                return;
            }

            string groupId =
                message.GroupId ?? "";

            if (string.IsNullOrWhiteSpace(groupId))
            {
                await client.Reply(
                    message,
                    "❌ يجب تشغيل اللعبة داخل روم."
                );

                return;
            }

            _game = new BalloonGame
            {
                GroupId = groupId,
                Started = false
            };

            await client.Reply(
                message,

                "🎈🔥 تم إنشاء لعبة البالونات! 🔥🎈\n\n" +

                "🎈 كل لاعب يبدأ بـ 7 بالونات.\n" +
                "👥 اللعبة فردية بدون فرق.\n\n" +

                "📌 للانضمام:\n" +
                "!بالونات انضم\n\n" +

                "📌 لمعرفة اللاعبين:\n" +
                "!بالونات لاعبين\n\n" +

                "📌 بعد اكتمال اللاعبين:\n" +
                "!بالونات بدء"
            );
        }

        // =========================================================
        // الانضمام
        // =========================================================

        private static async Task JoinGame(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة حالياً.\n" +
                    "اكتب !بالونات جديد"
                );

                return;
            }

            if (_game.Started)
            {
                await client.Reply(
                    message,
                    "❌ اللعبة بدأت بالفعل."
                );

                return;
            }

            if (_game.GroupId !=
                (message.GroupId ?? ""))
            {
                return;
            }

            string userId =
                message.UserId;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            if (_game.Players.Any(
                p => p.UserId == userId))
            {
                await client.Reply(
                    message,
                    "⚠️ أنت منضم مسبقاً إلى اللعبة."
                );

                return;
            }

            string nickname =
                await GetNickname(
                    client,
                    userId
                );

            BalloonPlayer player =
                new BalloonPlayer(
                    userId,
                    nickname
                );

            _game.Players.Add(
                player
            );

            await client.Reply(
                message,

                $"✅ تم انضمامك إلى لعبة البالونات!\n\n" +
                $"👤 اللاعب: {nickname}\n" +
                $"🎈 البالونات: 7"
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
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة حالياً."
                );

                return;
            }

            if (_game.GroupId !=
                (message.GroupId ?? ""))
            {
                return;
            }

            if (_game.Players.Count == 0)
            {
                await client.Reply(
                    message,
                    "👥 لا يوجد لاعبين حالياً.\n\n" +
                    "اكتب !بالونات انضم"
                );

                return;
            }

            string result =
                "🎈👥 لاعبو لعبة البالونات\n\n";

            int index = 1;

            foreach (BalloonPlayer player in
                     _game.Players.Where(
                         p => !p.Eliminated))
            {
                result +=
                    $"{GetNumberEmoji(index)} " +
                    $"{player.Nickname} — " +
                    $"{player.Balloons} 🎈\n";

                index++;
            }

            if (index == 1)
            {
                result +=
                    "❌ لا يوجد لاعبين أحياء.";
            }

            await client.Reply(
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
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة حالياً."
                );

                return;
            }

            if (_game.GroupId !=
                (message.GroupId ?? ""))
            {
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
                    "❌ لازم يكون هناك لاعبين على الأقل لبدء اللعبة."
                );

                return;
            }

            foreach (BalloonPlayer player
                     in _game.Players)
            {
                player.Balloons = 7;
                player.Eliminated = false;
            }

            _game.Started = true;
            _game.CurrentPlayerIndex = 0;
            _game.State =
                BalloonGameState.SelectOpponent;

            await SendGameBoard(
                client,
                message
            );
        }

        // =========================================================
        // معالجة الأرقام
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

            if (_game.GroupId !=
                (message.GroupId ?? ""))
            {
                return;
            }

            BalloonPlayer? currentPlayer =
                GetCurrentPlayer();

            if (currentPlayer == null)
            {
                return;
            }

            if (currentPlayer.UserId !=
                message.UserId)
            {
                await client.Reply(
                    message,
                    $"⏳ حالياً دور اللاعب: {currentPlayer.Nickname}"
                );

                return;
            }

            // =====================================================
            // اختيار الخصم
            // =====================================================

            if (_game.State ==
                BalloonGameState.SelectOpponent)
            {
                await SelectOpponent(
                    client,
                    message,
                    number
                );

                return;
            }

            // =====================================================
            // اختيار البالونة
            // =====================================================

            if (_game.State ==
                BalloonGameState.SelectBalloon)
            {
                await SelectBalloon(
                    client,
                    message,
                    number
                );
            }
        }

        // =========================================================
        // اختيار الخصم
        // =========================================================

        private static async Task SelectOpponent(
            IWolfClient client,
            Message message,
            int number)
        {
            if (_game == null)
            {
                return;
            }

            List<BalloonPlayer> alivePlayers =
                _game.Players
                    .Where(p => !p.Eliminated)
                    .ToList();

            if (number < 1 ||
                number > alivePlayers.Count)
            {
                await client.Reply(
                    message,
                    $"❌ اختر رقم لاعب من 1 إلى {alivePlayers.Count}."
                );

                return;
            }

            BalloonPlayer current =
                GetCurrentPlayer()!;

            BalloonPlayer opponent =
                alivePlayers[number - 1];

            if (opponent.UserId ==
                current.UserId)
            {
                await client.Reply(
                    message,
                    "❌ ما تقدر تختار نفسك.\n" +
                    "اختار لاعب ثاني."
                );

                return;
            }

            if (opponent.Eliminated ||
                opponent.Balloons <= 0)
            {
                await client.Reply(
                    message,
                    "❌ هذا اللاعب خرج من اللعبة."
                );

                return;
            }

            _game.SelectedOpponentId =
                opponent.UserId;

            _game.State =
                BalloonGameState.SelectBalloon;

            await client.Reply(
                message,

                $"🎯 اخترت اللاعب: {opponent.Nickname}\n\n" +

                $"🎈 عنده {opponent.Balloons} بالونات.\n\n" +

                $"اختر رقم البالونة من 1 إلى {opponent.Balloons}"
            );
        }

        // =========================================================
        // اختيار البالونة
        // =========================================================

        private static async Task SelectBalloon(
            IWolfClient client,
            Message message,
            int number)
        {
            if (_game == null)
            {
                return;
            }

            BalloonPlayer? current =
                GetCurrentPlayer();

            if (current == null)
            {
                return;
            }

            BalloonPlayer? opponent =
                _game.Players.FirstOrDefault(
                    p => p.UserId ==
                         _game.SelectedOpponentId
                );

            if (opponent == null ||
                opponent.Eliminated ||
                opponent.Balloons <= 0)
            {
                _game.State =
                    BalloonGameState.SelectOpponent;

                await client.Reply(
                    message,
                    "❌ هذا اللاعب لم يعد متاحاً.\n" +
                    "اختار لاعباً آخر."
                );

                return;
            }

            if (number < 1 ||
                number > opponent.Balloons)
            {
                await client.Reply(
                    message,
                    $"❌ اختر رقم بالونة من 1 إلى {opponent.Balloons}."
                );

                return;
            }

            int balloonNumber =
                number;

            // =====================================================
            // اختيار النتيجة العشوائية
            //
            // 0 - 60% انفجار
            // 1 - 15% حظ
            // 2 - 15% نجاة
            // 3 - 10% دور إضافي
            // =====================================================

            double roll =
                Random.Shared.NextDouble();

            string result;

            bool extraTurn = false;
            bool popped = false;

            if (roll < 0.60)
            {
                // 60% انفجار طبيعي
                opponent.Balloons--;
                popped = true;

                result =
                    "💥🎈 طاااااخ!\n\n" +
                    $"👤 {current.Nickname}\n" +
                    $"🎯 استهدف {opponent.Nickname}\n\n" +
                    $"🎈 البالونة رقم {balloonNumber} انفجرت!\n\n" +
                    $"💥 انفجرت بالونة واحدة.";
            }
            else if (roll < 0.75)
            {
                // 15% حظ
                result =
                    "🍀🎈 حظ قوي!\n\n" +
                    $"👤 {current.Nickname}\n" +
                    $"🎯 استهدف {opponent.Nickname}\n\n" +
                    $"🎈 البالونة رقم {balloonNumber} ما انفجرت!\n" +
                    "🍀 حظك أنقذها.";
            }
            else if (roll < 0.90)
            {
                // 15% نجاة
                result =
                    "🛡️🎈 نجت البالونة!\n\n" +
                    $"👤 {current.Nickname}\n" +
                    $"🎯 استهدف {opponent.Nickname}\n\n" +
                    $"🎈 البالونة رقم {balloonNumber} بقيت.\n" +
                    "🛡️ نجاة!";
            }
            else
            {
                // 10% دور إضافي
                opponent.Balloons--;
                popped = true;
                extraTurn = true;

                result =
                    "🔄🎈 دور إضافي!\n\n" +
                    $"👤 {current.Nickname}\n" +
                    $"🎯 استهدف {opponent.Nickname}\n\n" +
                    $"💥 البالونة رقم {balloonNumber} انفجرت!\n\n" +
                    "🔥 عندك دور إضافي!";
            }

            await client.Reply(
                message,
                result
            );

            // =====================================================
            // فحص خروج الخصم
            // =====================================================

            if (popped &&
                opponent.Balloons <= 0)
            {
                opponent.Balloons = 0;
                opponent.Eliminated = true;

                await client.Reply(
                    message,

                    $"💀🎈 انتهت بالونات {opponent.Nickname}!\n\n" +
                    $"❌ تم استبعاده من اللعبة."
                );

                List<BalloonPlayer> remaining =
                    _game.Players
                        .Where(p => !p.Eliminated)
                        .ToList();

                if (remaining.Count == 1)
                {
                    BalloonPlayer winner =
                        remaining[0];

                    _game.Started = false;

                    await client.Reply(
                        message,

                        "🏆🎈 انتهت لعبة البالونات! 🎈🏆\n\n" +
                        $"👑 الفائز: {winner.Nickname}\n" +
                        $"🎈 البالونات المتبقية: {winner.Balloons}"
                    );

                    _game = null;
                    return;
                }
            }

            // =====================================================
            // دور إضافي
            // =====================================================

            if (extraTurn &&
                !current.Eliminated)
            {
                _game.State =
                    BalloonGameState.SelectOpponent;

                _game.SelectedOpponentId =
                    "";

                await SendGameBoard(
                    client,
                    message
                );

                return;
            }

            // =====================================================
            // الانتقال للاعب التالي
            // =====================================================

            MoveToNextPlayer();

            if (_game == null ||
                !_game.Started)
            {
                return;
            }

            _game.State =
                BalloonGameState.SelectOpponent;

            _game.SelectedOpponentId =
                "";

            await SendGameBoard(
                client,
                message
            );
        }

        // =========================================================
        // الانتقال للاعب التالي
        // =========================================================

        private static void MoveToNextPlayer()
        {
            if (_game == null)
            {
                return;
            }

            List<BalloonPlayer> alivePlayers =
                _game.Players
                    .Where(p => !p.Eliminated)
                    .ToList();

            if (alivePlayers.Count == 0)
            {
                return;
            }

            BalloonPlayer? current =
                GetCurrentPlayer();

            int currentPosition =
                current == null
                    ? -1
                    : alivePlayers.FindIndex(
                        p => p.UserId == current.UserId
                    );

            if (currentPosition < 0)
            {
                _game.CurrentPlayerIndex = 0;
                return;
            }

            int nextPosition =
                currentPosition + 1;

            if (nextPosition >=
                alivePlayers.Count)
            {
                nextPosition = 0;
            }

            BalloonPlayer nextPlayer =
                alivePlayers[nextPosition];

            int originalIndex =
                _game.Players.FindIndex(
                    p => p.UserId ==
                         nextPlayer.UserId
                );

            _game.CurrentPlayerIndex =
                originalIndex;
        }

        // =========================================================
        // لوحة اللعبة
        // =========================================================

        private static async Task SendGameBoard(
            IWolfClient client,
            Message message)
        {
            if (_game == null)
            {
                return;
            }

            BalloonPlayer? current =
                GetCurrentPlayer();

            if (current == null)
            {
                return;
            }

            string result =
                "🎈🔥 لعبة البالونات 🔥🎈\n\n" +

                "👥 اللاعبين:\n";

            int index = 1;

            foreach (BalloonPlayer player in
                     _game.Players
                         .Where(p => !p.Eliminated))
            {
                result +=
                    $"{GetNumberEmoji(index)} " +
                    $"{player.Nickname} — " +
                    $"{player.Balloons} 🎈\n";

                index++;
            }

            result +=
                "\n━━━━━━━━━━━━━━\n\n" +

                $"🎯 الدور على: {current.Nickname}\n\n" +

                "👆 أرسل رقم اللاعب الذي تريد استهدافه.\n" +
                "مثال: 2";

            await client.Reply(
                message,
                result
            );
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
                await client.Reply(
                    message,
                    "❌ لا توجد لعبة حالياً."
                );

                return;
            }

            if (_game.GroupId !=
                (message.GroupId ?? ""))
            {
                return;
            }

            _game.Started = false;

            string result =
                "🛑🎈 تم إنهاء لعبة البالونات.\n\n";

            List<BalloonPlayer> ranking =
                _game.Players
                    .OrderByDescending(
                        p => p.Balloons
                    )
                    .ToList();

            if (ranking.Count > 0)
            {
                result +=
                    "🏆 النتائج:\n\n";

                int place = 1;

                foreach (BalloonPlayer player
                         in ranking)
                {
                    result +=
                        $"{GetNumberEmoji(place)} " +
                        $"{player.Nickname} — " +
                        $"{player.Balloons} 🎈";

                    if (player.Eliminated)
                    {
                        result +=
                            " 💀";
                    }

                    result += "\n";

                    place++;
                }
            }

            await client.Reply(
                message,
                result
            );

            _game = null;
        }

        // =========================================================
        // اللاعب الحالي
        // =========================================================

        private static BalloonPlayer? GetCurrentPlayer()
        {
            if (_game == null)
            {
                return null;
            }

            List<BalloonPlayer> alivePlayers =
                _game.Players
                    .Where(p => !p.Eliminated)
                    .ToList();

            if (alivePlayers.Count == 0)
            {
                return null;
            }

            if (_game.CurrentPlayerIndex < 0 ||
                _game.CurrentPlayerIndex >=
                _game.Players.Count)
            {
                _game.CurrentPlayerIndex = 0;
            }

            BalloonPlayer candidate =
                _game.Players[
                    _game.CurrentPlayerIndex
                ];

            if (!candidate.Eliminated)
            {
                return candidate;
            }

            int index =
                _game.Players.FindIndex(
                    p => !p.Eliminated
                );

            if (index < 0)
            {
                return null;
            }

            _game.CurrentPlayerIndex =
                index;

            return _game.Players[index];
        }

        // =========================================================
        // اسم اللاعب
        // =========================================================

        private static async Task<string> GetNickname(
            IWolfClient client,
            string userId)
        {
            try
            {
                var user =
                    await client.GetUser(
                        userId
                    );

                if (user != null &&
                    !string.IsNullOrWhiteSpace(
                        user.Nickname))
                {
                    return user.Nickname;
                }
            }
            catch
            {
                // استخدام UserId عند فشل جلب الاسم
            }

            return userId;
        }

        // =========================================================
        // أرقام اللاعبين
        // =========================================================

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

    // =============================================================
    // حالة اللعبة
    // =============================================================

    public enum BalloonGameState
    {
        SelectOpponent,
        SelectBalloon
    }

    // =============================================================
    // اللاعب
    // =============================================================

    public class BalloonPlayer
    {
        public string UserId { get; }

        public string Nickname { get; }

        public int Balloons { get; set; } = 7;

        public bool Eliminated { get; set; }

        public BalloonPlayer(
            string userId,
            string nickname)
        {
            UserId = userId;
            Nickname = nickname;
        }
    }

    // =============================================================
    // لعبة البالونات
    // =============================================================

    public class BalloonGame
    {
        public string GroupId { get; set; } = "";

        public bool Started { get; set; }

        public List<BalloonPlayer> Players { get; } =
            new();

        public int CurrentPlayerIndex { get; set; }

        public BalloonGameState State { get; set; } =
            BalloonGameState.SelectOpponent;

        public string SelectedOpponentId { get; set; } = "";
    }
}
