using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Fonts;

namespace PenaltyBot
{
    public class PenaltyPlayer
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";

        public int Shots { get; set; }
        public int Goals { get; set; }
        public int Saves { get; set; }
    }

    public class PenaltyGame
    {
        public string GroupId { get; set; } = "";

        public List<PenaltyPlayer> Players { get; set; } = new();

        public bool Started { get; set; }

        public int CurrentPlayerIndex { get; set; } = -1;

        public CancellationTokenSource? TurnCancellation { get; set; }

        public PenaltyPlayer? CurrentPlayer
        {
            get
            {
                if (CurrentPlayerIndex < 0 ||
                    CurrentPlayerIndex >= Players.Count)
                    return null;

                return Players[CurrentPlayerIndex];
            }
        }
    }

    public static class Program
    {
        private static IWolfClient? Client;

        private static readonly Dictionary<string, PenaltyGame> Games = new();

        private static readonly object GameLock = new();

        private static readonly Random Random = new();

        public static async Task Main()
        {
            Console.WriteLine("====================================");
            Console.WriteLine("       PENALTY BOT STARTING");
            Console.WriteLine("====================================");

            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("WOLF_EMAIL أو WOLF_PASSWORD غير موجود.");
                return;
            }

            try
            {
                Client = new WolfClient();

                // مهم جداً:
                // الاشتراك باستقبال الرسائل قبل تسجيل الدخول
                Client.Messaging.OnMessage += OnMessage;

                Console.WriteLine("تم تشغيل مستمع الرسائل.");

                await Client.Login(email, password);

                Console.WriteLine("====================================");
                Console.WriteLine("البوت متصل بوف بنجاح.");
                Console.WriteLine("بانتظار الأوامر...");
                Console.WriteLine("====================================");

                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR:");
                Console.WriteLine(ex);
            }
        }

        private static async void OnMessage(
            IWolfClient client,
            Message message)
        {
            try
            {
                string? groupId = GetGroupId(message);
                string text = GetMessageText(message);

                if (string.IsNullOrWhiteSpace(groupId))
                    return;

                if (string.IsNullOrWhiteSpace(text))
                    return;

                text = text.Trim();

                Console.WriteLine(
                    $"[MESSAGE] Room={groupId} Text={text}");

                if (!text.StartsWith("!جزاء",
                    StringComparison.OrdinalIgnoreCase))
                {
                    // أثناء اللعب نقبل 1 / 2 / 3
                    if (text == "1" ||
                        text == "2" ||
                        text == "3")
                    {
                        await ProcessShot(groupId, text);
                    }

                    return;
                }

                string command =
                    text.Substring(5).Trim();

                if (string.IsNullOrWhiteSpace(command))
                {
                    await Send(
                        groupId,
                        "⚽ لعبة الجزاء\n\n" +
                        "الأوامر:\n" +
                        "!جزاء مساعدة\n" +
                        "!جزاء انضم\n" +
                        "!جزاء لاعبين\n" +
                        "!جزاء بدء\n" +
                        "!جزاء حالة\n" +
                        "!جزاء انهاء");

                    return;
                }

                if (command.Equals(
                    "مساعدة",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await Send(
                        groupId,
                        "⚽ لعبة ركلات الجزاء\n\n" +
                        "👥 اللاعبين: من 2 إلى 10\n" +
                        "🎯 لكل لاعب 5 ركلات\n" +
                        "⏱️ مدة الركلة: 25 ثانية\n\n" +
                        "1️⃣ يسار\n" +
                        "2️⃣ وسط\n" +
                        "3️⃣ يمين\n\n" +
                        "الأوامر:\n" +
                        "!جزاء انضم\n" +
                        "!جزاء لاعبين\n" +
                        "!جزاء بدء\n" +
                        "!جزاء حالة\n" +
                        "!جزاء انهاء");

                    return;
                }

                if (command.Equals(
                    "انضم",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await JoinGame(groupId, message);
                    return;
                }

                if (command.Equals(
                    "لاعبين",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await ShowPlayers(groupId);
                    return;
                }

                if (command.Equals(
                    "بدء",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await StartGame(groupId);
                    return;
                }

                if (command.Equals(
                    "حالة",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await ShowStatus(groupId);
                    return;
                }

                if (command.Equals(
                    "انهاء",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await EndGame(groupId);
                    return;
                }

                await Send(
                    groupId,
                    "❌ أمر غير معروف.\n" +
                    "اكتب !جزاء مساعدة");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[MESSAGE ERROR] {ex}");

                try
                {
                    string? groupId = GetGroupId(message);

                    if (!string.IsNullOrWhiteSpace(groupId))
                    {
                        await Send(
                            groupId,
                            "❌ حدث خطأ أثناء تنفيذ الأمر.");
                    }
                }
                catch
                {
                }
            }
        }

        private static async Task JoinGame(
            string groupId,
            Message message)
        {
            string userId = GetUserId(message);
            string name = GetUserName(message);

            if (string.IsNullOrWhiteSpace(userId))
                userId = name;

            PenaltyGame game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out game!))
                {
                    game = new PenaltyGame
                    {
                        GroupId = groupId
                    };

                    Games[groupId] = game;
                }

                if (game.Started)
                {
                    _ = Send(
                        groupId,
                        "❌ اللعبة بدأت بالفعل.");

                    return;
                }

                if (game.Players.Any(
                    x => x.UserId == userId))
                {
                    _ = Send(
                        groupId,
                        $"⚠️ {name} أنت منضم مسبقاً.");

                    return;
                }

                if (game.Players.Count >= 10)
                {
                    _ = Send(
                        groupId,
                        "❌ اكتمل العدد، الحد الأقصى 10 لاعبين.");

                    return;
                }

                game.Players.Add(
                    new PenaltyPlayer
                    {
                        UserId = userId,
                        Name = name
                    });
            }

            await Send(
                groupId,
                $"✅ تم انضمام {name}\n" +
                $"👥 عدد اللاعبين: {Games[groupId].Players.Count}/10\n\n" +
                "اكتب !جزاء بدء عندما يكتمل اللعب.");
        }

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
                await Send(
                    groupId,
                    "❌ لا يوجد لاعبون حالياً.");

                return;
            }

            var lines = new List<string>
            {
                "⚽ لاعبو ركلات الجزاء:",
                ""
            };

            for (int i = 0;
                i < game.Players.Count;
                i++)
            {
                var p = game.Players[i];

                lines.Add(
                    $"{i + 1}. {p.Name} — " +
                    $"⚽ {p.Goals} / 🎯 {p.Shots}");
            }

            await Send(
                groupId,
                string.Join("\n", lines));
        }

        private static async Task StartGame(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                Games.TryGetValue(
                    groupId,
                    out game);

                if (game == null)
                    return;

                if (game.Started)
                {
                    _ = Send(
                        groupId,
                        "⚠️ اللعبة بدأت مسبقاً.");

                    return;
                }

                if (game.Players.Count < 2)
                {
                    _ = Send(
                        groupId,
                        "❌ لازم يكون هناك لاعبان على الأقل.");

                    return;
                }

                if (game.Players.Count > 10)
                {
                    _ = Send(
                        groupId,
                        "❌ الحد الأقصى 10 لاعبين.");

                    return;
                }

                game.Started = true;
                game.CurrentPlayerIndex = 0;
            }

            await Send(
                groupId,
                "🏆 بدأت لعبة ركلات الجزاء!\n\n" +
                $"👥 عدد اللاعبين: {game!.Players.Count}\n" +
                "🎯 لكل لاعب 5 ركلات\n" +
                "⏱️ أمام كل لاعب 25 ثانية\n\n" +
                "استعدوا! 🔥");

            await StartTurn(groupId);
        }

        private static async Task StartTurn(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out game))
                    return;

                if (!game.Started)
                    return;

                if (game.Players.Count == 0)
                {
                    game.Started = false;
                    game.CurrentPlayerIndex = -1;
                    return;
                }

                if (game.CurrentPlayerIndex < 0 ||
                    game.CurrentPlayerIndex >= game.Players.Count)
                {
                    game.CurrentPlayerIndex = 0;
                }

                game.TurnCancellation?.Cancel();

                game.TurnCancellation =
                    new CancellationTokenSource();
            }

            PenaltyPlayer? player;

            lock (GameLock)
            {
                player = game!.CurrentPlayer;
            }

            if (player == null)
                return;

            if (player.Shots >= 5)
            {
                await MoveToNextPlayer(groupId);
                return;
            }

            await Send(
                groupId,
                $"⚽ دور اللاعب: {player.Name}\n\n" +
                $"🎯 الركلة {player.Shots + 1}/5\n\n" +
                "اختار اتجاه التسديد:\n" +
                "1️⃣ يسار\n" +
                "2️⃣ وسط\n" +
                "3️⃣ يمين\n\n" +
                "⏱️ عندك 25 ثانية!");

            StartTimeout(
                groupId,
                player.UserId);
        }

        private static void StartTimeout(
            string groupId,
            string userId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out game))
                    return;
            }

            CancellationToken token;

            lock (GameLock)
            {
                if (game!.TurnCancellation == null)
                    return;

                token =
                    game.TurnCancellation.Token;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(25),
                        token);

                    if (!token.IsCancellationRequested)
                    {
                        await TimeoutPlayer(
                            groupId,
                            userId);
                    }
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[TIMEOUT ERROR] {ex}");
                }
            });
        }

        private static async Task TimeoutPlayer(
            string groupId,
            string userId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out game))
                    return;

                if (!game.Started)
                    return;

                int index =
                    game.Players.FindIndex(
                        x => x.UserId == userId);

                if (index < 0)
                    return;

                string name =
                    game.Players[index].Name;

                game.Players.RemoveAt(index);

                if (game.Players.Count == 0)
                {
                    game.Started = false;
                    game.CurrentPlayerIndex = -1;

                    _ = Send(
                        groupId,
                        $"⏰ اللاعب {name} لم يسدد خلال 25 ثانية.\n" +
                        "❌ تم استبعاده من اللعبة فقط.\n" +
                        "🚫 لم يتم طرده من الروم.");

                    return;
                }

                if (index <= game.CurrentPlayerIndex)
                    game.CurrentPlayerIndex--;

                if (game.CurrentPlayerIndex < 0)
                    game.CurrentPlayerIndex =
                        game.Players.Count - 1;

                if (game.CurrentPlayerIndex >= game.Players.Count)
                    game.CurrentPlayerIndex = 0;

                game.TurnCancellation?.Cancel();
            }

            await Send(
                groupId,
                $"⏰ انتهى الوقت!\n\n" +
                $"❌ {GetPlayerName(groupId, userId)} " +
                "لم يسدد خلال 25 ثانية.\n" +
                "تم استبعاده من اللعبة فقط.\n" +
                "🚫 لم يتم طرده من الروم.");

            await MoveToNextPlayer(groupId);
        }

        private static async Task ProcessShot(
            string groupId,
            string directionText)
        {
            int direction;

            if (!int.TryParse(
                directionText,
                out direction))
                return;

            if (direction < 1 || direction > 3)
                return;

            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out game))
                    return;

                if (!game.Started)
                    return;
            }

            PenaltyPlayer? player;

            lock (GameLock)
            {
                player = game!.CurrentPlayer;

                if (player == null)
                    return;

                if (player.Shots >= 5)
                    return;
            }

            int keeperDirection =
                Random.Next(1, 4);

            bool goal =
                direction != keeperDirection;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out game))
                    return;

                if (!game.Started)
                    return;

                if (game.CurrentPlayerIndex < 0 ||
                    game.CurrentPlayerIndex >= game.Players.Count)
                    return;

                player = game.Players[
                    game.CurrentPlayerIndex];

                if (player.Shots >= 5)
                    return;

                player.Shots++;

                if (goal)
                    player.Goals++;
                else
                    player.Saves++;

                game.TurnCancellation?.Cancel();
            }

            string directionName =
                GetDirectionName(direction);

            string keeperName =
                GetDirectionName(keeperDirection);

            string result =
                goal ? "⚽ GOAL!" : "🧤 SAVE!";

            await Send(
                groupId,
                $"{result}\n\n" +
                $"👤 اللاعب: {player!.Name}\n" +
                $"🎯 التسديد: {directionName}\n" +
                $"🧤 الحارس: {keeperName}\n\n" +
                $"⚽ الأهداف: {player.Goals}\n" +
                $"🎯 الركلات: {player.Shots}");

            // ==========================================
            // إنشاء الصورة
            // ==========================================

            byte[] imageBytes =
                CreatePenaltyImage(
                    player.Name,
                    direction,
                    keeperDirection,
                    goal,
                    player.Goals,
                    player.Shots);

            Console.WriteLine(
                $"[IMAGE CREATE] JPEG = {imageBytes.Length} bytes");

            // ==========================================
            // إرسال الصورة بالطريقة المباشرة
            // ==========================================

            try
            {
                var response =
                    await Client!.Message(
                        groupId,
                        true,
                        imageBytes,
                        "image/jpeg");

                Console.WriteLine(
                    $"[IMAGE SENT DIRECT] Room={groupId} Bytes={imageBytes.Length}");

                Console.WriteLine(
                    $"[IMAGE RESPONSE] {response}");
            }
            catch (Exception imageEx)
            {
                Console.WriteLine(
                    $"[IMAGE SEND ERROR] {imageEx}");

                await Send(
                    groupId,
                    "⚠️ تم تنفيذ الركلة، لكن حدث خطأ أثناء إرسال الصورة.");
            }

            await Task.Delay(1200);

            await ContinueAfterShot(groupId);
        }

        private static async Task ContinueAfterShot(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out game))
                    return;

                if (!game.Started)
                    return;
            }

            PenaltyPlayer? player;

            lock (GameLock)
            {
                player = game!.CurrentPlayer;
            }

            if (player == null)
            {
                await FinishGame(groupId);
                return;
            }

            if (player.Shots >= 5)
            {
                await MoveToNextPlayer(groupId);
                return;
            }

            await StartTurn(groupId);
        }

        private static async Task MoveToNextPlayer(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out game))
                    return;

                if (!game.Started)
                    return;

                if (game.Players.Count == 0)
                {
                    game.Started = false;
                    return;
                }

                game.CurrentPlayerIndex++;

                if (game.CurrentPlayerIndex >=
                    game.Players.Count)
                {
                    game.CurrentPlayerIndex = 0;
                }
            }

            await StartTurn(groupId);
        }

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
                await Send(
                    groupId,
                    "❌ لا توجد لعبة في هذا الروم.");

                return;
            }

            if (!game.Started)
            {
                await Send(
                    groupId,
                    "⏸️ اللعبة غير مبدوءة.");

                return;
            }

            var current =
                game.CurrentPlayer;

            string text =
                "⚽ حالة اللعبة\n\n" +
                $"👥 اللاعبين: {game.Players.Count}\n" +
                $"🎯 الدور: {current?.Name ?? "غير معروف"}\n\n";

            foreach (var p in game.Players)
            {
                text +=
                    $"👤 {p.Name}: " +
                    $"⚽ {p.Goals} " +
                    $"🎯 {p.Shots}/5\n";
            }

            await Send(
                groupId,
                text);
        }

        private static async Task FinishGame(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out game))
                    return;

                game.Started = false;
                game.TurnCancellation?.Cancel();
            }

            var ranking =
                game!.Players
                    .OrderByDescending(x => x.Goals)
                    .ThenByDescending(x => x.Shots)
                    .ToList();

            string result =
                "🏆 انتهت لعبة ركلات الجزاء!\n\n";

            for (int i = 0;
                i < ranking.Count;
                i++)
            {
                var p = ranking[i];

                string medal =
                    i == 0 ? "🥇" :
                    i == 1 ? "🥈" :
                    i == 2 ? "🥉" :
                    "🏅";

                result +=
                    $"{medal} {i + 1}. {p.Name} — " +
                    $"⚽ {p.Goals} / 🎯 {p.Shots}\n";
            }

            await Send(
                groupId,
                result);
        }

        private static async Task EndGame(
            string groupId)
        {
            PenaltyGame? game;

            lock (GameLock)
            {
                if (!Games.TryGetValue(
                    groupId,
                    out game))
                    return;

                game.TurnCancellation?.Cancel();

                Games.Remove(groupId);
            }

            await Send(
                groupId,
                "🛑 تم إنهاء لعبة الجزاء.");
        }

        // =========================================================
        // إرسال النص
        // =========================================================

        private static async Task Send(
            string groupId,
            string text)
        {
            try
            {
                await Client!.GroupMessage(
                    groupId,
                    text);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[TEXT SEND ERROR] {ex}");
            }
        }

        // =========================================================
        // إنشاء صورة الركلة
        // =========================================================

        private static byte[] CreatePenaltyImage(
            string playerName,
            int shotDirection,
            int keeperDirection,
            bool goal,
            int goals,
            int shots)
        {
            const int width = 1000;
            const int height = 650;

            using var image =
                new Image<Rgba32>(
                    width,
                    height);

            // خلفية
            image.Mutate(ctx =>
            {
                ctx.Fill(
                    Color.FromRgb(
                        12,
                        18,
                        30));

                // الملعب
                ctx.Fill(
                    Color.FromRgb(
                        25,
                        105,
                        55),
                    new Rectangle(
                        0,
                        390,
                        width,
                        260));

                // السماء
                ctx.Fill(
                    Color.FromRgb(
                        18,
                        30,
                        55),
                    new Rectangle(
                        0,
                        0,
                        width,
                        390));

                // منطقة الجزاء
                ctx.Draw(
                    Color.White,
                    5,
                    new Rectangle(
                        180,
                        300,
                        640,
                        250));

                // خط المرمى
                ctx.Draw(
                    Color.White,
                    6,
                    new Rectangle(
                        250,
                        100,
                        500,
                        290));

                // القائمين
                ctx.Fill(
                    Color.White,
                    new Rectangle(
                        250,
                        100,
                        15,
                        290));

                ctx.Fill(
                    Color.White,
                    new Rectangle(
                        735,
                        100,
                        15,
                        290));

                // العارضة
                ctx.Fill(
                    Color.White,
                    new Rectangle(
                        250,
                        100,
                        500,
                        15));

                // شبكة المرمى
                for (int x = 265;
                    x < 735;
                    x += 30)
                {
                    ctx.DrawLine(
                        Color.LightGray,
                        1,
                        new PointF(x, 115),
                        new PointF(x, 385));
                }

                for (int y = 140;
                    y < 385;
                    y += 30)
                {
                    ctx.DrawLine(
                        Color.LightGray,
                        1,
                        new PointF(265, y),
                        new PointF(735, y));
                }

                // الحارس
                float keeperX =
                    keeperDirection == 1 ? 330 :
                    keeperDirection == 2 ? 490 :
                    630;

                // جسم الحارس
                ctx.Fill(
                    Color.Red,
                    new EllipsePolygon(
                        new PointF(
                            keeperX,
                            210),
                        48));

                ctx.Fill(
                    Color.Red,
                    new RectangleF(
                        keeperX - 30,
                        255,
                        60,
                        100));

                // الذراعان
                ctx.DrawLine(
                    Color.Red,
                    18,
                    new PointF(
                        keeperX - 20,
                        270),
                    new PointF(
                        keeperX - 90,
                        220));

                ctx.DrawLine(
                    Color.Red,
                    18,
                    new PointF(
                        keeperX + 20,
                        270),
                    new PointF(
                        keeperX + 90,
                        220));

                // الأرجل
                ctx.DrawLine(
                    Color.Red,
                    18,
                    new PointF(
                        keeperX - 15,
                        350),
                    new PointF(
                        keeperX - 45,
                        390));

                ctx.DrawLine(
                    Color.Red,
                    18,
                    new PointF(
                        keeperX + 15,
                        350),
                    new PointF(
                        keeperX + 45,
                        390));

                // الكرة
                float ballX =
                    shotDirection == 1 ? 320 :
                    shotDirection == 2 ? 500 :
                    680;

                float ballY =
                    goal ? 180 : 285;

                ctx.Fill(
                    Color.White,
                    new EllipsePolygon(
                        new PointF(
                            ballX,
                            ballY),
                        22));

                ctx.Draw(
                    Color.Black,
                    3,
                    new EllipsePolygon(
                        new PointF(
                            ballX,
                            ballY),
                        22));
            });

            // النصوص
            try
            {
                FontFamily family =
                    SystemFonts.Families.First();

                Font titleFont =
                    family.CreateFont(
                        54,
                        FontStyle.Bold);

                Font normalFont =
                    family.CreateFont(
                        30,
                        FontStyle.Bold);

                Font smallFont =
                    family.CreateFont(
                        24,
                        FontStyle.Regular);

                string title =
                    goal ? "GOAL!" : "SAVE!";

                string direction =
                    GetDirectionName(
                        shotDirection);

                string keeper =
                    GetDirectionName(
                        keeperDirection);

                image.Mutate(ctx =>
                {
                    var titleOptions =
                        new RichTextOptions(titleFont)
                        {
                            Origin =
                                new PointF(
                                    50,
                                    30)
                        };

                    ctx.DrawText(
                        titleOptions,
                        title,
                        goal
                            ? Color.LimeGreen
                            : Color.Red);

                    ctx.DrawText(
                        new RichTextOptions(
                            normalFont)
                        {
                            Origin =
                                new PointF(
                                    50,
                                    100)
                        },
                        playerName,
                        Color.White);

                    ctx.DrawText(
                        new RichTextOptions(
                            smallFont)
                        {
                            Origin =
                                new PointF(
                                    50,
                                    145)
                        },
                        $"التسديد: {direction}",
                        Color.White);

                    ctx.DrawText(
                        new RichTextOptions(
                            smallFont)
                        {
                            Origin =
                                new PointF(
                                    50,
                                    180)
                        },
                        $"الحارس: {keeper}",
                        Color.White);

                    ctx.DrawText(
                        new RichTextOptions(
                            smallFont)
                        {
                            Origin =
                                new PointF(
                                    50,
                                    215)
                        },
                        $"الأهداف: {goals}   الركلات: {shots}",
                        Color.White);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[TEXT IMAGE ERROR] {ex}");
            }

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

        // =========================================================
        // الأدوات
        // =========================================================

        private static string GetDirectionName(
            int direction)
        {
            return direction switch
            {
                1 => "⬅️ يسار",
                2 => "⬆️ وسط",
                3 => "➡️ يمين",
                _ => "غير معروف"
            };
        }

        private static string GetPlayerName(
            string groupId,
            string userId)
        {
            lock (GameLock)
            {
                if (Games.TryGetValue(
                    groupId,
                    out var game))
                {
                    return game.Players
                        .FirstOrDefault(
                            x => x.UserId == userId)
                        ?.Name
                        ?? "اللاعب";
                }
            }

            return "اللاعب";
        }

        private static string GetGroupId(
            Message message)
        {
            try
            {
                var type =
                    message.GetType();

                var prop =
                    type.GetProperty("GroupId");

                if (prop != null)
                {
                    var value =
                        prop.GetValue(message)
                        ?.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }

                prop =
                    type.GetProperty("RecipientId");

                if (prop != null)
                {
                    var value =
                        prop.GetValue(message)
                        ?.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }

                prop =
                    type.GetProperty("RoomId");

                if (prop != null)
                {
                    var value =
                        prop.GetValue(message)
                        ?.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
            catch
            {
            }

            return "";
        }

        private static string GetMessageText(
            Message message)
        {
            try
            {
                var type =
                    message.GetType();

                string[] names =
                {
                    "Text",
                    "Content",
                    "Message",
                    "Body"
                };

                foreach (var name in names)
                {
                    var prop =
                        type.GetProperty(name);

                    if (prop == null)
                        continue;

                    var value =
                        prop.GetValue(message)
                        ?.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
            catch
            {
            }

            return "";
        }

        private static string GetUserId(
            Message message)
        {
            try
            {
                var type =
                    message.GetType();

                string[] names =
                {
                    "UserId",
                    "SenderId",
                    "FromId",
                    "AuthorId"
                };

                foreach (var name in names)
                {
                    var prop =
                        type.GetProperty(name);

                    if (prop == null)
                        continue;

                    var value =
                        prop.GetValue(message)
                        ?.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
            catch
            {
            }

            return Guid.NewGuid().ToString();
        }

        private static string GetUserName(
            Message message)
        {
            try
            {
                var type =
                    message.GetType();

                string[] names =
                {
                    "UserName",
                    "SenderName",
                    "FromName",
                    "AuthorName",
                    "Name"
                };

                foreach (var name in names)
                {
                    var prop =
                        type.GetProperty(name);

                    if (prop == null)
                        continue;

                    var value =
                        prop.GetValue(message)
                        ?.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
            catch
            {
            }

            return "لاعب";
        }
    }
}
