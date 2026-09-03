using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using WolfLive.Api;
using WolfLive.Api.Models;

using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace PenaltyBot
{
    public class PenaltyPlayer
    {
        public string UserId { get; set; } = "";
        public int Number { get; set; }
        public int Goals { get; set; }
        public int Shots { get; set; }
    }

    public class PenaltyGame
    {
        public string GroupId { get; set; } = "";

        public List<PenaltyPlayer> Players { get; } =
            new List<PenaltyPlayer>();

        public bool Started { get; set; }

        public int CurrentPlayerIndex { get; set; }

        public bool WaitingForShot { get; set; }

        public CancellationTokenSource? TurnTimeout { get; set; }

        public int TurnVersion { get; set; }

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

        private static readonly Dictionary<string, PenaltyGame> Games =
            new Dictionary<string, PenaltyGame>();

        private static readonly object GameLock = new object();

        private static readonly Random Rng = new Random();

        private const int MaxPlayers = 10;
        private const int MinPlayers = 2;

        private const int ShotsPerPlayer = 5;

        // 25 ثانية
        private const int TurnSeconds = 25;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("================================");
            Console.WriteLine("       PENALTY BOT");
            Console.WriteLine("================================");

            string? email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL");

            string? password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD");

            if (string.IsNullOrWhiteSpace(email))
            {
                Console.Write("Wolf Email: ");
                email = Console.ReadLine();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                Console.Write("Wolf Password: ");
                password = Console.ReadLine();
            }

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("[WOLF] Email or password missing.");
                return;
            }

            Client = new WolfClient();

            Client.OnConnected += c =>
            {
                Console.WriteLine("[WOLF] Connected");
            };

            Client.OnDisconnected += (c, reason) =>
            {
                Console.WriteLine(
                    "[WOLF] Disconnected: " + reason);
            };

            Client.OnConnectionError += (c, error) =>
            {
                Console.WriteLine(
                    "[WOLF] Connection Error: " + error);
            };

            Client.OnError += (c, ex) =>
            {
                Console.WriteLine(
                    "[WOLF] Error: " + ex.Message);
            };

            // مهم جداً:
            // استقبال الرسائل قبل Login
            Client.Messaging.OnMessage += OnMessage;

            bool login =
                await Client.Login(email, password);

            if (!login)
            {
                Console.WriteLine("[WOLF] Login Failed!");
                return;
            }

            Console.WriteLine("[WOLF] Login Success!");
            Console.WriteLine("[WOLF] Listening to ALL ROOMS");
            Console.WriteLine("[WOLF] Turn Time = 25 Seconds");

            await Task.Delay(Timeout.Infinite);
        }

        // =========================================================
        // استقبال الرسائل
        // =========================================================

        private static async void OnMessage(
            IWolfClient client,
            Message message)
        {
            try
            {
                if (!message.IsGroup)
                    return;

                if (string.IsNullOrWhiteSpace(message.GroupId))
                    return;

                string text =
                    (message.Content ?? "").Trim();

                if (string.IsNullOrWhiteSpace(text))
                    return;

                string groupId = message.GroupId;
                string userId = message.UserId;

                Console.WriteLine(
                    $"[MESSAGE] Room={groupId} User={userId} Text={text}");

                if (text.Equals(
                    "!جزاء",
                    StringComparison.OrdinalIgnoreCase) ||
                    text.Equals(
                        "!جزاء مساعدة",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await Send(
                        groupId,
                        "⚽ لعبة ركلات الجزاء\n\n" +
                        "📌 الأوامر:\n" +
                        "!جزاء انضم\n" +
                        "!جزاء لاعبين\n" +
                        "!جزاء بدء\n" +
                        "!جزاء حالة\n" +
                        "!جزاء انهاء\n\n" +
                        "🎯 أثناء دورك:\n" +
                        "1️⃣ يسار\n" +
                        "2️⃣ وسط\n" +
                        "3️⃣ يمين\n\n" +
                        "⏱️ لديك 25 ثانية للتسديد.\n" +
                        "🚫 انتهاء الوقت = خروج من اللعبة فقط.");

                    return;
                }

                if (text.Equals(
                    "!جزاء انضم",
                    StringComparison.OrdinalIgnoreCase))
                {
                    await JoinGame(groupId, userId);
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
                    await EndGame(
                        groupId,
                        "🛑 تم إنهاء لعبة الجزاء.");

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
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[MESSAGE ERROR] " + ex);
            }
        }

        // =========================================================
        // الانضمام
        // =========================================================

        private static async Task JoinGame(
            string groupId,
            string userId)
        {
            string response;

            lock (GameLock)
            {
                if (!Games.ContainsKey(groupId))
                {
                    Games[groupId] =
                        new PenaltyGame
                        {
                            GroupId = groupId
                        };
                }

                PenaltyGame game =
                    Games[groupId];

                if (game.Started)
                {
                    response =
                        "❌ اللعبة بدأت بالفعل.";
                }
                else if (game.Players.Any(
                    p => p.UserId == userId))
                {
                    response =
                        "⚠️ أنت منضم للعبة بالفعل.";
                }
                else if (game.Players.Count >= MaxPlayers)
                {
                    response =
                        $"❌ اللعبة مكتملة. الحد الأقصى {MaxPlayers} لاعبين.";
                }
                else
                {
                    int number =
                        game.Players.Count + 1;

                    game.Players.Add(
                        new PenaltyPlayer
                        {
                            UserId = userId,
                            Number = number,
                            Goals = 0,
                            Shots = 0
                        });

                    response =
                        $"✅ تم انضمامك للعبة.\n\n" +
                        $"🔢 رقمك: {number}\n" +
                        $"👥 اللاعبين: {game.Players.Count}/{MaxPlayers}";
                }
            }

            await Send(groupId, response);
        }

        // =========================================================
        // اللاعبين
        // =========================================================

        private static async Task ShowPlayers(
            string groupId)
        {
            string response;

            lock (GameLock)
            {
                if (!Games.ContainsKey(groupId) ||
                    Games[groupId].Players.Count == 0)
                {
                    response =
                        "⚠️ لا يوجد لاعبين.";
                }
                else
                {
                    PenaltyGame game =
                        Games[groupId];

                    var lines =
                        new List<string>();

                    lines.Add("⚽ لاعبي لعبة الجزاء:");
                    lines.Add("");

                    foreach (PenaltyPlayer p
                             in game.Players)
                    {
                        lines.Add(
                            $"{p.Number}. {p.UserId} — " +
                            $"{p.Goals} أهداف / " +
                            $"{p.Shots} تسديدات");
                    }

                    lines.Add("");

                    lines.Add(
                        $"👥 العدد: {game.Players.Count}/{MaxPlayers}");

                    response =
                        string.Join("\n", lines);
                }
            }

            await Send(groupId, response);
        }

        // =========================================================
        // بدء اللعبة
        // =========================================================

        private static async Task StartGame(
            string groupId)
        {
            string response;
            bool startTimeout = false;

            lock (GameLock)
            {
                if (!Games.ContainsKey(groupId))
                {
                    response =
                        "❌ لا توجد لعبة.";
                }
                else
                {
                    PenaltyGame game =
                        Games[groupId];

                    if (game.Started)
                    {
                        response =
                            "⚠️ اللعبة بدأت بالفعل.";
                    }
                    else if (game.Players.Count < MinPlayers)
                    {
                        response =
                            $"❌ يجب أن يكون هناك {MinPlayers} لاعبين على الأقل.";
                    }
                    else
                    {
                        game.Started = true;
                        game.CurrentPlayerIndex = 0;
                        game.WaitingForShot = true;
                        game.TurnVersion++;

                        PenaltyPlayer? player =
                            game.CurrentPlayer;

                        response =
                            "🏁 بدأت لعبة ركلات الجزاء!\n\n" +
                            $"🎯 الدور على: {player?.UserId}\n" +
                            $"🔢 اللاعب رقم {player?.Number}\n\n" +
                            "اختر مكان التسديد:\n\n" +
                            "1️⃣ يسار\n" +
                            "2️⃣ وسط\n" +
                            "3️⃣ يمين\n\n" +
                            "⏱️ لديك 25 ثانية.";

                        startTimeout = true;
                    }
                }
            }

            await Send(groupId, response);

            if (startTimeout)
                StartTimeout(groupId);
        }

        // =========================================================
        // تنفيذ التسديدة
        // =========================================================

        private static async Task ProcessShot(
            string groupId,
            string userId,
            int shot)
        {
            string? response = null;

            bool nextTurn = false;
            bool finish = false;

            int goalkeeper = 0;
            bool goal = false;

            PenaltyPlayer? playerForImage = null;

            lock (GameLock)
            {
                if (!Games.ContainsKey(groupId))
                    return;

                PenaltyGame game =
                    Games[groupId];

                if (!game.Started)
                    return;

                if (!game.WaitingForShot)
                    return;

                if (game.CurrentPlayer == null)
                    return;

                if (game.CurrentPlayer.UserId != userId)
                {
                    response =
                        "⏳ مو دورك حالياً.";
                }
                else
                {
                    CancelTimeout(game);

                    PenaltyPlayer player =
                        game.CurrentPlayer;

                    playerForImage = player;

                    player.Shots++;

                    lock (Rng)
                    {
                        goalkeeper =
                            Rng.Next(1, 4);
                    }

                    goal =
                        shot != goalkeeper;

                    if (goal)
                        player.Goals++;

                    game.WaitingForShot = false;
                    game.TurnVersion++;

                    string shotDirection =
                        DirectionName(shot);

                    string keeperDirection =
                        DirectionName(goalkeeper);

                    string result =
                        goal
                            ? "⚽ گوووووول!"
                            : "🧤 الحارس صــــدها!";

                    response =
                        $"{result}\n\n" +
                        $"🎯 التسديدة: {shotDirection}\n" +
                        $"🧤 الحارس: {keeperDirection}\n\n" +
                        $"👤 {player.UserId}\n" +
                        $"⚽ الأهداف: {player.Goals}\n" +
                        $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}";

                    if (AllPlayersFinished(game))
                        finish = true;
                    else
                        nextTurn = true;
                }
            }

            if (playerForImage != null)
            {
                try
                {
                    byte[] image =
                        CreatePenaltyImage(
                            shot,
                            goalkeeper,
                            goal,
                            playerForImage.UserId,
                            playerForImage.Goals,
                            playerForImage.Shots);

                    await Client!.GroupMessage(
                        groupId,
                        image);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "[IMAGE ERROR] " + ex.Message);
                }
            }

            if (!string.IsNullOrWhiteSpace(response))
                await Send(groupId, response);

            if (finish)
            {
                await FinishGame(groupId);
                return;
            }

            if (nextTurn)
                await MoveToNextPlayer(groupId);
        }

        // =========================================================
        // المؤقت
        // =========================================================

        private static void StartTimeout(
            string groupId)
        {
            PenaltyGame? game;
            int version;
            CancellationToken token;

            lock (GameLock)
            {
                if (!Games.ContainsKey(groupId))
                    return;

                game =
                    Games[groupId];

                CancelTimeout(game);

                game.WaitingForShot = true;

                version =
                    game.TurnVersion;

                game.TurnTimeout =
                    new CancellationTokenSource();

                token =
                    game.TurnTimeout.Token;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(TurnSeconds),
                        token);

                    await TimeoutPlayer(
                        groupId,
                        version);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "[TIMEOUT ERROR] " + ex);
                }
            });
        }

        // =========================================================
        // انتهاء الوقت
        // =========================================================

        private static async Task TimeoutPlayer(
            string groupId,
            int version)
        {
            string response;

            bool finish = false;
            bool next = false;

            lock (GameLock)
            {
                if (!Games.ContainsKey(groupId))
                    return;

                PenaltyGame game =
                    Games[groupId];

                if (!game.Started)
                    return;

                if (!game.WaitingForShot)
                    return;

                if (game.TurnVersion != version)
                    return;

                if (game.CurrentPlayer == null)
                    return;

                PenaltyPlayer player =
                    game.CurrentPlayer;

                string userId =
                    player.UserId;

                int removedIndex =
                    game.CurrentPlayerIndex;

                game.Players.RemoveAt(
                    removedIndex);

                game.WaitingForShot = false;
                game.TurnVersion++;

                response =
                    $"⏰ انتهت الـ25 ثانية!\n\n" +
                    $"🚫 اللاعب {userId} خرج من لعبة الجزاء.\n\n" +
                    "ℹ️ لم يتم طرده من الروم.";

                if (game.Players.Count < MinPlayers)
                {
                    finish = true;
                }
                else
                {
                    /*
                     * تصحيح مهم:
                     * نريد اللاعب الذي يأتي بعد اللاعب المطرود،
                     * وليس تخطي لاعب.
                     */

                    if (removedIndex >= game.Players.Count)
                    {
                        game.CurrentPlayerIndex = -1;
                    }
                    else
                    {
                        game.CurrentPlayerIndex =
                            removedIndex - 1;
                    }

                    next = true;
                }
            }

            await Send(groupId, response);

            if (finish)
            {
                await EndGame(
                    groupId,
                    "🏁 انتهت اللعبة لعدم بقاء عدد كافٍ من اللاعبين.");

                return;
            }

            if (next)
                await MoveToNextPlayer(groupId);
        }

        // =========================================================
        // اللاعب التالي
        // =========================================================

        private static async Task MoveToNextPlayer(
            string groupId)
        {
            string? response = null;

            bool finished = false;

            lock (GameLock)
            {
                if (!Games.ContainsKey(groupId))
                    return;

                PenaltyGame game =
                    Games[groupId];

                if (!game.Started)
                    return;

                if (game.Players.Count == 0)
                {
                    finished = true;
                }
                else
                {
                    int checkedPlayers = 0;

                    while (checkedPlayers <
                           game.Players.Count)
                    {
                        game.CurrentPlayerIndex++;

                        if (game.CurrentPlayerIndex >=
                            game.Players.Count)
                        {
                            game.CurrentPlayerIndex = 0;
                        }

                        PenaltyPlayer player =
                            game.CurrentPlayer;

                        checkedPlayers++;

                        if (player == null)
                            continue;

                        if (player.Shots <
                            ShotsPerPlayer)
                        {
                            game.WaitingForShot = true;
                            game.TurnVersion++;

                            response =
                                "🎯 الدور الآن على:\n\n" +
                                $"👤 {player.UserId}\n" +
                                $"🔢 اللاعب رقم {player.Number}\n" +
                                $"⚽ الأهداف: {player.Goals}\n" +
                                $"🎯 التسديدات: {player.Shots}/{ShotsPerPlayer}\n\n" +
                                "1️⃣ يسار\n" +
                                "2️⃣ وسط\n" +
                                "3️⃣ يمين\n\n" +
                                "⏱️ لديك 25 ثانية.";

                            break;
                        }
                    }

                    if (response == null)
                        finished = true;
                }
            }

            if (finished)
            {
                await FinishGame(groupId);
                return;
            }

            await Send(groupId, response!);

            StartTimeout(groupId);
        }

        // =========================================================
        // الحالة
        // =========================================================

        private static async Task ShowStatus(
            string groupId)
        {
            string response;

            lock (GameLock)
            {
                if (!Games.ContainsKey(groupId))
                {
                    response =
                        "❌ لا توجد لعبة.";
                }
                else
                {
                    PenaltyGame game =
                        Games[groupId];

                    if (!game.Started)
                    {
                        response =
                            "⚪ اللعبة غير مبدوءة.\n" +
                            $"👥 اللاعبين: {game.Players.Count}/{MaxPlayers}";
                    }
                    else
                    {
                        PenaltyPlayer? player =
                            game.CurrentPlayer;

                        response =
                            "⚽ حالة لعبة الجزاء\n\n" +
                            $"👥 اللاعبين: {game.Players.Count}\n" +
                            $"🎯 الدور: {player?.UserId}\n" +
                            $"⚽ الأهداف: {player?.Goals}\n" +
                            $"🎯 التسديدات: {player?.Shots}/{ShotsPerPlayer}\n" +
                            "⏱️ الوقت: 25 ثانية.";
                    }
                }
            }

            await Send(groupId, response);
        }

        // =========================================================
        // انتهاء اللعبة
        // =========================================================

        private static async Task FinishGame(
            string groupId)
        {
            string response;

            lock (GameLock)
            {
                if (!Games.ContainsKey(groupId))
                    return;

                PenaltyGame game =
                    Games[groupId];

                CancelTimeout(game);

                game.Started = false;
                game.WaitingForShot = false;

                if (game.Players.Count == 0)
                {
                    response =
                        "🏁 انتهت لعبة الجزاء.";
                }
                else
                {
                    var ranking =
                        game.Players
                            .OrderByDescending(
                                p => p.Goals)
                            .ThenByDescending(
                                p => p.Shots)
                            .ToList();

                    var lines =
                        new List<string>();

                    lines.Add(
                        "🏆 انتهت لعبة ركلات الجزاء!");

                    lines.Add("");

                    lines.Add(
                        "📊 النتائج:");

                    int rank = 1;

                    foreach (PenaltyPlayer p
                             in ranking)
                    {
                        lines.Add(
                            $"{rank}. {p.UserId} — " +
                            $"{p.Goals} أهداف / " +
                            $"{p.Shots} تسديدات");

                        rank++;
                    }

                    lines.Add("");

                    PenaltyPlayer winner =
                        ranking.First();

                    lines.Add(
                        $"🥇 الفائز: {winner.UserId}");

                    lines.Add(
                        $"⚽ الأهداف: {winner.Goals}");

                    response =
                        string.Join("\n", lines);
                }

                // تنظيف اللعبة حتى يستطيعون بدء لعبة جديدة
                game.Players.Clear();

                game.CurrentPlayerIndex = 0;

                game.TurnVersion++;
            }

            await Send(groupId, response);
        }

        // =========================================================
        // إنهاء يدوي
        // =========================================================

        private static async Task EndGame(
            string groupId,
            string reason)
        {
            bool exists;

            lock (GameLock)
            {
                exists =
                    Games.ContainsKey(groupId);

                if (exists)
                {
                    PenaltyGame game =
                        Games[groupId];

                    CancelTimeout(game);

                    game.Started = false;
                    game.WaitingForShot = false;

                    game.Players.Clear();

                    game.CurrentPlayerIndex = 0;

                    game.TurnVersion++;
                }
            }

            if (exists)
                await Send(groupId, reason);
        }

        // =========================================================
        // إلغاء المؤقت
        // =========================================================

        private static void CancelTimeout(
            PenaltyGame game)
        {
            try
            {
                if (game.TurnTimeout != null)
                {
                    game.TurnTimeout.Cancel();
                    game.TurnTimeout.Dispose();
                    game.TurnTimeout = null;
                }
            }
            catch
            {
            }
        }

        // =========================================================
        // إرسال رسالة
        // =========================================================

        private static async Task Send(
            string groupId,
            string text)
        {
            try
            {
                if (Client == null)
                    return;

                if (string.IsNullOrWhiteSpace(groupId))
                    return;

                await Client.GroupMessage(
                    groupId,
                    text);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[SEND ERROR] {ex.Message}");
            }
        }

        // =========================================================
        // اسم الاتجاه
        // =========================================================

        private static string DirectionName(
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

        // =========================================================
        // إنشاء صورة ركلة الجزاء
        // =========================================================

        private static byte[] CreatePenaltyImage(
            int shot,
            int goalkeeper,
            bool goal,
            string userId,
            int goals,
            int shots)
        {
            const int width = 1200;
            const int height = 800;

            using var image =
                new Image<Rgba32>(
                    width,
                    height);

            image.Mutate(ctx =>
            {
                // السماء
                ctx.Fill(
                    Color.ParseHex("#101827"));

                // أضواء الملعب
                ctx.Fill(
                    Color.ParseHex("#18253A"),
                    new Rectangle(
                        0,
                        0,
                        width,
                        360));

                // أرضية الملعب
                ctx.Fill(
                    Color.ParseHex("#176B3A"),
                    new Rectangle(
                        0,
                        360,
                        width,
                        440));

                // خطوط الملعب
                ctx.DrawLine(
                    Color.White,
                    5,
                    new PointF(0, 650),
                    new PointF(width, 650));

                // منطقة الجزاء
                ctx.DrawLine(
                    Color.White,
                    5,
                    new PointF(260, 500),
                    new PointF(940, 500));

                ctx.DrawLine(
                    Color.White,
                    5,
                    new PointF(260, 500),
                    new PointF(260, 800));

                ctx.DrawLine(
                    Color.White,
                    5,
                    new PointF(940, 500),
                    new PointF(940, 800));

                // =========================
                // المرمى
                // =========================

                float goalLeft = 300;
                float goalRight = 900;
                float goalTop = 160;
                float goalBottom = 500;

                // إطار المرمى
                ctx.DrawLine(
                    Color.White,
                    14,
                    new PointF(
                        goalLeft,
                        goalBottom),
                    new PointF(
                        goalLeft,
                        goalTop));

                ctx.DrawLine(
                    Color.White,
                    14,
                    new PointF(
                        goalLeft,
                        goalTop),
                    new PointF(
                        goalRight,
                        goalTop));

                ctx.DrawLine(
                    Color.White,
                    14,
                    new PointF(
                        goalRight,
                        goalTop),
                    new PointF(
                        goalRight,
                        goalBottom));

                // الشبكة العمودية
                for (int x = 330;
                     x < goalRight;
                     x += 40)
                {
                    ctx.DrawLine(
                        Color.ParseHex("#DDE5E8"),
                        2,
                        new PointF(
                            x,
                            goalTop),
                        new PointF(
                            x,
                            goalBottom));
                }

                // الشبكة الأفقية
                for (int y = 200;
                     y < goalBottom;
                     y += 40)
                {
                    ctx.DrawLine(
                        Color.ParseHex("#DDE5E8"),
                        2,
                        new PointF(
                            goalLeft,
                            y),
                        new PointF(
                            goalRight,
                            y));
                }

                // =========================
                // الحارس
                // =========================

                float keeperX =
                    goalkeeper switch
                    {
                        1 => 390,
                        2 => 600,
                        3 => 810,
                        _ => 600
                    };

                float keeperY = 300;

                // الرأس
                ctx.Fill(
                    Color.ParseHex("#F0B28A"),
                    new EllipsePolygon(
                        new PointF(
                            keeperX,
                            keeperY - 90),
                        38));

                // الجسم
                ctx.Fill(
                    Color.ParseHex("#253B8E"),
                    new Rectangle(
                        (int)keeperX - 45,
                        (int)keeperY - 50,
                        90,
                        150));

                // الرجل اليسرى
                ctx.DrawLine(
                    Color.ParseHex("#111111"),
                    28,
                    new PointF(
                        keeperX - 20,
                        keeperY + 90),
                    new PointF(
                        keeperX - 65,
                        keeperY + 180));

                // الرجل اليمنى
                ctx.DrawLine(
                    Color.ParseHex("#111111"),
                    28,
                    new PointF(
                        keeperX + 20,
                        keeperY + 90),
                    new PointF(
                        keeperX + 65,
                        keeperY + 180));

                // الذراعان باتجاه الكرة
                ctx.DrawLine(
                    Color.ParseHex("#F0B28A"),
                    24,
                    new PointF(
                        keeperX - 40,
                        keeperY - 20),
                    new PointF(
                        keeperX - 100,
                        keeperY - 70));

                ctx.DrawLine(
                    Color.ParseHex("#F0B28A"),
                    24,
                    new PointF(
                        keeperX + 40,
                        keeperY - 20),
                    new PointF(
                        keeperX + 100,
                        keeperY - 70));

                // =========================
                // الكرة
                // =========================

                float ballX;

                if (goal)
                {
                    ballX =
                        shot switch
                        {
                            1 => 370,
                            2 => 600,
                            3 => 830,
                            _ => 600
                        };
                }
                else
                {
                    ballX =
                        goalkeeper switch
                        {
                            1 => 390,
                            2 => 600,
                            3 => 810,
                            _ => 600
                        };
                }

                float ballY = 250;

                // ظل الكرة
                ctx.Fill(
                    Color.ParseHex("#333333"),
                    new EllipsePolygon(
                        new PointF(
                            ballX + 5,
                            ballY + 7),
                        25));

                // الكرة
                ctx.Fill(
                    Color.White,
                    new EllipsePolygon(
                        new PointF(
                            ballX,
                            ballY),
                        24));

                // تفاصيل الكرة
                ctx.Fill(
                    Color.ParseHex("#111111"),
                    new EllipsePolygon(
                        new PointF(
                            ballX - 7,
                            ballY - 5),
                        5));

                ctx.Fill(
                    Color.ParseHex("#111111"),
                    new EllipsePolygon(
                        new PointF(
                            ballX + 8,
                            ballY + 4),
                        4));

                // =========================
                // النتيجة
                // =========================

                Font fontBig =
                    SystemFonts.CreateFont(
                        "Arial",
                        58,
                        FontStyle.Bold);

                Font fontSmall =
                    SystemFonts.CreateFont(
                        "Arial",
                        30,
                        FontStyle.Bold);

                string resultText =
                    goal
                        ? "⚽  G O A L !"
                        : "🧤  S A V E !";

                Color resultColor =
                    goal
                        ? Color.ParseHex("#42FF78")
                        : Color.ParseHex("#FF5252");

                ctx.DrawText(
                    resultText,
                    fontBig,
                    resultColor,
                    new PointF(
                        380,
                        40));

                ctx.DrawText(
                    $"Player: {userId}",
                    fontSmall,
                    Color.White,
                    new PointF(
                        40,
                        700));

                ctx.DrawText(
                    $"Goals: {goals}   Shots: {shots}/{ShotsPerPlayer}",
                    fontSmall,
                    Color.White,
                    new PointF(
                        650,
                        700));
            });

            using var stream =
                new MemoryStream();

            image.SaveAsJpeg(stream);

            return stream.ToArray();
        }

        // =========================================================
        // فحص انتهاء اللاعبين
        // =========================================================

        private static bool AllPlayersFinished(
            PenaltyGame game)
        {
            return game.Players.Count > 0 &&
                   game.Players.All(
                       p => p.Shots >= ShotsPerPlayer);
        }
    }
}
