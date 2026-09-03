using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using WolfLive.Api;
using WolfLive.Api.Models;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace PenaltyBot
{
    // ============================================================
    // PLAYER
    // ============================================================

    public class PenaltyPlayer
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Number { get; set; }

        public int Shots { get; set; }
        public int Goals { get; set; }

        public bool Eliminated { get; set; }
    }

    // ============================================================
    // GAME
    // ============================================================

    public class PenaltyGame
    {
        public string GroupId { get; set; } = "";

        public List<PenaltyPlayer> Players { get; } =
            new List<PenaltyPlayer>();

        public int CurrentPlayerIndex { get; set; }

        public bool Started { get; set; }

        public bool TurnAnswered { get; set; }

        public long TurnId { get; set; }

        public CancellationTokenSource? TurnCancellation { get; set; }
    }

    // ============================================================
    // PROGRAM
    // ============================================================

    public static class Program
    {
        private static IWolfClient? _client;

        private const int MaxPlayers = 10;
        private const int MinPlayers = 2;

        private const int ShotsPerPlayer = 5;

        // وقت التسديدة
        private const int TurnSeconds = 25;

        private static readonly ConcurrentDictionary<
            string,
            PenaltyGame> Games =
            new();

        private static long _turnCounter = 0;

        // ============================================================
        // MAIN
        // ============================================================

        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("================================");
            Console.WriteLine("      PENALTY BOT STARTING");
            Console.WriteLine("================================");

            await ConnectBot();

            await Task.Delay(Timeout.Infinite);
        }

        // ============================================================
        // CONNECT
        // ============================================================

        private static async Task ConnectBot()
        {
            string email =
                Environment.GetEnvironmentVariable(
                    "WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable(
                    "WOLF_PASSWORD") ?? "";

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(
                    "ERROR: WOLF_EMAIL or WOLF_PASSWORD is missing.");

                return;
            }

            try
            {
                Console.WriteLine("Creating Wolf client...");

                _client = new WolfClient();

                // ====================================================
                // أهم جزء:
                // ربط الرسائل بالطريقة الرسمية للمكتبة
                // ====================================================

                _client.Messaging.OnMessage +=
                    OnWolfMessage;

                _client.OnConnected +=
                    (_) =>
                    {
                        Console.WriteLine(
                            "================================");

                        Console.WriteLine(
                            "CONNECTED TO WOLF.LIVE");

                        Console.WriteLine(
                            "MESSAGE LISTENER READY");

                        Console.WriteLine(
                            "================================");
                    };

                Console.WriteLine("Logging in...");

                bool result =
                    await _client.Login(
                        email,
                        password);

                Console.WriteLine(
                    "Login result: " +
                    result);

                if (!result)
                {
                    Console.WriteLine(
                        "LOGIN FAILED");

                    return;
                }

                Console.WriteLine(
                    "BOT IS ONLINE.");

                Console.WriteLine(
                    "Waiting for Wolf messages...");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "CONNECT ERROR:");

                Console.WriteLine(
                    ex.ToString());
            }
        }

        // ============================================================
        // MESSAGE RECEIVED
        // ============================================================

        private static void OnWolfMessage(
            IWolfClient client,
            Message message)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await ProcessMessage(
                            message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            "MESSAGE PROCESS ERROR:");

                        Console.WriteLine(
                            ex.ToString());
                    }
                });
        }

        // ============================================================
        // PROCESS MESSAGE
        // ============================================================

        private static async Task ProcessMessage(
            Message message)
        {
            if (message == null)
                return;

            Console.WriteLine(
                "--------------------------------");

            Console.WriteLine(
                "MESSAGE RECEIVED");

            Console.WriteLine(
                "GroupId: " +
                (message.GroupId ?? ""));

            Console.WriteLine(
                "UserId: " +
                (message.UserId ?? ""));

            Console.WriteLine(
                "IsGroup: " +
                message.IsGroup);

            Console.WriteLine(
                "Content: " +
                (message.Content ?? ""));

            Console.WriteLine(
                "--------------------------------");

            if (!message.IsGroup)
                return;

            string groupId =
                message.GroupId ?? "";

            string userId =
                message.UserId ?? "";

            string text =
                message.Content ?? "";

            if (string.IsNullOrWhiteSpace(groupId))
                return;

            if (string.IsNullOrWhiteSpace(userId))
                return;

            text = text.Trim();

            if (text.Length == 0)
                return;

            string normalized =
                NormalizeText(text);

            // ========================================================
            // الأرقام 1 2 3
            // ========================================================

            if (normalized == "1" ||
                normalized == "2" ||
                normalized == "3")
            {
                await ProcessShot(
                    groupId,
                    userId,
                    normalized);

                return;
            }

            // ========================================================
            // COMMANDS
            // ========================================================

            if (normalized == "!جزاء")
            {
                await SendHelp(groupId);
                return;
            }

            if (normalized == "!جزاء مساعدة")
            {
                await SendHelp(groupId);
                return;
            }

            if (normalized == "!جزاء انضم")
            {
                string name =
                    await GetPlayerName(
                        groupId,
                        userId);

                await JoinGame(
                    groupId,
                    userId,
                    name);

                return;
            }

            if (normalized == "!جزاء لاعبين")
            {
                await ShowPlayers(groupId);
                return;
            }

            if (normalized == "!جزاء بدء")
            {
                await StartGame(groupId);
                return;
            }

            if (normalized == "!جزاء حالة")
            {
                await ShowStatus(groupId);
                return;
            }

            if (normalized == "!جزاء انهاء")
            {
                await EndGame(groupId);
                return;
            }
        }

        // ============================================================
        // NORMALIZE
        // ============================================================

        private static string NormalizeText(
            string text)
        {
            return text
                .Trim()
                .Replace(
                    "أ",
                    "ا")
                .Replace(
                    "إ",
                    "ا")
                .Replace(
                    "آ",
                    "ا")
                .ToLowerInvariant();
        }

        // ============================================================
        // HELP
        // ============================================================

        private static async Task SendHelp(
            string groupId)
        {
            string message =
                "⚽ لعبة الجزاء ⚽\n\n" +

                "الأوامر:\n" +

                "!جزاء انضم\n" +
                "الانضمام إلى اللعبة\n\n" +

                "!جزاء لاعبين\n" +
                "عرض اللاعبين\n\n" +

                "!جزاء بدء\n" +
                "بدء اللعبة\n\n" +

                "!جزاء حالة\n" +
                "عرض حالة اللعبة\n\n" +

                "!جزاء انهاء\n" +
                "إنهاء اللعبة\n\n" +

                "بعد بدء اللعبة:\n" +
                "1️⃣ يسار\n" +
                "2️⃣ وسط\n" +
                "3️⃣ يمين\n\n" +

                "⏱ لديك 25 ثانية للتسديد.";

            await SendMessage(
                groupId,
                message);
        }

        // ============================================================
        // JOIN
        // ============================================================

        private static async Task JoinGame(
            string groupId,
            string userId,
            string name)
        {
            PenaltyGame game =
                Games.GetOrAdd(
                    groupId,
                    _ => new PenaltyGame
                    {
                        GroupId = groupId
                    });

            if (game.Started)
            {
                await SendMessage(
                    groupId,
                    "⚠️ اللعبة بدأت بالفعل.");

                return;
            }

            if (game.Players.Any(
                    p => p.UserId == userId))
            {
                await SendMessage(
                    groupId,
                    "⚠️ أنت مشترك بالفعل.");

                return;
            }

            if (game.Players.Count >= MaxPlayers)
            {
                await SendMessage(
                    groupId,
                    "⚠️ اكتمل عدد اللاعبين. الحد الأقصى 10.");

                return;
            }

            if (string.IsNullOrWhiteSpace(name))
                name = "لاعب";

            var player =
                new PenaltyPlayer
                {
                    UserId = userId,
                    Name = name,
                    Number = game.Players.Count + 1
                };

            game.Players.Add(player);

            await SendMessage(
                groupId,
                "✅ تم انضمام " +
                name +
                "\n" +
                "رقم اللاعب: " +
                player.Number +
                "\n" +
                "عدد اللاعبين: " +
                game.Players.Count +
                "/" +
                MaxPlayers);
        }

        // ============================================================
        // SHOW PLAYERS
        // ============================================================

        private static async Task ShowPlayers(
            string groupId)
        {
            if (!Games.TryGetValue(
                    groupId,
                    out var game))
            {
                await SendMessage(
                    groupId,
                    "⚠️ لا توجد لعبة حالياً.");

                return;
            }

            if (game.Players.Count == 0)
            {
                await SendMessage(
                    groupId,
                    "⚠️ لا يوجد لاعبين.");

                return;
            }

            var sb =
                new StringBuilder();

            sb.AppendLine(
                "⚽ لاعبي لعبة الجزاء:");

            sb.AppendLine();

            foreach (var player in game.Players)
            {
                string state =
                    player.Eliminated
                        ? "❌ خرج"
                        : "✅";

                sb.AppendLine(
                    player.Number +
                    ". " +
                    player.Name +
                    " " +
                    state +
                    " | أهداف: " +
                    player.Goals +
                    " | تسديدات: " +
                    player.Shots);
            }

            sb.AppendLine();

            sb.AppendLine(
                "العدد: " +
                game.Players.Count +
                "/" +
                MaxPlayers);

            await SendMessage(
                groupId,
                sb.ToString());
        }

        // ============================================================
        // START GAME
        // ============================================================

        private static async Task StartGame(
            string groupId)
        {
            if (!Games.TryGetValue(
                    groupId,
                    out var game))
            {
                await SendMessage(
                    groupId,
                    "⚠️ لا توجد لعبة.\nاكتب !جزاء انضم");

                return;
            }

            if (game.Started)
            {
                await SendMessage(
                    groupId,
                    "⚠️ اللعبة بدأت بالفعل.");

                return;
            }

            if (game.Players.Count < MinPlayers)
            {
                await SendMessage(
                    groupId,
                    "⚠️ تحتاج اللعبة إلى لاعبين اثنين على الأقل.");

                return;
            }

            foreach (var player in game.Players)
            {
                player.Shots = 0;
                player.Goals = 0;
                player.Eliminated = false;
            }

            game.CurrentPlayerIndex = 0;
            game.Started = true;
            game.TurnAnswered = false;

            await SendMessage(
                groupId,
                "🔥 بدأت لعبة الجزاء!\n\n" +

                "عدد اللاعبين: " +
                game.Players.Count +
                "\n" +

                "كل لاعب لديه " +
                ShotsPerPlayer +
                " تسديدات.\n\n" +

                "⏱ وقت كل تسديدة: " +
                TurnSeconds +
                " ثانية.\n\n" +

                "1️⃣ يسار\n" +
                "2️⃣ وسط\n" +
                "3️⃣ يمين");

            await StartTurn(game);
        }

        // ============================================================
        // START TURN
        // ============================================================

        private static async Task StartTurn(
            PenaltyGame game)
        {
            if (!game.Started)
                return;

            // إلغاء المؤقت القديم
            try
            {
                game.TurnCancellation?.Cancel();
                game.TurnCancellation?.Dispose();
            }
            catch
            {
            }

            // البحث عن اللاعب التالي
            while (game.CurrentPlayerIndex <
                   game.Players.Count)
            {
                var player =
                    game.Players[
                        game.CurrentPlayerIndex];

                if (!player.Eliminated &&
                    player.Shots < ShotsPerPlayer)
                {
                    break;
                }

                game.CurrentPlayerIndex++;
            }

            // انتهت اللعبة
            if (game.CurrentPlayerIndex >=
                game.Players.Count)
            {
                await FinishGame(game);
                return;
            }

            var currentPlayer =
                game.Players[
                    game.CurrentPlayerIndex];

            game.TurnAnswered = false;

            game.TurnId =
                Interlocked.Increment(
                    ref _turnCounter);

            var turnId =
                game.TurnId;

            var cts =
                new CancellationTokenSource();

            game.TurnCancellation =
                cts;

            await SendMessage(
                game.GroupId,
                "🎯 دور اللاعب: " +
                currentPlayer.Name +
                "\n\n" +

                "التسديدة رقم " +
                (currentPlayer.Shots + 1) +
                "/" +
                ShotsPerPlayer +
                "\n\n" +

                "اختر اتجاه التسديدة:\n" +

                "1️⃣ يسار\n" +
                "2️⃣ وسط\n" +
                "3️⃣ يمين\n\n" +

                "⏱ أمامك " +
                TurnSeconds +
                " ثانية.");

            // إرسال صورة الملعب
            try
            {
                byte[] image =
                    CreatePenaltyImage(
                        currentPlayer.Name,
                        currentPlayer.Shots + 1);

                await SendImage(
                    game.GroupId,
                    image);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "IMAGE CREATE ERROR:");

                Console.WriteLine(
                    ex.ToString());
            }

            _ = RunTurnTimeout(
                game,
                currentPlayer.UserId,
                turnId,
                cts.Token);
        }

        // ============================================================
        // TIMEOUT
        // ============================================================

        private static async Task RunTurnTimeout(
            PenaltyGame game,
            string userId,
            long turnId,
            CancellationToken token)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        TurnSeconds),
                    token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (!game.Started)
                return;

            if (game.TurnId != turnId)
                return;

            if (game.TurnAnswered)
                return;

            var player =
                game.Players.FirstOrDefault(
                    p => p.UserId == userId);

            if (player == null)
                return;

            // مهم:
            // لا نطرده من الروم.
            // فقط نخرجه من لعبة الجزاء.

            player.Eliminated = true;

            game.TurnAnswered = true;

            await SendMessage(
                game.GroupId,
                "⏰ انتهى الوقت!\n\n" +

                "❌ اللاعب " +
                player.Name +
                " لم يسدد خلال " +
                TurnSeconds +
                " ثانية.\n\n" +

                "🚪 خرج من لعبة الجزاء فقط.\n" +
                "⚠️ لم يتم طرده من الروم.");

            // ننتقل للاعب التالي هنا مرة واحدة فقط
            game.CurrentPlayerIndex++;

            await CheckGameAfterTurn(
                game);
        }

        // ============================================================
        // SHOT
        // ============================================================

        private static async Task ProcessShot(
            string groupId,
            string userId,
            string direction)
        {
            if (!Games.TryGetValue(
                    groupId,
                    out var game))
            {
                return;
            }

            if (!game.Started)
            {
                await SendMessage(
                    groupId,
                    "⚠️ اللعبة لم تبدأ.");

                return;
            }

            if (game.TurnAnswered)
            {
                return;
            }

            if (game.CurrentPlayerIndex <
                0 ||
                game.CurrentPlayerIndex >=
                game.Players.Count)
            {
                return;
            }

            var player =
                game.Players[
                    game.CurrentPlayerIndex];

            if (player.UserId != userId)
            {
                await SendMessage(
                    groupId,
                    "⛔ مو دورك.");

                return;
            }

            if (player.Eliminated)
                return;

            if (player.Shots >=
                ShotsPerPlayer)
                return;

            game.TurnAnswered = true;

            try
            {
                game.TurnCancellation?.Cancel();
            }
            catch
            {
            }

            // حارس عشوائي
            int keeper =
                Random.Shared.Next(
                    1,
                    4);

            int shot =
                int.Parse(direction);

            player.Shots++;

            bool goal =
                keeper != shot;

            if (goal)
                player.Goals++;

            string shotName =
                GetDirectionName(
                    shot);

            string keeperName =
                GetDirectionName(
                    keeper);

            if (goal)
            {
                await SendMessage(
                    groupId,
                    "⚽⚽⚽ هــــدف!\n\n" +

                    "اللاعب: " +
                    player.Name +
                    "\n" +

                    "التسديدة: " +
                    shotName +
                    "\n" +

                    "الحارس ذهب إلى: " +
                    keeperName +
                    "\n\n" +

                    "🎯 الأهداف: " +
                    player.Goals +
                    "\n" +

                    "📊 التسديدات: " +
                    player.Shots +
                    "/" +
                    ShotsPerPlayer);

                try
                {
                    byte[] image =
                        CreateResultImage(
                            true,
                            player.Name,
                            shotName,
                            keeperName);

                    await SendImage(
                        groupId,
                        image);
                }
                catch
                {
                }
            }
            else
            {
                await SendMessage(
                    groupId,
                    "🧤 تصــــدى الحارس!\n\n" +

                    "اللاعب: " +
                    player.Name +
                    "\n" +

                    "التسديدة: " +
                    shotName +
                    "\n" +

                    "الحارس ذهب إلى: " +
                    keeperName +
                    "\n\n" +

                    "🎯 الأهداف: " +
                    player.Goals +
                    "\n" +

                    "📊 التسديدات: " +
                    player.Shots +
                    "/" +
                    ShotsPerPlayer);

                try
                {
                    byte[] image =
                        CreateResultImage(
                            false,
                            player.Name,
                            shotName,
                            keeperName);

                    await SendImage(
                        groupId,
                        image);
                }
                catch
                {
                }
            }

            game.CurrentPlayerIndex++;

            await CheckGameAfterTurn(
                game);
        }

        // ============================================================
        // CHECK GAME
        // ============================================================

        private static async Task CheckGameAfterTurn(
            PenaltyGame game)
        {
            if (!game.Started)
                return;

            var activePlayers =
                game.Players
                    .Where(
                        p => !p.Eliminated)
                    .ToList();

            if (activePlayers.Count <= 1)
            {
                await FinishGame(game);
                return;
            }

            bool allFinished =
                activePlayers.All(
                    p => p.Shots >= ShotsPerPlayer);

            if (allFinished)
            {
                await FinishGame(game);
                return;
            }

            await StartTurn(game);
        }

        // ============================================================
        // STATUS
        // ============================================================

        private static async Task ShowStatus(
            string groupId)
        {
            if (!Games.TryGetValue(
                    groupId,
                    out var game))
            {
                await SendMessage(
                    groupId,
                    "⚠️ لا توجد لعبة حالياً.");

                return;
            }

            string state =
                game.Started
                    ? "🔥 قيد اللعب"
                    : "⏳ بانتظار البدء";

            var sb =
                new StringBuilder();

            sb.AppendLine(
                "⚽ حالة لعبة الجزاء");

            sb.AppendLine();

            sb.AppendLine(
                state);

            sb.AppendLine();

            foreach (var player in game.Players)
            {
                sb.AppendLine(
                    player.Number +
                    ". " +
                    player.Name +
                    " — " +
                    player.Goals +
                    " أهداف / " +
                    player.Shots +
                    " تسديدات" +
                    (player.Eliminated
                        ? " ❌"
                        : ""));
            }

            if (game.Started &&
                game.CurrentPlayerIndex >= 0 &&
                game.CurrentPlayerIndex <
                game.Players.Count)
            {
                var current =
                    game.Players[
                        game.CurrentPlayerIndex];

                sb.AppendLine();

                sb.AppendLine(
                    "🎯 الدور: " +
                    current.Name);
            }

            await SendMessage(
                groupId,
                sb.ToString());
        }

        // ============================================================
        // END GAME
        // ============================================================

        private static async Task EndGame(
            string groupId)
        {
            if (!Games.TryRemove(
                    groupId,
                    out var game))
            {
                await SendMessage(
                    groupId,
                    "⚠️ لا توجد لعبة.");

                return;
            }

            try
            {
                game.TurnCancellation?.Cancel();
                game.TurnCancellation?.Dispose();
            }
            catch
            {
            }

            await SendMessage(
                groupId,
                "🛑 تم إنهاء لعبة الجزاء.");

            game.Started = false;
        }

        // ============================================================
        // FINISH GAME
        // ============================================================

        private static async Task FinishGame(
            PenaltyGame game)
        {
            if (!game.Started)
                return;

            game.Started = false;

            try
            {
                game.TurnCancellation?.Cancel();
                game.TurnCancellation?.Dispose();
            }
            catch
            {
            }

            var activePlayers =
                game.Players
                    .Where(
                        p => !p.Eliminated)
                    .OrderByDescending(
                        p => p.Goals)
                    .ThenByDescending(
                        p => p.Shots)
                    .ToList();

            var winner =
                activePlayers.FirstOrDefault();

            var sb =
                new StringBuilder();

            sb.AppendLine(
                "🏆🏆 انتهت لعبة الجزاء 🏆🏆");

            sb.AppendLine();

            if (winner != null)
            {
                sb.AppendLine(
                    "🥇 الفائز: " +
                    winner.Name);

                sb.AppendLine(
                    "⚽ الأهداف: " +
                    winner.Goals);
            }
            else
            {
                sb.AppendLine(
                    "لم يبقَ لاعب في اللعبة.");
            }

            sb.AppendLine();

            sb.AppendLine(
                "📊 النتائج:");

            foreach (var player in
                     game.Players
                         .OrderByDescending(
                             p => p.Goals))
            {
                sb.AppendLine(
                    player.Name +
                    " — " +
                    player.Goals +
                    "/" +
                    player.Shots +
                    (player.Eliminated
                        ? " ❌ خرج"
                        : ""));
            }

            sb.AppendLine();

            sb.AppendLine(
                "شكراً للجميع ❤️");

            await SendMessage(
                game.GroupId,
                sb.ToString());

            Games.TryRemove(
                game.GroupId,
                out _);
        }

        // ============================================================
        // PLAYER NAME
        // ============================================================

        private static async Task<string> GetPlayerName(
            string groupId,
            string userId)
        {
            try
            {
                if (_client == null)
                    return "لاعب";

                GroupUser groupUser =
                    await _client.GetGroupUser(
                        groupId,
                        userId);

                if (groupUser?.User != null)
                {
                    if (!string.IsNullOrWhiteSpace(
                            groupUser.User.Nickname))
                    {
                        return groupUser.User.Nickname;
                    }

                    if (!string.IsNullOrWhiteSpace(
                            groupUser.User.Id))
                    {
                        return groupUser.User.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "GET USER ERROR:");

                Console.WriteLine(
                    ex.Message);
            }

            return "لاعب";
        }

        // ============================================================
        // DIRECTION NAME
        // ============================================================

        private static string GetDirectionName(
            int direction)
        {
            return direction switch
            {
                1 => "يسار",
                2 => "وسط",
                3 => "يمين",
                _ => "غير معروف"
            };
        }

        // ============================================================
        // SEND MESSAGE
        // ============================================================

        private static async Task SendMessage(
            string groupId,
            string message)
        {
            try
            {
                if (_client == null)
                {
                    Console.WriteLine(
                        "MESSAGE ERROR: client null");

                    return;
                }

                if (string.IsNullOrWhiteSpace(
                        groupId))
                {
                    return;
                }

                await _client.GroupMessage(
                    groupId,
                    message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SEND MESSAGE ERROR:");

                Console.WriteLine(
                    ex.ToString());
            }
        }

        // ============================================================
        // SEND IMAGE
        // ============================================================

        private static async Task SendImage(
            string groupId,
            byte[] imageBytes)
        {
            try
            {
                if (_client == null)
                    return;

                if (imageBytes == null ||
                    imageBytes.Length == 0)
                    return;

                Console.WriteLine(
                    "IMAGE SEND: " +
                    imageBytes.Length +
                    " bytes");

                await _client.GroupMessage(
                    groupId,
                    imageBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SEND IMAGE ERROR:");

                Console.WriteLine(
                    ex.ToString());
            }
        }

        // ============================================================
        // CREATE PENALTY IMAGE
        // ============================================================

        private static byte[] CreatePenaltyImage(
            string playerName,
            int shotNumber)
        {
            using var image =
                new Image<Rgba32>(
                    900,
                    600);

            FillRect(
                image,
                0,
                0,
                900,
                600,
                new Rgba32(
                    20,
                    25,
                    30,
                    255));

            // العنوان
            FillRect(
                image,
                0,
                0,
                900,
                100,
                new Rgba32(
                    10,
                    10,
                    15,
                    255));

            // الملعب
            FillRect(
                image,
                0,
                100,
                900,
                500,
                new Rgba32(
                    30,
                    120,
                    55,
                    255));

            // منطقة الجزاء
            DrawRect(
                image,
                250,
                120,
                400,
                350,
                6,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            // خط المرمى
            DrawLine(
                image,
                300,
                210,
                600,
                210,
                7,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            // المرمى
            DrawRect(
                image,
                325,
                115,
                250,
                100,
                7,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            // شبكة المرمى
            for (int x = 330;
                 x <= 570;
                 x += 30)
            {
                DrawLine(
                    image,
                    x,
                    120,
                    x,
                    210,
                    2,
                    new Rgba32(
                        210,
                        210,
                        210,
                        255));
            }

            for (int y = 125;
                 y <= 205;
                 y += 20)
            {
                DrawLine(
                    image,
                    330,
                    y,
                    570,
                    y,
                    2,
                    new Rgba32(
                        210,
                        210,
                        210,
                        255));
            }

            // الحارس
            FillCircle(
                image,
                450,
                180,
                28,
                new Rgba32(
                    245,
                    190,
                    60,
                    255));

            FillCircle(
                image,
                450,
                220,
                38,
                new Rgba32(
                    40,
                    80,
                    220,
                    255));

            // الكرة
            FillCircle(
                image,
                450,
                475,
                24,
                new Rgba32(
                    245,
                    245,
                    245,
                    255));

            DrawCircle(
                image,
                450,
                475,
                24,
                3,
                new Rgba32(
                    20,
                    20,
                    20,
                    255));

            // نقطة الجزاء
            FillCircle(
                image,
                450,
                475,
                6,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            return EncodeJpeg(
                image);
        }

        // ============================================================
        // RESULT IMAGE
        // ============================================================

        private static byte[] CreateResultImage(
            bool goal,
            string playerName,
            string shotDirection,
            string keeperDirection)
        {
            using var image =
                new Image<Rgba32>(
                    900,
                    600);

            FillRect(
                image,
                0,
                0,
                900,
                600,
                goal
                    ? new Rgba32(
                        20,
                        110,
                        45,
                        255)
                    : new Rgba32(
                        110,
                        25,
                        25,
                        255));

            // المرمى
            DrawRect(
                image,
                325,
                115,
                250,
                100,
                8,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            // الشبكة
            for (int x = 330;
                 x <= 570;
                 x += 30)
            {
                DrawLine(
                    image,
                    x,
                    120,
                    x,
                    210,
                    2,
                    new Rgba32(
                        220,
                        220,
                        220,
                        255));
            }

            for (int y = 125;
                 y <= 205;
                 y += 20)
            {
                DrawLine(
                    image,
                    330,
                    y,
                    570,
                    y,
                    2,
                    new Rgba32(
                        220,
                        220,
                        220,
                        255));
            }

            if (goal)
            {
                DrawGoalEffect(
                    image);

                FillCircle(
                    image,
                    450,
                    190,
                    20,
                    new Rgba32(
                        255,
                        80,
                        80,
                        255));
            }
            else
            {
                DrawSaveEffect(
                    image);
            }

            // الكرة
            FillCircle(
                image,
                450,
                450,
                25,
                new Rgba32(
                    245,
                    245,
                    245,
                    255));

            DrawCircle(
                image,
                450,
                450,
                25,
                3,
                new Rgba32(
                    20,
                    20,
                    20,
                    255));

            return EncodeJpeg(
                image);
        }

        // ============================================================
        // JPEG
        // ============================================================

        private static byte[] EncodeJpeg(
            Image<Rgba32> image)
        {
            using var stream =
                new MemoryStream();

            image.Save(
                stream,
                new JpegEncoder
                {
                    Quality = 90
                });

            return stream.ToArray();
        }

        // ============================================================
        // GOAL EFFECT
        // ============================================================

        private static void DrawGoalEffect(
            Image<Rgba32> image)
        {
            DrawCircle(
                image,
                450,
                190,
                100,
                7,
                new Rgba32(
                    255,
                    220,
                    40,
                    255));

            DrawCircle(
                image,
                450,
                190,
                70,
                5,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            for (int i = 0;
                 i < 16;
                 i++)
            {
                double angle =
                    i * Math.PI * 2 / 16;

                int x1 =
                    450 +
                    (int)(
                        105 *
                        Math.Cos(angle));

                int y1 =
                    190 +
                    (int)(
                        105 *
                        Math.Sin(angle));

                int x2 =
                    450 +
                    (int)(
                        145 *
                        Math.Cos(angle));

                int y2 =
                    190 +
                    (int)(
                        145 *
                        Math.Sin(angle));

                DrawLine(
                    image,
                    x1,
                    y1,
                    x2,
                    y2,
                    5,
                    new Rgba32(
                        255,
                        220,
                        40,
                        255));
            }

            FillCircle(
                image,
                450,
                190,
                25,
                new Rgba32(
                    255,
                    80,
                    80,
                    255));
        }

        // ============================================================
        // SAVE EFFECT
        // ============================================================

        private static void DrawSaveEffect(
            Image<Rgba32> image)
        {
            DrawCircle(
                image,
                450,
                190,
                90,
                6,
                new Rgba32(
                    255,
                    80,
                    80,
                    255));

            DrawCircle(
                image,
                450,
                190,
                65,
                4,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            for (int i = 0;
                 i < 12;
                 i++)
            {
                double angle =
                    i * Math.PI * 2 / 12;

                int x1 =
                    450 +
                    (int)(
                        90 *
                        Math.Cos(angle));

                int y1 =
                    190 +
                    (int)(
                        90 *
                        Math.Sin(angle));

                int x2 =
                    450 +
                    (int)(
                        125 *
                        Math.Cos(angle));

                int y2 =
                    190 +
                    (int)(
                        125 *
                        Math.Sin(angle));

                DrawLine(
                    image,
                    x1,
                    y1,
                    x2,
                    y2,
                    5,
                    new Rgba32(
                        255,
                        80,
                        80,
                        255));
            }
        }

        // ============================================================
        // FILL RECT
        // ============================================================

        private static void FillRect(
            Image<Rgba32> image,
            int x,
            int y,
            int width,
            int height,
            Rgba32 color)
        {
            int xStart =
                Math.Max(
                    0,
                    x);

            int yStart =
                Math.Max(
                    0,
                    y);

            int xEnd =
                Math.Min(
                    image.Width,
                    x + width);

            int yEnd =
                Math.Min(
                    image.Height,
                    y + height);

            if (xStart >= xEnd ||
                yStart >= yEnd)
                return;

            image.ProcessPixelRows(
                accessor =>
                {
                    for (int yy = yStart;
                         yy < yEnd;
                         yy++)
                    {
                        Span<Rgba32> row =
                            accessor.GetRowSpan(
                                yy);

                        for (int xx = xStart;
                             xx < xEnd;
                             xx++)
                        {
                            row[xx] = color;
                        }
                    }
                });
        }

        // ============================================================
// DRAW LINE
// ============================================================

private static void DrawLine(
    Image<Rgba32> image,
    int x1,
    int y1,
    int x2,
    int y2,
    int thickness,
    Rgba32 color)
{
    int dx = x2 - x1;
    int dy = y2 - y1;

    int steps = Math.Max(
        Math.Abs(dx),
        Math.Abs(dy));

    if (steps == 0)
    {
        FillCircle(
            image,
            x1,
            y1,
            Math.Max(1, thickness / 2),
            color);

        return;
    }

    double stepX = (double)dx / steps;
    double stepY = (double)dy / steps;

    double x = x1;
    double y = y1;

    int radius = Math.Max(1, thickness / 2);

    for (int i = 0; i <= steps; i++)
    {
        FillCircle(
            image,
            (int)Math.Round(x),
            (int)Math.Round(y),
            radius,
            color);

        x += stepX;
        y += stepY;
    }
}

// ============================================================
// FILL CIRCLE
// ============================================================

private static void FillCircle(
    Image<Rgba32> image,
    int centerX,
    int centerY,
    int radius,
    Rgba32 color)
{
    int radiusSquared = radius * radius;

    int minX = Math.Max(0, centerX - radius);
    int maxX = Math.Min(image.Width - 1, centerX + radius);

    int minY = Math.Max(0, centerY - radius);
    int maxY = Math.Min(image.Height - 1, centerY + radius);

    for (int y = minY; y <= maxY; y++)
    {
        for (int x = minX; x <= maxX; x++)
        {
            int dx = x - centerX;
            int dy = y - centerY;

            if (dx * dx + dy * dy <= radiusSquared)
            {
                image[x, y] = color;
            }
        }
    }
}
