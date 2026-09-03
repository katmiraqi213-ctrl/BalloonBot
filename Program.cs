using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

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

        public List<PenaltyPlayer> Players { get; set; } =
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
                if (Players.Count == 0)
                    return null;

                if (CurrentPlayerIndex < 0 ||
                    CurrentPlayerIndex >= Players.Count)
                    return null;

                return Players[CurrentPlayerIndex];
            }
        }
    }

    public class Program
    {
        private static IWolfClient Client = null!;

        // لعبة مستقلة لكل روم
        private static readonly Dictionary<string, PenaltyGame> Games =
            new Dictionary<string, PenaltyGame>();

        private static readonly SemaphoreSlim GamesLock =
            new SemaphoreSlim(1, 1);

        private static readonly Random Random =
            new Random();

        private const int MaxPlayers = 10;
        private const int MinPlayers = 2;
        private const int ShotsPerPlayer = 5;
        private const int TurnSeconds = 10;

        public static async Task Main(string[] args)
        {
            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            Console.WriteLine("======================================");
            Console.WriteLine("       ⚽ PENALTY BOT STARTING");
            Console.WriteLine("======================================");

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(
                    "ERROR: WOLF_EMAIL or WOLF_PASSWORD missing."
                );

                return;
            }

            Client = new WolfClient();

            // استقبال رسائل ولف
            Client.On<WolfMessage>(
                "message send",
                OnWolfMessage
            );

            bool loggedIn =
                await LoginManually(email, password);

            if (!loggedIn)
            {
                Console.WriteLine("LOGIN FAILED.");
                return;
            }

            Console.WriteLine("LOGIN SUCCESS");
            Console.WriteLine("--------------------------------------");
            Console.WriteLine("⚽ PENALTY BOT ONLINE");
            Console.WriteLine("🌐 MULTI ROOM MODE");
            Console.WriteLine("📡 WAITING FOR MESSAGES...");
            Console.WriteLine("--------------------------------------");

            await Task.Delay(Timeout.Infinite);
        }

        private static async Task<bool> LoginManually(
            string email,
            string password)
        {
            try
            {
                Console.WriteLine("Connecting to WOLF...");

                var welcomeSource =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    );

                Client.On<object>(
                    "welcome",
                    _ =>
                    {
                        Console.WriteLine("WELCOME RECEIVED");

                        welcomeSource.TrySetResult(true);
                    }
                );

                await Client.Connect();

                Console.WriteLine(
                    "Connected to WOLF server."
                );

                Console.WriteLine(
                    "Waiting for WOLF welcome..."
                );

                var completed =
                    await Task.WhenAny(
                        welcomeSource.Task,
                        Task.Delay(
                            TimeSpan.FromSeconds(15)
                        )
                    );

                if (completed != welcomeSource.Task)
                {
                    Console.WriteLine(
                        "ERROR: WOLF welcome timeout."
                    );

                    return false;
                }

                Console.WriteLine("Sending login...");

                var user =
                    await Client.Emit<User>(
                        new Packet(
                            "security login",
                            new
                            {
                                username = email,
                                password = password
                            }
                        )
                    );

                if (user == null)
                {
                    Console.WriteLine(
                        "LOGIN RESPONSE IS NULL."
                    );

                    return false;
                }

                Client.Profiling.Profile = user;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("LOGIN ERROR:");
                Console.WriteLine(ex);

                return false;
            }
        }

        private static async void OnWolfMessage(
            WolfMessage wolfMessage)
        {
            try
            {
                if (wolfMessage == null)
                    return;

                var message =
                    new Message(wolfMessage);

                // مهم جداً:
                // يطبع كل رسالة تصل للبوت حتى نعرف
                // هل الرسائل من الرومات الأخرى تصل أم لا.
                Console.WriteLine(
                    "--------------------------------------"
                );

                Console.WriteLine(
                    $"📩 MESSAGE RECEIVED"
                );

                Console.WriteLine(
                    $"🏠 GroupId : {message.GroupId}"
                );

                Console.WriteLine(
                    $"👤 UserId  : {message.UserId}"
                );

                Console.WriteLine(
                    $"💬 Content : {message.Content}"
                );

                Console.WriteLine(
                    $"👥 IsGroup : {message.IsGroup}"
                );

                Console.WriteLine(
                    "--------------------------------------"
                );

                if (!message.IsGroup)
                    return;

                if (string.IsNullOrWhiteSpace(
                    message.GroupId))
                    return;

                string content =
                    (message.Content ?? "").Trim();

                if (string.IsNullOrWhiteSpace(content))
                    return;

                await HandleMessage(
                    message,
                    content
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "MESSAGE ERROR:"
                );

                Console.WriteLine(ex);
            }
        }

        private static async Task HandleMessage(
            Message message,
            string content)
        {
            await GamesLock.WaitAsync();

            try
            {
                string groupId =
                    message.GroupId ?? "";

                if (string.IsNullOrWhiteSpace(groupId))
                    return;

                string lower =
                    content.Trim()
                           .ToLowerInvariant();

                // ==============================
                // المساعدة
                // ==============================

                if (lower == "!جزاء مساعدة" ||
                    lower == "!جزاء help")
                {
                    await Send(
                        message,
                        "⚽🔥 لعبة ضربة الجزاء 🔥⚽\n\n" +

                        "🎮 الأوامر:\n" +
                        "!جزاء — إنشاء لعبة\n" +
                        "!جزاء انضم — الانضمام\n" +
                        "!جزاء لاعبين — اللاعبين\n" +
                        "!جزاء بدء — بدء اللعبة\n" +
                        "!جزاء حالة — النتيجة\n" +
                        "!جزاء انهاء — إنهاء اللعبة\n\n" +

                        "🎯 أثناء دورك:\n" +
                        "1️⃣ يمين\n" +
                        "2️⃣ وسط\n" +
                        "3️⃣ يسار\n\n" +

                        "🥅 الحارس يختار مكاناً عشوائياً.\n" +
                        "إذا اختار نفس المكان = تصدي 🧤\n" +
                        "إذا اختار مكاناً مختلفاً = هدف ⚽\n\n" +

                        "⏱️ 10 ثواني لكل تسديدة\n" +
                        "🎯 5 تسديدات لكل لاعب\n" +
                        "👥 من 2 إلى 10 لاعبين"
                    );

                    return;
                }

                // ==============================
                // إنشاء لعبة
                // ==============================

                if (lower == "!جزاء")
                {
                    await NewGame(message);
                    return;
                }

                // اللعبة الخاصة بهذا الروم فقط
                if (!Games.TryGetValue(
                    groupId,
                    out PenaltyGame? game))
                {
                    if (lower.StartsWith("!جزاء"))
                    {
                        await Send(
                            message,
                            "❌ لا توجد لعبة بهذا الروم.\n\n" +
                            "اكتب !جزاء لإنشاء لعبة جديدة."
                        );
                    }

                    return;
                }

                // ==============================
                // انضمام
                // ==============================

                if (lower == "!جزاء انضم" ||
                    lower == "!جزاء انضمام")
                {
                    await JoinGame(
                        message,
                        game
                    );

                    return;
                }

                // ==============================
                // اللاعبين
                // ==============================

                if (lower == "!جزاء لاعبين")
                {
                    await ShowPlayers(
                        message,
                        game
                    );

                    return;
                }

                // ==============================
                // بدء
                // ==============================

                if (lower == "!جزاء بدء")
                {
                    await StartGame(
                        message,
                        game
                    );

                    return;
                }

                // ==============================
                // الحالة
                // ==============================

                if (lower == "!جزاء حالة")
                {
                    await ShowStatus(
                        message,
                        game
                    );

                    return;
                }

                // ==============================
                // إنهاء
                // ==============================

                if (lower == "!جزاء انهاء" ||
                    lower == "!جزاء إنهاء")
                {
                    await EndGame(
                        message,
                        game
                    );

                    return;
                }

                // ==============================
                // تسديدة
                // ==============================

                if (game.Started &&
                    game.WaitingForShot)
                {
                    if (TryParseChoice(
                        content,
                        out int choice))
                    {
                        await ProcessShot(
                            message,
                            game,
                            choice
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "HANDLE ERROR:"
                );

                Console.WriteLine(ex);
            }
            finally
            {
                GamesLock.Release();
            }
        }

        // ==========================================
        // إنشاء اللعبة
        // ==========================================

        private static async Task NewGame(
            Message message)
        {
            string groupId =
                message.GroupId ?? "";

            if (string.IsNullOrWhiteSpace(groupId))
                return;

            if (Games.ContainsKey(groupId))
            {
                await Send(
                    message,
                    "⚠️ توجد لعبة بالفعل بهذا الروم.\n" +
                    "اكتب !جزاء لاعبين لمعرفة اللاعبين."
                );

                return;
            }

            var game =
                new PenaltyGame
                {
                    GroupId = groupId,
                    Started = false,
                    CurrentPlayerIndex = 0,
                    WaitingForShot = false,
                    TurnVersion = 0
                };

            Games[groupId] = game;

            await Send(
                message,
                "⚽🔥 تم إنشاء لعبة ضربة الجزاء! 🔥⚽\n\n" +

                "👥 عدد اللاعبين: من 2 إلى 10\n" +
                "🎯 لكل لاعب 5 تسديدات\n" +
                "⏱️ 10 ثواني لكل تسديدة\n\n" +

                "👇 للانضمام اكتب:\n" +
                "!جزاء انضم\n\n" +

                "وعند اكتمال اللاعبين اكتب:\n" +
                "!جزاء بدء"
            );
        }

        // ==========================================
        // الانضمام
        // ==========================================

        private static async Task JoinGame(
            Message message,
            PenaltyGame game)
        {
            if (game.Started)
            {
                await Send(
                    message,
                    "❌ اللعبة بدأت بالفعل."
                );

                return;
            }

            string userId =
                message.UserId ?? "";

            if (string.IsNullOrWhiteSpace(userId))
            {
                await Send(
                    message,
                    "❌ تعذر معرفة حساب اللاعب."
                );

                return;
            }

            var existing =
                game.Players.FirstOrDefault(
                    p => p.UserId == userId
                );

            if (existing != null)
            {
                await Send(
                    message,
                    $"⚠️ أنت مسجل بالفعل.\n" +
                    $"👤 رقمك: اللاعب {existing.Number}️⃣"
                );

                return;
            }

            if (game.Players.Count >= MaxPlayers)
            {
                await Send(
                    message,
                    "❌ اللعبة مكتملة.\n" +
                    "الحد الأقصى 10 لاعبين."
                );

                return;
            }

            int number =
                game.Players.Count + 1;

            var player =
                new PenaltyPlayer
                {
                    UserId = userId,
                    Number = number,
                    Goals = 0,
                    Shots = 0
                };

            game.Players.Add(player);

            await Send(
                message,
                "✅ تم انضمامك للعبة! ⚽\n\n" +

                $"👤 رقمك: اللاعب {number}️⃣\n" +
                $"👥 اللاعبين: {game.Players.Count}/{MaxPlayers}\n\n" +

                "عند الجاهزية:\n" +
                "!جزاء بدء"
            );
        }

        // ==========================================
        // عرض اللاعبين
        // ==========================================

        private static async Task ShowPlayers(
            Message message,
            PenaltyGame game)
        {
            if (game.Players.Count == 0)
            {
                await Send(
                    message,
                    "👥 لا يوجد لاعبون."
                );

                return;
            }

            string text =
                "⚽🔥 لاعبو اللعبة 🔥⚽\n\n";

            foreach (var player in game.Players)
            {
                text +=
                    $"👤 اللاعب {player.Number}️⃣\n" +
                    $"⚽ الأهداف: {player.Goals}\n" +
                    $"🎯 التسديدات: " +
                    $"{player.Shots}/{ShotsPerPlayer}\n\n";
            }

            await Send(
                message,
                text
            );
        }

        // ==========================================
        // بدء اللعبة
        // ==========================================

        private static async Task StartGame(
            Message message,
            PenaltyGame game)
        {
            if (game.Started)
            {
                await Send(
                    message,
                    "⚠️ اللعبة بدأت بالفعل."
                );

                return;
            }

            if (game.Players.Count < MinPlayers)
            {
                await Send(
                    message,
                    "❌ يجب وجود لاعبين على الأقل.\n" +
                    "👥 الحد الأدنى: 2"
                );

                return;
            }

            game.Started = true;
            game.CurrentPlayerIndex = 0;
            game.WaitingForShot = true;
            game.TurnVersion++;

            await Send(
                message,
                "⚽🔥 بدأت لعبة ضربة الجزاء! 🔥⚽\n\n" +

                $"👥 اللاعبين: {game.Players.Count}\n" +
                $"🎯 التسديدات لكل لاعب: {ShotsPerPlayer}\n" +
                $"⏱️ وقت التسديدة: {TurnSeconds} ثواني\n\n" +

                "🏆 صاحب أكبر عدد من الأهداف يفوز!"
            );

            await Task.Delay(500);

            await SendCurrentTurn(game);

            StartShotTimeout(game);
        }

        // ==========================================
        // الدور الحالي
        // ==========================================

        private static async Task SendCurrentTurn(
            PenaltyGame game)
        {
            if (!game.Started ||
                !game.WaitingForShot)
                return;

            var player =
                game.CurrentPlayer;

            if (player == null)
                return;

            int shotNumber =
                player.Shots + 1;

            await SendToGroup(
                game.GroupId,
                "⚽🔥 حان دورك! 🔥⚽\n\n" +

                $"👤 اللاعب {player.Number}️⃣\n" +
                $"🎯 التسديدة {shotNumber}/{ShotsPerPlayer}\n\n" +

                "اختار مكان التسديدة:\n" +
                "1️⃣ يمين ➡️\n" +
                "2️⃣ وسط ⬆️\n" +
                "3️⃣ يسار ⬅️\n\n" +

                $"⏱️ أمامك {TurnSeconds} ثواني!"
            );
        }

        // ==========================================
        // معالجة التسديدة
        // ==========================================

        private static async Task ProcessShot(
            Message message,
            PenaltyGame game,
            int choice)
        {
            if (!game.Started ||
                !game.WaitingForShot)
                return;

            var player =
                game.CurrentPlayer;

            if (player == null)
                return;

            if (player.UserId != message.UserId)
            {
                await Send(
                    message,
                    $"⛔ مو دورك!\n" +
                    $"🔥 الآن دور اللاعب {player.Number}️⃣"
                );

                return;
            }

            if (choice < 1 || choice > 3)
            {
                await Send(
                    message,
                    "❌ الاختيار غير صحيح.\n" +
                    "اكتب 1 أو 2 أو 3."
                );

                return;
            }

            CancelTurnTimeout(game);

            game.WaitingForShot = false;
            game.TurnVersion++;

            player.Shots++;

            int goalkeeper =
                Random.Next(1, 4);

            string shotName =
                GetDirectionName(choice);

            string goalkeeperName =
                GetDirectionName(goalkeeper);

            bool goal =
                choice != goalkeeper;

            if (goal)
            {
                player.Goals++;

                await SendToGroup(
                    game.GroupId,
                    "⚽🔥🔥 هــــــــدف!!! 🔥🔥⚽\n\n" +

                    $"👤 اللاعب {player.Number}️⃣\n" +
                    $"🎯 سدد: {shotName}\n" +
                    $"🧤 الحارس: {goalkeeperName}\n\n" +

                    $"🥅 أهدافك: {player.Goals}\n" +
                    $"🎯 التسديدات: " +
                    $"{player.Shots}/{ShotsPerPlayer}"
                );
            }
            else
            {
                await SendToGroup(
                    game.GroupId,
                    "🧤❌ تـــــم التصدي!\n\n" +

                    $"👤 اللاعب {player.Number}️⃣\n" +
                    $"🎯 سدد: {shotName}\n" +
                    $"🧤 الحارس اختار: {goalkeeperName}\n\n" +

                    "الحارس قرأ التسديدة! 🔥"
                );
            }

            if (AllShotsFinished(game))
            {
                await Task.Delay(700);

                await FinishGame(game);

                return;
            }

            MoveToNextPlayer(game);

            await Task.Delay(500);

            await SendCurrentTurn(game);

            StartShotTimeout(game);
        }

        // ==========================================
        // الانتقال للاعب التالي
        // ==========================================

        private static void MoveToNextPlayer(
            PenaltyGame game)
        {
            if (game.Players.Count == 0)
                return;

            int count =
                game.Players.Count;

            for (int i = 1; i <= count; i++)
            {
                int index =
                    (game.CurrentPlayerIndex + i)
                    % count;

                var player =
                    game.Players[index];

                if (player.Shots < ShotsPerPlayer)
                {
                    game.CurrentPlayerIndex =
                        index;

                    game.WaitingForShot = true;

                    game.TurnVersion++;

                    return;
                }
            }
        }

        private static bool AllShotsFinished(
            PenaltyGame game)
        {
            return game.Players.All(
                p => p.Shots >= ShotsPerPlayer
            );
        }

        // ==========================================
        // مؤقت 10 ثواني
        // ==========================================

        private static void StartShotTimeout(
            PenaltyGame game)
        {
            CancelTurnTimeout(game);

            game.TurnTimeout =
                new CancellationTokenSource();

            CancellationToken token =
                game.TurnTimeout.Token;

            string groupId =
                game.GroupId;

            int version =
                game.TurnVersion;

            int playerIndex =
                game.CurrentPlayerIndex;

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(
                                TurnSeconds
                            ),
                            token
                        );

                        await GamesLock.WaitAsync();

                        try
                        {
                            if (!Games.TryGetValue(
                                groupId,
                                out PenaltyGame? currentGame))
                            {
                                return;
                            }

                            if (!currentGame.Started)
                                return;

                            if (!currentGame.WaitingForShot)
                                return;

                            if (currentGame.TurnVersion != version)
                                return;

                            if (currentGame.CurrentPlayerIndex != playerIndex)
                                return;

                            var player =
                                currentGame.CurrentPlayer;

                            if (player == null)
                                return;

                            player.Shots++;

                            currentGame.WaitingForShot =
                                false;

                            currentGame.TurnVersion++;

                            await SendToGroup(
                                currentGame.GroupId,
                                "⏰ انتهى الوقت!\n\n" +

                                $"👤 اللاعب {player.Number}️⃣\n" +
                                "❌ لم يسدد في الوقت المحدد.\n" +
                                "تم احتساب التسديدة كإضاعة.\n\n" +

                                $"🎯 التسديدات: " +
                                $"{player.Shots}/{ShotsPerPlayer}"
                            );

                            // اللاعب لا ينطرد
                            // فقط تنتقل اللعبة للاعب التالي

                            if (AllShotsFinished(currentGame))
                            {
                                await Task.Delay(500);

                                await FinishGame(
                                    currentGame
                                );

                                return;
                            }

                            MoveToNextPlayer(
                                currentGame
                            );

                            await Task.Delay(500);

                            await SendCurrentTurn(
                                currentGame
                            );

                            StartShotTimeout(
                                currentGame
                            );
                        }
                        finally
                        {
                            GamesLock.Release();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            "TIMEOUT ERROR:"
                        );

                        Console.WriteLine(ex);
                    }
                }
            );
        }

        // ==========================================
        // إلغاء المؤقت
        // ==========================================

        private static void CancelTurnTimeout(
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

        // ==========================================
        // حالة اللعبة
        // ==========================================

        private static async Task ShowStatus(
            Message message,
            PenaltyGame game)
        {
            string text =
                "⚽🔥 حالة اللعبة 🔥⚽\n\n";

            foreach (var player in game.Players)
            {
                text +=
                    $"👤 اللاعب {player.Number}️⃣\n" +
                    $"⚽ الأهداف: {player.Goals}\n" +
                    $"🎯 التسديدات: " +
                    $"{player.Shots}/{ShotsPerPlayer}\n\n";
            }

            if (game.Started &&
                game.CurrentPlayer != null)
            {
                text +=
                    $"🔥 الدور الآن على اللاعب " +
                    $"{game.CurrentPlayer.Number}️⃣";
            }

            await Send(
                message,
                text
            );
        }

        // ==========================================
        // إنهاء اللعبة
        // ==========================================

        private static async Task EndGame(
            Message message,
            PenaltyGame game)
        {
            CancelTurnTimeout(game);

            Games.Remove(game.GroupId);

            await Send(
                message,
                "🛑 تم إنهاء لعبة ضربة الجزاء بهذا الروم."
            );
        }

        // ==========================================
        // النتائج
        // ==========================================

        private static async Task FinishGame(
            PenaltyGame game)
        {
            CancelTurnTimeout(game);

            game.WaitingForShot = false;
            game.Started = false;

            var ranking =
                game.Players
                    .OrderByDescending(
                        p => p.Goals
                    )
                    .ToList();

            if (ranking.Count == 0)
            {
                Games.Remove(game.GroupId);
                return;
            }

            string text =
                "🏆🔥 انتهت لعبة ضربة الجزاء 🔥🏆\n\n";

            for (int i = 0;
                 i < ranking.Count;
                 i++)
            {
                var player =
                    ranking[i];

                string medal;

                if (i == 0)
                    medal = "🥇";
                else if (i == 1)
                    medal = "🥈";
                else if (i == 2)
                    medal = "🥉";
                else
                    medal = "🔹";

                text +=
                    $"{medal} اللاعب {player.Number}️⃣\n" +
                    $"⚽ الأهداف: {player.Goals}\n" +
                    $"🎯 التسديدات: " +
                    $"{player.Shots}/{ShotsPerPlayer}\n\n";
            }

            int highest =
                ranking.First().Goals;

            var winners =
                ranking
                    .Where(
                        p => p.Goals == highest
                    )
                    .ToList();

            if (winners.Count == 1)
            {
                text +=
                    "👑🏆 الفائز هو:\n" +
                    $"اللاعب {winners[0].Number}️⃣\n" +
                    $"⚽ {winners[0].Goals} أهداف 🔥";
            }
            else
            {
                text +=
                    "🤝 تعادل!\n\n" +
                    "🏆 الفائزون:\n";

                foreach (var winner in winners)
                {
                    text +=
                        $"👑 اللاعب {winner.Number}️⃣ — " +
                        $"{winner.Goals} أهداف\n";
                }
            }

            await SendToGroup(
                game.GroupId,
                text
            );

            Games.Remove(game.GroupId);
        }

        // ==========================================
        // اتجاهات التسديد
        // ==========================================

        private static string GetDirectionName(
            int direction)
        {
            return direction switch
            {
                1 => "يمين ➡️",
                2 => "وسط ⬆️",
                3 => "يسار ⬅️",
                _ => "غير معروف"
            };
        }

        // ==========================================
        // قراءة الأرقام العربية والإنجليزية
        // ==========================================

        private static bool TryParseChoice(
            string text,
            out int number)
        {
            text =
                text
                    .Replace('٠', '0')
                    .Replace('١', '1')
                    .Replace('٢', '2')
                    .Replace('٣', '3')
                    .Replace('٤', '4')
                    .Replace('٥', '5')
                    .Replace('٦', '6')
                    .Replace('٧', '7')
                    .Replace('٨', '8')
                    .Replace('٩', '9');

            return int.TryParse(
                text,
                out number
            );
        }

        // ==========================================
        // إرسال رد
        // ==========================================

        private static async Task Send(
            Message message,
            string text)
        {
            try
            {
                await message.Reply(
                    Client,
                    text
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SEND ERROR:"
                );

                Console.WriteLine(ex);
            }
        }

        // ==========================================
        // إرسال للروم المحدد
        // ==========================================

        private static async Task SendToGroup(
            string groupId,
            string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(groupId))
                    return;

                await Client.GroupMessage(
                    groupId,
                    text
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "GROUP SEND ERROR:"
                );

                Console.WriteLine(ex);
            }
        }
    }
}
