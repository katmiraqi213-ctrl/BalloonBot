using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using WolfLive.Api;
using WolfLive.Api.Models;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace PenaltyBot
{
    public class PenaltyPlayer
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Number { get; set; }

        public int Shots { get; set; }
        public int Goals { get; set; }

        public bool Eliminated { get; set; }
    }

    public class PenaltyGame
    {
        public string GroupId { get; set; } = "";

        public List<PenaltyPlayer> Players { get; set; } =
            new List<PenaltyPlayer>();

        public int CurrentPlayerIndex { get; set; } = 0;

        public bool Started { get; set; } = false;

        public bool TurnAnswered { get; set; } = false;

        public int TurnId { get; set; } = 0;

        public CancellationTokenSource? TurnCancellation { get; set; }
    }

    public static class Program
    {
        private static IWolfClient? _client;

        private static readonly Dictionary<string, PenaltyGame> Games =
            new Dictionary<string, PenaltyGame>();

        private static readonly object GameLock = new object();

        private static readonly Random Random =
            new Random();

        private const int MaxPlayers = 10;
        private const int MinPlayers = 2;
        private const int ShotsPerPlayer = 5;
        private const int TurnSeconds = 25;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("================================");
            Console.WriteLine("        PENALTY BOT");
            Console.WriteLine("================================");

            try
            {
                await ConnectBot();

                Console.WriteLine("Bot is running...");

                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine("MAIN ERROR:");
                Console.WriteLine(ex);
            }
        }

        // =========================================================
        // CONNECT
        // =========================================================

        private static async Task ConnectBot()
        {
            /*
             * ضع بيانات حساب البوت هنا
             */

            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL")
                ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD")
                ?? "";

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(
                    "WARNING: WOLF_EMAIL / WOLF_PASSWORD not set.");
            }

            _client = new WolfClient();

            RegisterMessageHandler(_client);

            try
            {
                bool result =
                    await _client.Login(
                        email,
                        password);

                Console.WriteLine(
                    "Login: " +
                    (result ? "SUCCESS" : "FAILED"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("LOGIN ERROR:");
                Console.WriteLine(ex);
                throw;
            }
        }

        // =========================================================
        // MESSAGE HANDLER
        // =========================================================

        private static void RegisterMessageHandler(
            IWolfClient client)
        {
            try
            {
                Type type = client.GetType();

                Console.WriteLine(
                    "Client type: " +
                    type.FullName);

                EventInfo[] events =
                    type.GetEvents(
                        BindingFlags.Public |
                        BindingFlags.Instance);

                foreach (EventInfo evt in events)
                {
                    string name =
                        evt.Name.ToLowerInvariant();

                    if (!name.Contains("message"))
                        continue;

                    Console.WriteLine(
                        "Trying message event: " +
                        evt.Name);

                    MethodInfo? method =
                        typeof(Program).GetMethod(
                            nameof(GenericMessageHandler),
                            BindingFlags.NonPublic |
                            BindingFlags.Static);

                    if (method == null)
                        continue;

                    Delegate? handler =
                        Delegate.CreateDelegate(
                            evt.EventHandlerType!,
                            method);

                    if (handler != null)
                    {
                        evt.AddEventHandler(
                            client,
                            handler);

                        Console.WriteLine(
                            "Message handler registered: " +
                            evt.Name);

                        return;
                    }
                }

                Console.WriteLine(
                    "WARNING: Could not automatically find message event.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "REGISTER HANDLER ERROR:");

                Console.WriteLine(ex);
            }
        }

        private static async void GenericMessageHandler(
            object? sender,
            object? message)
        {
            try
            {
                if (message == null)
                    return;

                await HandleMessageObject(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "MESSAGE HANDLER ERROR:");

                Console.WriteLine(ex);
            }
        }

        // =========================================================
        // MESSAGE PARSING
        // =========================================================

        private static async Task HandleMessageObject(
            object message)
        {
            try
            {
                string text =
                    GetMessageText(message);

                string groupId =
                    GetGroupId(message);

                string userId =
                    GetUserId(message);

                string userName =
                    GetUserName(message);

                if (string.IsNullOrWhiteSpace(text))
                    return;

                if (string.IsNullOrWhiteSpace(groupId))
                    return;

                if (string.IsNullOrWhiteSpace(userId))
                    return;

                Console.WriteLine(
                    $"MESSAGE | Group={groupId} | User={userName} | Text={text}");

                await ProcessCommand(
                    groupId,
                    userId,
                    userName,
                    text);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "HANDLE MESSAGE ERROR:");

                Console.WriteLine(ex);
            }
        }

        // =========================================================
        // COMMANDS
        // =========================================================

        private static async Task ProcessCommand(
            string groupId,
            string userId,
            string userName,
            string text)
        {
            text =
                text.Trim();

            if (text.Equals(
                    "!جزاء",
                    StringComparison.OrdinalIgnoreCase) ||
                text.Equals(
                    "!جزاء مساعدة",
                    StringComparison.OrdinalIgnoreCase))
            {
                await SendMessage(
                    groupId,
                    "⚽ لعبة الجزاء ⚽\n\n" +
                    "الأوامر:\n" +
                    "!جزاء انضم — الانضمام للعبة\n" +
                    "!جزاء لاعبين — عرض اللاعبين\n" +
                    "!جزاء بدء — بدء اللعبة\n" +
                    "!جزاء حالة — حالة اللعبة\n" +
                    "!جزاء انهاء — إنهاء اللعبة\n\n" +
                    "أثناء دورك:\n" +
                    "1 — تسديد يسار\n" +
                    "2 — تسديد وسط\n" +
                    "3 — تسديد يمين\n\n" +
                    $"⏱ لديك {TurnSeconds} ثانية للإجابة.");
                return;
            }

            if (text.Equals(
                    "!جزاء انضم",
                    StringComparison.OrdinalIgnoreCase))
            {
                await JoinGame(
                    groupId,
                    userId,
                    userName);

                return;
            }

            if (text.Equals(
                    "!جزاء لاعبين",
                    StringComparison.OrdinalIgnoreCase))
            {
                await ShowPlayers(groupId);
                return;
            }

            if (text.Equals(
                    "!جزاء بدء",
                    StringComparison.OrdinalIgnoreCase))
            {
                await StartGame(groupId);
                return;
            }

            if (text.Equals(
                    "!جزاء حالة",
                    StringComparison.OrdinalIgnoreCase))
            {
                await ShowStatus(groupId);
                return;
            }

            if (text.Equals(
                    "!جزاء انهاء",
                    StringComparison.OrdinalIgnoreCase))
            {
                await EndGame(groupId);
                return;
            }

            if (text == "1" ||
                text == "2" ||
                text == "3")
            {
                await ProcessShot(
                    groupId,
                    userId,
                    int.Parse(text));

                return;
            }
        }

        // =========================================================
        // JOIN
        // =========================================================

        private static async Task JoinGame(
            string groupId,
            string userId,
            string userName)
        {
            PenaltyGame game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out game!))
                {
                    game =
                        new PenaltyGame
                        {
                            GroupId = groupId
                        };

                    Games[groupId] = game;
                }

                if (game.Started)
                {
                    _ = SendMessage(
                        groupId,
                        "⚠️ اللعبة بدأت بالفعل.");

                    return;
                }

                if (game.Players.Any(
                        x => x.UserId == userId))
                {
                    _ = SendMessage(
                        groupId,
                        "⚠️ أنت مشترك بالفعل.");

                    return;
                }

                if (game.Players.Count >= MaxPlayers)
                {
                    _ = SendMessage(
                        groupId,
                        $"⚠️ اكتمل العدد. الحد الأقصى {MaxPlayers} لاعبين.");

                    return;
                }

                int number =
                    game.Players.Count + 1;

                game.Players.Add(
                    new PenaltyPlayer
                    {
                        UserId = userId,
                        Name = string.IsNullOrWhiteSpace(userName)
                            ? userId
                            : userName,
                        Number = number
                    });
            }

            await SendMessage(
                groupId,
                $"✅ {userName} انضم للعبة.\n" +
                $"👥 عدد اللاعبين: {GetPlayerCount(groupId)}/{MaxPlayers}\n\n" +
                "اكتب !جزاء بدء عندما يكتمل العدد.");
        }

        private static int GetPlayerCount(
            string groupId)
        {
            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                    return 0;

                return game.Players.Count;
            }
        }

        // =========================================================
        // PLAYERS
        // =========================================================

        private static async Task ShowPlayers(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                Games.TryGetValue(
                    groupId,
                    out game);
            }

            if (game == null ||
                game.Players.Count == 0)
            {
                await SendMessage(
                    groupId,
                    "⚠️ لا يوجد لاعبين.");
                return;
            }

            string result =
                "⚽ لاعبين لعبة الجزاء ⚽\n\n";

            foreach (PenaltyPlayer player in game.Players)
            {
                string status =
                    player.Eliminated
                        ? "❌ خارج اللعبة"
                        : "🟢";

                result +=
                    $"{player.Number}. {player.Name} {status}\n";
            }

            result +=
                $"\n👥 العدد: {game.Players.Count}/{MaxPlayers}";

            await SendMessage(
                groupId,
                result);
        }

        // =========================================================
        // START GAME
        // =========================================================

        private static async Task StartGame(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out game))
                {
                    game =
                        new PenaltyGame
                        {
                            GroupId = groupId
                        };

                    Games[groupId] = game;
                }

                if (game.Started)
                {
                    _ = SendMessage(
                        groupId,
                        "⚠️ اللعبة بدأت بالفعل.");
                    return;
                }

                if (game.Players.Count < MinPlayers)
                {
                    _ = SendMessage(
                        groupId,
                        $"⚠️ يجب أن يكون هناك {MinPlayers} لاعبين على الأقل.");

                    return;
                }

                game.Started = true;
                game.CurrentPlayerIndex = 0;

                foreach (PenaltyPlayer player in game.Players)
                {
                    player.Shots = 0;
                    player.Goals = 0;
                    player.Eliminated = false;
                }
            }

            await SendMessage(
                groupId,
                "🔥 بدأت لعبة الجزاء! 🔥\n\n" +
                $"⚽ كل لاعب لديه {ShotsPerPlayer} تسديدات.\n" +
                $"⏱ وقت كل دور {TurnSeconds} ثانية.\n\n" +
                "استعدوا...");

            await Task.Delay(1000);

            await StartTurn(groupId);
        }

        // =========================================================
        // START TURN
        // =========================================================

        private static async Task StartTurn(
            string groupId)
        {
            PenaltyPlayer? player = null;
            int turnId;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                    return;

                if (!game.Started)
                    return;

                while (
                    game.CurrentPlayerIndex <
                    game.Players.Count &&
                    game.Players[
                        game.CurrentPlayerIndex
                    ].Eliminated)
                {
                    game.CurrentPlayerIndex++;
                }

                if (
                    game.CurrentPlayerIndex >=
                    game.Players.Count)
                {
                    _ = FinishGame(groupId);
                    return;
                }

                player =
                    game.Players[
                        game.CurrentPlayerIndex];

                game.TurnAnswered = false;

                game.TurnId++;

                turnId =
                    game.TurnId;

                game.TurnCancellation?.Cancel();

                game.TurnCancellation =
                    new CancellationTokenSource();
            }

            if (player == null)
                return;

            await SendMessage(
                groupId,
                $"⚽ دور اللاعب: {player.Name}\n\n" +
                $"🎯 التسديدة رقم {player.Shots + 1}/{ShotsPerPlayer}\n\n" +
                "اختر الاتجاه:\n" +
                "1️⃣ يسار\n" +
                "2️⃣ وسط\n" +
                "3️⃣ يمين\n\n" +
                $"⏱ لديك {TurnSeconds} ثانية.");

            byte[] image =
                CreatePenaltyImage(
                    player.Name,
                    player.Shots + 1,
                    null,
                    false);

            await SendImage(
                groupId,
                image);

            _ = StartTurnTimeout(
                groupId,
                player.UserId,
                turnId);
        }

        // =========================================================
        // TIMEOUT
        // =========================================================

        private static async Task StartTurnTimeout(
            string groupId,
            string userId,
            int turnId)
        {
            CancellationToken token;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                    return;

                token =
                    game.TurnCancellation?.Token
                    ?? CancellationToken.None;
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(TurnSeconds),
                    token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            await TimeoutPlayer(
                groupId,
                userId,
                turnId);
        }

        private static async Task TimeoutPlayer(
            string groupId,
            string userId,
            int turnId)
        {
            PenaltyPlayer? player = null;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                    return;

                if (game.TurnId != turnId)
                    return;

                if (game.TurnAnswered)
                    return;

                player =
                    game.Players.FirstOrDefault(
                        x => x.UserId == userId);

                if (player == null)
                    return;

                game.TurnAnswered = true;

                /*
                 * مهم:
                 * اللاعب ينطرد من اللعبة فقط
                 * وليس من روم Wolf.
                 */

                player.Eliminated = true;

                game.CurrentPlayerIndex++;
            }

            await SendMessage(
                groupId,
                $"⏰ انتهى الوقت!\n" +
                $"❌ {player.Name} لم يسدد وتم إخراجه من لعبة الجزاء.\n" +
                "🚫 لم يتم طرده من الروم.");

            await Task.Delay(500);

            await CheckGameAfterTurn(groupId);
        }

        // =========================================================
        // SHOT
        // =========================================================

        private static async Task ProcessShot(
            string groupId,
            string userId,
            int direction)
        {
            PenaltyPlayer? player = null;
            bool goal = false;
            int shotNumber = 0;
            int turnId = 0;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                    return;

                if (!game.Started)
                    return;

                if (game.TurnAnswered)
                    return;

                if (
                    game.CurrentPlayerIndex <
                    0 ||
                    game.CurrentPlayerIndex >=
                    game.Players.Count)
                    return;

                player =
                    game.Players[
                        game.CurrentPlayerIndex];

                if (player.Eliminated)
                    return;

                if (player.UserId != userId)
                    return;

                game.TurnAnswered = true;

                game.TurnCancellation?.Cancel();

                player.Shots++;

                shotNumber =
                    player.Shots;

                turnId =
                    game.TurnId;

                /*
                 * احتمال التصدي.
                 * يمكن تغييره حسب رغبتك.
                 */
                int keeperDirection =
                    Random.Next(1, 4);

                goal =
                    keeperDirection != direction;

                if (goal)
                    player.Goals++;
            }

            string directionName =
                GetDirectionName(direction);

            if (goal)
            {
                await SendMessage(
                    groupId,
                    $"⚽🔥 هدف!\n\n" +
                    $"👤 اللاعب: {player!.Name}\n" +
                    $"🎯 الاتجاه: {directionName}\n" +
                    $"🥅 الهدف رقم: {player.Goals}\n" +
                    $"📊 التسديدات: {player.Shots}/{ShotsPerPlayer}");
            }
            else
            {
                await SendMessage(
                    groupId,
                    $"🧤❌ تصدي!\n\n" +
                    $"👤 اللاعب: {player!.Name}\n" +
                    $"🎯 الاتجاه: {directionName}\n" +
                    $"📊 التسديدات: {player.Shots}/{ShotsPerPlayer}");
            }

            byte[] image =
                CreatePenaltyImage(
                    player.Name,
                    shotNumber,
                    direction,
                    goal);

            await SendImage(
                groupId,
                image);

            await Task.Delay(700);

            await CheckGameAfterTurn(
                groupId);
        }

        // =========================================================
        // CHECK TURN
        // =========================================================

        private static async Task CheckGameAfterTurn(
            string groupId)
        {
            bool finish = false;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                    return;

                if (!game.Started)
                    return;

                /*
                 * إذا كل اللاعبين خلصوا 5 تسديدات
                 * أو تم إخراجهم من اللعبة.
                 */

                bool allDone =
                    game.Players.All(
                        p =>
                            p.Eliminated ||
                            p.Shots >= ShotsPerPlayer);

                if (allDone)
                {
                    finish = true;
                }
                else
                {
                    game.CurrentPlayerIndex++;

                    if (
                        game.CurrentPlayerIndex >=
                        game.Players.Count)
                    {
                        game.CurrentPlayerIndex = 0;
                    }
                }
            }

            if (finish)
            {
                await FinishGame(groupId);
            }
            else
            {
                await StartTurn(groupId);
            }
        }

        // =========================================================
        // DIRECTION
        // =========================================================

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

        // =========================================================
        // STATUS
        // =========================================================

        private static async Task ShowStatus(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                Games.TryGetValue(
                    groupId,
                    out game);
            }

            if (game == null)
            {
                await SendMessage(
                    groupId,
                    "⚠️ لا توجد لعبة حالياً.");
                return;
            }

            string text =
                "⚽ حالة لعبة الجزاء ⚽\n\n";

            text +=
                $"👥 اللاعبين: {game.Players.Count}\n";

            text +=
                $"🎮 الحالة: " +
                (game.Started
                    ? "🟢 بدأت"
                    : "🟡 انتظار") +
                "\n\n";

            foreach (PenaltyPlayer player in game.Players)
            {
                string status =
                    player.Eliminated
                        ? "❌ خارج اللعبة"
                        : "🟢";

                text +=
                    $"{player.Number}. {player.Name} " +
                    $"| ⚽ {player.Goals} " +
                    $"| 🎯 {player.Shots}/{ShotsPerPlayer} " +
                    $"| {status}\n";
            }

            await SendMessage(
                groupId,
                text);
        }

        // =========================================================
        // END GAME
        // =========================================================

        private static async Task EndGame(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out game))
                {
                    _ = SendMessage(
                        groupId,
                        "⚠️ لا توجد لعبة.");
                    return;
                }

                game.TurnCancellation?.Cancel();

                Games.Remove(groupId);
            }

            await SendMessage(
                groupId,
                "🛑 تم إنهاء لعبة الجزاء.");

            Console.WriteLine(
                "Game ended: " +
                groupId);
        }

        // =========================================================
        // FINISH GAME
        // =========================================================

        private static async Task FinishGame(
            string groupId)
        {
            List<PenaltyPlayer> players;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                        groupId,
                        out PenaltyGame? game))
                    return;

                game.Started = false;

                game.TurnCancellation?.Cancel();

                players =
                    game.Players
                        .Where(x => !x.Eliminated)
                        .OrderByDescending(x => x.Goals)
                        .ThenByDescending(x => x.Shots)
                        .ToList();
            }

            if (players.Count == 0)
            {
                await SendMessage(
                    groupId,
                    "🏁 انتهت اللعبة.\n" +
                    "❌ لا يوجد لاعب باقٍ.");

                lock (GameLock)
                {
                    Games.Remove(groupId);
                }

                return;
            }

            PenaltyPlayer winner =
                players[0];

            string result =
                "🏆⚽ انتهت لعبة الجزاء ⚽🏆\n\n" +
                $"🥇 الفائز: {winner.Name}\n" +
                $"⚽ الأهداف: {winner.Goals}\n" +
                $"🎯 التسديدات: {winner.Shots}\n\n" +
                "📊 النتائج:\n";

            int rank = 1;

            foreach (PenaltyPlayer player in players)
            {
                result +=
                    $"{rank}. {player.Name} — " +
                    $"{player.Goals} هدف / " +
                    $"{player.Shots} تسديدة\n";

                rank++;
            }

            await SendMessage(
                groupId,
                result);

            lock (GameLock)
            {
                Games.Remove(groupId);
            }
        }

        // =========================================================
        // IMAGE
        // =========================================================

        private static byte[] CreatePenaltyImage(
            string playerName,
            int shotNumber,
            int? direction,
            bool goal)
        {
            const int width = 900;
            const int height = 600;

            using Image<Rgba32> image =
                new Image<Rgba32>(
                    width,
                    height);

            /*
             * خلفية
             */
            FillRect(
                image,
                0,
                0,
                width,
                height,
                new Rgba32(
                    25,
                    25,
                    30,
                    255));

            /*
             * أرضية
             */
            FillRect(
                image,
                0,
                390,
                width,
                210,
                new Rgba32(
                    30,
                    120,
                    50,
                    255));

            /*
             * منطقة الجزاء
             */
            DrawRect(
                image,
                250,
                330,
                400,
                260,
                new Rgba32(
                    255,
                    255,
                    255,
                    255),
                5);

            /*
             * المرمى
             */
            DrawGoal(
                image);

            /*
             * حارس
             */
            DrawKeeper(
                image);

            /*
             * الكرة
             */
            int ballX = 450;
            int ballY = 470;

            if (direction.HasValue)
            {
                ballX =
                    direction.Value switch
                    {
                        1 => 330,
                        2 => 450,
                        3 => 570,
                        _ => 450
                    };

                ballY =
                    goal
                        ? 350
                        : 410;
            }

            FillCircle(
                image,
                ballX,
                ballY,
                18,
                new Rgba32(
                    255,
                    255,
                    255,
                    255));

            DrawCircle(
                image,
                ballX,
                ballY,
                18,
                new Rgba32(
                    0,
                    0,
                    0,
                    255),
                3);

            /*
             * تأثير الهدف / التصدي
             */
            if (direction.HasValue)
            {
                if (goal)
                {
                    DrawGoalEffect(
                        image,
                        ballX,
                        ballY);
                }
                else
                {
                    DrawSaveEffect(
                        image,
                        ballX,
                        ballY);
                }
            }

            /*
             * إطار علوي
             */
            DrawRect(
                image,
                20,
                20,
                width - 40,
                90,
                new Rgba32(
                    255,
                    255,
                    255,
                    255),
                3);

            /*
             * لأننا لا نستخدم Drawing/Fonts هنا،
             * لا نعتمد على DrawText.
             *
             * معلومات اللاعب تُرسل أيضاً كنص في الرسالة.
             */

            using MemoryStream stream =
                new MemoryStream();

            image.Save(
                stream,
                new JpegEncoder
                {
                    Quality = 90
                });

            return stream.ToArray();
        }

        // =========================================================
        // GOAL
        // =========================================================

        private static void DrawGoal(
            Image<Rgba32> image)
        {
            Rgba32 white =
                new Rgba32(
                    255,
                    255,
                    255,
                    255);

            int x = 300;
            int y = 150;
            int w = 300;
            int h = 220;

            DrawRect(
                image,
                x,
                y,
                w,
                h,
                white,
                8);

            for (int i = 0; i <= 6; i++)
            {
                int gx =
                    x +
                    (i * w / 6);

                DrawLine(
                    image,
                    gx,
                    y,
                    gx,
                    y + h,
                    new Rgba32(
                        220,
                        220,
                        220,
                        255),
                    2);
            }

            for (int i = 0; i <= 5; i++)
            {
                int gy =
                    y +
                    (i * h / 5);

                DrawLine(
                    image,
                    x,
                    gy,
                    x + w,
                    gy,
                    new Rgba32(
                        220,
                        220,
                        220,
                        255),
                    2);
            }
        }

        // =========================================================
        // KEEPER
        // =========================================================

        private static void DrawKeeper(
            Image<Rgba32> image)
        {
            Rgba32 color =
                new Rgba32(
                    20,
                    80,
                    180,
                    255);

            FillRect(
                image,
                425,
                260,
                50,
                100,
                color);

            FillCircle(
                image,
                450,
                240,
                25,
                new Rgba32(
                    230,
                    180,
                    140,
                    255));

            DrawLine(
                image,
                430,
                280,
                380,
                330,
                color,
                12);

            DrawLine(
                image,
                470,
                280,
                520,
                330,
                color,
                12);

            DrawLine(
                image,
                440,
                355,
                410,
                405,
                color,
                12);

            DrawLine(
                image,
                460,
                355,
                490,
                405,
                color,
                12);
        }

        // =========================================================
        // GOAL EFFECT
        // =========================================================

        private static void DrawGoalEffect(
            Image<Rgba32> image,
            int x,
            int y)
        {
            Rgba32 color =
                new Rgba32(
                    255,
                    220,
                    0,
                    255);

            DrawCircle(
                image,
                x,
                y,
                35,
                color,
                5);

            DrawCircle(
                image,
                x,
                y,
                50,
                new Rgba32(
                    255,
                    140,
                    0,
                    255),
                3);

            DrawLine(
                image,
                x - 65,
                y,
                x - 35,
                y,
                color,
                5);

            DrawLine(
                image,
                x + 35,
                y,
                x + 65,
                y,
                color,
                5);

            DrawLine(
                image,
                x,
                y - 65,
                x,
                y - 35,
                color,
                5);

            DrawLine(
                image,
                x,
                y + 35,
                x,
                y + 65,
                color,
                5);
        }

        // =========================================================
        // SAVE EFFECT
        // =========================================================

        private static void DrawSaveEffect(
            Image<Rgba32> image,
            int x,
            int y)
        {
            Rgba32 color =
                new Rgba32(
                    220,
                    30,
                    30,
                    255);

            DrawCircle(
                image,
                x,
                y,
                35,
                color,
                5);

            DrawLine(
                image,
                x - 25,
                y - 25,
                x + 25,
                y + 25,
                color,
                7);

            DrawLine(
                image,
                x + 25,
                y - 25,
                x - 25,
                y + 25,
                color,
                7);
        }

        // =========================================================
        // DRAW RECT
        // =========================================================

        private static void FillRect(
            Image<Rgba32> image,
            int x,
            int y,
            int width,
            int height,
            Rgba32 color)
        {
            int x2 =
                Math.Min(
                    image.Width,
                    x + width);

            int y2 =
                Math.Min(
                    image.Height,
                    y + height);

            int startX =
                Math.Max(0, x);

            int startY =
                Math.Max(0, y);

            image.ProcessPixelRows(
                accessor =>
                {
                    for (
                        int yy = startY;
                        yy < y2;
                        yy++)
                    {
                        Span<Rgba32> row =
                            accessor.GetRowSpan(yy);

                        for (
                            int xx = startX;
                            xx < x2;
                            xx++)
                        {
                            row[xx] = color;
                        }
                    }
                });
        }

        private static void DrawRect(
            Image<Rgba32> image,
            int x,
            int y,
            int width,
            int height,
            Rgba32 color,
            int thickness)
        {
            FillRect(
                image,
                x,
                y,
                width,
                thickness,
                color);

            FillRect(
                image,
                x,
                y + height - thickness,
                width,
                thickness,
                color);

            FillRect(
                image,
                x,
                y,
                thickness,
                height,
                color);

            FillRect(
                image,
                x + width - thickness,
                y,
                thickness,
                height,
                color);
        }

        // =========================================================
        // LINE
        // =========================================================

        private static void DrawLine(
            Image<Rgba32> image,
            int x1,
            int y1,
            int x2,
            int y2,
            Rgba32 color,
            int thickness)
        {
            int dx =
                Math.Abs(x2 - x1);

            int sx =
                x1 < x2 ? 1 : -1;

            int dy =
                -Math.Abs(y2 - y1);

            int sy =
                y1 < y2 ? 1 : -1;

            int err =
                dx + dy;

            while (true)
            {
                FillCircle(
                    image,
                    x1,
                    y1,
                    Math.Max(1, thickness / 2),
                    color);

                if (
                    x1 == x2 &&
                    y1 == y2)
                    break;

                int e2 =
                    2 * err;

                if (e2 >= dy)
                {
                    err += dy;
                    x1 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y1 += sy;
                }
            }
        }

        // =========================================================
        // CIRCLE
        // =========================================================

        private static void FillCircle(
            Image<Rgba32> image,
            int cx,
            int cy,
            int radius,
            Rgba32 color)
        {
            int r2 =
                radius * radius;

            image.ProcessPixelRows(
                accessor =>
                {
                    int minY =
                        Math.Max(
                            0,
                            cy - radius);

                    int maxY =
                        Math.Min(
                            image.Height - 1,
                            cy + radius);

                    for (
                        int y = minY;
                        y <= maxY;
                        y++)
                    {
                        Span<Rgba32> row =
                            accessor.GetRowSpan(y);

                        int dy =
                            y - cy;

                        int dx =
                            (int)Math.Sqrt(
                                Math.Max(
                                    0,
                                    r2 - dy * dy));

                        int minX =
                            Math.Max(
                                0,
                                cx - dx);

                        int maxX =
                            Math.Min(
                                image.Width - 1,
                                cx + dx);

                        for (
                            int x = minX;
                            x <= maxX;
                            x++)
                        {
                            row[x] = color;
                        }
                    }
                });
        }

        private static void DrawCircle(
            Image<Rgba32> image,
            int cx,
            int cy,
            int radius,
            Rgba32 color,
            int thickness)
        {
            int inner =
                Math.Max(
                    0,
                    radius - thickness);

            int outer2 =
                radius * radius;

            int inner2 =
                inner * inner;

            image.ProcessPixelRows(
                accessor =>
                {
                    int minY =
                        Math.Max(
                            0,
                            cy - radius);

                    int maxY =
                        Math.Min(
                            image.Height - 1,
                            cy + radius);

                    for (
                        int y = minY;
                        y <= maxY;
                        y++)
                    {
                        Span<Rgba32> row =
                            accessor.GetRowSpan(y);

                        int dy =
                            y - cy;

                        int minX =
                            Math.Max(
                                0,
                                cx - radius);

                        int maxX =
                            Math.Min(
                                image.Width - 1,
                                cx + radius);

                        for (
                            int x = minX;
                            x <= maxX;
                            x++)
                        {
                            int dx =
                                x - cx;

                            int distance =
                                dx * dx +
                                dy * dy;

                            if (
                                distance <= outer2 &&
                                distance >= inner2)
                            {
                                row[x] = color;
                            }
                        }
                    }
                });
        }

        // =========================================================
        // SEND MESSAGE
        // =========================================================

        private static async Task SendMessage(
            string groupId,
            string text)
        {
            try
            {
                if (_client == null)
                {
                    Console.WriteLine(
                        "MESSAGE ERROR: client is null");

                    return;
                }

                if (string.IsNullOrWhiteSpace(groupId))
                    return;

                if (text == null)
                    text = "";

                Console.WriteLine(
                    $"SEND MESSAGE | Group={groupId}");

                Console.WriteLine(
                    text);

                await _client.GroupMessage(
                    groupId,
                    text);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SEND MESSAGE ERROR:");

                Console.WriteLine(ex);
            }
        }

        // =========================================================
        // SEND IMAGE
        // =========================================================

        private static async Task SendImage(
            string groupId,
            byte[] imageBytes)
        {
            try
            {
                if (_client == null)
                {
                    Console.WriteLine(
                        "IMAGE ERROR: client is null");

                    return;
                }

                if (imageBytes == null ||
                    imageBytes.Length == 0)
                {
                    Console.WriteLine(
                        "IMAGE ERROR: image is empty");

                    return;
                }

                Console.WriteLine(
                    "================================");

                Console.WriteLine(
                    "IMAGE TEST");

                Console.WriteLine(
                    "Group: " +
                    groupId);

                Console.WriteLine(
                    "Bytes: " +
                    imageBytes.Length);

                /*
                 * الطريقة الصحيحة لإرسال صورة
                 * في WolfLive.Api:
                 *
                 * GroupMessage(groupId, byte[])
                 *
                 * والـ API يتعامل معها كـ image/jpeg.
                 */

                var result =
                    await _client.GroupMessage(
                        groupId,
                        imageBytes);

                Console.WriteLine(
                    "IMAGE SENT!");

                Console.WriteLine(
                    "Response: " +
                    result)
